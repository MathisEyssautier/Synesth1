using UnityEngine;
using UnityEngine.Serialization;
using Unity.XR.CoreUtils;

/// <summary>
/// Affiche le tuto table de mix au-dessus de la console quand les trois faders apparaissent
/// (spot allumée, équilibrage requis), puis le masque quand la séquence finale démarre.
/// </summary>
public class MixingConsoleTutorialUIController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject tutorialPrefab;
    [SerializeField] private FinalSequenceController finalSequenceController;

    [Header("Placement — snap point (recommandé)")]
    [Tooltip("Empty en scène au-dessus des faders. Si rempli, le tuto suit sa position/rotation.")]
    [SerializeField] private Transform tutorialSnapPoint;
    [Tooltip("Décalage local optionnel par rapport au snap point (mètres).")]
    [SerializeField] private Vector3 offsetMetersFromSnapPoint = Vector3.zero;

    [Header("Placement — repli (si pas de snap point)")]
    [Tooltip("Legacy : utilisé si Tutorial Snap Point est vide.")]
    [SerializeField] private Transform anchor;
    [SerializeField] private Transform tableMixage;
    [FormerlySerializedAs("localOffset")]
    [Tooltip("Décalage en mètres (axes locaux TableMixage, rotation seule).")]
    [SerializeField] private Vector3 offsetMetersFromTable = new Vector3(0f, 0.35f, 0.08f);

    [Header("Placement — rendu")]
    [SerializeField] private bool faceMainCamera = true;
    [Tooltip("Rotation locale ajoutée à l'activation (ex. Y = 90 pour orienter le panneau vers les faders).")]
    [SerializeField] private Vector3 eulerRotationOffset = new Vector3(0f, 90f, 0f);
    [Tooltip("Largeur du panneau en mètres (monde).")]
    [SerializeField] private float targetWorldWidthMeters = 0.47f;

    [Header("Bobbing")]
    [SerializeField] private float bobAmplitude = 0.015f;
    [SerializeField] private float bobSpeed = 2.5f;

    [Header("Debug")]
    [Tooltip("Affiche le tuto sans condition (test visuel dans l'éditeur).")]
    [SerializeField] private bool debugForceVisible;

    private GameObject _tutorialInstance;
    private Vector3 _baseWorldPosition;
    private bool _visible;
    private bool _dismissed;
    private Camera _mainCamera;

    private Transform TableAnchor => tableMixage != null ? tableMixage : anchor;
    private bool UsesSnapPoint => tutorialSnapPoint != null;

    private void Awake()
    {
        ResolveTableMixageAnchor();
    }

    private void ResolveTableMixageAnchor()
    {
        if (tableMixage == null && anchor != null)
            tableMixage = anchor;

        if (tableMixage != null || UsesSnapPoint)
            return;

        var found = GameObject.Find("TableMixage");
        if (found != null)
            tableMixage = found.transform;
    }

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

        bool shouldShow = SalonExplorationNarrative.IsFinalMixGameplayUnlocked
                          && finalSequenceController.AreAllMixFadersActiveInHierarchy
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
        if (!_visible || _tutorialInstance == null || !HasPlacementAnchor())
            return;

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        ApplyWorldPlacement(bob);

        if (!faceMainCamera)
            return;

        if (_mainCamera == null)
            _mainCamera = ResolveMainCamera();
        if (_mainCamera == null)
            return;

        ApplyTutorialRotation(_tutorialInstance.transform);
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

        Transform root = _tutorialInstance.transform;
        root.SetParent(transform, worldPositionStays: false);

        ApplyPanelWorldScale(root);
        ApplyWorldPlacement(0f);
        ApplyTutorialRotation(root);

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

    private bool HasPlacementAnchor() => UsesSnapPoint || TableAnchor != null;

    private void ApplyWorldPlacement(float bobMeters)
    {
        if (UsesSnapPoint)
        {
            _baseWorldPosition = tutorialSnapPoint.TransformPoint(offsetMetersFromSnapPoint);
            Vector3 bobAxis = tutorialSnapPoint.up;
            _tutorialInstance.transform.position = _baseWorldPosition + bobAxis * bobMeters;
            return;
        }

        Transform table = TableAnchor;
        Vector3 offsetWorld = table.rotation * offsetMetersFromTable;
        _baseWorldPosition = table.position + offsetWorld + Vector3.up * bobMeters;
        _tutorialInstance.transform.position = _baseWorldPosition;
    }

    private Quaternion GetBaseWorldRotation()
    {
        if (UsesSnapPoint)
            return tutorialSnapPoint.rotation;
        if (TableAnchor != null)
            return TableAnchor.rotation;
        return Quaternion.identity;
    }

    private void ApplyTutorialRotation(Transform root)
    {
        Quaternion offset = Quaternion.Euler(eulerRotationOffset);

        if (faceMainCamera)
        {
            if (_mainCamera == null)
                _mainCamera = ResolveMainCamera();
            if (_mainCamera != null)
            {
                Vector3 toCam = _mainCamera.transform.position - root.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    root.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up) * offset;
                else
                    root.rotation = GetBaseWorldRotation() * offset;
                return;
            }
        }

        root.rotation = GetBaseWorldRotation() * offset;
    }

    private void ApplyPanelWorldScale(Transform root)
    {
        float widthUnits = 933f;
        var rt = root.GetComponent<RectTransform>();
        if (rt != null && rt.sizeDelta.x > 1f)
            widthUnits = rt.sizeDelta.x;

        float uniform = targetWorldWidthMeters / widthUnits;
        root.localScale = new Vector3(uniform, uniform, uniform);
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

        ResolveTableMixageAnchor();
        if (!HasPlacementAnchor())
        {
            Debug.LogWarning(
                $"{nameof(MixingConsoleTutorialUIController)} on {name}: assigne un Tutorial Snap Point (empty en scène) ou TableMixage.",
                this);
            return false;
        }

        _tutorialInstance = Instantiate(tutorialPrefab, transform);
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (tutorialSnapPoint == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(tutorialSnapPoint.TransformPoint(offsetMetersFromSnapPoint), 0.04f);
        Gizmos.DrawLine(tutorialSnapPoint.position, tutorialSnapPoint.TransformPoint(offsetMetersFromSnapPoint));
    }
#endif
}
