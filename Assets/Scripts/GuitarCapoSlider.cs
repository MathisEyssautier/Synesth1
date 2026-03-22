using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class GuitarCapoSlider : MonoBehaviour
{
    public enum SlideAxis { X, Y, Z }

    [Header("References")]
    [Tooltip("Renderer du capot (pour la couleur/emission).")]
    [SerializeField] private Renderer capoRenderer;

    [Header("Slide")]
    [SerializeField] private SlideAxis slideAxis = SlideAxis.Z;
    [Tooltip("Coordonnée locale min sur l'axe choisi (dans le repère du parent du capot).")]
    [SerializeField] private float minLocalCoord = -0.25f;
    [Tooltip("Coordonnée locale max sur l'axe choisi (dans le repère du manche).")]
    [SerializeField] private float maxLocalCoord = 0.25f;
    [SerializeField] private float smoothTime = 0.02f;

    [Header("Tuning zone (position 'correcte')")]
    [Range(0f, 1f)]
    [SerializeField] private float targetNormalized = 0.6f;
    [Range(0f, 0.5f)]
    [SerializeField] private float targetTolerance = 0.08f;

    [Header("Color by position")]
    [Tooltip("Couleur générée automatiquement sur tout le spectre HSV en fonction de la position (0..1).")]
    [Range(0f, 4f)]
    [SerializeField] private float emissionIntensity = 0f;

    [Header("Haptics (zone correcte)")]
    [Range(0f, 1f)]
    [SerializeField] private float hapticIntensity = 0.5f;
    [SerializeField] private float hapticDuration = 0.05f;
    [SerializeField] private float hapticPulseInterval = 0.15f;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private Material _matInstance;

    private bool _isHeld;
    private IXRSelectInteractor _interactor;
    private float _grabOffsetCoord;
    private float _vel;

    private Vector3 _baseLocalPos;
    private Transform _parent;
    private float _nextHapticTime = 0f;

    public float NormalizedPosition
    {
        get
        {
            float c = GetCurrentLocalCoord();
            float t = Mathf.InverseLerp(minLocalCoord, maxLocalCoord, c);
            return Mathf.Clamp01(t);
        }
    }

    public bool IsInTuneZone => Mathf.Abs(NormalizedPosition - targetNormalized) <= targetTolerance;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();

        _parent = transform.parent;
        if (capoRenderer == null)
            capoRenderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        if (_parent == null)
        {
            enabled = false;
            return;
        }

        _baseLocalPos = transform.localPosition;

        _rb.isKinematic = true;
        _rb.useGravity = false;
        _grab.trackPosition = false;
        _grab.trackRotation = false;
        _grab.throwOnDetach = false;

        if (capoRenderer != null)
        {
            _matInstance = capoRenderer.material;
            _matInstance.EnableKeyword("_EMISSION");
        }

        SetLocalCoord(Mathf.Clamp(GetCurrentLocalCoord(), Mathf.Min(minLocalCoord, maxLocalCoord), Mathf.Max(minLocalCoord, maxLocalCoord)), true);
        UpdateVisuals();
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrab);
        _grab.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrab);
        _grab.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _isHeld = true;
        _interactor = args.interactorObject;
        _vel = 0f;

        // On désactive explicitement le suivi de position/rotation de XR pendant le grab,
        // car ce script gère le placement (sinon conflit = snaps/téléport).
        if (_grab != null)
        {
            _grab.trackPosition = false;
            _grab.trackRotation = false;
            _grab.throwOnDetach = false;
        }
        if (_rb != null)
            _rb.isKinematic = true;

        // Important: XRGrabInteractable peut "snap" l'objet à son attach point au moment du grab.
        // Si on garde _baseLocalPos tel qu'il était au Start(), on peut provoquer un téléport brutal.
        // On recale la base sur la pose courante dès qu'on attrape le capot.
        _baseLocalPos = transform.localPosition;

        float handCoord = GetInteractorCoord();
        float currentCoord = GetCurrentLocalCoord();
        _grabOffsetCoord = currentCoord - handCoord;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isHeld = false;
        _interactor = null;
        _vel = 0f;
    }

    private void Update()
    {
        if (_isHeld && _interactor != null)
        {
            float handCoord = GetInteractorCoord();
            float targetCoord = handCoord + _grabOffsetCoord;
            float min = Mathf.Min(minLocalCoord, maxLocalCoord);
            float max = Mathf.Max(minLocalCoord, maxLocalCoord);
            targetCoord = Mathf.Clamp(targetCoord, min, max);
            SetLocalCoord(targetCoord, smoothTime <= 0f);

            // Haptique légère quand on passe dans la zone correcte.
            if (IsInTuneZone && Time.time >= _nextHapticTime)
            {
                TrySendHaptics(_interactor, hapticIntensity, hapticDuration);
                _nextHapticTime = Time.time + hapticPulseInterval;
            }
        }
        UpdateVisuals();
    }

    private float GetInteractorCoord()
    {
        Vector3 local = _parent.InverseTransformPoint(_interactor.transform.position);
        return GetAxisValue(local);
    }

    private float GetCurrentLocalCoord()
    {
        Vector3 local = transform.localPosition;
        return GetAxisValue(local);
    }

    private void SetLocalCoord(float coord, bool instant)
    {
        float current = GetCurrentLocalCoord();
        float next = instant ? coord : Mathf.SmoothDamp(current, coord, ref _vel, smoothTime);

        Vector3 local = _baseLocalPos;
        SetAxisValue(ref local, next);
        transform.localPosition = local;
    }

    private float GetAxisValue(Vector3 v)
    {
        switch (slideAxis)
        {
            case SlideAxis.X: return v.x;
            case SlideAxis.Y: return v.y;
            default: return v.z;
        }
    }

    private void SetAxisValue(ref Vector3 v, float value)
    {
        switch (slideAxis)
        {
            case SlideAxis.X: v.x = value; break;
            case SlideAxis.Y: v.y = value; break;
            default: v.z = value; break;
        }
    }

    private void UpdateVisuals()
    {
        if (_matInstance == null) return;

        float t = NormalizedPosition;
        // Spectre complet : t -> hue (0..1).
        Color c = Color.HSVToRGB(t, 1f, 1f);
        _matInstance.color = c;
        _matInstance.SetColor("_EmissionColor", c * emissionIntensity);
    }

    private void TrySendHaptics(IXRSelectInteractor interactor, float intensity, float duration)
    {
        if (intensity <= 0f || duration <= 0f) return;

        if (interactor is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(intensity, duration);
        }
    }
}

