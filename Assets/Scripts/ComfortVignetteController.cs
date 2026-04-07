using UnityEngine;
using UnityEngine.XR;

public class ComfortVignetteController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup vignetteCanvasGroup;

    [Header("Input sampling")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private XRNode rightHandNode = XRNode.RightHand;
    [SerializeField] private float moveDeadzone = 0.2f;
    [SerializeField] private float turnDeadzone = 0.45f;

    [Header("Vignette")]
    [SerializeField] private float maxAlpha = 0.55f;
    [SerializeField] private float fadeInSpeed = 7f;
    [SerializeField] private float fadeOutSpeed = 5f;
    [SerializeField] private bool startEnabled = false;

    private bool _enabledByUser;
    private bool _suppressed;

    public bool IsEnabledByUser => _enabledByUser;

    private void Start()
    {
        _enabledByUser = startEnabled;
        if (vignetteCanvasGroup != null)
            vignetteCanvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (vignetteCanvasGroup == null) return;

        float target = 0f;
        if (_enabledByUser && !_suppressed)
            target = IsPlayerMovingOrTurning() ? maxAlpha : 0f;

        float speed = target > vignetteCanvasGroup.alpha ? fadeInSpeed : fadeOutSpeed;
        vignetteCanvasGroup.alpha = Mathf.MoveTowards(vignetteCanvasGroup.alpha, target, speed * Time.unscaledDeltaTime);
    }

    public void SetEnabledByUser(bool enabled)
    {
        _enabledByUser = enabled;
    }

    public void SetSuppressed(bool suppressed)
    {
        _suppressed = suppressed;
    }

    private bool IsPlayerMovingOrTurning()
    {
        var left = InputDevices.GetDeviceAtXRNode(leftHandNode);
        var right = InputDevices.GetDeviceAtXRNode(rightHandNode);

        Vector2 leftAxis = Vector2.zero;
        Vector2 rightAxis = Vector2.zero;

        if (left.isValid) left.TryGetFeatureValue(CommonUsages.primary2DAxis, out leftAxis);
        if (right.isValid) right.TryGetFeatureValue(CommonUsages.primary2DAxis, out rightAxis);

        bool moving = leftAxis.magnitude > moveDeadzone;
        bool turning = Mathf.Abs(rightAxis.x) > turnDeadzone;
        return moving || turning;
    }
}
