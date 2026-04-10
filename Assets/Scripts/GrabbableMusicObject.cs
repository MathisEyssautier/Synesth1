using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;
using System.Collections;


[RequireComponent(typeof(XRGrabInteractable))]
public class GrabbableMusicObject : MonoBehaviour
{
    public static event System.Action<GrabbableMusicObject, bool> OnStateChanged;
    [Header("FMOD")]
    [SerializeField] private EventReference musicEvent;
    [Range(0f, 1f)]
    [SerializeField] private float onVolume = 1f;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Color onColor = Color.magenta;
    [SerializeField] private bool useEmission = true;
    [SerializeField] private float emissionOnIntensity = 2f;

    [Header("Canvas")]
    [SerializeField] private GameObject canvasRoot;

    [Header("State")]
    [SerializeField] private bool startEnabled = true;

    private XRGrabInteractable _grab;
    private EventInstance _eventInstance;
    public EventInstance EventInstance => _eventInstance;
    private Material _materialInstance;
    private Color _baseColor = Color.white;
    private bool _isOn = false;
    public bool IsOn => _isOn;
    private FMOD.DSP _dsp;
    private FMOD.DSP_METERING_INFO _inputInfo;
    private FMOD.DSP_METERING_INFO _outputInfo;
    private FMOD.Studio.PLAYBACK_STATE _playbackState;
    private Coroutine _meterCoroutine;




    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.activated.AddListener(OnActivated);

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            _materialInstance = targetRenderer.material;
            _baseColor = _materialInstance.color;
            if (useEmission)
                _materialInstance.EnableKeyword("_EMISSION");
        }
    }

    private void OnEnable()
    {
        if (!musicEvent.IsNull)
        {
            _eventInstance = RuntimeManager.CreateInstance(musicEvent);
            RuntimeManager.AttachInstanceToGameObject(_eventInstance, gameObject);
            _eventInstance.start();
            _meterCoroutine = StartCoroutine(SetupMetering());
        }

        SetEnabledState(startEnabled, instant: true);
    }

    private void OnDisable()
    {
        if (_eventInstance.isValid())
        {
            _eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _eventInstance.release();
        }
        if (_meterCoroutine != null)
        {
            StopCoroutine(_meterCoroutine);
            _meterCoroutine = null;
        }
    }
    private IEnumerator SetupMetering()
    {
        // attendre que l'event soit réellement en train de jouer
        do
        {
            _eventInstance.getPlaybackState(out _playbackState);
            yield return null;
        }
        while (_playbackState != FMOD.Studio.PLAYBACK_STATE.PLAYING);

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

        if (_playbackState == FMOD.Studio.PLAYBACK_STATE.PLAYING && _dsp.hasHandle())
        {
            _dsp.getMeteringInfo(out _inputInfo, out _outputInfo);

            float left = _outputInfo.rmslevel[0];
            float right = _outputInfo.rmslevel[1];

            Debug.Log($"FMOD L: {left:F4} | R: {right:F4}");
        }
    }
    private void OnDestroy()
    {
        if (_grab != null)
            _grab.activated.RemoveListener(OnActivated);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        // Sécurité: on ne toggle que si l'objet est bien en main.
        if (!_grab.isSelected) return;
        SetEnabledState(!_isOn, instant: false);
    }

    private void SetEnabledState(bool enabledState, bool instant)
    {
        _isOn = enabledState;

        if (_eventInstance.isValid())
            _eventInstance.setVolume(_isOn ? onVolume : 0f);

        if (canvasRoot != null)
            canvasRoot.SetActive(_isOn);

        if (_materialInstance != null)
        {
            Color c = _isOn ? onColor : _baseColor;
            if (_materialInstance.HasProperty("_BaseColor"))
                _materialInstance.SetColor("_BaseColor", c);
            else
                _materialInstance.color = c;

            if (useEmission && _materialInstance.HasProperty("_EmissionColor"))
            {
                float intensity = _isOn ? emissionOnIntensity : 0f;
                _materialInstance.SetColor("_EmissionColor", c * intensity);
            }
        }

        OnStateChanged?.Invoke(this, _isOn);
    }
}

