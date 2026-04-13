using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;

/// <summary>
/// Onboarding salon : séquence voix off + lumières + iPod + piano jusqu'à l'exploration libre.
/// Les EventReference doivent pointer vers les events 2D avec marqueurs sous-titres + sub_end.
/// </summary>
public class SalonOnboardingController : MonoBehaviour
{
    private enum Phase
    {
        Intro3Lines,
        WaitLaisseLineEnd,
        WaitIpodGrab,
        WaitIpodOff,
        WaitFirstPianoNote,
        PostFirstPianoVo,
        Exploration
    }

    [Header("Voice / subtitles")]
    [SerializeField] private SubtitleManager subtitleManager;
    [Tooltip("Délai avant la première phrase (les 3 intro s'enfilent juste après).")]
    [SerializeField] private float delayBeforeFirstIntroLineSeconds = 5f;

    [Header("Intro (3 lignes puis lumière + iPod + Laisse)")]
    [SerializeField] private EventReference voNayaTuPeuxGarder;
    [SerializeField] private EventReference voNayaOkJeSuisPrete;
    [SerializeField] private EventReference voTherapeuteJeVaisCompter;
    [SerializeField] private EventReference voTherapeuteLaisseToiGuiderPar;

    [Header("iPod")]
    [SerializeField] private GrabbableMusicObject ipodMusicObject;
    [SerializeField] private XRGrabInteractable ipodGrab;

    [Header("Après prise iPod")]
    [SerializeField] private EventReference voTherapeuteTuEsDansTonMonde;

    [Header("Après iPod éteint (gâchette)")]
    [SerializeField] private EventReference voTherapeuteSuperNaya;
    [SerializeField] private EventReference voNayaAhCestLePiano;

    [Header("Après première touche piano")]
    [SerializeField] private EventReference voNayaWowLesSensations;
    [SerializeField] private EventReference voTherapeuteExploreEtTuAuras;

    [Header("Lighting progression")]
    [SerializeField] private Light pianoDirectionalLight;
    [SerializeField] private float pianoDirectionalTargetIntensity = 10f;
    [SerializeField] private float pianoDirectionalFadeDuration = 1.2f;
    [SerializeField] private bool rotatePianoLightWhenRoomTurnsOn = true;
    [SerializeField] private Vector3 pianoLightRotationAfterPiano = new Vector3(21f, 532f, 40f);
    [SerializeField] private float pianoLightRotationDuration = 1.2f;
    [SerializeField] private Light[] otherLightsToFadeIn;
    [SerializeField] private float otherLightsFadeDuration = 1.8f;

    [Header("Piano interaction gate")]
    [SerializeField] private PianoKey[] pianoKeys;
    [SerializeField] private bool autoDiscoverScenePianoKeys = true;

    [Header("Objects to reveal after first piano note")]
    [SerializeField] private GameObject[] objectsToActivateAfterPiano;

    [Header("Behaviours to enable after first piano note")]
    [SerializeField] private Behaviour[] behavioursToDisableUntilPiano;

    [Header("Exploration narrative (timers indices piano)")]
    [SerializeField] private SalonExplorationNarrative explorationNarrative;

    private Phase _phase = Phase.Intro3Lines;
    private int _introLinesRemaining = 3;
    private int _postFirstPianoVoRemaining;
    private float[] _otherLightsTargetIntensities;
    private bool _otherLightsFadeStarted;
    private PianoKey[] _resolvedPianoKeys;
    private bool _ipodGrabLineQueued;

    private void Awake()
    {
        ResolvePianoKeys();

        if (pianoDirectionalLight != null)
        {
            if (!pianoDirectionalLight.gameObject.activeSelf)
                pianoDirectionalLight.gameObject.SetActive(true);
            pianoDirectionalLight.intensity = 0f;
        }

        CacheAndTurnOffOtherLights();

        if (ipodMusicObject != null)
            ipodMusicObject.enabled = false;
        if (ipodGrab != null)
            ipodGrab.enabled = false;

        SetPianoInteractable(false);
        SetObjectsActive(objectsToActivateAfterPiano, false);
        SetBehavioursEnabled(behavioursToDisableUntilPiano, false);
    }

