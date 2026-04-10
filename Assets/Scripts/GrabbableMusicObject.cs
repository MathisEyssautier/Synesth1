using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

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

