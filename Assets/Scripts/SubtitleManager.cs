using FMOD.Studio;
using FMODUnity;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

public class SubtitleManager : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference voiceEventRef;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private GameObject subtitlePanel;

    private EventInstance voiceInstance;
    private bool _ignoreStoppedEvents = false;
    private bool _isShuttingDown = false;

    // Marqueurs dont le nom EST le texte à afficher.
    // "sub_end" est le seul cas spécial : il efface le texte.
    private const string END_MARKER = "sub_end";
    public static event System.Action OnVoiceEnded;

    void Start()
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
        if (subtitleText != null)
            subtitleText.text = "";

        voiceInstance = RuntimeManager.CreateInstance(voiceEventRef);
        voiceInstance.setCallback(OnFMODCallback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER | EVENT_CALLBACK_TYPE.STOPPED);
        RuntimeManager.AttachInstanceToGameObject(voiceInstance, gameObject);
        StartCoroutine(StartVoiceDelayed(5f));
    }

    private IEnumerator StartVoiceDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        voiceInstance.start();
    }

    public void ReplayVoice(float delaySeconds = 0f)
    {
        StartCoroutine(ReplayVoiceRoutine(delaySeconds));
    }

    private IEnumerator ReplayVoiceRoutine(float delaySeconds)
    {
        // Nettoie l'UI avant de rejouer
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
        if (subtitleText != null)
            subtitleText.text = "";

        // On veut ignorer uniquement le STOPPED causé par notre stop manuel.
        // Pour éviter les courses (fadeout qui finit après la 2e lecture), on stop IMMEDIATE
        // et on attend l'état STOPPED avant de relancer.
        _ignoreStoppedEvents = true;
        voiceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

        // Attendre que FMOD confirme l'arrêt (sécurité anti race-condition)
        const float timeoutSeconds = 1.5f;
        float t = 0f;
        while (t < timeoutSeconds)
        {
            if (voiceInstance.getPlaybackState(out PLAYBACK_STATE state) == FMOD.RESULT.OK &&
                state == PLAYBACK_STATE.STOPPED)
                break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        _ignoreStoppedEvents = false;
        voiceInstance.start();
    }

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private FMOD.RESULT OnFMODCallback(
        EVENT_CALLBACK_TYPE type,
        System.IntPtr instancePtr,
        System.IntPtr paramPtr)
    {
        try
        {
            if (_isShuttingDown) return FMOD.RESULT.OK;

            if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
            {
                if (paramPtr == System.IntPtr.Zero)
                    return FMOD.RESULT.OK;

                var props = (TIMELINE_MARKER_PROPERTIES)
                    Marshal.PtrToStructure(paramPtr, typeof(TIMELINE_MARKER_PROPERTIES));

                string markerName = props.name;

                var dispatcher = UnityMainThreadDispatcher.Instance();
                if (dispatcher != null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        // Unity "fake null" after destroy.
                        if (this == null || _isShuttingDown) return;
                        ShowSubtitle(markerName);
                    });
                }
            }

            if (type == EVENT_CALLBACK_TYPE.STOPPED)
            {
                var dispatcher = UnityMainThreadDispatcher.Instance();
                if (dispatcher != null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        if (this == null || _isShuttingDown) return;
                        if (_ignoreStoppedEvents) return;
                        OnVoiceEnded?.Invoke();
                    });
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
        return FMOD.RESULT.OK;
    }

    private void ShowSubtitle(string markerName)
    {
        if (_isShuttingDown) return;
        if (subtitlePanel == null || subtitleText == null)
            return;

        if (markerName == END_MARKER)
        {
            subtitlePanel.SetActive(false);
            subtitleText.text = "";
        }
        else
        {
            subtitleText.text = markerName;
            subtitlePanel.SetActive(true);
        }
    }

    void OnDestroy()
    {
        _isShuttingDown = true;
        voiceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        voiceInstance.release();
    }
}