    private void Start()
    {
        if (subtitleManager == null)
            return;

        StartCoroutine(EnqueueIntroAfterDelay());
    }

    private IEnumerator EnqueueIntroAfterDelay()
    {
        float d = Mathf.Max(0f, delayBeforeFirstIntroLineSeconds);
        if (d > 0f)
            yield return new WaitForSeconds(d);

        if (subtitleManager == null)
            yield break;

        subtitleManager.EnqueueSubtitledLine(voNayaTuPeuxGarder);
        subtitleManager.EnqueueSubtitledLine(voNayaOkJeSuisPrete);
        subtitleManager.EnqueueSubtitledLine(voTherapeuteJeVaisCompter);
    }

    private void OnEnable()
    {
        SubtitleManager.OnVoiceEnded += OnVoiceEnded;
        GrabbableMusicObject.OnStateChanged += OnMusicObjectStateChanged;
        PianoKey.OnAnyKeyPressed += OnAnyPianoKeyPressed;

        if (ipodGrab != null)
            ipodGrab.selectEntered.AddListener(OnIpodSelectEntered);
    }

    private void OnDisable()
    {
        SubtitleManager.OnVoiceEnded -= OnVoiceEnded;
        GrabbableMusicObject.OnStateChanged -= OnMusicObjectStateChanged;
        PianoKey.OnAnyKeyPressed -= OnAnyPianoKeyPressed;

        if (ipodGrab != null)
            ipodGrab.selectEntered.RemoveListener(OnIpodSelectEntered);
    }

    private void OnIpodSelectEntered(SelectEnterEventArgs args)
    {
        if (_phase != Phase.WaitIpodGrab && _phase != Phase.WaitLaisseLineEnd)
            return;
        if (_ipodGrabLineQueued)
            return;

        _ipodGrabLineQueued = true;
        if (subtitleManager != null && !voTherapeuteTuEsDansTonMonde.IsNull)
            subtitleManager.EnqueueSubtitledLine(voTherapeuteTuEsDansTonMonde);
        _phase = Phase.WaitIpodOff;
    }

    private void OnVoiceEnded()
    {
        if (_phase == Phase.Intro3Lines)
        {
            _introLinesRemaining--;
            if (_introLinesRemaining > 0)
                return;

            StartCoroutine(FadeLightIntensity(pianoDirectionalLight, 0f, pianoDirectionalTargetIntensity, pianoDirectionalFadeDuration));
            EnableIpod();
            _phase = Phase.WaitLaisseLineEnd;

            if (subtitleManager != null && !voTherapeuteLaisseToiGuiderPar.IsNull)
                subtitleManager.EnqueueSubtitledLine(voTherapeuteLaisseToiGuiderPar);
            return;
        }

        if (_phase == Phase.WaitLaisseLineEnd)
        {
            _phase = Phase.WaitIpodGrab;
            return;
        }

        if (_phase == Phase.PostFirstPianoVo)
        {
            _postFirstPianoVoRemaining--;
            if (_postFirstPianoVoRemaining > 0)
                return;

            _phase = Phase.Exploration;
            if (explorationNarrative != null)
                explorationNarrative.NotifySalonExplorationStarted();
            return;
        }
    }

    private void OnMusicObjectStateChanged(GrabbableMusicObject obj, bool isOn)
    {
        if (_phase != Phase.WaitIpodOff)
            return;
        if (ipodMusicObject == null || obj != ipodMusicObject)
            return;
        if (isOn)
            return;

        if (subtitleManager != null && !voTherapeuteSuperNaya.IsNull)
            subtitleManager.EnqueueSubtitledLine(voTherapeuteSuperNaya);
        if (subtitleManager != null && !voNayaAhCestLePiano.IsNull)
            subtitleManager.EnqueueSubtitledLine(voNayaAhCestLePiano);

        SetPianoInteractable(true);
        _phase = Phase.WaitFirstPianoNote;
    }

