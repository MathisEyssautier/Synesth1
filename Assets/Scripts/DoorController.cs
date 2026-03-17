using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DoorController : MonoBehaviour
{
    [Header("References")]
    public Transform doorPivot;
    public Transform handle1;
    public Transform handle2;

    [Header("Door Settings")]
    public float maxOpenAngle = 90f;
    public float closedAngleThreshold = 2f;

    [Header("Inertie")]
    [Tooltip("Facteur de freinage apres le release (0 = pas de frein, 1 = arret immediat)")]
    public float damping = 0.85f;
    [Tooltip("Vitesse angulaire max en degres par seconde")]
    public float vitesseMax = 200f;

    [Header("FMOD")]
    public float transitionSpeed = 1f;

    private XRSimpleInteractable simpleInteractable;
    private XRSimpleInteractable simpleInteractable2;

    private bool isGrabbed = false;
    private bool _inertieActive = false;
    public float currentYAngle = 0f;
    private float currentDoorParam = 0f;
    private IXRSelectInteractor currentInteractor;
    private float grabAngleOffset = 0f;
    private float angleAtGrab = 0f;

    // Pour le calcul de velocite
    private float _anglePrecedent = 0f;
    private float _velociteAngulaire = 0f;

    void Start()
    {
        simpleInteractable = handle1.GetComponent<XRSimpleInteractable>();
        simpleInteractable.selectEntered.AddListener(OnGrab);
        simpleInteractable.selectExited.AddListener(OnRelease);

        simpleInteractable2 = handle2.GetComponent<XRSimpleInteractable>();
        simpleInteractable2.selectEntered.AddListener(OnGrab);
        simpleInteractable2.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        currentInteractor = args.interactorObject;
        angleAtGrab = currentYAngle;
        grabAngleOffset = GetHandAngle();
        _anglePrecedent = currentYAngle;
        _velociteAngulaire = 0f;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;
        _inertieActive = Mathf.Abs(_velociteAngulaire) > 0.1f;
        Debug.Log("Release - velocite : " + _velociteAngulaire);
    }

    float GetHandAngle()
    {
        Vector3 handPosition = currentInteractor.transform.position;
        Vector3 directionToHand = handPosition - doorPivot.position;
        directionToHand.y = 0f;
        if (directionToHand.magnitude < 0.01f) return 0f;
        return Vector3.SignedAngle(Vector3.forward, directionToHand.normalized, Vector3.up);
    }

    void LateUpdate()
    {
        if (isGrabbed && currentInteractor != null)
        {
            UpdateDoorRotation();

            // Calcule la velocite angulaire en temps reel pendant le grab
            _velociteAngulaire = (currentYAngle - _anglePrecedent) / Time.deltaTime;
            _velociteAngulaire = Mathf.Clamp(_velociteAngulaire, -vitesseMax, vitesseMax);
            _anglePrecedent = currentYAngle;
        }
        else if (_inertieActive)
        {
            Debug.Log("Inertie frame - velocite: " + _velociteAngulaire + " | angle: " + currentYAngle);
            _velociteAngulaire *= Mathf.Pow(1f - damping, Time.deltaTime * 60f);

            currentYAngle += _velociteAngulaire * Time.deltaTime;
            currentYAngle = Mathf.Clamp(currentYAngle, -maxOpenAngle, 0f);

            doorPivot.rotation = Quaternion.Euler(0f, currentYAngle, 0f);

            if (currentYAngle <= -maxOpenAngle || currentYAngle >= 0f || Mathf.Abs(_velociteAngulaire) < 0.1f)
            {
                _velociteAngulaire = 0f;
                _inertieActive = false;
            }
        }
    }

    void UpdateDoorRotation()
    {
        float handAngle = GetHandAngle();
        float angleDelta = handAngle - grabAngleOffset;
        float targetAngle = angleAtGrab + angleDelta;
        targetAngle = Mathf.Clamp(targetAngle, -maxOpenAngle, 0f);
        currentYAngle = targetAngle;
        doorPivot.rotation = Quaternion.Euler(0f, currentYAngle, 0f);
    }

    public void ForceClose()
    {
        currentYAngle = 0f;
        grabAngleOffset = 0f;
        angleAtGrab = 0f;
        isGrabbed = false;
        _inertieActive = false;
        currentInteractor = null;
        _velociteAngulaire = 0f;
        doorPivot.rotation = Quaternion.Euler(0f, 0f, 0f);
    }

    void OnDestroy()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnGrab);
            simpleInteractable.selectExited.RemoveListener(OnRelease);
            simpleInteractable2.selectEntered.RemoveListener(OnGrab);
            simpleInteractable2.selectExited.RemoveListener(OnRelease);
        }
    }
}