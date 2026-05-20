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
    [Tooltip("Degrès de rotation poignet pour valider un cran logique (haptique + index). Plus bas = cran plus facile. La rotation visuelle inter-crans suit l'angle entre deux crans si « suivi visuel » est actif.")]
    public float seuilDegresCran = 10f;
    [Tooltip("Cran logique au Start (0 = premier cran). À ajuster si la pose du mesh en scène ne correspond pas à l'index 0.")]
    [SerializeField] private int cranIndexDepart = 0;

    [Header("Stabilité (VR)")]
    [Tooltip("Inverse le sens horaire / antihoraire si la main tourne dans le mauvais sens.")]
    [SerializeField] private bool invertRotationDirection = false;
    [Tooltip("Multiplicateur sur l'angle poignet mesuré avant seuil (gestes naturels : 1,15–1,35 souvent plus confortable).")]
    [SerializeField] private float wristRotationGain = 1.2f;
    [Tooltip("Plafonne l'écart d'angle main (°) par frame — évite les sauts de plusieurs crans (glitch tracking). 0 = pas de plafond (recommandé pour un suivi fluide).")]
    [SerializeField] private float maxHandAngleDeltaPerFrame = 0f;
    [Tooltip("Nombre max de crans par frame (recommandé : 1).")]
    [SerializeField] private int maxCransParFrame = 1;
    [Tooltip("Entre deux clics : fait tourner le mesh au prorata du poignet (même rythme que le geste). Désactiver pour pose fixe stricte seulement sur les crans.")]
    [SerializeField] private bool smoothVisualBetweenStepsWhileGrabbed = true;

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
    private float _accumulateurDelta = 0f;
    private Quaternion _rotationBaseLocale;
    private Vector3 _grabAxis;
    private bool _grabReferenceValid;
    private Quaternion _attachRotationPrecedente;
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
    public bool EstSurB => _positionBEnabled && _cranActuel == cranPositionB;

    private bool _positionBEnabled = true;

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

    /// <summary>Seine Lab : false = le cran « position B » (bureau) est inaccessible.</summary>
    public void SetPositionBEnabled(bool enabled)
    {
        _positionBEnabled = enabled;
        if (!enabled && _cranActuel == cranPositionB)
            SetCranSansInteraction(cranPositionA);
        else
            MettreAJourCouleur();
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

        float delta = ComputeSignedGrabTwistDeltaThisFrame();

        if (invertRotationDirection)
            delta = -delta;

        if (wristRotationGain > 0f && !Mathf.Approximately(wristRotationGain, 1f))
            delta *= wristRotationGain;

        if (maxHandAngleDeltaPerFrame > 0.01f)
            delta = Mathf.Clamp(delta, -maxHandAngleDeltaPerFrame, maxHandAngleDeltaPerFrame);

        _accumulateurDelta += delta;

        int ticksLeft = Mathf.Max(1, maxCransParFrame);
        while (ticksLeft > 0)
        {
            if (_accumulateurDelta >= seuilDegresCran)
            {
                _accumulateurDelta -= seuilDegresCran;
                ChangerCran(1);
                ticksLeft--;
            }
            else if (_accumulateurDelta <= -seuilDegresCran)
            {
                _accumulateurDelta += seuilDegresCran;
                ChangerCran(-1);
                ticksLeft--;
            }
            else
                break;
        }

        RestoreOriginalParentIfNeeded();

        // Certains setups XRI continuent d'injecter une rotation de grab
        // (offset visuel, ex. Y = -90) même si trackRotation est désactivé.
        // Pose du mesh : discrète ou suivi fluide entre crans selon l'accumulateur.
        if (lockRotationEveryFrameWhileGrabbed)
        {
            if (smoothVisualBetweenStepsWhileGrabbed)
                AppliquerRotationCranAvecFraction(_cranActuel, _accumulateurDelta);
            else
                AppliquerRotationCran(_cranActuel);
        }
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
        int n = Mathf.Max(1, nombreCrans);
        int next = _cranActuel;
        for (int guard = 0; guard < n; guard++)
        {
            next = (next + direction + n) % n;
            if (_positionBEnabled || next != cranPositionB)
                break;
        }

        _cranActuel = next;

        OnCranChange?.Invoke(_cranActuel);
        EnvoyerHapticsCran();
        MettreAJourCouleur();

        if (_cranActuel == cranPositionA)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(true);
        }
        else if (_positionBEnabled && _cranActuel == cranPositionB)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(false);
        }
    }

    private void AppliquerRotationCran(int cran)
    {
        float degresParCran = 360f / Mathf.Max(1, nombreCrans);
        float angleZ = cran * degresParCran;
        transform.localRotation = _rotationBaseLocale * Quaternion.Euler(0f, 0f, angleZ);
    }

    /// <summary>
    /// Rotation Z : cran logique + une fraction (accumulateur / seuil) pour coller au geste entre deux détentes.
    /// </summary>
    private void AppliquerRotationCranAvecFraction(int cran, float accumulateurDegres)
    {
        int n = Mathf.Max(1, nombreCrans);
        float degresParCran = 360f / n;
        float seuil = Mathf.Max(0.001f, seuilDegresCran);
        float frac = Mathf.Clamp(accumulateurDegres / seuil, -1f, 1f);
        float angleZ = cran * degresParCran + frac * degresParCran;
        transform.localRotation = _rotationBaseLocale * Quaternion.Euler(0f, 0f, angleZ);
    }

    private void MettreAJourCouleur()
    {
        if (cubeIndicateur == null) return;

        Color cible = couleurNeutre;
        if (_cranActuel == cranPositionA) cible = couleurA;
        else if (_positionBEnabled && _cranActuel == cranPositionB) cible = couleurB;

        cubeIndicateur.material.SetColor("_EmissionColor", cible);
        cubeIndicateur.material.color = cible;
    }

    private void InitGrabReference(IXRSelectInteractor interactor)
    {
        // Axe de rotation du potard (local Z = forward) : invariant en monde quand on ne fait que tourner le cran.
        _grabAxis = transform.forward.normalized;
        _grabReferenceValid = false;

        if (interactor == null) return;

        Transform attach = interactor.GetAttachTransform(_grabInteractable);
        if (attach == null) return;

        _attachRotationPrecedente = attach.rotation;
        _grabReferenceValid = true;
    }

    /// <summary>
    /// Delta d'orientation de la main frame à frame, projeté (twist) sur l'axe du potard.
    /// Évite SignedAngle sur forward projeté et ToAngleAxis bruités quand la main s'incline.
    /// </summary>
    private float ComputeSignedGrabTwistDeltaThisFrame()
    {
        if (_interactorCourant == null || !_grabReferenceValid) return 0f;

        Transform attach = _interactorCourant.GetAttachTransform(_grabInteractable);
        if (attach == null) return 0f;

        Quaternion current = attach.rotation;
        Quaternion dq = Quaternion.Inverse(_attachRotationPrecedente) * current;
        _attachRotationPrecedente = current;

        return SignedTwistAngleDegrees(dq, _grabAxis);
    }

    /// <summary>Extrait la composante &quot;vrille&quot; (twist) d'un quaternion autour d'un axe unitaire, en degrés signés.</summary>
    private static float SignedTwistAngleDegrees(Quaternion q, Vector3 twistAxis)
    {
        twistAxis = twistAxis.normalized;
        q = q.normalized;

        Vector3 p = new Vector3(q.x, q.y, q.z);
        float proj = Vector3.Dot(p, twistAxis);
        Quaternion twist = new Quaternion(twistAxis.x * proj, twistAxis.y * proj, twistAxis.z * proj, q.w);
        float len = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
        if (len < 1e-12f)
            return 0f;

        len = Mathf.Sqrt(len);
        twist.x /= len;
        twist.y /= len;
        twist.z /= len;
        twist.w /= len;

        float angleRad = 2f * Mathf.Acos(Mathf.Clamp(twist.w, -1f, 1f));
        if (angleRad < 1e-6f)
            return 0f;

        Vector3 im = new Vector3(twist.x, twist.y, twist.z);
        return angleRad * Mathf.Rad2Deg * Mathf.Sign(Vector3.Dot(im, twistAxis));
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