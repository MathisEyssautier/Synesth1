using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShellPlacementZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private ShellColorId zoneColorId;
    [Tooltip("Light (ou GO parent) à activer quand correct (souvent enfant de la sphère).")]
    [SerializeField] private GameObject confirmationLightObject;

    [Header("Optional snap")]
    [SerializeField] private bool snapOnCorrectWhenReleased = true;
    [SerializeField] private Transform snapPoint;

    [Header("Manager")]
    [SerializeField] private ShellPuzzleManager puzzleManager;

    public ShellColorId ZoneColorId => zoneColorId;
    public bool IsCorrectlyOccupied { get; private set; }
    public ShellProximityFeedback CurrentShell { get; private set; }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (confirmationLightObject != null)
            confirmationLightObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        var shell = other.GetComponentInParent<ShellProximityFeedback>();
        if (shell == null) return;

        CurrentShell = shell;
        UpdateState();
    }

    private void OnTriggerStay(Collider other)
    {
        if (CurrentShell == null) return;
        UpdateState();
    }

    private void OnTriggerExit(Collider other)
    {
        var shell = other.GetComponentInParent<ShellProximityFeedback>();
        if (shell == null) return;
        if (shell != CurrentShell) return;

        CurrentShell = null;
        SetCorrect(false);
    }

    private void UpdateState()
    {
        if (CurrentShell == null)
            return;

        bool correct = CurrentShell.ColorId == zoneColorId;
        SetCorrect(correct);

        if (correct && snapOnCorrectWhenReleased && !CurrentShell.IsHeld)
            SnapShell(CurrentShell);
    }

    private void SetCorrect(bool correct)
    {
        if (IsCorrectlyOccupied == correct) return;
        IsCorrectlyOccupied = correct;

        if (confirmationLightObject != null)
            confirmationLightObject.SetActive(correct);

        if (puzzleManager != null)
            puzzleManager.NotifyZoneChanged();
    }

    private void SnapShell(ShellProximityFeedback shell)
    {
        if (snapPoint == null) return;

        var rb = shell.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        shell.transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
    }
}

