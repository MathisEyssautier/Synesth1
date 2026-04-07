using UnityEngine;
using UnityEngine.XR;

public class GodModeShortcut : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private XRNode inputNode = XRNode.LeftHand;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Faders to activate")]
    [SerializeField] private GameObject[] fadersToActivate;

    [Header("Doors to open")]
    [SerializeField] private DoorController doorA;
    [SerializeField] private DoorController doorB;
    [SerializeField] private float openAngle = -20f;
    [SerializeField] private bool enableDoorHandles = true;

    private bool _wasPressed;
    private bool _alreadyTriggered;

    private void Update()
    {
        if (_alreadyTriggered && triggerOnlyOnce) return;

        InputDevice device = InputDevices.GetDeviceAtXRNode(inputNode);
        bool pressed = false;
        if (device.isValid)
            device.TryGetFeatureValue(CommonUsages.primaryButton, out pressed);

        if (pressed && !_wasPressed)
        {
            TriggerGodMode();
            if (triggerOnlyOnce)
                _alreadyTriggered = true;
        }

        _wasPressed = pressed;
    }

    private void TriggerGodMode()
    {
        ActivateFaders();
        OpenDoor(doorA);
        OpenDoor(doorB);
    }

    private void ActivateFaders()
    {
        if (fadersToActivate == null) return;
        for (int i = 0; i < fadersToActivate.Length; i++)
        {
            var go = fadersToActivate[i];
            if (go == null) continue;
            go.SetActive(true);
        }
    }

    private void OpenDoor(DoorController door)
    {
        if (door == null || door.doorPivot == null) return;

        if (enableDoorHandles)
        {
            if (door.handle1 != null) door.handle1.gameObject.SetActive(true);
            if (door.handle2 != null) door.handle2.gameObject.SetActive(true);
        }

        float clampedAngle = Mathf.Clamp(openAngle, -Mathf.Abs(door.maxOpenAngle), 0f);
        door.currentYAngle = clampedAngle;
        door.doorPivot.rotation = Quaternion.Euler(0f, clampedAngle, 0f);
    }
}
