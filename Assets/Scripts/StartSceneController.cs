using System.Collections;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class StartSceneController : MonoBehaviour
{
    [Header("Scene loading")]
    [SerializeField] private string gameplaySceneName = "Synesthesia_SeineLab";
    [SerializeField] private float loadDelay = 0.05f;

    [Header("Input")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private float triggerPressThreshold = 0.8f;

    [Header("Visual fade (optional)")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("Menu music (optional)")]
    [SerializeField] private EventReference startMusicEvent;
    [SerializeField] private float musicFadeOutDuration = 0.5f;

    private bool _starting;
    private EventInstance _musicInstance;

    private void Start()
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        if (!startMusicEvent.IsNull)
        {
            _musicInstance = RuntimeManager.CreateInstance(startMusicEvent);
            _musicInstance.start();
        }
    }

    private void Update()
    {
        if (_starting) return;

        InputDevice left = InputDevices.GetDeviceAtXRNode(leftHandNode);
        if (!left.isValid) return;

        float triggerValue = 0f;
        left.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
        if (triggerValue >= triggerPressThreshold)
            StartCoroutine(StartGameRoutine());
    }

    private IEnumerator StartGameRoutine()
    {
        _starting = true;

        if (fadeCanvasGroup != null)
            yield return FadeCanvas(0f, 1f, fadeOutDuration);

        if (_musicInstance.isValid())
            yield return FadeOutAndStopMusic(musicFadeOutDuration);

        if (loadDelay > 0f)
            yield return new WaitForSeconds(loadDelay);

        GameAudioBootstrap.EnsureUnpausedForGameplay();
        SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
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

    private IEnumerator FadeOutAndStopMusic(float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        while (t < d)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(1f, 0f, Mathf.Clamp01(t / d));
            _musicInstance.setVolume(v);
            yield return null;
        }
        _musicInstance.setVolume(0f);
        _musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _musicInstance.release();
        _musicInstance.clearHandle();
    }

    private void OnDestroy()
    {
        if (_musicInstance.isValid())
        {
            _musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _musicInstance.release();
            _musicInstance.clearHandle();
        }
    }
}
