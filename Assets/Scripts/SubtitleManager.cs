using System;
using FMOD.Studio;
using FMODUnity;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

/// <summary>
/// File d'events FMOD 2D avec marqueurs = sous-titres (sauf lignes attach?es 3D sans UI).
/// Les lignes se jouent ? la suite ; les nouvelles demandes sont mises en file si une lecture est en cours.
/// </summary>
public class SubtitleManager : MonoBehaviour
{
    private struct QueuedLine
    {
        public EventReference EventRef;
        public Transform AttachTransform;
        public bool UseSubtitles;
    }

    [Header("FMOD (optionnel, legacy)")]
    [Tooltip("Si Auto Start Single Voice est coch?, cet event est jou? au d?marrage (file).")]
    [SerializeField] private EventReference voiceEventRef;
    [SerializeField] private bool autoStartSingleVoice = false;
    [SerializeField] private float autoStartDelaySeconds = 0f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private GameObject subtitlePanel;

    private readonly Queue<QueuedLine> _queue = new Queue<QueuedLine>();

    private EventInstance voiceInstance;
    private bool _isShuttingDown;
    private Coroutine _replayRoutine;
    private bool _isReplaying;
    private bool _pendingReplay;
    private float _pendingReplayDelay;
    private bool _currentLineUsesSubtitles = true;

    private const string END_MARKER = "sub_end";
    public static event System.Action OnVoiceEnded;
    public static event System.Action<string> OnSubtitleMarker;

    /// <summary>Vrai si aucune ligne en file et aucune lecture FMOD active (ou lecture arr?t?e).</summary>
    public bool IsNarrationIdle()
    {
        if (_isShuttingDown)
            return true;
        if (_queue.Count > 0)
            return false;
        if (!voiceInstance.isValid())
            return true;
        if (voiceInstance.getPlaybackState(out PLAYBACK_STATE st) != FMOD.RESULT.OK)
            return true;
        return st == PLAYBACK_STATE.STOPPED;
    }

