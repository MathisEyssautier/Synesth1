using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
public class ShellProximityFeedback : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private ShellColorId shellColorId;

    [Header("Head reference (XR Camera)")]
    [SerializeField] private Transform headTransform;

    [Header("FMOD")]
    [SerializeField] private EventReference shellEvent;
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 0.15f;
    [Range(0f, 1f)]
    [SerializeField] private float maxVolume = 1f;
    [Tooltip("Distance (m) à laquelle le son est au max (près de la tête).")]
    [SerializeField] private float maxVolumeDistance = 0.12f;
    [Tooltip("Distance (m) à laquelle le boost est nul (loin de la tête).")]
    [SerializeField] private float minVolumeDistance = 0.70f;
    [SerializeField] private AnimationCurve volumeByProximity01 = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Tint (non-emissif)")]
    [SerializeField] private Renderer[] tintedRenderers;
    [SerializeField] private Color yellow = new Color(1f, 0.92f, 0.1f);
    [SerializeField] private Color green = new Color(0.1f, 0.95f, 0.15f);
    [SerializeField] private Color red = new Color(0.95f, 0.1f, 0.1f);
    [SerializeField] private Color darkBlue = new Color(0.05f, 0.12f, 0.8f);
    [Tooltip("Teinte max quand très proche de la tête.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxTint = 0.9f;
    [SerializeField] private AnimationCurve tintByProximity01 = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private XRGrabInteractable _grab;
    private EventInstance _instance;
    private bool _hasInstance = false;

    // Piloté par la porte : quand true, on coupe le son mais on garde le feedback visuel.
    private bool _doorClosed = false;

    private Material[] _materials;
    private Color[] _baseColors;

    public ShellColorId ColorId => shellColorId;
    public bool IsHeld => _grab != null && _grab.isSelected;

    /// <summary>
    /// Appelé par le DoorController pour couper / réactiver le son.
    /// </summary>
    public void SetDoorClosed(bool closed)
    {
        _doorClosed = closed;
    }

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();

        if (tintedRenderers != null && tintedRenderers.Length > 0)
        {
            _materials = new Material[tintedRenderers.Length];
            _baseColors = new Color[tintedRenderers.Length];
            for (int i = 0; i < tintedRenderers.Length; i++)
            {
                if (tintedRenderers[i] == null) continue;
                _materials[i] = tintedRenderers[i].material;
                _baseColors[i] = _materials[i].color;
            }
        }
    }

    private void OnEnable()
    {
        if (!shellEvent.IsNull)
        {
            _instance = RuntimeManager.CreateInstance(shellEvent);
            RuntimeManager.AttachInstanceToGameObject(_instance, gameObject);
            _instance.start();
            _hasInstance = true;
        }

        Apply(0f);
    }

    private void OnDisable()
    {
        if (_hasInstance)
        {
            _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _instance.release();
            _hasInstance = false;
        }
    }

    private void Update()
    {
        if (headTransform == null)
            return;

        float d = Vector3.Distance(transform.position, headTransform.position);

        // Proximity01: 0 = loin, 1 = très proche
        float proximity01 = Mathf.InverseLerp(minVolumeDistance, maxVolumeDistance, d);
        proximity01 = Mathf.Clamp01(proximity01);

        Apply(proximity01);
    }

    private void Apply(float proximity01)
    {
        // Audio
        if (_hasInstance)
        {
            if (_doorClosed)
            {
                _instance.setVolume(0f);
                return;
            }

            float shaped = volumeByProximity01.Evaluate(proximity01);
            float v = Mathf.Lerp(baseVolume, maxVolume, shaped);
            _instance.setVolume(v);
        }

        // Tint
        if (_materials != null)
        {
            float shapedTint = tintByProximity01.Evaluate(proximity01);
            float t = Mathf.Clamp01(shapedTint) * maxTint;
            Color target = GetTargetColor();

            for (int i = 0; i < _materials.Length; i++)
            {
                if (_materials[i] == null) continue;
                _materials[i].color = Color.Lerp(_baseColors[i], target, t);
            }
        }
    }

    private Color GetTargetColor()
    {
        switch (shellColorId)
        {
            case ShellColorId.Yellow: return yellow;
            case ShellColorId.Green: return green;
            case ShellColorId.Red: return red;
            case ShellColorId.DarkBlue: return darkBlue;
            default: return Color.white;
        }
    }
}

