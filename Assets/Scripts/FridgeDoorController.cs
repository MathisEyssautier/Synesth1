using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Porte de frigo : fait tourner le pivot vide (ex. Pivot_PorteGauche) en rotation Z locale.
/// Ouvert au maximum = openLocalZ (souvent 0). Fermé = closedLocalZ (ex. -110 gauche, +110 droite).
/// </summary>
public class FridgeDoorController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Pivot vide à faire tourner (ex. Pivot_PorteGauche).")]
    [SerializeField] private Transform doorPivot;
    [Tooltip("Objet avec XRSimpleInteractable + collider (souvent le mesh de la porte ou une poignée).")]
    [SerializeField] private XRSimpleInteractable interactable;
    [Tooltip("Mesh de la porte pour le pulse jaune (ex. PorteGauche_low). Vide = Renderer sur l'interactable.")]
    [SerializeField] private Renderer doorVisualRenderer;

    [Header("Angles (local Euler Z du pivot)")]
    [Tooltip("Rotation Z quand la porte est ouverte au maximum (souvent 0 sur le pivot).")]
    [SerializeField] private float openLocalZ = 0f;
    [Tooltip("Rotation Z quand la porte est fermée (ex. -110 porte gauche, +110 porte droite).")]
    [SerializeField] private float closedLocalZ = -110f;

    [Header("Inertie / élan")]
    [Tooltip("0 = la porte suit la main instantanément. Plus haut = plus de retard (élan pendant le tirage).")]
    [SerializeField] private float rotationFollowSmoothTime = 0.08f;
    [Tooltip("Après lâcher la gachette : freinage de l'élan. Plus BAS = glisse plus longtemps (ex. 0.05–0.15). Plus HAUT = s'arrête vite (ex. 0.7+).")]
    [Range(0f, 0.99f)]
    [SerializeField] private float damping = 0.08f;
    [Tooltip("Vitesse angulaire max (°/s) pendant le tirage et après le lâcher. Plus haut = on peut « flinguer » la porte plus fort.")]
    [SerializeField] private float maxAngularSpeed = 420f;
    [Tooltip("Multiplicateur de vitesse au moment où tu lâches la porte (1 = normal).")]
    [SerializeField] private float releaseVelocityBoost = 1.15f;

    [Header("Hand → porte")]
    [SerializeField] private float handToDoorSign = 1f;
    [SerializeField] private Vector3 hingeAxisLocal = Vector3.forward;
    [SerializeField] private Vector3 handAngleReferenceLocal = Vector3.up;

    [Header("Démarrage")]
    [SerializeField] private bool applyClosedPoseOnStart = true;

    [Header("Pulse jaune (hint)")]
    [SerializeField] private bool enableHintPulse = true;
    [SerializeField] private float hintPulseIntervalSeconds = 60f;
    [SerializeField] private int hintPulsesPerCycle = 2;
    [SerializeField] private float hintPulseDuration = 0.35f;
    [SerializeField] private float hintPulseGap = 0.18f;
    [SerializeField] private Color hintPulseColor = new Color(1f, 0.92f, 0.2f);
    [SerializeField] private float hintEmissionIntensity = 1.2f;
    [Tooltip("Si coché : pulse seulement quand la porte est fermée.")]
    [SerializeField] private bool hintOnlyWhenClosed = true;

    private bool _isGrabbed;
    private bool _inertiaActive;
    private float _currentLocalZ;
    private float _closedReferenceZ;
    private IXRSelectInteractor _interactor;
    private float _lastHandAngle;
    private float _anglePrevious;
    private float _angularVelocity;
    private float _rotationFollowVelocity;
    private Coroutine _hintPulseRoutine;

    private Material _doorMatInstance;
    private Color _doorBaseColor;
    private Color _doorBaseEmission;
    private bool _doorHasEmission;
    private bool _doorHasBaseColor;

    public float CurrentLocalZ => _currentLocalZ;
    public bool IsClosed => Mathf.Abs(_currentLocalZ - _closedReferenceZ) <= 2f;

    private void Awake()
    {
        if (doorPivot == null)
            doorPivot = transform;

        if (interactable == null)
            interactable = GetComponentInChildren<XRSimpleInteractable>(true);

        if (doorVisualRenderer == null && interactable != null)
            doorVisualRenderer = interactable.GetComponent<Renderer>();

        CacheDoorMaterial();
    }

    private void Start()
    {
        _closedReferenceZ = closedLocalZ;
        _currentLocalZ = applyClosedPoseOnStart ? closedLocalZ : GetPivotLocalZ();
        if (applyClosedPoseOnStart)
            ApplyPivotRotation(_currentLocalZ);
        _anglePrevious = _currentLocalZ;

        if (interactable == null)
        {
            Debug.LogWarning("[FridgeDoorController] Aucun XRSimpleInteractable assigné.", this);
            return;
        }

        EnsureInteractionCollidersRegistered();

        interactable.selectEntered.AddListener(OnGrab);
        interactable.selectExited.AddListener(OnRelease);

        if (enableHintPulse && doorVisualRenderer != null)
            _hintPulseRoutine = StartCoroutine(HintPulseLoop());
    }

    private void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
        }

        if (_hintPulseRoutine != null)
            StopCoroutine(_hintPulseRoutine);

        if (_doorMatInstance != null)
            Destroy(_doorMatInstance);
    }

    private void CacheDoorMaterial()
    {
        if (doorVisualRenderer == null) return;

        _doorMatInstance = doorVisualRenderer.material;
        if (_doorMatInstance == null) return;

        _doorHasBaseColor = _doorMatInstance.HasProperty("_BaseColor");
        _doorBaseColor = _doorHasBaseColor
            ? _doorMatInstance.GetColor("_BaseColor")
            : _doorMatInstance.color;

        _doorHasEmission = _doorMatInstance.HasProperty("_EmissionColor");
        if (_doorHasEmission)
        {
            _doorMatInstance.EnableKeyword("_EMISSION");
            _doorBaseEmission = _doorMatInstance.GetColor("_EmissionColor");
        }
    }

    private void EnsureInteractionCollidersRegistered()
    {
        if (interactable == null) return;
        if (interactable.colliders.Count > 0) return;

        var cols = interactable.GetComponents<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null || !c.enabled) continue;
            interactable.colliders.Add(c);
        }

        if (interactable.colliders.Count == 0)
        {
            Debug.LogWarning(
                "[FridgeDoorController] Aucun Collider sur l'objet interactable. Ajoute un Box Collider.",
                interactable);
        }
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _inertiaActive = false;
        _interactor = args.interactorObject;
        _lastHandAngle = GetHandAngleAroundPivot();
        _anglePrevious = _currentLocalZ;
        _rotationFollowVelocity = _angularVelocity;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        _interactor = null;
        _angularVelocity *= releaseVelocityBoost;
        _inertiaActive = Mathf.Abs(_angularVelocity) > 0.1f;
    }

    private void LateUpdate()
    {
        if (doorPivot == null) return;

        if (_isGrabbed && _interactor != null)
        {
            float handAngle = GetHandAngleAroundPivot();
            // DeltaAngle évite les sauts ±180° près de l'ouverture max (Z=0) qui bloquaient la fermeture.
            float frameDelta = Mathf.DeltaAngle(_lastHandAngle, handAngle) * handToDoorSign;
            _lastHandAngle = handAngle;

            float target = _currentLocalZ + frameDelta;

            if (rotationFollowSmoothTime > 0.001f)
            {
                _currentLocalZ = Mathf.SmoothDamp(
                    _currentLocalZ,
                    target,
                    ref _rotationFollowVelocity,
                    rotationFollowSmoothTime,
                    maxAngularSpeed);
            }
            else
            {
                _currentLocalZ = target;
                _rotationFollowVelocity = 0f;
            }

            _currentLocalZ = Mathf.Clamp(_currentLocalZ, MinZ(), MaxZ());
            ApplyPivotRotation(_currentLocalZ);

            _angularVelocity = (_currentLocalZ - _anglePrevious) / Mathf.Max(Time.deltaTime, 0.0001f);
            _angularVelocity = Mathf.Clamp(_angularVelocity, -maxAngularSpeed, maxAngularSpeed);
            _anglePrevious = _currentLocalZ;
        }
        else if (_inertiaActive)
        {
            _angularVelocity *= Mathf.Pow(1f - damping, Time.deltaTime * 60f);
            _currentLocalZ += _angularVelocity * Time.deltaTime;
            _currentLocalZ = Mathf.Clamp(_currentLocalZ, MinZ(), MaxZ());
            ApplyPivotRotation(_currentLocalZ);

            bool hitLimit = _currentLocalZ <= MinZ() + 0.01f || _currentLocalZ >= MaxZ() - 0.01f;
            if (hitLimit)
                _angularVelocity *= 0.35f;

            if (hitLimit || Mathf.Abs(_angularVelocity) < 0.1f)
            {
                _angularVelocity = 0f;
                _inertiaActive = false;
            }
        }
    }

    private IEnumerator HintPulseLoop()
    {
        float interval = Mathf.Max(1f, hintPulseIntervalSeconds);
        var waitInterval = new WaitForSeconds(interval);

        while (true)
        {
            yield return waitInterval;

            if (_isGrabbed) continue;
            if (hintOnlyWhenClosed && !IsClosed) continue;
            if (_doorMatInstance == null) continue;

            int count = Mathf.Max(1, hintPulsesPerCycle);
            for (int i = 0; i < count; i++)
            {
                yield return PulseDoorColorOnce();
                if (i < count - 1 && hintPulseGap > 0f)
                    yield return new WaitForSeconds(hintPulseGap);
            }
        }
    }

    private IEnumerator PulseDoorColorOnce()
    {
        SetDoorVisualColor(hintPulseColor, hintEmissionIntensity);
        if (hintPulseDuration > 0f)
            yield return new WaitForSeconds(hintPulseDuration);
        RestoreDoorVisualColor();
    }

    private void SetDoorVisualColor(Color c, float emissionMul)
    {
        if (_doorMatInstance == null) return;

        if (_doorHasBaseColor)
            _doorMatInstance.SetColor("_BaseColor", c);
        else
            _doorMatInstance.color = c;

        if (_doorHasEmission)
            _doorMatInstance.SetColor("_EmissionColor", c * emissionMul);
    }

    private void RestoreDoorVisualColor()
    {
        if (_doorMatInstance == null) return;

        if (_doorHasBaseColor)
            _doorMatInstance.SetColor("_BaseColor", _doorBaseColor);
        else
            _doorMatInstance.color = _doorBaseColor;

        if (_doorHasEmission)
            _doorMatInstance.SetColor("_EmissionColor", _doorBaseEmission);
    }

    private float MinZ() => Mathf.Min(closedLocalZ, openLocalZ);
    private float MaxZ() => Mathf.Max(closedLocalZ, openLocalZ);

    private float GetPivotLocalZ()
    {
        float z = doorPivot.localEulerAngles.z;
        if (z > 180f) z -= 360f;
        return z;
    }

    private void ApplyPivotRotation(float localZ)
    {
        Vector3 euler = doorPivot.localEulerAngles;
        euler.z = localZ;
        doorPivot.localEulerAngles = euler;
    }

    private float GetHandAngleAroundPivot()
    {
        if (_interactor == null) return 0f;

        Vector3 axisWorld = doorPivot.TransformDirection(hingeAxisLocal.normalized);
        Vector3 refWorld = doorPivot.TransformDirection(handAngleReferenceLocal.normalized);

        Vector3 arm = _interactor.transform.position - doorPivot.position;
        arm -= Vector3.Project(arm, axisWorld);
        if (arm.sqrMagnitude < 0.0001f) return 0f;

        refWorld -= Vector3.Project(refWorld, axisWorld);
        if (refWorld.sqrMagnitude < 0.0001f) return 0f;

        refWorld.Normalize();
        return Vector3.SignedAngle(refWorld, arm.normalized, axisWorld);
    }

    public void ForceClose()
    {
        _currentLocalZ = _closedReferenceZ;
        _isGrabbed = false;
        _inertiaActive = false;
        _interactor = null;
        _angularVelocity = 0f;
        _rotationFollowVelocity = 0f;
        ApplyPivotRotation(_currentLocalZ);
    }

    private void OnValidate()
    {
        if (doorPivot == null)
            doorPivot = transform;
        hintPulseIntervalSeconds = Mathf.Max(1f, hintPulseIntervalSeconds);
        hintPulsesPerCycle = Mathf.Max(1, hintPulsesPerCycle);
        rotationFollowSmoothTime = Mathf.Max(0f, rotationFollowSmoothTime);
        releaseVelocityBoost = Mathf.Max(0f, releaseVelocityBoost);
    }
}
