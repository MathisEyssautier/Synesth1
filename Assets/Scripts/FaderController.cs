using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FaderController : MonoBehaviour
{
    public enum FaderType { Violons, Guitare, Bass }

    [Header("FMOD")]
    public FaderType faderType;
    public MusicManagerScript musicManager;

    [Header("Références")]
    public Transform faderBase;
    public float railHalfLength = 0.30f;

    [Header("Valeur courante du fader")]
    [Range(0f, 1f)]
    public float value = 0f;

    [Header("Haptique cible")]
    [SerializeField] private bool enableTargetHaptics = true;
    [Range(0f, 1f)] [SerializeField] private float targetValue = 0.5f;
    [Range(0.001f, 0.2f)] [SerializeField] private float targetTolerance = 0.03f;
    [Range(0f, 1f)] [SerializeField] private float hapticAmplitude = 0.75f;
    [SerializeField] private float hapticDuration = 0.06f;

    private XRGrabInteractable _grab;
    private bool _isGrabbed = false;
    private float _lockedLocalY;
    private IXRSelectInteractor _activeInteractor;
    private bool _wasInsideTarget;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.trackPosition = false;
        _grab.throwOnDetach = false;
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);

        _lockedLocalY = faderBase.InverseTransformPoint(transform.position).y;
    }

    private void OnEnable()
    {
        // Quand le fader apparaît (SetActive true), on pousse tout de suite sa valeur vers FMOD
        // pour éviter d'avoir une piste muette tant qu'on ne l'a pas grab.
        ConstrainToRail();
        ApplyValueToMusic();
    }

    void Update()
    {
        if (!_isGrabbed) return;

        Vector3 handWorldPos = _grab.interactorsSelecting[0].GetAttachTransform(_grab).position;
        Vector3 localPos = faderBase.InverseTransformPoint(handWorldPos);

        localPos.y = _lockedLocalY;
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);

        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);

        UpdateTargetHaptics();
        ApplyValueToMusic();
    }

    void ConstrainToRail()
    {
        Vector3 localPos = faderBase.InverseTransformPoint(transform.position);
        localPos.y = _lockedLocalY;
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);
        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);
    }

    private void ApplyValueToMusic()
    {
        if (musicManager == null) return;

        switch (faderType)
        {
            case FaderType.Violons:
                musicManager.SetVolumeViolons(value);
                break;
            case FaderType.Guitare:
                musicManager.SetVolumeGuitare(value);
                break;
            case FaderType.Bass:
                musicManager.SetVolumeBass(value);
                break;
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _activeInteractor = args.interactorObject;
        _wasInsideTarget = Mathf.Abs(value - targetValue) <= targetTolerance;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        _activeInteractor = null;
        _wasInsideTarget = false;
        ConstrainToRail();
        ApplyValueToMusic();
    }

    private void UpdateTargetHaptics()
    {
        if (!enableTargetHaptics || !_isGrabbed) return;

        bool inside = Mathf.Abs(value - targetValue) <= targetTolerance;
        if (inside && !_wasInsideTarget)
            SendHapticToCurrentInteractor(hapticAmplitude, hapticDuration);

        _wasInsideTarget = inside;
    }

    private void SendHapticToCurrentInteractor(float amplitude, float duration)
    {
        float amp = Mathf.Clamp01(amplitude);
        float dur = Mathf.Max(0.01f, duration);

        if (_activeInteractor is XRBaseInputInteractor inputInteractor)
        {
            inputInteractor.SendHapticImpulse(amp, dur);
            return;
        }

        Transform t = _activeInteractor != null ? _activeInteractor.transform : null;
        if (t == null) return;

        if (NameLooksLeft(t))
        {
            TrySendToNode(XRNode.LeftHand, amp, dur);
            return;
        }

        if (NameLooksRight(t))
        {
            TrySendToNode(XRNode.RightHand, amp, dur);
        }
    }

    private static bool NameLooksLeft(Transform t)
    {
        if (t == null) return false;
        if (t.name.Contains("Left Controller")) return true;
        if (t.parent != null && t.parent.name.Contains("Left Controller")) return true;
        return false;
    }

    private static bool NameLooksRight(Transform t)
    {
        if (t == null) return false;
        if (t.name.Contains("Right Controller")) return true;
        if (t.parent != null && t.parent.name.Contains("Right Controller")) return true;
        return false;
    }

    private static bool TrySendToNode(XRNode node, float amp, float dur)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        if (!device.TryGetHapticCapabilities(out HapticCapabilities caps)) return false;
        if (!caps.supportsImpulse || caps.numChannels <= 0) return false;
        return device.SendHapticImpulse(0u, amp, dur);
    }
}