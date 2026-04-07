using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class ShellPlacementZone : MonoBehaviour
{
    [Header("Zone")]
    [SerializeField] private ShellColorId zoneColorId;
    [Tooltip("Light (ou GO parent) : allumée seulement tant qu'un coquillage repose dans la zone (pas en main).")]
    [SerializeField] private GameObject confirmationLightObject;

    [Header("Optional snap")]
    [Tooltip("Quand le coquillage est relâché dans cette zone : on le verrouille au socle (même s'il est incorrect).")]
    [SerializeField] private bool snapOnCorrectWhenReleased = true;
    [SerializeField] private Transform snapPoint;

    [Header("Manager")]
    [SerializeField] private ShellPuzzleManager puzzleManager;

    public ShellColorId ZoneColorId => zoneColorId;
    public bool IsCorrectlyOccupied { get; private set; }
    public ShellProximityFeedback CurrentShell { get; private set; }

    // Verrouillage : une fois un coquillage “snap” sur ce socle, il reste collé jusqu'à ce qu'il soit re-attrapé.
    private ShellProximityFeedback _lockedShell;
    private bool _lockedShellOriginalKinematic;
    private bool _lockedShellOriginalDetectCollisions;
    private bool _lockedShellOriginalUseGravity;
    private RigidbodyConstraints _lockedShellOriginalConstraints;

    private Transform _lockReference;
    private Vector3 _lockedShellLocalPosOffset;
    private Quaternion _lockedShellLocalRotOffset;
    private XRGrabInteractable _lockedShellGrab;
    private bool _unlockedForDrop;
    private ShellProximityFeedback _ignoreSnapShellUntilExit;

    /// <summary>
    /// Compte les overlaps par coquillage (plusieurs colliders sur le même RB).
    /// </summary>
    private readonly Dictionary<ShellProximityFeedback, int> _shellOverlapRefCount = new Dictionary<ShellProximityFeedback, int>();

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (confirmationLightObject != null)
            confirmationLightObject.SetActive(false);
    }

    private void Update()
    {
        UpdateConfirmationLight();

        if (_lockedShell == null) return;

        // Tant que le coquillage est tenu, on ne force rien (on laisse XR le bouger).
        if (_lockedShell.IsHeld) return;

        // Tant que le coquillage reste “verrouillé”, on force sa pose au socle.
        ApplyLockedPose();
    }

    private void OnTriggerEnter(Collider other)
    {
        var shell = other.GetComponentInParent<ShellProximityFeedback>();
        if (shell == null) return;

        if (!_shellOverlapRefCount.TryGetValue(shell, out int n))
            n = 0;
        _shellOverlapRefCount[shell] = n + 1;

        // Si le socle contient déjà un coquillage verrouillé, on ignore les autres.
        if (_lockedShell != null && shell != _lockedShell) return;

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

        if (_shellOverlapRefCount.TryGetValue(shell, out int count))
        {
            count--;
            if (count <= 0)
                _shellOverlapRefCount.Remove(shell);
            else
                _shellOverlapRefCount[shell] = count;
        }

        // Le coquillage verrouillé reste collé même s'il sort du trigger.
        if (_lockedShell != null && shell == _lockedShell) return;

        if (shell != CurrentShell) return;

        if (_ignoreSnapShellUntilExit != null && shell == _ignoreSnapShellUntilExit)
            _ignoreSnapShellUntilExit = null;

        CurrentShell = null;
        SetCorrect(false);
    }

    private void UpdateState()
    {
        if (CurrentShell == null)
            return;

        bool correct = CurrentShell.ColorId == zoneColorId;
        SetCorrect(correct);

        // Une fois posé (relâché) dans la zone, le coquillage reste collé au socle (correct ou non).
        if (snapOnCorrectWhenReleased && !CurrentShell.IsHeld)
        {
            // Après un re-grab, on ignore le snap immédiat au moment du lâcher :
            // le coquillage est encore dans le trigger et sinon il re-snap instantanément.
            if (_ignoreSnapShellUntilExit != null && CurrentShell == _ignoreSnapShellUntilExit)
                return;

            SnapShell(CurrentShell);
        }
    }

    private void SetCorrect(bool correct)
    {
        if (IsCorrectlyOccupied == correct) return;
        IsCorrectlyOccupied = correct;

        if (puzzleManager != null)
            puzzleManager.NotifyZoneChanged();
    }

    /// <summary>
    /// Lumière : au moins un coquillage intersecte la zone et n'est pas en main (posé / au repos sur le socle).
    /// </summary>
    private void UpdateConfirmationLight()
    {
        if (confirmationLightObject == null) return;

        bool lit = false;
        foreach (var kv in _shellOverlapRefCount)
        {
            ShellProximityFeedback shell = kv.Key;
            if (shell == null || kv.Value <= 0) continue;
            if (!shell.IsHeld)
            {
                lit = true;
                break;
            }
        }

        confirmationLightObject.SetActive(lit);
    }

    private void SnapShell(ShellProximityFeedback shell)
    {
        if (shell == null) return;

        Transform reference = snapPoint != null ? snapPoint : transform;

        // Idempotent : si c'est déjà le coquillage verrouillé, on ne fait rien.
        if (_lockedShell == shell) return;

        _lockedShell = shell;
        _lockReference = reference;
        _unlockedForDrop = false;

        var rb = shell.GetComponent<Rigidbody>();
        if (rb != null)
        {
            _lockedShellOriginalKinematic = rb.isKinematic;
            _lockedShellOriginalDetectCollisions = rb.detectCollisions;
            _lockedShellOriginalUseGravity = rb.useGravity;
            _lockedShellOriginalConstraints = rb.constraints;

            // Évite les warnings : on ne touche pas aux velocities si le body est déjà kinematic.
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Lock via constraints (pas via isKinematic) pour ne pas casser l'état XRIT/gravity.
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.useGravity = false;
        }

        // Déverrouiller immédiatement quand l'utilisateur essaie de prendre le coquillage,
        // plutôt que dépendre d'un update frame (ordre des events XR).
        _lockedShellGrab = shell.GetComponent<XRGrabInteractable>();
        if (_lockedShellGrab != null)
        {
            // Toujours re-activer la saisie tant que le puzzle n'est pas résolu.
            if (puzzleManager == null || !puzzleManager.IsSolved)
                _lockedShellGrab.enabled = true;

            _lockedShellGrab.selectEntered.AddListener(OnLockedShellSelectEntered);
            _lockedShellGrab.selectExited.AddListener(OnLockedShellSelectExited);
        }

        // Snap uniquement en position : on évite de forcer la rotation (sinon ça peut “tourner” le mesh).
        shell.transform.position = reference.position;

        // On mémorise la pose relative au socle pour que ça suive quand le plateau bouge.
        _lockedShellLocalPosOffset = reference.InverseTransformPoint(shell.transform.position);
        _lockedShellLocalRotOffset = Quaternion.Inverse(reference.rotation) * shell.transform.rotation;
    }

    private void ApplyLockedPose()
    {
        if (_lockedShell == null) return;
        if (_lockReference == null) return;
        if (_lockedShell.IsHeld) return;

        _lockedShell.transform.position = _lockReference.TransformPoint(_lockedShellLocalPosOffset);
        _lockedShell.transform.rotation = _lockReference.rotation * _lockedShellLocalRotOffset;
    }

    private void ClearLockAndUnsubscribe()
    {
        if (_lockedShell == null) return;

        // Après un re-grab et un lâcher : on ignore le re-snap tant que le coquillage
        // n'a pas quitté le trigger (sinon il revient immédiatement sur le socle).
        _ignoreSnapShellUntilExit = _lockedShell;

        if (_lockedShellGrab != null)
        {
            _lockedShellGrab.selectEntered.RemoveListener(OnLockedShellSelectEntered);
            _lockedShellGrab.selectExited.RemoveListener(OnLockedShellSelectExited);
            _lockedShellGrab = null;
        }

        var rb = _lockedShell.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = _lockedShellOriginalUseGravity;
            rb.constraints = _lockedShellOriginalConstraints;
            rb.detectCollisions = _lockedShellOriginalDetectCollisions;
        }

        // On arrête de forcer la pose à la prochaine frame.
        _lockedShell = null;
        _lockReference = null;
    }

    private void OnLockedShellSelectEntered(SelectEnterEventArgs args)
    {
        if (_lockedShell == null) return;
        if (puzzleManager != null && puzzleManager.IsSolved) return; // on reste verrouillé si puzzle résolu

        _unlockedForDrop = true;

        // On restaure la physique dès qu'on commence à saisir.
        var rb = _lockedShell.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = _lockedShellOriginalUseGravity;
            rb.constraints = _lockedShellOriginalConstraints;
            rb.detectCollisions = _lockedShellOriginalDetectCollisions;
        }
    }

    private void OnLockedShellSelectExited(SelectExitEventArgs args)
    {
        if (_lockedShell == null) return;

        // Si le puzzle vient de se résoudre pendant que tu tiens le coquillage, on le re-lock.
        if (puzzleManager != null && puzzleManager.IsSolved)
        {
            var rb = _lockedShell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.useGravity = false;
            }
            ApplyLockedPose();
            return;
        }

        // Sinon : après lâcher, on arrête de re-snap et on laisse gravity faire.
        if (_unlockedForDrop)
        {
            // Sécurité : on remet la physique d'origine juste avant la fin du grab.
            var rb = _lockedShell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = _lockedShellOriginalUseGravity;
                rb.constraints = _lockedShellOriginalConstraints;
                rb.detectCollisions = _lockedShellOriginalDetectCollisions;
            }
            ClearLockAndUnsubscribe();
        }
    }
}