    private static bool IsEndSubtitleMarker(string markerName)
    {
        if (string.IsNullOrEmpty(markerName))
            return false;
        string t = markerName.Trim().TrimStart('\uFEFF');
        if (string.Equals(t, END_MARKER, StringComparison.OrdinalIgnoreCase))
            return true;
        if (string.Equals(t, "sub end", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private void Start()
    {
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
        if (subtitleText != null)
            subtitleText.text = "";

        if (autoStartSingleVoice && !voiceEventRef.IsNull)
            StartCoroutine(AutoStartRoutine());
    }

    private IEnumerator AutoStartRoutine()
    {
        if (autoStartDelaySeconds > 0f)
            yield return new WaitForSeconds(autoStartDelaySeconds);
        if (_isShuttingDown) yield break;
        EnqueueSubtitledLine(voiceEventRef);
    }

    /// <summary>Ajoute une ligne avec sous-titres (marqueurs ? UI). 2D : suit le Studio Listener.</summary>
    public void EnqueueSubtitledLine(EventReference eventReference)
    {
        if (eventReference.IsNull) return;
        _queue.Enqueue(new QueuedLine
        {
            EventRef = eventReference,
            AttachTransform = null,
            UseSubtitles = true
        });
        TryStartNextInQueue();
    }

    /// <summary>Enfile plusieurs lignes subtitr?es dans l'ordre.</summary>
    public void EnqueueSubtitledLines(params EventReference[] lines)
    {
        if (lines == null) return;
        for (int i = 0; i < lines.Length; i++)
            EnqueueSubtitledLine(lines[i]);
    }

    /// <summary>Event 3D sans sous-titres (ex. vocal parents sur la radio). Attacher au Transform donn?.</summary>
    public void EnqueueAttachedWithoutSubtitles(EventReference eventReference, Transform attachTo)
    {
        if (eventReference.IsNull || attachTo == null) return;
        _queue.Enqueue(new QueuedLine
        {
            EventRef = eventReference,
            AttachTransform = attachTo,
            UseSubtitles = false
        });
        TryStartNextInQueue();
    }

    /// <summary>Compatibilit? : rejoue voiceEventRef s'il est assign?.</summary>
    public void ReplayVoice(float delaySeconds = 0f)
    {
        if (_isShuttingDown || voiceEventRef.IsNull) return;

        if (!voiceInstance.isValid())
        {
            StartReplayRoutine(delaySeconds, voiceEventRef);
            return;
        }

        var result = voiceInstance.getPlaybackState(out PLAYBACK_STATE state);
        bool isPlaying = result == FMOD.RESULT.OK && state != PLAYBACK_STATE.STOPPED;
        if (isPlaying || _isReplaying)
        {
            _pendingReplay = true;
            _pendingReplayDelay = Mathf.Max(0f, delaySeconds);
            return;
        }

        StartReplayRoutine(delaySeconds, voiceEventRef);
    }

    private void StartReplayRoutine(float delaySeconds, EventReference er)
    {
        if (_replayRoutine != null)
            StopCoroutine(_replayRoutine);
        _replayRoutine = StartCoroutine(ReplayVoiceRoutine(delaySeconds, er));
    }

    private IEnumerator ReplayVoiceRoutine(float delaySeconds, EventReference er)
    {
        _isReplaying = true;
        if (subtitlePanel != null)
            subtitlePanel.SetActive(false);
        if (subtitleText != null)
            subtitleText.text = "";

        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        if (!_isShuttingDown)
            EnqueueSubtitledLine(er);

        _replayRoutine = null;
        _isReplaying = false;
    }

    private void TryStartNextInQueue()
    {
        if (_isShuttingDown) return;
        if (_queue.Count == 0) return;

        if (voiceInstance.isValid())
        {
            if (voiceInstance.getPlaybackState(out PLAYBACK_STATE st) == FMOD.RESULT.OK &&
                st != PLAYBACK_STATE.STOPPED)
                return;
            SafeStopAndReleaseVoiceInstance();
        }

        StartNextQueuedInternal();
    }

    /// <summary>
    /// Libère une instance voix bloquée puis démarre la prochaine ligne en file.
    /// Utile après une pause (ex. sonnerie) si <see cref="TryStartNextInQueue"/> ne débloque pas.
    /// </summary>
    public void ForcePlayNextQueuedLine()
    {
        if (_isShuttingDown || _queue.Count == 0) return;
        SafeStopAndReleaseVoiceInstance();
        StartNextQueuedInternal();
    }

    private void StartNextQueuedInternal()
    {
        if (_queue.Count == 0) return;

        var line = _queue.Peek();
        SafeStopAndReleaseVoiceInstance();

        try
        {
            voiceInstance = RuntimeManager.CreateInstance(line.EventRef);
        }
        catch (EventNotFoundException ex)
        {
            _queue.Dequeue();
            Debug.LogError("[SubtitleManager] FMOD event not found (bank manquant ou GUID obsol?te) : " + ex.Message);
            TryStartNextInQueue();
            return;
        }

        if (!voiceInstance.isValid())
        {
            _queue.Dequeue();
            TryStartNextInQueue();
            return;
        }

        _queue.Dequeue();

        _currentLineUsesSubtitles = line.UseSubtitles;

        GCHandle selfHandle = GCHandle.Alloc(this, GCHandleType.Normal);
        voiceInstance.setUserData(GCHandle.ToIntPtr(selfHandle));
        voiceInstance.setCallback(FmodEventCallback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER | EVENT_CALLBACK_TYPE.STOPPED);

        GameObject attachGo = ResolveAttachTarget(line);
        RuntimeManager.AttachInstanceToGameObject(voiceInstance, attachGo);

        voiceInstance.start();
    }

    private GameObject ResolveAttachTarget(QueuedLine line)
    {
        if (line.AttachTransform != null)
            return line.AttachTransform.gameObject;

        var listener = UnityEngine.Object.FindFirstObjectByType<FMODUnity.StudioListener>();
        if (listener != null)
            return listener.gameObject;

        return gameObject;
    }

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    private static FMOD.RESULT FmodEventCallback(
        EVENT_CALLBACK_TYPE type,
        IntPtr eventPtr,
        IntPtr paramPtr)
    {
        try
        {
            var ev = new EventInstance(eventPtr);
            if (ev.getUserData(out IntPtr userdata) != FMOD.RESULT.OK || userdata == IntPtr.Zero)
                return FMOD.RESULT.OK;

            if (!GCHandle.FromIntPtr(userdata).IsAllocated)
                return FMOD.RESULT.OK;

            var mgr = GCHandle.FromIntPtr(userdata).Target as SubtitleManager;
            if (mgr == null)
                return FMOD.RESULT.OK;

            if (mgr._isShuttingDown)
                return FMOD.RESULT.OK;

            if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
            {
                if (!mgr._currentLineUsesSubtitles)
                    return FMOD.RESULT.OK;
                if (paramPtr == IntPtr.Zero)
                    return FMOD.RESULT.OK;

                var props = (TIMELINE_MARKER_PROPERTIES)
                    Marshal.PtrToStructure(paramPtr, typeof(TIMELINE_MARKER_PROPERTIES));

                string markerName = props.name;

                var dispatcher = UnityMainThreadDispatcher.Instance();
                if (dispatcher != null)
                {
                    dispatcher.Enqueue(() =>
                    {
                        if (mgr == null || mgr._isShuttingDown) return;
                        OnSubtitleMarker?.Invoke(markerName);
                        mgr.ShowSubtitle(markerName);
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
                        if (mgr == null || mgr._isShuttingDown) return;

                        if (mgr._pendingReplay)
                        {
                            mgr._pendingReplay = false;
                            float delay = mgr._pendingReplayDelay;
                            mgr._pendingReplayDelay = 0f;
                            mgr.StartReplayRoutine(delay, mgr.voiceEventRef);
                            return;
                        }

                        // Libérer l'instance AVANT OnVoiceEnded : sinon un Enqueue dans un
                        // abonné démarre la ligne suivante puis est tuée par le release ci-dessous.
                        mgr.SafeStopAndReleaseVoiceInstance();
                        OnVoiceEnded?.Invoke();
                        if (mgr._queue.Count > 0)
                            mgr.TryStartNextInQueue();
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

        if (IsEndSubtitleMarker(markerName))
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

    private void OnDestroy()
    {
        _isShuttingDown = true;
        if (_replayRoutine != null)
            StopCoroutine(_replayRoutine);
        _queue.Clear();
        SafeStopAndReleaseVoiceInstance();
    }

    private void SafeStopAndReleaseVoiceInstance()
    {
        if (!voiceInstance.isValid()) return;
        voiceInstance.setCallback(null);
        voiceInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);

        if (voiceInstance.getUserData(out IntPtr userdata) == FMOD.RESULT.OK && userdata != IntPtr.Zero)
        {
            if (GCHandle.FromIntPtr(userdata).IsAllocated)
                GCHandle.FromIntPtr(userdata).Free();
            voiceInstance.setUserData(IntPtr.Zero);
        }

        voiceInstance.release();
        voiceInstance.clearHandle();
    }
}
