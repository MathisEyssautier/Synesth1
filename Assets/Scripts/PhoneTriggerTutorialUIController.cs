using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Tuto gâchette : visible en main tant que le téléphone est allumé, masqué si éteint ou lâché.
/// </summary>
[DisallowMultipleComponent]
public class PhoneTriggerTutorialUIController : MonoBehaviour
{
    [Header("Refs (auto si vides)")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private RingingPhone ringingPhone;

    [Header("Bobbing (axe Y local)")]
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobSpeed = 2.5f;

    private Vector3 _baseLocalPosition;
    private bool _baseCaptured;

    private void Awake()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();

        if (grabInteractable == null)
            grabInteractable = GetComponentInParent<XRGrabInteractable>();

        if (ringingPhone == null)
            ringingPhone = GetComponentInParent<RingingPhone>();

        CaptureBaseLocalPosition();
        SetCanvasVisible(false);
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabChanged);
            grabInteractable.selectExited.AddListener(OnGrabChanged);
        }

        RingingPhone.OnStateChanged += OnPhoneStateChanged;
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabChanged);
            grabInteractable.selectExited.RemoveListener(OnGrabChanged);
        }

        RingingPhone.OnStateChanged -= OnPhoneStateChanged;
        SetCanvasVisible(false);
    }

    private void Update()
    {
        RefreshVisibility();
    }

    private void LateUpdate()
    {
        if (targetCanvas == null || !targetCanvas.enabled)
            return;

        if (!_baseCaptured)
            CaptureBaseLocalPosition();

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.localPosition = _baseLocalPosition + Vector3.up * bob;
    }

    private void OnGrabChanged(SelectEnterEventArgs args) => RefreshVisibility();
    private void OnGrabChanged(SelectExitEventArgs args) => RefreshVisibility();

    private void OnPhoneStateChanged(RingingPhone phone, bool isOn)
    {
        if (ringingPhone != null && phone != ringingPhone)
            return;

        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        bool show = ShouldShow();
        if (targetCanvas != null && targetCanvas.enabled == show)
            return;

        SetCanvasVisible(show);

        if (show && !_baseCaptured)
            CaptureBaseLocalPosition();
    }

    private bool ShouldShow()
    {
        if (ringingPhone == null || grabInteractable == null)
            return false;

        return ringingPhone.IsOn && grabInteractable.isSelected;
    }

    private void SetCanvasVisible(bool visible)
    {
        if (targetCanvas != null)
            targetCanvas.enabled = visible;
        else
            gameObject.SetActive(visible);
    }

    private void CaptureBaseLocalPosition()
    {
        _baseLocalPosition = transform.localPosition;
        _baseCaptured = true;
    }
}
