using System.Collections;
using System.Collections.Generic;
using FMOD;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FinalSequenceController : MonoBehaviour
{
    [System.Serializable]
    private class FaderTarget
    {
        public FaderController fader;
        [Range(0f, 1f)] public float targetValue = 0.5f;
        [Range(0.001f, 0.2f)] public float tolerance = 0.05f;
    }

    [Header("Fader win condition")]
    [SerializeField] private FaderTarget redFader = new FaderTarget { targetValue = 0.25f, tolerance = 0.05f };
    [SerializeField] private FaderTarget greenFader = new FaderTarget { targetValue = 0.5f, tolerance = 0.05f };
    [SerializeField] private FaderTarget blueFader = new FaderTarget { targetValue = 1f, tolerance = 0.05f };
    [SerializeField] private float requiredStableTime = 0.35f;

    [Header("Final mix behavior")]
    [SerializeField] private float masterFadeOutDuration = 1.5f;
    [SerializeField] private float fadersRiseDuration = 2f;
    [SerializeField] private float masterFadeInDuration = 1.5f;

    [Header("Object audio modulation (bus)")]
    [Tooltip("Buses des sons d'objets à moduler (pan + volume). Ex: bus:/SFX/Objects")]
    [SerializeField] private string[] objectAudioBusPaths;
    [SerializeField] private float panOscillationSpeed = 0.8f;
    [SerializeField] private float volumeOscillationSpeed = 0.6f;
    [SerializeField] private Vector2 panSpeedRandomMultiplierRange = new Vector2(0.75f, 1.35f);
    [SerializeField] private Vector2 volumeSpeedRandomMultiplierRange = new Vector2(0.7f, 1.4f);
    [Range(0f, 1f)] [SerializeField] private float minModulatedVolume = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float maxModulatedVolume = 1f;

    [Header("Visual modulation (materials + lights)")]
    [SerializeField] private Renderer[] renderersToModulate;
    [SerializeField] private Light[] lightsToModulate;
    [SerializeField] private float colorLerpSpeed = 3f;
    [SerializeField] private float lightIntensityLerpSpeed = 2.5f;
    [SerializeField] private Vector2 lightIntensityRange = new Vector2(0.6f, 2.2f);
    [SerializeField] private float targetRefreshThreshold = 0.02f;

    [Header("Exit unlock + final outside sequence")]
    [SerializeField] private Collider exitBlockerAndTriggerCollider;
    [Tooltip("Grand trigger englobant toute la maison. La sortie est détectée quand la tête du joueur n'est plus dedans.")]
    [SerializeField] private Collider houseBoundsTriggerCollider;
    [Tooltip("Legacy/fallback: trigger de sortie local (si houseBoundsTriggerCollider n'est pas assigné).")]
    [SerializeField] private Collider exitTriggerCollider;
    [SerializeField] private Transform playerHead;
    [SerializeField] private Transform playerRigRoot;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeToBlackDuration = 1.1f;
    [SerializeField] private float fadeFromBlackDuration = 1.1f;
    [SerializeField] private Transform outsideSpawn;
    [SerializeField] private Transform outsideLookAt;
    [SerializeField] private LocomotionManager locomotionManager;
    [Tooltip("One-shot FMOD 2D quand le joueur franchit la zone de sortie.")]
    [SerializeField] private EventReference exitHouseSound;

    [Header("Outside final music + credits")]
    [SerializeField] private EventReference outsideFinalMusicEvent;
    [SerializeField] private float outsideFinalMusicFadeInDuration = 1.2f;
    [SerializeField] private WorldSpaceCreditsScroller creditsScroller;

    [Header("Voix off (début séquence finale, après bons faders)")]
    [SerializeField] private SubtitleManager subtitleManager;
    [SerializeField] private float delaySecondsBeforeFinalDialogue = 5f;
    [SerializeField] private EventReference voNayaJeComprendsPas;
    [SerializeField] private EventReference voTherapeuteCestNormalController;
    [SerializeField] private EventReference voTherapeuteQuandPreteSors;

    [Header("Porte sortie (déblocage après dernière voix)")]
    [Tooltip("Poignées/objets XR de la porte à activer uniquement après la fin de 'QuandPreteSors'.")]
    [SerializeField] private GameObject[] exitDoorHandlesToEnableAfterFinalVoice;
    [SerializeField] private bool hideExitHandlesUntilFinalVoice = true;

    private bool _started;
    private bool _modulationEnabled;
    private float _stableTimer;
    private Bus _masterBus;
    private float _masterInitialVolume = 1f;
    private readonly List<Bus> _objectBuses = new List<Bus>();
    private readonly List<float> _objectBusPanPhase = new List<float>();
    private readonly List<float> _objectBusVolumePhase = new List<float>();
    private readonly List<float> _objectBusPanSpeedMul = new List<float>();
    private readonly List<float> _objectBusVolumeSpeedMul = new List<float>();
    private Color[] _rendererBaseColors;
    private Color[] _rendererTargetColors;
    private Color[] _lightTargetColors;
    private float[] _lightBaseIntensities;
    private float[] _lightTargetIntensities;
    private bool _watchForExitTrigger;
    private bool _wasInsideExitTrigger;
    private bool _outsideSequenceStarted;
    private EventInstance _outsideFinalMusicInstance;
    private static EventInstance s_outsideFinalMusicInstanceGlobal;
    private bool _waitingForFinalDialogueEndToUnlockExit;
    private int _finalDialogueVoicesRemaining;

    private void Awake()
    {
        _masterBus = RuntimeManager.GetBus("bus:/");
        if (_masterBus.isValid())
            _masterBus.getVolume(out _masterInitialVolume);

        CacheObjectBuses();
        SyncFaderHapticTargetsWithWinCondition();

        // Optional convenience: use one same collider for blocking+trigger.
        if (exitTriggerCollider == null && exitBlockerAndTriggerCollider != null)
            exitTriggerCollider = exitBlockerAndTriggerCollider;

        if (hideExitHandlesUntilFinalVoice)
            SetExitHandlesActive(false);
    }

    private void OnEnable()
    {
        SubtitleManager.OnVoiceEnded += OnSubtitleVoiceEnded;
    }

    private void OnSubtitleVoiceEnded()
    {
        if (!_waitingForFinalDialogueEndToUnlockExit)
            return;
        if (_finalDialogueVoicesRemaining <= 0)
            return;

        _finalDialogueVoicesRemaining--;
        if (_finalDialogueVoicesRemaining <= 0)
        {
            _waitingForFinalDialogueEndToUnlockExit = false;
            EnableExitAfterFinalVoice();
        }
    }

    private void Update()
    {
        if (!_started)
        {
            if (AreAllFadersActiveAndMatching())
            {
                _stableTimer += Time.deltaTime;
                if (_stableTimer >= requiredStableTime)
                {
                    _started = true;
                    StartCoroutine(RunFinalAudioSequence());
                }
            }
            else
            {
                _stableTimer = 0f;
            }
        }

        if (_modulationEnabled)
        {
            UpdateObjectBusModulation(Time.time);
            UpdateVisualModulation(Time.deltaTime);
        }

        if (_watchForExitTrigger && !_outsideSequenceStarted && playerHead != null)
        {
            var monitored = houseBoundsTriggerCollider != null ? houseBoundsTriggerCollider : exitTriggerCollider;
            if (monitored == null)
                return;

            bool inside = monitored.bounds.Contains(playerHead.position);
            if (houseBoundsTriggerCollider != null)
            {
                // Nouveau comportement: on déclenche quand le joueur sort du volume de la maison.
                if (_wasInsideExitTrigger && !inside)
                    StartCoroutine(RunOutsideFinalSequence());
            }
            else
            {
                // Fallback legacy: on déclenche quand il entre dans le trigger de sortie local.
                if (!_wasInsideExitTrigger && inside)
                    StartCoroutine(RunOutsideFinalSequence());
            }
            _wasInsideExitTrigger = inside;
        }
    }

    private void OnValidate()
    {
        SyncFaderHapticTargetsWithWinCondition();
    }

    private void SyncFaderHapticTargetsWithWinCondition()
    {
        SyncSingleFaderHaptics(redFader);
        SyncSingleFaderHaptics(greenFader);
        SyncSingleFaderHaptics(blueFader);
    }

    private static void SyncSingleFaderHaptics(FaderTarget target)
    {
        if (target == null || target.fader == null) return;
        target.fader.ConfigureTargetHaptics(target.targetValue, target.tolerance);
    }

    private void CacheObjectBuses()
    {
        _objectBuses.Clear();
        _objectBusPanPhase.Clear();
        _objectBusVolumePhase.Clear();
        _objectBusPanSpeedMul.Clear();
        _objectBusVolumeSpeedMul.Clear();

        if (objectAudioBusPaths == null) return;

        for (int i = 0; i < objectAudioBusPaths.Length; i++)
        {
            string path = objectAudioBusPaths[i];
            if (string.IsNullOrWhiteSpace(path)) continue;

            Bus bus = RuntimeManager.GetBus(path);
            if (!bus.isValid()) continue;

            _objectBuses.Add(bus);
            _objectBusPanPhase.Add(Random.Range(0f, Mathf.PI * 2f));
            _objectBusVolumePhase.Add(Random.Range(0f, Mathf.PI * 2f));
            _objectBusPanSpeedMul.Add(Random.Range(
                Mathf.Min(panSpeedRandomMultiplierRange.x, panSpeedRandomMultiplierRange.y),
                Mathf.Max(panSpeedRandomMultiplierRange.x, panSpeedRandomMultiplierRange.y)));
            _objectBusVolumeSpeedMul.Add(Random.Range(
                Mathf.Min(volumeSpeedRandomMultiplierRange.x, volumeSpeedRandomMultiplierRange.y),
                Mathf.Max(volumeSpeedRandomMultiplierRange.x, volumeSpeedRandomMultiplierRange.y)));
        }
    }

    private bool AreAllFadersActiveAndMatching()
    {
        return IsFaderTargetReached(redFader) &&
               IsFaderTargetReached(greenFader) &&
               IsFaderTargetReached(blueFader);
    }

    private void UpdateVisualModulation(float deltaTime)
    {
        if (renderersToModulate != null)
        {
            EnsureRendererCaches();
            for (int i = 0; i < renderersToModulate.Length; i++)
            {
                var r = renderersToModulate[i];
                if (r == null) continue;

                Material mat = r.material;
                Color current = ReadMaterialColor(mat);
                if (ColorDistanceSqr(current, _rendererTargetColors[i]) <= targetRefreshThreshold * targetRefreshThreshold)
                    _rendererTargetColors[i] = RandomColor();

                Color next = Color.Lerp(current, _rendererTargetColors[i], deltaTime * colorLerpSpeed);
                WriteMaterialColor(mat, next);
            }
        }

        if (lightsToModulate != null)
        {
            EnsureLightCaches();
            float minI = Mathf.Min(lightIntensityRange.x, lightIntensityRange.y);
            float maxI = Mathf.Max(lightIntensityRange.x, lightIntensityRange.y);

            for (int i = 0; i < lightsToModulate.Length; i++)
            {
                var l = lightsToModulate[i];
                if (l == null) continue;

                if (ColorDistanceSqr(l.color, _lightTargetColors[i]) <= targetRefreshThreshold * targetRefreshThreshold)
                    _lightTargetColors[i] = RandomColor();

                if (Mathf.Abs(l.intensity - _lightTargetIntensities[i]) <= targetRefreshThreshold)
                    _lightTargetIntensities[i] = Random.Range(minI, maxI);

                l.color = Color.Lerp(l.color, _lightTargetColors[i], deltaTime * colorLerpSpeed);
                l.intensity = Mathf.Lerp(l.intensity, _lightTargetIntensities[i], deltaTime * lightIntensityLerpSpeed);
            }
        }
    }

    private void EnsureRendererCaches()
    {
        if (renderersToModulate == null) return;
        if (_rendererTargetColors != null && _rendererTargetColors.Length == renderersToModulate.Length) return;

        _rendererBaseColors = new Color[renderersToModulate.Length];
        _rendererTargetColors = new Color[renderersToModulate.Length];
        for (int i = 0; i < renderersToModulate.Length; i++)
        {
            var r = renderersToModulate[i];
            if (r == null) continue;
            Color c = ReadMaterialColor(r.material);
            _rendererBaseColors[i] = c;
            _rendererTargetColors[i] = RandomColor();
        }
    }

    private void EnsureLightCaches()
    {
        if (lightsToModulate == null) return;
        if (_lightTargetColors != null && _lightTargetColors.Length == lightsToModulate.Length) return;

        _lightTargetColors = new Color[lightsToModulate.Length];
        _lightBaseIntensities = new float[lightsToModulate.Length];
        _lightTargetIntensities = new float[lightsToModulate.Length];
        float minI = Mathf.Min(lightIntensityRange.x, lightIntensityRange.y);
        float maxI = Mathf.Max(lightIntensityRange.x, lightIntensityRange.y);

        for (int i = 0; i < lightsToModulate.Length; i++)
        {
            var l = lightsToModulate[i];
            if (l == null) continue;
            _lightTargetColors[i] = RandomColor();
            _lightBaseIntensities[i] = l.intensity;
            _lightTargetIntensities[i] = Random.Range(minI, maxI);
        }
    }

    private static bool IsFaderTargetReached(FaderTarget target)
    {
        if (target == null || target.fader == null) return false;
        if (!target.fader.gameObject.activeInHierarchy) return false;
        return Mathf.Abs(target.fader.value - target.targetValue) <= target.tolerance;
    }

    private IEnumerator RunFinalAudioSequence()
    {
        if (subtitleManager != null)
            StartCoroutine(PlayDelayedFinalDialogueRoutine());

        // 1) Fade out master.
        if (_masterBus.isValid())
            yield return FadeBusVolume(_masterBus, _masterInitialVolume, 0f, masterFadeOutDuration);

        // 2) Bring all faders to 1 progressively.
        yield return RaiseAllFadersToOne(fadersRiseDuration);

        // 3) Return global volume.
        if (_masterBus.isValid())
            yield return FadeBusVolume(_masterBus, 0f, _masterInitialVolume, masterFadeInDuration);

        // 4) Start continuous pan/volume modulation on object buses.
        _modulationEnabled = true;
    }

    private IEnumerator PlayDelayedFinalDialogueRoutine()
    {
        float d = Mathf.Max(0f, delaySecondsBeforeFinalDialogue);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        if (subtitleManager == null)
        {
            EnableExitAfterFinalVoice();
            yield break;
        }

        _finalDialogueVoicesRemaining = 0;
        if (!voNayaJeComprendsPas.IsNull)
        {
            subtitleManager.EnqueueSubtitledLine(voNayaJeComprendsPas);
            _finalDialogueVoicesRemaining++;
        }
        if (!voTherapeuteCestNormalController.IsNull)
        {
            subtitleManager.EnqueueSubtitledLine(voTherapeuteCestNormalController);
            _finalDialogueVoicesRemaining++;
        }
        if (!voTherapeuteQuandPreteSors.IsNull)
        {
            subtitleManager.EnqueueSubtitledLine(voTherapeuteQuandPreteSors);
            _finalDialogueVoicesRemaining++;
        }

        if (_finalDialogueVoicesRemaining > 0)
            _waitingForFinalDialogueEndToUnlockExit = true;
        else
            EnableExitAfterFinalVoice();
    }

    private void EnableExitAfterFinalVoice()
    {
        SetExitHandlesActive(true);
        UnlockExitForPlayer();
    }

    private void SetExitHandlesActive(bool active)
    {
        if (exitDoorHandlesToEnableAfterFinalVoice == null)
            return;
        for (int i = 0; i < exitDoorHandlesToEnableAfterFinalVoice.Length; i++)
        {
            var go = exitDoorHandlesToEnableAfterFinalVoice[i];
            if (go != null)
                go.SetActive(active);
        }
    }

    private void UnlockExitForPlayer()
    {
        var col = exitBlockerAndTriggerCollider != null ? exitBlockerAndTriggerCollider : exitTriggerCollider;
        if (col != null)
            col.isTrigger = true;

        var monitored = houseBoundsTriggerCollider != null ? houseBoundsTriggerCollider : exitTriggerCollider;
        if (playerHead != null && monitored != null)
            _wasInsideExitTrigger = monitored.bounds.Contains(playerHead.position);
        else
            _wasInsideExitTrigger = houseBoundsTriggerCollider != null;

        _watchForExitTrigger = true;
    }

    private IEnumerator RunOutsideFinalSequence()
    {
        if (_outsideSequenceStarted) yield break;
        _outsideSequenceStarted = true;
        _watchForExitTrigger = false;

        PlayExitHouseSound();

        if (fadeCanvasGroup != null)
            yield return FadeCanvas(0f, 1f, fadeToBlackDuration);

        // Keep synesthesia modulation running outside as requested.
        _modulationEnabled = true;

        DisableLocomotionForOutside();
        ReleaseHeldGrabObjectsBeforeOutsideTeleport();
        TeleportOutside();
        StartOutsideFinalMusic();

        if (creditsScroller != null)
            creditsScroller.BeginScroll();

        if (fadeCanvasGroup != null)
            yield return FadeCanvas(1f, 0f, fadeFromBlackDuration);
    }

    private IEnumerator RaiseAllFadersToOne(float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;

        float startR = GetFaderValue(redFader);
        float startG = GetFaderValue(greenFader);
        float startB = GetFaderValue(blueFader);

        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            SetFaderValue(redFader, Mathf.Lerp(startR, 1f, k));
            SetFaderValue(greenFader, Mathf.Lerp(startG, 1f, k));
            SetFaderValue(blueFader, Mathf.Lerp(startB, 1f, k));
            yield return null;
        }

        SetFaderValue(redFader, 1f);
        SetFaderValue(greenFader, 1f);
        SetFaderValue(blueFader, 1f);
    }

    private static float GetFaderValue(FaderTarget target)
    {
        if (target == null || target.fader == null) return 0f;
        return Mathf.Clamp01(target.fader.value);
    }

    private static void SetFaderValue(FaderTarget target, float value)
    {
        if (target == null || target.fader == null) return;
        FaderController f = target.fader;
        float v = Mathf.Clamp01(value);
        f.value = v;

        if (f.faderBase != null)
        {
            Vector3 local = f.faderBase.InverseTransformPoint(f.transform.position);
            local.x = Mathf.Lerp(-f.railHalfLength, f.railHalfLength, v);
            f.transform.position = f.faderBase.TransformPoint(local);
        }

        if (f.musicManager == null) return;
        switch (f.faderType)
        {
            case FaderController.FaderType.Violons:
                f.musicManager.SetVolumeViolons(v);
                break;
            case FaderController.FaderType.Guitare:
                f.musicManager.SetVolumeGuitare(v);
                break;
            case FaderController.FaderType.Bass:
                f.musicManager.SetVolumeBass(v);
                break;
        }
    }

    private void UpdateObjectBusModulation(float time)
    {
        if (_objectBuses.Count == 0) return;

        float minV = Mathf.Min(minModulatedVolume, maxModulatedVolume);
        float maxV = Mathf.Max(minModulatedVolume, maxModulatedVolume);

        for (int i = 0; i < _objectBuses.Count; i++)
        {
            Bus bus = _objectBuses[i];
            if (!bus.isValid()) continue;

            float panSpeed = panOscillationSpeed * _objectBusPanSpeedMul[i];
            float volSpeed = volumeOscillationSpeed * _objectBusVolumeSpeedMul[i];
            float pan = Mathf.Sin(time * panSpeed + _objectBusPanPhase[i]);
            float vol01 = (Mathf.Sin(time * volSpeed + _objectBusVolumePhase[i]) + 1f) * 0.5f;
            float vol = Mathf.Lerp(minV, maxV, vol01);

            bus.setVolume(vol);
            bus.getChannelGroup(out ChannelGroup group);
            if (group.hasHandle())
                group.setPan(pan);
        }
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null) yield break;
        EnsureFadeCanvasVisible();
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        fadeCanvasGroup.alpha = from;
        while (t < d)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / d));
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }

    private void EnsureFadeCanvasVisible()
    {
        Transform t = fadeCanvasGroup.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }

    private static IEnumerator FadeBusVolume(Bus bus, float from, float to, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        bus.setVolume(from);
        while (t < d)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(from, to, Mathf.Clamp01(t / d));
            bus.setVolume(v);
            yield return null;
        }
        bus.setVolume(to);
    }

    private void DisableLocomotionForOutside()
    {
        if (locomotionManager == null) return;

        locomotionManager.SetForceDisabled(true);
    }

    private void TeleportOutside()
    {
        if (playerRigRoot == null || outsideSpawn == null) return;
        playerRigRoot.SetPositionAndRotation(outsideSpawn.position, outsideSpawn.rotation);

        // Aligne le regard sur la façade : il faut tourner le rig du bon angle par rapport
        // au forward actuel de la caméra (offset Camera Offset), pas imposer un yaw absolu.
        if (outsideLookAt != null && playerHead != null)
        {
            Vector3 toTarget = outsideLookAt.position - playerHead.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f) return;
            toTarget.Normalize();

            Vector3 camFlat = playerHead.forward;
            camFlat.y = 0f;
            if (camFlat.sqrMagnitude < 0.001f) return;
            camFlat.Normalize();

            float yawDelta = Vector3.SignedAngle(camFlat, toTarget, Vector3.up);
            playerRigRoot.Rotate(0f, yawDelta, 0f, Space.World);
        }
    }

    private void PlayExitHouseSound()
    {
        if (exitHouseSound.IsNull) return;
        // Event 2D : la position passée à PlayOneShot n’a pas d’effet sur le mix.
        PlayOneShotFmod(exitHouseSound, Vector3.zero);
    }

    private static void PlayOneShotFmod(EventReference er, Vector3 position)
    {
        if (er.IsNull) return;

        try
        {
            RuntimeManager.PlayOneShot(er, position);
        }
        catch
        {
            //
        }
    }

    private void StartOutsideFinalMusic()
    {
        if (outsideFinalMusicEvent.IsNull) return;

        _outsideFinalMusicInstance = RuntimeManager.CreateInstance(outsideFinalMusicEvent);
        s_outsideFinalMusicInstanceGlobal = _outsideFinalMusicInstance;
        _outsideFinalMusicInstance.setVolume(0f);
        _outsideFinalMusicInstance.start();
        StartCoroutine(FadeOutsideFinalMusicIn());
    }

    public static void StopOutsideFinalMusicIfPlaying()
    {
        if (!RuntimeManager.IsInitialized)
            return;

        if (s_outsideFinalMusicInstanceGlobal.isValid())
        {
            s_outsideFinalMusicInstanceGlobal.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            s_outsideFinalMusicInstanceGlobal.release();
            s_outsideFinalMusicInstanceGlobal.clearHandle();
        }
    }

    private void ReleaseHeldGrabObjectsBeforeOutsideTeleport()
    {
        var grabbables = FindObjectsByType<XRGrabInteractable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < grabbables.Length; i++)
        {
            var grab = grabbables[i];
            if (grab == null) continue;
            var mgr = grab.interactionManager;
            var selecting = grab.interactorsSelecting;
            if (mgr == null || selecting == null || selecting.Count == 0)
                continue;

            var snapshot = new IXRSelectInteractor[selecting.Count];
            for (int s = 0; s < selecting.Count; s++)
                snapshot[s] = selecting[s];

            for (int s = snapshot.Length - 1; s >= 0; s--)
            {
                var interactor = snapshot[s];
                if (interactor == null) continue;
                mgr.SelectExit(interactor, grab);
            }
        }
    }

    private IEnumerator FadeOutsideFinalMusicIn()
    {
        if (!_outsideFinalMusicInstance.isValid()) yield break;

        float d = Mathf.Max(0.01f, outsideFinalMusicFadeInDuration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            _outsideFinalMusicInstance.setVolume(Mathf.Clamp01(t / d));
            yield return null;
        }
        _outsideFinalMusicInstance.setVolume(1f);
    }

    private void OnDisable()
    {
        SubtitleManager.OnVoiceEnded -= OnSubtitleVoiceEnded;
        _modulationEnabled = false;

        var fmodReady = RuntimeManager.IsInitialized;

        if (fmodReady && _masterBus.isValid())
            _masterBus.setVolume(_masterInitialVolume);

        // Ne pas appeler getChannelGroup ici : à l'arrêt du Play / déchargement FMOD, le studio peut être
        // déjà déchargé (ERR_STUDIO_NOT_LOADED) alors que IsInitialized reste vrai un instant.
        if (fmodReady)
        {
            for (int i = 0; i < _objectBuses.Count; i++)
            {
                Bus bus = _objectBuses[i];
                if (!bus.isValid()) continue;
                bus.setVolume(1f);
            }
        }

        if (renderersToModulate != null && _rendererBaseColors != null)
        {
            for (int i = 0; i < renderersToModulate.Length && i < _rendererBaseColors.Length; i++)
            {
                var r = renderersToModulate[i];
                if (r == null) continue;
                WriteMaterialColor(r.material, _rendererBaseColors[i]);
            }
        }

        if (lightsToModulate != null && _lightBaseIntensities != null)
        {
            for (int i = 0; i < lightsToModulate.Length && i < _lightBaseIntensities.Length; i++)
            {
                var l = lightsToModulate[i];
                if (l == null) continue;
                l.intensity = _lightBaseIntensities[i];
            }
        }

        if (fmodReady && _outsideFinalMusicInstance.isValid())
        {
            _outsideFinalMusicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _outsideFinalMusicInstance.release();
            _outsideFinalMusicInstance.clearHandle();
        }
        if (s_outsideFinalMusicInstanceGlobal.isValid())
            s_outsideFinalMusicInstanceGlobal.clearHandle();
    }

    private static Color ReadMaterialColor(Material mat)
    {
        if (mat == null) return Color.white;
        if (mat.HasProperty("_BaseColor")) return mat.GetColor("_BaseColor");
        if (mat.HasProperty("_Color")) return mat.GetColor("_Color");
        return Color.white;
    }

    private static void WriteMaterialColor(Material mat, Color c)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        else if (mat.HasProperty("_Color"))
            mat.color = c;
        else
            return;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", c * 0.7f);
        }
    }

    private static float ColorDistanceSqr(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        float da = a.a - b.a;
        return dr * dr + dg * dg + db * db + da * da;
    }

    private static Color RandomColor()
    {
        return Random.ColorHSV(0f, 1f, 0.65f, 1f, 0.75f, 1f);
    }
}
