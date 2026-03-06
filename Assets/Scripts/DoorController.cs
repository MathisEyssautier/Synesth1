using FMODUnity;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DoorController : MonoBehaviour
{
    [Header("Références")]
    public Transform doorPivot;
    public Transform handle;

    [Header("Door Settings")]
    public float maxOpenAngle = 90f;
    public float closedAngleThreshold = 2f;

    [Header("FMOD")]
    public float transitionSpeed = 1f;

    private XRSimpleInteractable simpleInteractable;
    private bool isGrabbed = false;
    private float currentYAngle = 0f;
    private float currentDoorParam = 0f;
    private IXRSelectInteractor currentInteractor;

    private float grabAngleOffset = 0f;
    private float angleAtGrab = 0f;

    void Start()
    {
        simpleInteractable = handle.GetComponent<XRSimpleInteractable>();
        simpleInteractable.selectEntered.AddListener(OnGrab);
        simpleInteractable.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        currentInteractor = args.interactorObject;
        angleAtGrab = currentYAngle;
        grabAngleOffset = GetHandAngle();
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;
    }

    float GetHandAngle()
    {
        Vector3 handPosition = currentInteractor.transform.position;
        Vector3 directionToHand = handPosition - doorPivot.position;
        directionToHand.y = 0f;

        if (directionToHand.magnitude < 0.01f) return 0f;

        return Vector3.SignedAngle(Vector3.forward, directionToHand.normalized, Vector3.up);
    }

    void Update()
    {
        if (isGrabbed && currentInteractor != null)
        {
            UpdateDoorRotation();
        }

        UpdateFMODParameter();
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

    void UpdateFMODParameter()
    {
        bool isDoorOpen = currentYAngle < -closedAngleThreshold;
        float targetParam = isDoorOpen ? 1f : 0f;

        currentDoorParam = Mathf.Lerp(currentDoorParam, targetParam, Time.deltaTime * transitionSpeed);
        RuntimeManager.StudioSystem.setParameterByName("DoorOpen", currentDoorParam);
    }

    void OnDestroy()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnGrab);
            simpleInteractable.selectExited.RemoveListener(OnRelease);
        }
    }
}