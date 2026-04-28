using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System;
using System.Reflection;

public class FMODMeteringSource : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Script qui fournit un EventInstance")]
    [SerializeField] private MonoBehaviour eventProvider;

    private EventInstance _eventInstance;
    private bool _meterReady;
    private bool _missingProviderLogged;
    private bool _missingEventInstanceLogged;

    private FMOD.DSP _dsp;
    private FMOD.DSP_METERING_INFO _inputInfo;
    private FMOD.DSP_METERING_INFO _outputInfo;
    private FMOD.Studio.PLAYBACK_STATE _playbackState;

    public float MeterLeft  { get; private set; }
    public float MeterRight { get; private set; }

    private void OnEnable()
    {
        _eventInstance.clearHandle();
        _meterReady = false;
        _missingProviderLogged = false;
        _missingEventInstanceLogged = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }

    private void OnDisable()
    {
        if (_dsp.hasHandle())
        {
            _dsp.clearHandle();
        }
        _meterReady = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }

    private bool TryResolveEventInstance(out EventInstance resolved)
    {
        resolved = default;

        if (eventProvider == null)
        {
            if (!_missingProviderLogged)
            {
                Debug.LogWarning("[FMODMeteringSource] Aucun provider assigné.");
                _missingProviderLogged = true;
            }
            return false;
        }

        _missingProviderLogged = false;
        Type type = eventProvider.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var prop = type.GetProperty("EventInstance", flags);
        if (prop != null && prop.PropertyType == typeof(EventInstance))
        {
            resolved = (EventInstance)prop.GetValue(eventProvider);
            _missingEventInstanceLogged = false;
            return true;
        }

        var field = type.GetField("EventInstance", flags);
        if (field != null && field.FieldType == typeof(EventInstance))
        {
            resolved = (EventInstance)field.GetValue(eventProvider);
            _missingEventInstanceLogged = false;
            return true;
        }

        var method = type.GetMethod("GetEventInstance", flags, null, System.Type.EmptyTypes, null);
        if (method != null && method.ReturnType == typeof(EventInstance))
        {
            resolved = (EventInstance)method.Invoke(eventProvider, null);
            _missingEventInstanceLogged = false;
            return true;
        }

        if (!_missingEventInstanceLogged)
        {
            Debug.LogError("[FMODMeteringSource] Le provider n'expose pas EventInstance.");
            _missingEventInstanceLogged = true;
        }
        return false;
    }

    private void EnsureMeterSetupIfNeeded()
    {
        if (_meterReady || !_eventInstance.isValid())
            return;

        if (_eventInstance.getPlaybackState(out _playbackState) != FMOD.RESULT.OK)
            return;
        if (_playbackState != PLAYBACK_STATE.PLAYING)
            return;

        if (_eventInstance.getChannelGroup(out FMOD.ChannelGroup channelGroup) != FMOD.RESULT.OK)
            return;
        if (channelGroup.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.FADER, out _dsp) != FMOD.RESULT.OK)
            return;

        _dsp.setMeteringEnabled(true, true);
        _meterReady = _dsp.hasHandle();
    }

    private void Update()
    {
        if (!TryResolveEventInstance(out EventInstance resolved) || !resolved.isValid())
        {
            _meterReady = false;
            _eventInstance.clearHandle();
            MeterLeft = 0f;
            MeterRight = 0f;
            return;
        }

        if (!_eventInstance.isValid() || !_eventInstance.Equals(resolved))
        {
            _eventInstance = resolved;
            _meterReady = false;
            if (_dsp.hasHandle())
                _dsp.clearHandle();
        }

        EnsureMeterSetupIfNeeded();
        if (!_meterReady || !_eventInstance.isValid())
            return;

        _eventInstance.getPlaybackState(out _playbackState);

        if (_playbackState == PLAYBACK_STATE.PLAYING && _dsp.hasHandle())
        {
            _dsp.getMeteringInfo(out _inputInfo, out _outputInfo);

            MeterLeft  = _outputInfo.rmslevel[0];
            MeterRight = _outputInfo.rmslevel[1];
        }
        else
        {
            MeterLeft = 0f;
            MeterRight = 0f;
        }
    }
}