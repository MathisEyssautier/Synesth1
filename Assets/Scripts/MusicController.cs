using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    public static string CurrentInstrument { get; private set; } = "guitar";

    private EventInstance _musicInstance;
    private EVENT_CALLBACK _callback;
    private bool _isPlaying = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void StartMusic(EventReference musicEvent)
    {
        if (_isPlaying) return;

        _musicInstance = RuntimeManager.CreateInstance(musicEvent);
        _callback = new EVENT_CALLBACK(MusicCallback);
        _musicInstance.setCallback(_callback, EVENT_CALLBACK_TYPE.TIMELINE_MARKER);
        _musicInstance.start();
        _isPlaying = true;
    }

    public void StopMusic()
    {
        if (!_isPlaying) return;

        _musicInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _musicInstance.release();
        _isPlaying = false;
        CurrentInstrument = "guitar";
    }

    public bool IsPlaying() => _isPlaying;

    [AOT.MonoPInvokeCallback(typeof(EVENT_CALLBACK))]
    static FMOD.RESULT MusicCallback(EVENT_CALLBACK_TYPE type, System.IntPtr instancePtr, System.IntPtr parameterPtr)
    {
        try
        {
            if (type == EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
            {
                if (parameterPtr == IntPtr.Zero)
                    return FMOD.RESULT.OK;

                var marker = (TIMELINE_MARKER_PROPERTIES)System.Runtime.InteropServices.Marshal.PtrToStructure(
                    parameterPtr, typeof(TIMELINE_MARKER_PROPERTIES));

                if (marker.name == "GuitarStart") CurrentInstrument = "guitar";
                else if (marker.name == "PianoStart") CurrentInstrument = "piano";
            }
        }
        catch (Exception e)
        {
        }
        return FMOD.RESULT.OK;
    }

    void OnDestroy()
    {
        StopMusic();
    }
}