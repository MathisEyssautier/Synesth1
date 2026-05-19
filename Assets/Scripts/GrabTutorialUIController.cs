using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;

/// <summary>
/// Affiche un canvas world-space (prefab 2D) au-dessus d'un objet saisi,
/// puis le masque quand l'objet est « éteint » via la gâchette (XRGrabInteractable activated → GrabbableMusicObject).
/// </summary>
public class GrabTutorialUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject tutorialPrefab;
    [Tooltip("Point d'ancrage (souvent la racine de l'objet saisi).")]
    [SerializeField] private Transform anchor;
    [SerializeField] private XRGrabInteractable grabInteractable;
    [SerializeField] private GrabbableMusicObject musicObject;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.14f, 0f);
    [SerializeField] private bool faceMainCamera = true;

    [Header("Comportement")]
    [SerializeField] private bool hideAfterFirstDismiss = true;

    private GameObject _tutorialInstance;
    private bool _visible;
    private bool _dismissed;
    private Camera _mainCamera;

    private void Awake()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponentInChildren<XRGrabInteractable>();

        if (musicObject == null && grabInteractable != null)
            musicObject = grabInteractable.GetComponent<GrabbableMusicObject>();

        if (anchor == null && grabInteractable != null)
            anchor = grabInteractable.transform;
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnGrabbed);

        GrabbableMusicObject.OnStateChanged += OnMusicStateChanged;
        EnsureHidden();
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);

        GrabbableMusicObject.OnStateChanged -= OnMusicStateChanged;
        EnsureHidden();
    }

    private void LateUpdate()
    {
        if (!_visible || _tutorialInstance == null || !faceMainCamera)
            return;

        if (_mainCamera == null)
            _mainCamera = ResolveMainCamera();
        if (_mainCamera == null)
            return;

        Transform t = _tutorialInstance.transform;
        Vector3 toCam = _mainCamera.transform.position - t.position;
        if (toCam.sqrMagnitude > 0.0001f)
            t.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (hideAfterFirstDismiss && _dismissed)
            return;

        Show();
    }

    private void OnMusicStateChanged(GrabbableMusicObject obj, bool isOn)
    {
        if (!_visible || musicObject == null || obj != musicObject || isOn)
            return;

        Hide();
        if (hideAfterFirstDismiss)
            _dismissed = true;
    }

    private void Show()
    {
        if (!EnsureInstance())
            return;

        _tutorialInstance.transform.SetParent(anchor, false);
        _tutorialInstance.transform.localPosition = localOffset;
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
            Debug.LogWarning($"{nameof(GrabTutorialUIController)} on {name}: tutorialPrefab non assigné.", this);
            return false;
        }

        if (anchor == null)
        {
            Debug.LogWarning($"{nameof(GrabTutorialUIController)} on {name}: anchor non assigné.", this);
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
