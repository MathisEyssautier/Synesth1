using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SalonOnboardingController : MonoBehaviour
{
    private enum FlowState
    {
        WaitVoice1End,
        WaitIpodOff,
        WaitVoice2End,
        WaitFirstPianoNote,
        WaitVoice3End,
        Done
    }

    [Header("Voice / subtitles")]
    [SerializeField] private SubtitleManager subtitleManager;
    [SerializeField] private float replayDelayAfterIpodOff = 0.1f;
    [SerializeField] private float replayDelayAfterFirstPianoNote = 0.1f;

    [Header("Lighting progression")]
    [SerializeField] private Light pianoDirectionalLight;
    [SerializeField] private float pianoDirectionalTargetIntensity = 10f;
    [SerializeField] private float pianoDirectionalFadeDuration = 1.2f;
    [SerializeField] private bool rotatePianoLightWhenRoomTurnsOn = true;
    [SerializeField] private Vector3 pianoLightRotationAfterPiano = new Vector3(21f, 532f, 40f);
    [SerializeField] private float pianoLightRotationDuration = 1.2f;
    [SerializeField] private Light[] otherLightsToFadeIn;
    [SerializeField] private float otherLightsFadeDuration = 1.8f;

    [Header("iPod (starts later)")]
    [SerializeField] private GrabbableMusicObject ipodMusicObject;
    [SerializeField] private XRGrabInteractable ipodGrab;

    [Header("Piano interaction gate")]
    [SerializeField] private PianoKey[] pianoKeys;
    [SerializeField] private bool autoDiscoverScenePianoKeys = true;

    [Header("Objects to reveal after first piano note (cube, old phone, etc.)")]
    [SerializeField] private GameObject[] objectsToActivateAfterPiano;

    [Header("Potential light conflicts (disable until first piano note)")]
    [SerializeField] private Behaviour[] behavioursToDisableUntilPiano;

    private FlowState _state = FlowState.WaitVoice1End;
    private float[] _otherLightsTargetIntensities;
    private bool _otherLightsFadeStarted;
    private PianoKey[] _resolvedPianoKeys;

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

    private void OnEnable()
    {
        SubtitleManager.OnVoiceEnded += OnVoiceEnded;
        GrabbableMusicObject.OnStateChanged += OnMusicObjectStateChanged;
        PianoKey.OnAnyKeyPressed += OnAnyPianoKeyPressed;
    }

    private void OnDisable()
    {
        SubtitleManager.OnVoiceEnded -= OnVoiceEnded;
        GrabbableMusicObject.OnStateChanged -= OnMusicObjectStateChanged;
        PianoKey.OnAnyKeyPressed -= OnAnyPianoKeyPressed;
    }

    private void OnVoiceEnded()
    {
        if (_state == FlowState.WaitVoice1End)
        {
            StartCoroutine(FadeLightIntensity(pianoDirectionalLight, 0f, pianoDirectionalTargetIntensity, pianoDirectionalFadeDuration));
            EnableIpod();
            _state = FlowState.WaitIpodOff;
            return;
        }

        if (_state == FlowState.WaitVoice2End)
        {
            SetPianoInteractable(true);
            _state = FlowState.WaitFirstPianoNote;
            return;
        }

        if (_state == FlowState.WaitVoice3End)
        {
            _state = FlowState.Done;
        }
    }

    private void OnMusicObjectStateChanged(GrabbableMusicObject obj, bool isOn)
    {
        if (_state != FlowState.WaitIpodOff) return;
        if (ipodMusicObject == null || obj != ipodMusicObject) return;
        if (isOn) return;

        if (subtitleManager != null)
            subtitleManager.ReplayVoice(replayDelayAfterIpodOff);
        _state = FlowState.WaitVoice2End;
    }

    private void OnAnyPianoKeyPressed(PianoKey key)
    {
        if (_state != FlowState.WaitFirstPianoNote) return;

        if (!_otherLightsFadeStarted)
        {
            _otherLightsFadeStarted = true;
            StartCoroutine(FadeOtherLightsIn());
            if (rotatePianoLightWhenRoomTurnsOn && pianoDirectionalLight != null)
                StartCoroutine(RotateLightTo(pianoDirectionalLight.transform, pianoLightRotationAfterPiano, pianoLightRotationDuration));
        }

        SetObjectsActive(objectsToActivateAfterPiano, true);
        SetBehavioursEnabled(behavioursToDisableUntilPiano, true);

        if (subtitleManager != null)
            subtitleManager.ReplayVoice(replayDelayAfterFirstPianoNote);

        _state = FlowState.WaitVoice3End;
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
