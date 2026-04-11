using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using System.Collections;

public class FMODMeteringSource : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Script qui fournit un EventInstance")]
    [SerializeField] private MonoBehaviour eventProvider;

    private EventInstance _eventInstance;

    private FMOD.DSP _dsp;
    private FMOD.DSP_METERING_INFO _inputInfo;
    private FMOD.DSP_METERING_INFO _outputInfo;
    private FMOD.Studio.PLAYBACK_STATE _playbackState;

    private Coroutine _meterCoroutine;

    public float MeterLeft  { get; private set; }
    public float MeterRight { get; private set; }

    void OnEnable()
    {
        _meterCoroutine = StartCoroutine(WaitForInstanceThenSetup());
    }

    IEnumerator WaitForInstanceThenSetup()
    {
        // Attendre que le provider soit assigné et l'instance valide
        while (!_eventInstance.isValid())
        {
            TryGetEventInstance();
            yield return null;
        }

        yield return SetupMetering();
    }
    void OnDisable()
    {
        if (_meterCoroutine != null)
        {
            StopCoroutine(_meterCoroutine);
            _meterCoroutine = null;
        }
    }

    void TryGetEventInstance()
    {
        if (eventProvider == null)
        {
            Debug.LogWarning("[FMODMeteringSource] Aucun provider assigné.");
            return;
        }

        // 🔥 Ici on récupère dynamiquement la propriété EventInstance
        var prop = eventProvider.GetType().GetProperty("EventInstance");

        if (prop != null)
        {
            _eventInstance = (EventInstance)prop.GetValue(eventProvider);

        }
        else
        {
            Debug.LogError("[FMODMeteringSource] Le provider n'expose pas EventInstance.");
        }
    }

    IEnumerator SetupMetering()
    {
        do
        {
            _eventInstance.getPlaybackState(out _playbackState);
            yield return null;
        }
        while (_playbackState != PLAYBACK_STATE.PLAYING);

        _eventInstance.getChannelGroup(out FMOD.ChannelGroup channelGroup);

        channelGroup.getDSP(
            FMOD.CHANNELCONTROL_DSP_INDEX.FADER,
            out _dsp
        );

        _dsp.setMeteringEnabled(true, true);
    }

    private void Update()
    {
        if (!_eventInstance.isValid()) return;
        Debug.Log("FMOD");


        _eventInstance.getPlaybackState(out _playbackState);

        if (_playbackState == PLAYBACK_STATE.PLAYING && _dsp.hasHandle())
        {
            _dsp.getMeteringInfo(out _inputInfo, out _outputInfo);

            MeterLeft  = _outputInfo.rmslevel[0];
            MeterRight = _outputInfo.rmslevel[1];
            Debug.Log($"FMOD L: {MeterLeft:F4} | R: {MeterRight:F4}");

        }
    }
}