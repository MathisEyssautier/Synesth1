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
    [Tooltip("Mode SWAP : si défini, le material à cet index du targetRenderer est remplacé par onMaterial quand l'objet est activé, et restauré quand désactivé. Mode TEINTE (fallback) : si onMaterial est vide, applique seulement onColor et l'émission sur le material existant.")]
    [SerializeField] private Material onMaterial;
    [Tooltip("Index du material à swap dans le Renderer (utile si le Renderer a plusieurs materials).")]
    [SerializeField] private int materialIndexToSwap = 0;
    [Tooltip("Utilisé uniquement en mode TEINTE (quand onMaterial est vide).")]
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

    private bool _useMaterialSwap;
    private Material _offMaterial;

    private bool _isOn = false;
    public bool IsOn => _isOn;

    private FMOD.Studio.PLAYBACK_STATE _playbackState;


    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.activated.AddListener(OnActivated);

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            _useMaterialSwap = onMaterial != null;

            if (_useMaterialSwap)
            {
                Material[] shared = targetRenderer.sharedMaterials;
                int safeIndex = Mathf.Clamp(materialIndexToSwap, 0, shared.Length - 1);
                materialIndexToSwap = safeIndex;
                _offMaterial = shared[safeIndex];
            }
            else
            {
                _materialInstance = targetRenderer.material;
                _baseColor = _materialInstance.color;
                if (useEmission)
                    _materialInstance.EnableKeyword("_EMISSION");
            }
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

        if (_useMaterialSwap && targetRenderer != null && _offMaterial != null)
        {
            Material[] mats = targetRenderer.sharedMaterials;
            if (materialIndexToSwap >= 0 && materialIndexToSwap < mats.Length)
            {
                mats[materialIndexToSwap] = _isOn ? onMaterial : _offMaterial;
                targetRenderer.sharedMaterials = mats;
            }
        }
        else if (_materialInstance != null)
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

