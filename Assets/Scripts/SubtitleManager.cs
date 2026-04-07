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
    private bool _isShuttingDown = false;
    private Coroutine _replayRoutine;
    private bool _isReplaying = false;
    private bool _pendingReplay = false;
    private float _pendingReplayDelay = 0f;
    private EVENT_CALLBACK _voiceCallback;

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

        // Keep a strong reference to FMOD callback delegate to avoid GC collection.
        _voiceCallback = new EVENT_CALLBACK(OnFMODCallback);
        RecreateVoiceInstance();
        StartCoroutine(StartVoiceDelayed(5f));
    }

    private IEnumerator StartVoiceDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_isShuttingDown) yield break;
        if (voiceInstance.isValid())
            voiceInstance.start();
    }

    public void ReplayVoice(float delaySeconds = 0f)
    {
        if (_isShuttingDown) return;

        // Ultra-safe strategy:
        // never stop a currently playing instance in the middle (avoids FMOD callback races).
        if (!voiceInstance.isValid())
            return;

        var result = voiceInstance.getPlaybackState(out PLAYBACK_STATE state);
        bool isPlaying = result == FMOD.RESULT.OK && state != PLAYBACK_STATE.STOPPED;
        if (isPlaying || _isReplaying)
        {
            // Queue one replay for when current voice naturally ends.
            _pendingReplay = true;
            _pendingReplayDelay = Mathf.Max(0f, delaySeconds);
            return;
        }

        StartReplayRoutine(delaySeconds);
    }

    private void StartReplayRoutine(float delaySeconds)
    {
        if (_replayRoutine != null)
            StopCoroutine(_replayRoutine);
        _replayRoutine = StartCoroutine(ReplayVoiceRoutine(delaySeconds));
    }

    private IEnumerator ReplayVoiceRoutine(float delaySeconds)
    {
        _isReplaying = true;

        // Nettoie l'UI avant de rejouer
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
        if (subtitleText != null)
            subtitleText.text = "";

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        if (!_isShuttingDown && voiceInstance.isValid())
            voiceInstance.start();
        _replayRoutine = null;
        _isReplaying = false;
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
                        if (_pendingReplay)
                        {
                            _pendingReplay = false;
                            float delay = _pendingReplayDelay;
                            _pendingReplayDelay = 0f;
                            StartReplayRoutine(delay);
                            return;
                        }
                        OnVoiceEnded?.Invoke();
                    });
                }
            }
        }
        catch
        {
            //
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
        if (_replayRoutine != null)
            StopCoroutine(_replayRoutine);
        SafeStopAndReleaseVoiceInstance();
    }

    private void RecreateVoiceInstance()
    {
        if (_isShuttingDown || voiceEventRef.IsNull) return;
        voiceInstance = RuntimeManager.CreateInstance(voiceEventRef);
        if (!voiceInstance.isValid()) return;
        voiceInstance.setCallback(_voiceCallback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER | EVENT_CALLBACK_TYPE.STOPPED);
        RuntimeManager.AttachInstanceToGameObject(voiceInstance, gameObject);
    }

    private void SafeStopAndReleaseVoiceInstance()
    {
        if (!voiceInstance.isValid()) return;
        voiceInstance.setCallback(null);
        voiceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        voiceInstance.release();
        voiceInstance.clearHandle();
    }
}