using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class DrawerGrab : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform du tiroir à déplacer. Laisser vide pour utiliser ce GameObject.")]
    [SerializeField] private Transform drawerTransform;

    [Header("Range (world distance along forward)")]
    [Tooltip("Distance max (en mètres) que le tiroir peut être tiré le long de son axe avant (transform.forward).")]
    [SerializeField] private float maxPullDistance = 0.5f;

    [Header("Behavior")]
    [Tooltip("Si true: force le XRGrabInteractable à ne pas déplacer l'objet (on le fait nous-mêmes).")]
    [SerializeField] private bool overrideGrabTracking = true;
    [Tooltip("Adoucissement du mouvement (0 = instant).")]
    [SerializeField] private float smoothTime = 0.02f;

    [Tooltip("Si true, on utilise -transform.forward (pour inverser le sens tiré/poussé).")]
    [SerializeField] private bool invertDirection = true;
    [Tooltip("Axe local du tiroir utilisé pour le coulissement (X=1,0,0 | Y=0,1,0 | Z=0,0,1).")]
    [SerializeField] private Vector3 localSlideAxis = Vector3.forward;
    [Tooltip("Verrouille la chaine de parents pendant le grab pour eviter qu'une partie de la scene soit emportee.")]
    [SerializeField] private bool lockAncestorChainWhileGrabbed = true;
    [Tooltip("En dessous de cette distance (m), le tiroir est considéré fermé (indice flèche, etc.).")]
    [SerializeField] private float closedDistanceThreshold = 0.015f;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private bool _isHeld;
    private IXRSelectInteractor _interactor;

    private Vector3 _closedLocalPos;
    private Vector3 _axisInParentLocal;
    private Transform _initialParent;
    private Transform[] _ancestorChain = new Transform[0];
    private Vector3[] _ancestorLocalPositions = new Vector3[0];
    private Quaternion[] _ancestorLocalRotations = new Quaternion[0];
    private Vector3 _grabStartHandInParentLocal;
    private float _grabStartDist;
    private float _currentDist;
    private float _distVel;

    /// <summary>Distance actuelle du coulissement (0 = fermé).</summary>
    public float CurrentPullDistance => _currentDist;

    /// <summary>Vrai si le tiroir est à sa position fermée (± seuil).</summary>
    public bool IsClosed => _currentDist <= closedDistanceThreshold;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        if (drawerTransform == null)
            drawerTransform = transform;
        _initialParent = drawerTransform.parent;
        CacheAncestorChain();
    }

    private void Start()
    {
        // On prend la pose de la scène comme position "fermée" (en local pour éviter les dérives).
        _closedLocalPos = drawerTransform.localPosition;
        Vector3 axis = localSlideAxis.sqrMagnitude > 0.0001f ? localSlideAxis.normalized : Vector3.forward;
        if (invertDirection)
            axis = -axis;
        _axisInParentLocal = (drawerTransform.localRotation * axis).normalized;
        _currentDist = 0f;
        ApplyCurrentDistanceImmediate();

        // Optionnel: pour éviter la physique qui se bat avec notre contrainte.
        _rb.isKinematic = true;
        _rb.useGravity = false;

        if (overrideGrabTracking)
        {
            _grab.trackPosition = false;
            _grab.trackRotation = false;
            _grab.trackScale = false;
            _grab.useDynamicAttach = false;
            _grab.attachEaseInTime = 0f;
            _grab.retainTransformParent = true;
            _grab.throwOnDetach = false;
        }
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
        _distVel = 0f;
        RestoreExpectedParentIfNeeded();
        RestoreAncestorChainIfNeeded();
        ApplyCurrentDistanceImmediate();

        if (_interactor != null)
        {
            // On part de la distance logique déjà suivie, pas de la pose instantanée
            // (qui peut être polluée par un déplacement XRI au tout début du grab).
            _grabStartDist = _currentDist;

            // On mémorise la main dans le repère parent pour travailler en delta local stable.
            _grabStartHandInParentLocal = WorldToParentLocal(_interactor.transform.position);
        }
        else
        {
            _grabStartDist = _currentDist;
            _grabStartHandInParentLocal = Vector3.zero;
        }
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _isHeld = false;
        _interactor = null;
        _distVel = 0f;
    }

    private void Update()
    {
        if (!_isHeld || _interactor == null) return;

        RestoreExpectedParentIfNeeded();
        Vector3 handNowInParentLocal = WorldToParentLocal(_interactor.transform.position);
        Vector3 handDeltaInParentLocal = handNowInParentLocal - _grabStartHandInParentLocal;
        float axisDelta = Vector3.Dot(handDeltaInParentLocal, _axisInParentLocal);
        float rawDist = _grabStartDist + axisDelta;
        float targetDist = Mathf.Clamp(rawDist, 0f, maxPullDistance);

        SetDistance(targetDist, instant: smoothTime <= 0f);
    }

    private void LateUpdate()
    {
        RestoreExpectedParentIfNeeded();
        RestoreAncestorChainIfNeeded();
        ApplyCurrentDistanceImmediate();
    }

    private void SetDistance(float targetDist, bool instant)
    {
        float newDist = targetDist;
        if (!instant)
            newDist = Mathf.SmoothDamp(_currentDist, targetDist, ref _distVel, smoothTime);

        _currentDist = newDist;
    }

    private void ApplyCurrentDistanceImmediate()
    {
        drawerTransform.localPosition = _closedLocalPos + _axisInParentLocal * _currentDist;
    }

    private void OnValidate()
    {
        if (localSlideAxis.sqrMagnitude <= 0.0001f)
            localSlideAxis = Vector3.forward;
        maxPullDistance = Mathf.Max(0f, maxPullDistance);
        smoothTime = Mathf.Max(0f, smoothTime);
    }

    private Vector3 WorldToParentLocal(Vector3 worldPos)
    {
        return drawerTransform.parent != null
            ? drawerTransform.parent.InverseTransformPoint(worldPos)
            : worldPos;
    }

    private void RestoreExpectedParentIfNeeded()
    {
        if (_initialParent == null || drawerTransform.parent == _initialParent)
            return;
        drawerTransform.SetParent(_initialParent, true);
    }

    private void CacheAncestorChain()
    {
        if (_initialParent == null)
        {
            _ancestorChain = new Transform[0];
            _ancestorLocalPositions = new Vector3[0];
            _ancestorLocalRotations = new Quaternion[0];
            return;
        }

        int count = 0;
        Transform t = _initialParent;
        while (t != null)
        {
            count++;
            t = t.parent;
        }

        _ancestorChain = new Transform[count];
        _ancestorLocalPositions = new Vector3[count];
        _ancestorLocalRotations = new Quaternion[count];

        t = _initialParent;
        int i = 0;
        while (t != null)
        {
            _ancestorChain[i] = t;
            _ancestorLocalPositions[i] = t.localPosition;
            _ancestorLocalRotations[i] = t.localRotation;
            i++;
            t = t.parent;
        }
    }

    private void RestoreAncestorChainIfNeeded()
    {
        if (!lockAncestorChainWhileGrabbed || !_isHeld)
            return;
        if (_ancestorChain == null || _ancestorChain.Length == 0)
            return;

        for (int i = 0; i < _ancestorChain.Length; i++)
        {
            Transform t = _ancestorChain[i];
            if (t == null) continue;
            t.localPosition = _ancestorLocalPositions[i];
            t.localRotation = _ancestorLocalRotations[i];
        }
    }
}

