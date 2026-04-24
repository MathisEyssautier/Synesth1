using System.Collections;
using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;

/// <summary>
/// Onboarding salon : séquence voix off + lumières + iPod + piano jusqu'à l'exploration libre.
/// Intro : une seule ligne VO (anc. 3 lignes fusionnées). Les EventReference : events 2D + marqueurs sous-titres + sub_end.
/// </summary>
public class SalonOnboardingController : MonoBehaviour
{
    private enum Phase
    {
        IntroFirstLine,
        WaitIntroVoiceStopAfterSubEnd,
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

    [Header("Intro (1 ligne regroupée puis lumière + iPod + Laisse)")]
    [SerializeField] private EventReference voNayaTuPeuxGarder;
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
    [Header("Intro light reveal (cube blocker)")]
    [SerializeField] private Transform introWindowLightBlocker;
    [SerializeField] private float introBlockerTravelUpDistance = 2f;
    [SerializeField] private float introBlockerTravelDuration = 1.2f;
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

    private Phase _phase = Phase.IntroFirstLine;
    private int _introLinesRemaining = 1;
    private int _postFirstPianoVoRemaining;
    private float[] _otherLightsTargetIntensities;
    private bool _otherLightsFadeStarted;
    private PianoKey[] _resolvedPianoKeys;
    private bool _ipodGrabLineQueued;
    private bool _introRevealTriggered;

    private void Awake()
    {
        ResolvePianoKeys();

        if (pianoDirectionalLight != null)
        {
            if (!pianoDirectionalLight.gameObject.activeSelf)
                pianoDirectionalLight.gameObject.SetActive(true);
            pianoDirectionalLight.intensity = pianoDirectionalTargetIntensity;
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
            yield return new WaitForSecondsRealtime(d);

        if (subtitleManager == null)
            yield break;

        subtitleManager.EnqueueSubtitledLine(voNayaTuPeuxGarder);
    }

    private void OnEnable()
    {
        SubtitleManager.OnVoiceEnded += OnVoiceEnded;
        SubtitleManager.OnSubtitleMarker += OnSubtitleMarker;
        GrabbableMusicObject.OnStateChanged += OnMusicObjectStateChanged;
        PianoKey.OnAnyKeyPressed += OnAnyPianoKeyPressed;

        if (ipodGrab != null)
            ipodGrab.selectEntered.AddListener(OnIpodSelectEntered);
    }

    private void OnDisable()
    {
        SubtitleManager.OnVoiceEnded -= OnVoiceEnded;
        SubtitleManager.OnSubtitleMarker -= OnSubtitleMarker;
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

    private void OnSubtitleMarker(string markerName)
    {
        if (_phase != Phase.IntroFirstLine) return;
        if (_introRevealTriggered) return;
        if (!IsEndSubtitleMarker(markerName)) return;

        TriggerIntroRevealAndQueueLaisse();
        // On attend encore le STOP FMOD du 1er event avant de considérer
        // qu'on est réellement en attente de la ligne suivante.
        _phase = Phase.WaitIntroVoiceStopAfterSubEnd;
    }

    private void OnVoiceEnded()
    {
        if (_phase == Phase.IntroFirstLine)
        {
            _introLinesRemaining--;
            if (_introLinesRemaining > 0)
                return;

            // Fallback si l'event n'avait pas de marqueur sub_end.
            TriggerIntroRevealAndQueueLaisse();
            _phase = Phase.WaitLaisseLineEnd;
            return;
        }

        if (_phase == Phase.WaitIntroVoiceStopAfterSubEnd)
        {
            _phase = Phase.WaitLaisseLineEnd;
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

    private void TriggerIntroRevealAndQueueLaisse()
    {
        if (_introRevealTriggered) return;
        _introRevealTriggered = true;

        if (introWindowLightBlocker != null)
            StartCoroutine(MoveBlockerUpThenHide(introWindowLightBlocker, introBlockerTravelUpDistance, introBlockerTravelDuration));

        EnableIpod();
        if (subtitleManager != null && !voTherapeuteLaisseToiGuiderPar.IsNull)
            subtitleManager.EnqueueSubtitledLine(voTherapeuteLaisseToiGuiderPar);
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

    private static bool IsEndSubtitleMarker(string markerName)
    {
        if (string.IsNullOrEmpty(markerName))
            return false;
        string t = markerName.Trim().TrimStart('\uFEFF');
        if (string.Equals(t, "sub_end", StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(t, "sub end", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static IEnumerator MoveBlockerUpThenHide(Transform blocker, float travelUpDistance, float duration)
    {
        if (blocker == null) yield break;

        Vector3 startPos = blocker.position;
        Vector3 targetPos = startPos + Vector3.up * travelUpDistance;
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            blocker.position = Vector3.Lerp(startPos, targetPos, Mathf.Clamp01(t / d));
            yield return null;
        }

        blocker.position = targetPos;
        blocker.gameObject.SetActive(false);
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
