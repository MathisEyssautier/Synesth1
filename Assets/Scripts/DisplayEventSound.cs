using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class DisplayEventSound : MonoBehaviour
{
    public FMODUnity.EventReference eventReference;

    [Header("VFX Targets")]
    [SerializeField] private List<VisualEffect> vfxTargets = new();

    [Header("Amplitude")]
    [SerializeField] private string propertyName = "Amplitude";
    [SerializeField] private float amplitudeMultiplier = 50f;

    public float leftVolume;
    public float rightVolume;

    private int _propertyID;

    FMOD.DSP_METERING_INFO inputInfo, outputInfo;
    FMOD.Studio.PLAYBACK_STATE playbackState;
    FMOD.DSP dsp;

    void Start()
    {
        _propertyID = Shader.PropertyToID(propertyName);
        StartCoroutine(PlayEventAsync());
    }

    IEnumerator PlayEventAsync()
    {
        FMODUnity.RuntimeManager.StudioSystem.getEvent(eventReference.Path, out FMOD.Studio.EventDescription eventDescription);
        eventDescription.createInstance(out FMOD.Studio.EventInstance eventInstance);
        eventInstance.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
        eventInstance.start();

        // Wait until playing
        do
        {
            eventInstance.getPlaybackState(out playbackState);
            yield return null;
        }
        while (playbackState != FMOD.Studio.PLAYBACK_STATE.PLAYING);

        FMOD.ChannelGroup channelGroup;
        eventInstance.getChannelGroup(out channelGroup);

        channelGroup.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.FADER, out dsp);
        dsp.setMeteringEnabled(true, true);
    }

    void Update()
    {
        if (playbackState != FMOD.Studio.PLAYBACK_STATE.PLAYING || dsp.handle == System.IntPtr.Zero)
            return;

        dsp.getMeteringInfo(out inputInfo, out outputInfo);

        leftVolume = outputInfo.rmslevel[0];
        rightVolume = outputInfo.rmslevel[1];

        // Moyenne + amplification
        float amplitude = ((leftVolume + rightVolume) * 0.5f) * amplitudeMultiplier;

        // Clamp pour éviter les valeurs extrêmes
        amplitude = Mathf.Clamp(amplitude, 0f, 1f);

        foreach (var vfx in vfxTargets)
        {
            if (vfx == null) continue;

            if (vfx.HasFloat(_propertyID))
                vfx.SetFloat(_propertyID, amplitude);
        }
    }
}