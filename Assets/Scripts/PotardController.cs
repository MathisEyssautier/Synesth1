using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class PotardController : MonoBehaviour
{
    [Header("Crans")]
    public int nombreCrans = 12;
    [Tooltip("Degres de rotation de main necessaires pour avancer d'un cran")]
    public float seuilDegresCran = 8f;
    [Tooltip("Cran logique au Start (0 = premier cran). À ajuster si la pose du mesh en scène ne correspond pas à l'index 0.")]
    [SerializeField] private int cranIndexDepart = 0;

    [Header("Positions valides (index de cran, 0 a nombreCrans-1)")]
    public int cranPositionA = 3;
    public int cranPositionB = 9;

    [Header("Haptics")]
    [Range(0f, 1f)] public float intensiteCran = 0.15f;
    public float dureeCran = 0.05f;
    [Range(0f, 1f)] public float intensitePositionValide = 0.8f;
    public float dureePositionValide = 0.15f;

    [Header("Indicateur visuel")]
    public Renderer cubeIndicateur;
    public Color couleurNeutre = Color.white;
    public Color couleurA = Color.green;
    public Color couleurB = Color.red;

    private int _cranActuel = 0;
    private bool _estSaisi = false;
    private IXRSelectInteractor _interactorCourant;
    private float _angleMainPrecedent;
    private float _accumulateurDelta = 0f;
    private Quaternion _rotationBaseLocale;
    private Vector3 _grabReferenceVector;
    private Vector3 _grabAxis;
    private bool _grabReferenceValid;
    private Transform _initialParent;
    private int _initialSiblingIndex;

    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rb;
    [Header("XR Grab behavior")]
    [Tooltip("Empêche le snap de rotation du XRGrabInteractable au grab (ex: offset visuel -90°).")]
    [SerializeField] private bool disableXrTrackRotation = true;
    [Tooltip("Réapplique la rotation discrète du cran en continu pendant le grab pour éviter toute rotation parasite injectée par XRI.")]
    [SerializeField] private bool lockRotationEveryFrameWhileGrabbed = true;

    public int CranActuel => _cranActuel;
    public bool EstSurA => _cranActuel == cranPositionA;
    public bool EstSurB => _cranActuel == cranPositionB;

    public System.Action<int> OnCranChange;
    public System.Action<bool> OnPositionValide;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();

        if (_grabInteractable != null)
        {
            // Le potard est piloté par nos crans :
            // on évite tout déplacement/rotation/reparenting injecté par XRI au grab.
            _grabInteractable.trackPosition = false;
            if (disableXrTrackRotation)
                _grabInteractable.trackRotation = false;
            _grabInteractable.useDynamicAttach = false;
            _grabInteractable.retainTransformParent = true;
            _grabInteractable.throwOnDetach = false;
        }
    }

    /// <summary>Désactive la saisie XR (grab) sans retirer le script.</summary>
    public void SetInteractable(bool interactable)
    {
        if (_grabInteractable != null)
            _grabInteractable.enabled = interactable;
    }

    /// <summary>
    /// Force le cran logique (0 = premier cran) sans haptique ni grab.
    /// À utiliser pour un reset distant (ex. pose cassette / guitare sur socket).
    /// </summary>
    public void SetCranSansInteraction(int cranIndex)
    {
        int max = Mathf.Max(0, nombreCrans - 1);
        _cranActuel = Mathf.Clamp(cranIndex, 0, max);
        _accumulateurDelta = 0f;
        AppliquerRotationCran(_cranActuel);
        MettreAJourCouleur();
    }

    private void Start()
    {
        _initialParent = transform.parent;
        _initialSiblingIndex = transform.GetSiblingIndex();

        _rotationBaseLocale = Quaternion.Euler(
            transform.localEulerAngles.x,
            transform.localEulerAngles.y,
            0f
        );
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        int max = Mathf.Max(0, nombreCrans - 1);
        _cranActuel = Mathf.Clamp(cranIndexDepart, 0, max);
        AppliquerRotationCran(_cranActuel);
        MettreAJourCouleur();
    }

    private void OnEnable()
    {
        _grabInteractable.selectEntered.AddListener(OnGrab);
        _grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrab);
        _grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _estSaisi = true;
        _interactorCourant = args.interactorObject;
        InitGrabReference(_interactorCourant);
        _angleMainPrecedent = GetAngleControllerAroundPotardAxis(_interactorCourant);
        _accumulateurDelta = 0f;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _estSaisi = false;
        _interactorCourant = null;
        _accumulateurDelta = 0f;
        _grabReferenceValid = false;

        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        RestoreOriginalParentIfNeeded();
        AppliquerRotationCran(_cranActuel);
    }

    private void Update()
    {
        if (!_estSaisi || _interactorCourant == null) return;
        if (!_grabReferenceValid) return;

        float angleActuel = GetAngleControllerAroundPotardAxis(_interactorCourant);
        float delta = Mathf.DeltaAngle(_angleMainPrecedent, angleActuel);
        _angleMainPrecedent = angleActuel;

        _accumulateurDelta += delta;

        while (_accumulateurDelta >= seuilDegresCran)
        {
            _accumulateurDelta -= seuilDegresCran;
            ChangerCran(1);
        }
        while (_accumulateurDelta <= -seuilDegresCran)
        {
            _accumulateurDelta += seuilDegresCran;
            ChangerCran(-1);
        }

        RestoreOriginalParentIfNeeded();

        // Certains setups XRI continuent d'injecter une rotation de grab
        // (offset visuel, ex. Y = -90) même si trackRotation est désactivé.
        // On force donc la pose exacte du cran courant à chaque frame.
        if (lockRotationEveryFrameWhileGrabbed)
            AppliquerRotationCran(_cranActuel);
    }

    private void RestoreOriginalParentIfNeeded()
    {
        if (_initialParent == null)
            return;

        if (transform.parent != _initialParent)
            transform.SetParent(_initialParent, true);

        if (transform.GetSiblingIndex() != _initialSiblingIndex)
            transform.SetSiblingIndex(_initialSiblingIndex);
    }

    private void ChangerCran(int direction)
    {
        _cranActuel = (_cranActuel + direction + nombreCrans) % nombreCrans;

        AppliquerRotationCran(_cranActuel);
        OnCranChange?.Invoke(_cranActuel);
        EnvoyerHapticsCran();
        MettreAJourCouleur();

        if (_cranActuel == cranPositionA)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(true);
        }
        else if (_cranActuel == cranPositionB)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(false);
        }
    }

    private void AppliquerRotationCran(int cran)
    {
        float degresParCran = 360f / nombreCrans;
        float angleZ = cran * degresParCran;
        transform.localRotation = _rotationBaseLocale * Quaternion.Euler(0f, 0f, angleZ);
    }

    private void MettreAJourCouleur()
    {
        if (cubeIndicateur == null) return;

        Color cible = couleurNeutre;
        if (_cranActuel == cranPositionA) cible = couleurA;
        else if (_cranActuel == cranPositionB) cible = couleurB;

        cubeIndicateur.material.SetColor("_EmissionColor", cible);
        cubeIndicateur.material.color = cible;
    }

    private void InitGrabReference(IXRSelectInteractor interactor)
    {
        _grabAxis = transform.forward.normalized;
        _grabReferenceValid = false;
        _grabReferenceVector = Vector3.forward;

        if (interactor == null) return;

        Transform attach = interactor.GetAttachTransform(_grabInteractable);
        if (attach == null) return;

        if (TryGetProjectedControllerVector(attach, _grabAxis, out Vector3 projected))
        {
            _grabReferenceVector = projected;
            _grabReferenceValid = true;
        }
    }

    // Angle signé du controller autour de l'axe du potard, relativement à l'orientation de la main au moment du grab.
    private float GetAngleControllerAroundPotardAxis(IXRSelectInteractor interactor)
    {
        if (interactor == null || !_grabReferenceValid) return 0f;

        Transform attach = interactor.GetAttachTransform(_grabInteractable);
        if (attach == null) return _angleMainPrecedent;

        if (!TryGetProjectedControllerVector(attach, _grabAxis, out Vector3 currentProjected))
            return _angleMainPrecedent;

        return Vector3.SignedAngle(_grabReferenceVector, currentProjected, _grabAxis);
    }

    private static bool TryGetProjectedControllerVector(Transform attach, Vector3 axis, out Vector3 projected)
    {
        projected = Vector3.zero;
        if (attach == null) return false;

        Vector3 forwardProjected = Vector3.ProjectOnPlane(attach.forward, axis);
        if (forwardProjected.sqrMagnitude > 1e-6f)
        {
            projected = forwardProjected.normalized;
            return true;
        }

        Vector3 rightProjected = Vector3.ProjectOnPlane(attach.right, axis);
        if (rightProjected.sqrMagnitude > 1e-6f)
        {
            projected = rightProjected.normalized;
            return true;
        }

        return false;
    }

    private void EnvoyerHapticsCran()
    {
        bool sentByInteractor = false;
        if (_interactorCourant is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(intensiteCran, dureeCran);
            sentByInteractor = true;
        }

        if (!sentByInteractor || ShouldUseLegacyHapticsFallback())
            TrySendLegacyHapticsToCurrentInteractor(intensiteCran, dureeCran);
    }

    private void EnvoyerHapticsPositionValide()
    {
        bool sentByInteractor = false;
        if (_interactorCourant is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(intensitePositionValide, dureePositionValide);
            sentByInteractor = true;
        }

        if (!sentByInteractor || ShouldUseLegacyHapticsFallback())
            TrySendLegacyHapticsToCurrentInteractor(intensitePositionValide, dureePositionValide);
    }

    private void TrySendLegacyHapticsToCurrentInteractor(float amplitude, float duration)
    {
        var t = _interactorCourant != null ? (_interactorCourant as Component)?.transform : null;
        if (t == null)
            return;

        if (NameLooksLeft(t))
        {
            TrySendToNode(XRNode.LeftHand, amplitude, duration);
            return;
        }

        if (NameLooksRight(t))
        {
            TrySendToNode(XRNode.RightHand, amplitude, duration);
            return;
        }

        // Last resort for runtimes that do not expose clear interactor naming.
        // Avoids losing haptics entirely on Quest Link fallback.
        if (TrySendToNode(XRNode.LeftHand, amplitude, duration))
            return;
        TrySendToNode(XRNode.RightHand, amplitude, duration);
    }

    private static bool NameLooksLeft(Transform t)
    {
        if (t == null) return false;
        if (ContainsInHierarchy(t, "Left Controller")) return true;
        return false;
    }

    private static bool NameLooksRight(Transform t)
    {
        if (t == null) return false;
        if (ContainsInHierarchy(t, "Right Controller")) return true;
        return false;
    }

    private static bool ContainsInHierarchy(Transform t, string token)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.Contains(token))
                return true;
            current = current.parent;
        }

        return false;
    }

    private static bool TrySendToNode(XRNode node, float amplitude, float duration)
    {
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        if (!device.TryGetHapticCapabilities(out HapticCapabilities caps)) return false;
        if (!caps.supportsImpulse || caps.numChannels <= 0) return false;
        return device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
    }

    private static bool ShouldUseLegacyHapticsFallback()
    {
        bool hasLegacyController = IsLegacyControllerAvailable(XRNode.LeftHand) || IsLegacyControllerAvailable(XRNode.RightHand);
        if (!hasLegacyController)
            return false;

        return !HasInputSystemXrControllers();
    }

    private static bool IsLegacyControllerAvailable(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked))
            return tracked;

        return true;
    }

    private static bool HasInputSystemXrControllers()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (var device in InputSystem.devices)
        {
            if (device == null || !device.added || !device.enabled)
                continue;
            if (device is UnityEngine.InputSystem.XR.XRController)
                return true;
        }
#endif
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float degresParCran = 360f / nombreCrans;
        for (int i = 0; i < nombreCrans; i++)
        {
            float angle = i * degresParCran * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = transform.position + dir * 0.06f;
            Gizmos.color = i == cranPositionA ? Color.green : i == cranPositionB ? Color.blue : Color.gray;
            Gizmos.DrawSphere(pos, 0.005f);
        }
    }
#endif
}