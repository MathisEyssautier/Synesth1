using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class DrawerGrab : MonoBehaviour
{
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

    private XRGrabInteractable _grab;
    private Rigidbody _rb;

    private bool _isHeld;
    private IXRSelectInteractor _interactor;

    private Vector3 _closedWorldPos;
    private Vector3 _forward;
    private float _grabOffsetDist;
    private float _currentDist;
    private float _distVel;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // On prend la pose de la scène comme position "fermée".
        _closedWorldPos = transform.position;
        _forward = (invertDirection ? -transform.forward : transform.forward).normalized;
        _currentDist = 0f;

        // Optionnel: pour éviter la physique qui se bat avec notre contrainte.
        _rb.isKinematic = true;
        _rb.useGravity = false;

        if (overrideGrabTracking)
        {
            _grab.trackPosition = false;
            _grab.trackRotation = false;
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

        if (_interactor != null)
        {
            // Distance actuelle du tiroir le long de son forward.
            _currentDist = Vector3.Dot(transform.position - _closedWorldPos, _forward);

            // Distance de la main par rapport à la position fermée, projetée sur forward.
            float handDist = Vector3.Dot(_interactor.transform.position - _closedWorldPos, _forward);

            // Offset pour éviter le "snap" brutal : on garde l'offset entre la main et le tiroir.
            _grabOffsetDist = _currentDist - handDist;
        }
        else
        {
            _grabOffsetDist = 0f;
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

        // Distance de la main depuis la position fermée, projetée sur l'axe forward du tiroir.
        Vector3 handWorld = _interactor.transform.position;
        float handDist = Vector3.Dot(handWorld - _closedWorldPos, _forward);

        float rawDist = handDist + _grabOffsetDist;
        float targetDist = Mathf.Clamp(rawDist, 0f, maxPullDistance);

        SetDistance(targetDist, instant: smoothTime <= 0f);
    }

    private void SetDistance(float targetDist, bool instant)
    {
        float newDist = targetDist;
        if (!instant)
            newDist = Mathf.SmoothDamp(_currentDist, targetDist, ref _distVel, smoothTime);

        _currentDist = newDist;
        transform.position = _closedWorldPos + _forward * _currentDist;
    }
}

