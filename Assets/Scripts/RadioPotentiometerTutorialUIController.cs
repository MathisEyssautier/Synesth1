using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Affiche le tuto potards au-dessus de la radio quand elle s'allume (déblocage piano),
/// puis le masque dès que le joueur entre dans la cuisine ou le bureau.
/// </summary>
public class RadioPotentiometerTutorialUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject tutorialPrefab;
    [SerializeField] private Transform anchor;
    [SerializeField] private RadioManager radioManager;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.22f, 0f);
    [SerializeField] private bool faceMainCamera = true;

    [Header("Bobbing (axe Y local du panneau)")]
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobSpeed = 2.5f;

    private GameObject _tutorialInstance;
    private Vector3 _baseLocalPosition;
    private bool _visible;
    private bool _dismissed;
    private Camera _mainCamera;

    private void OnEnable()
    {
        if (radioManager != null)
            radioManager.onRadioUnlocked.AddListener(OnRadioUnlocked);

        SalonNarrativeTriggerZone.PlayerEnteredZone += OnPlayerEnteredZone;
        EnsureHidden();

        if (radioManager != null && radioManager.IsRadioUnlocked)
            OnRadioUnlocked();
    }

    private void OnDisable()
    {
        if (radioManager != null)
            radioManager.onRadioUnlocked.RemoveListener(OnRadioUnlocked);

        SalonNarrativeTriggerZone.PlayerEnteredZone -= OnPlayerEnteredZone;
        EnsureHidden();
    }

    private void LateUpdate()
    {
        if (!_visible || _tutorialInstance == null)
            return;

        Transform t = _tutorialInstance.transform;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        t.localPosition = _baseLocalPosition + Vector3.up * bob;

        if (!faceMainCamera)
            return;

        if (_mainCamera == null)
            _mainCamera = ResolveMainCamera();
        if (_mainCamera == null)
            return;

        Vector3 toCam = _mainCamera.transform.position - t.position;
        if (toCam.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
    }

    private void OnRadioUnlocked()
    {
        if (_dismissed)
            return;

        Show();
    }

    private void OnPlayerEnteredZone(SalonNarrativeTriggerZone.ZoneType zone)
    {
        if (zone != SalonNarrativeTriggerZone.ZoneType.Kitchen
            && zone != SalonNarrativeTriggerZone.ZoneType.Office)
            return;

        Hide();
        _dismissed = true;
    }

    private void Show()
    {
        if (!EnsureInstance())
            return;

        _tutorialInstance.transform.SetParent(anchor, false);
        _baseLocalPosition = localOffset;
        _tutorialInstance.transform.localPosition = _baseLocalPosition;
        _tutorialInstance.transform.localRotation = Quaternion.identity;

        if (!_tutorialInstance.activeSelf)
            _tutorialInstance.SetActive(true);

        _visible = true;
    }

    private void Hide()
    {
        _visible = false;
        if (_tutorialInstance != null && _tutorialInstance.activeSelf)
            _tutorialInstance.SetActive(false);
    }

    private void EnsureHidden()
    {
        _visible = false;
        if (_tutorialInstance != null && _tutorialInstance.activeSelf)
            _tutorialInstance.SetActive(false);
    }

    private bool EnsureInstance()
    {
        if (_tutorialInstance != null)
            return true;

        if (tutorialPrefab == null)
        {
            Debug.LogWarning($"{nameof(RadioPotentiometerTutorialUIController)} on {name}: tutorialPrefab non assigné.", this);
            return false;
        }

        if (anchor == null)
        {
            Debug.LogWarning($"{nameof(RadioPotentiometerTutorialUIController)} on {name}: anchor non assigné.", this);
            return false;
        }

        _tutorialInstance = Instantiate(tutorialPrefab, anchor);
        _tutorialInstance.name = tutorialPrefab.name;
        _tutorialInstance.SetActive(false);
        return true;
    }

    private static Camera ResolveMainCamera()
    {
        if (Camera.main != null)
            return Camera.main;

        XROrigin origin = FindFirstObjectByType<XROrigin>();
        if (origin != null && origin.Camera != null)
            return origin.Camera;

        return FindFirstObjectByType<Camera>();
    }
}
