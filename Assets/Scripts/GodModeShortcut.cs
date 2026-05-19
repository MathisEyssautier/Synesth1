using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif

public class GodModeShortcut : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private XRNode inputNode = XRNode.LeftHand;
    [Tooltip("Left controller Y on Quest.")]
    [SerializeField] private bool useSecondaryButton = true;
    [SerializeField] private bool triggerOnlyOnce = true;

    [Header("Faders to activate")]
    [SerializeField] private GameObject[] fadersToActivate;

    [Header("Doors to open")]
    [SerializeField] private DoorController doorA;
    [SerializeField] private DoorController doorB;
    [SerializeField] private float openAngle = -20f;
    [SerializeField] private bool enableDoorHandles = true;

    [Header("Sync (tuto table de mix, spot faders)")]
    [SerializeField] private FinalSequenceController finalSequenceController;
    [SerializeField] private MixingConsoleTutorialUIController mixingConsoleTutorial;

    private bool _wasPressed;
    private bool _alreadyTriggered;

    private void Update()
    {
        if (_alreadyTriggered && triggerOnlyOnce) return;

        bool pressed = ReadTriggerButtonHeld();

        if (pressed && !_wasPressed)
        {
            TriggerGodMode();
            if (triggerOnlyOnce)
                _alreadyTriggered = true;
        }

        _wasPressed = pressed;
    }

    private bool ReadTriggerButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        var xrDevice = inputNode == XRNode.LeftHand ? XRController.leftHand : XRController.rightHand;
        if (xrDevice != null)
        {
            string controlName = useSecondaryButton ? "secondaryButton" : "primaryButton";
            var btn = xrDevice.TryGetChildControl<ButtonControl>(controlName);
            if (btn != null)
                return btn.isPressed;
        }
#endif
        var device = InputDevices.GetDeviceAtXRNode(inputNode);
        if (!device.isValid) return false;

        var usage = useSecondaryButton
            ? UnityEngine.XR.CommonUsages.secondaryButton
            : UnityEngine.XR.CommonUsages.primaryButton;
        return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
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

        if (finalSequenceController == null)
            finalSequenceController = FindFirstObjectByType<FinalSequenceController>();
        finalSequenceController?.RefreshMixFadersVisibilityState();

        if (mixingConsoleTutorial == null)
            mixingConsoleTutorial = FindFirstObjectByType<MixingConsoleTutorialUIController>();
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