    private void OnAnyPianoKeyPressed(PianoKey key)
    {
        if (_phase != Phase.WaitFirstPianoNote)
            return;

        if (!_otherLightsFadeStarted)
        {
            _otherLightsFadeStarted = true;
            StartCoroutine(FadeOtherLightsIn());
            if (rotatePianoLightWhenRoomTurnsOn && pianoDirectionalLight != null)
                StartCoroutine(RotateLightTo(pianoDirectionalLight.transform, pianoLightRotationAfterPiano, pianoLightRotationDuration));
        }

        SetObjectsActive(objectsToActivateAfterPiano, true);
        SetBehavioursEnabled(behavioursToDisableUntilPiano, true);

        _postFirstPianoVoRemaining = 0;
        if (subtitleManager != null && !voNayaWowLesSensations.IsNull)
        {
            subtitleManager.EnqueueSubtitledLine(voNayaWowLesSensations);
            _postFirstPianoVoRemaining++;
        }
        if (subtitleManager != null && !voTherapeuteExploreEtTuAuras.IsNull)
        {
            subtitleManager.EnqueueSubtitledLine(voTherapeuteExploreEtTuAuras);
            _postFirstPianoVoRemaining++;
        }

        if (_postFirstPianoVoRemaining == 0)
        {
            _phase = Phase.Exploration;
            explorationNarrative?.NotifySalonExplorationStarted();
        }
        else
            _phase = Phase.PostFirstPianoVo;
    }

    private void EnableIpod()
    {
        if (ipodMusicObject != null)
            ipodMusicObject.enabled = true;
        if (ipodGrab != null)
            ipodGrab.enabled = true;
    }

    private void SetPianoInteractable(bool interactable)
    {
        if (_resolvedPianoKeys == null) return;
        for (int i = 0; i < _resolvedPianoKeys.Length; i++)
        {
            if (_resolvedPianoKeys[i] == null) continue;
            _resolvedPianoKeys[i].SetInteractable(interactable);
        }
    }

    private void ResolvePianoKeys()
    {
        if (autoDiscoverScenePianoKeys)
        {
            _resolvedPianoKeys = FindObjectsByType<PianoKey>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (_resolvedPianoKeys != null && _resolvedPianoKeys.Length > 0)
                return;
        }

        _resolvedPianoKeys = pianoKeys;
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null) return;
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null) continue;
            objects[i].SetActive(active);
        }
    }

    private static void SetBehavioursEnabled(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null) return;
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null) continue;
            behaviours[i].enabled = enabled;
        }
    }

    private void CacheAndTurnOffOtherLights()
    {
        if (otherLightsToFadeIn == null) return;
        _otherLightsTargetIntensities = new float[otherLightsToFadeIn.Length];
        for (int i = 0; i < otherLightsToFadeIn.Length; i++)
        {
            var l = otherLightsToFadeIn[i];
            if (l == null) continue;
            if (!l.gameObject.activeSelf)
                l.gameObject.SetActive(true);
            _otherLightsTargetIntensities[i] = l.intensity;
            l.intensity = 0f;
        }
    }

    private IEnumerator FadeOtherLightsIn()
    {
        float d = Mathf.Max(0.01f, otherLightsFadeDuration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / d);
            for (int i = 0; i < otherLightsToFadeIn.Length; i++)
            {
                var l = otherLightsToFadeIn[i];
                if (l == null) continue;
                float target = (i < _otherLightsTargetIntensities.Length) ? _otherLightsTargetIntensities[i] : l.intensity;
                l.intensity = Mathf.Lerp(0f, target, k);
            }
            yield return null;
        }
    }

    private static IEnumerator FadeLightIntensity(Light lightRef, float from, float to, float duration)
    {
        if (lightRef == null) yield break;
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        lightRef.intensity = from;
        while (t < d)
        {
            t += Time.deltaTime;
            lightRef.intensity = Mathf.Lerp(from, to, Mathf.Clamp01(t / d));
            yield return null;
        }
        lightRef.intensity = to;
    }

    private static IEnumerator RotateLightTo(Transform tr, Vector3 targetEuler, float duration)
    {
        if (tr == null) yield break;
        Quaternion start = tr.rotation;
        Quaternion target = Quaternion.Euler(targetEuler);
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            tr.rotation = Quaternion.Slerp(start, target, Mathf.Clamp01(t / d));
            yield return null;
        }
        tr.rotation = target;
    }
}
