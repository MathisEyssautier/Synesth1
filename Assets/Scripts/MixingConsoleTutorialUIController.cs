using UnityEngine;
using Unity.XR.CoreUtils;

/// <summary>
/// Affiche le tuto table de mix au-dessus de la console quand les trois faders apparaissent
/// (spot allumée, équilibrage requis), puis le masque quand la séquence finale démarre.
/// </summary>
public class MixingConsoleTutorialUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject tutorialPrefab;
    [SerializeField] private Transform anchor;
    [SerializeField] private FinalSequenceController finalSequenceController;

    [Header("Placement")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private bool faceMainCamera = true;

    [Header("Bobbing (axe Y local du panneau)")]
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobSpeed = 2.5f;

    [Header("Debug")]
    [Tooltip("Affiche le tuto sans condition (test visuel dans l'éditeur).")]
    [SerializeField] private bool debugForceVisible;

    private GameObject _tutorialInstance;
    private Vector3 _baseLocalPosition;
    private bool _visible;
    private bool _dismissed;
    private Camera _mainCamera;

    private void OnEnable()
    {
        if (finalSequenceController != null)
            finalSequenceController.onFinalMixSequenceStarted.AddListener(OnFinalMixSequenceStarted);

        EnsureHidden();
        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (finalSequenceController != null)
            finalSequenceController.onFinalMixSequenceStarted.RemoveListener(OnFinalMixSequenceStarted);

        EnsureHidden();
    }

    private void Update()
    {
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (debugForceVisible)
        {
            if (!_visible)
                Show();
            return;
        }

        if (_dismissed || finalSequenceController == null)
        {
            if (_visible)
                Hide();
            return;
        }

        bool shouldShow = finalSequenceController.AreAllMixFadersActiveInHierarchy
                          && !finalSequenceController.IsFinalMixSequenceStarted;

        if (shouldShow)
        {
            if (!_visible)
                Show();
        }
        else if (_visible)
        {
            Hide();
        }
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

    private void OnFinalMixSequenceStarted()
    {
        Hide();
        _dismissed = true;
    }

    /// <summary>Pour God Mode / tests : force l'affichage même si les faders ne sont pas encore actifs.</summary>
    public void SetDebugForceVisible(bool force)
    {
        debugForceVisible = force;
        if (!force)
            RefreshVisibility();
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
            Debug.LogWarning($"{nameof(MixingConsoleTutorialUIController)} on {name}: tutorialPrefab non assigné.", this);
            return false;
        }

        if (anchor == null)
        {
            Debug.LogWarning($"{nameof(MixingConsoleTutorialUIController)} on {name}: anchor non assigné.", this);
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
