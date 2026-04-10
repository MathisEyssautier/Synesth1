using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(GrabbableMusicObject))]
public class VFXAmplitudeDriver : MonoBehaviour
{
    [Header("VFX Targets")]
    [SerializeField] private List<VisualEffect> vfxTargets = new();

    [Header("Amplitude")]
    [SerializeField] private string propertyName = "Amplitude";

    [Range(0f, 0.99f)]
    [SerializeField] private float smoothing = 0.85f;

    [SerializeField] private float amplitudeMultiplier = 3f;

    private GrabbableMusicObject _source;

    private float _smoothedAmplitude;
    private int _propertyID;

    // FMOD DSP
    private FMOD.DSP _meteringDSP;
    private FMOD.ChannelGroup _channelGroup;
    private bool _dspReady = false;

    private void Awake()
    {
        _source = GetComponent<GrabbableMusicObject>();
        _propertyID = Shader.PropertyToID(propertyName);
    }

    private void Start()
    {
        SetupDSP();
    }

    private void Update()
    {
        if (!_dspReady)
            SetupDSP();

        float rms = SampleRMS();

        _smoothedAmplitude = Mathf.Lerp(rms, _smoothedAmplitude, smoothing);

        float value = Mathf.Clamp01(_smoothedAmplitude * amplitudeMultiplier);

        for (int i = 0; i < vfxTargets.Count; i++)
        {
            var vfx = vfxTargets[i];
            if (vfx == null) continue;

            if (vfx.HasFloat(_propertyID))
                vfx.SetFloat(_propertyID, value);
        }
    }

    // ── FMOD DSP SETUP ─────────────────────────────

    private void SetupDSP()
    {
        if (_dspReady) return;

        if (!_source.EventInstance.isValid())
            return;

        _source.EventInstance.getChannelGroup(out _channelGroup);

        if (!_channelGroup.hasHandle())
            return;

        FMODUnity.RuntimeManager.CoreSystem.createDSPByType(
            FMOD.DSP_TYPE.FADER,
            out _meteringDSP
        );

        _meteringDSP.setMeteringEnabled(false, true);

        _channelGroup.addDSP(0, _meteringDSP);

        _dspReady = true;
    }

    // ── RMS ─────────────────────────────────────────

    private float SampleRMS()
    {
        if (!_dspReady || !_meteringDSP.hasHandle())
            return 0f;

        _meteringDSP.getMeteringInfo(System.IntPtr.Zero, out FMOD.DSP_METERING_INFO meter);

        if (meter.numchannels == 0)
            return 0f;

        float sum = 0f;

        for (int i = 0; i < meter.numchannels; i++)
            sum += meter.rmslevel[i];

        return sum / meter.numchannels;
    }

    // ── CLEANUP FIX (IMPORTANT) ─────────────────────

    private void OnDestroy()
    {
        try
        {
            if (_meteringDSP.hasHandle())
            {
                if (_channelGroup.hasHandle())
                {
                    _channelGroup.removeDSP(_meteringDSP);
                }

                _meteringDSP.release();
            }
        }
        catch
        {
            // sécurité shutdown FMOD / editor reload
        }
    }

    // ── Gizmos ──────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.Lerp(Color.cyan, Color.magenta, _smoothedAmplitude);
        Gizmos.DrawWireSphere(transform.position, 0.1f + _smoothedAmplitude * 0.5f);
    }
}