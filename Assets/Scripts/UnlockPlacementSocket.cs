using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class UnlockPlacementSocket : MonoBehaviour
{
    [Header("Expected object")]
    [Tooltip("Root de l'objet à déposer (ex: RECORDER ou GUITAR).")]
    [SerializeField] private Transform expectedObjectRoot;
    [Tooltip("Anchor optionnel sur l'objet (ex: GuitarBody) à aligner sur le snap point, pour éviter les problèmes de pivot root.")]
    [SerializeField] private Transform expectedObjectAnchor;
    [SerializeField] private Transform snapPoint;

    [Header("Unlock requirement (optional)")]
    [SerializeField] private ShellPuzzleManager requiredShellPuzzle;
    [SerializeField] private GuitarCapoCrankController requiredGuitarPuzzle;
    [Tooltip("VFX à activer quand la socket est débloquée, puis à couper quand l'objet est posé (ex: Visual Effect K7 / Visual Effect Guitar).")]
    [SerializeField] private GameObject unlockReadyVisualEffect;

    [Header("On placed")]
    [Tooltip("Fader/objet à activer quand le bon objet est déposé.")]
    [SerializeField] private GameObject faderToActivate;
    [Tooltip("Évite la pose instantanée pendant qu'on tient encore l'objet. Le dépôt se valide quand relâché dans la socket.")]
    [SerializeField] private bool requireObjectReleased = true;
    [SerializeField] private bool disableGrabAfterPlaced = true;
    [SerializeField] private bool lockRigidbodyKinematic = true;
    [Tooltip("Rend l'objet traversable après placement (désactive ses colliders).")]
    [SerializeField] private bool disableObjectCollidersOnPlaced = true;
    [Tooltip("Désactive ces comportements sur l'objet (ex: GuitarSoundZone) après placement.")]
    [SerializeField] private Behaviour[] behavioursToDisableOnPlaced;
    [Tooltip("Optionnel : coupe la boucle audio cassette gérée par ShellPuzzleManager.")]
    [SerializeField] private ShellPuzzleManager shellPuzzleAudioToStop;
    [Tooltip("Optionnel : renderer à recolorer quand l'objet est posé (ex: RECORDER).")]
    [SerializeField] private Renderer placedObjectRenderer;
    [SerializeField] private Color placedObjectColor = Color.yellow;
    [SerializeField] private bool hideSocketOnPlaced = true;
    [Tooltip("Optionnel: visuel à cacher (mesh jaune). Si vide et hideSocketOnPlaced=true, on masque ce GameObject.")]
    [SerializeField] private GameObject socketVisualToHide;

    [Header("Après placement (ex. assiette → zone coquillage)")]
    [Tooltip("Activés en dernier (ex. CylinderSeaBlue + canvas symbole). Leurs colliders sont réactivés même si l’option ci-dessous a désactivé ceux du root.")]
    [SerializeField] private GameObject[] activateAfterPlaced;

    [Header("Narration")]
    [SerializeField] private UnityEvent onObjectPlaced;

    private bool _filled;
    private bool _unlockReadyVisualWasActive;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (snapPoint == null) snapPoint = transform;

        UpdateUnlockReadyVisual(force: true);
    }

    private void Update()
    {
        if (_filled)
        {
            UpdateUnlockReadyVisual(force: false);
            return;
        }

        UpdateUnlockReadyVisual(force: false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_filled) return;
        if (expectedObjectRoot == null || other == null) return;
        if (!other.transform.IsChildOf(expectedObjectRoot)) return;
        if (!IsRequirementMet()) return;
        if (requireObjectReleased && IsExpectedObjectHeld()) return;

        PlaceExpectedObject();
    }

    private void OnTriggerStay(Collider other)
    {
        if (_filled) return;
        if (expectedObjectRoot == null || other == null) return;
        if (!other.transform.IsChildOf(expectedObjectRoot)) return;
        if (!IsRequirementMet()) return;
        if (requireObjectReleased && IsExpectedObjectHeld()) return;

        PlaceExpectedObject();
    }

    private bool IsRequirementMet()
    {
        if (requiredShellPuzzle != null)
        {
            if (!requiredShellPuzzle.IsSolved) return false;
        }

        if (requiredGuitarPuzzle != null)
        {
            if (!requiredGuitarPuzzle.IsSolved) return false;
        }

        return true;
    }

    private void PlaceExpectedObject()
    {
        _filled = true;
        UpdateUnlockReadyVisual(force: true);

        if (expectedObjectRoot != null && snapPoint != null)
        {
            if (expectedObjectAnchor != null && expectedObjectAnchor.IsChildOf(expectedObjectRoot))
            {
                // Aligne l'anchor de l'objet sur le snapPoint (plus robuste que le pivot root).
                Quaternion rootRot = snapPoint.rotation * Quaternion.Inverse(expectedObjectAnchor.localRotation);
                Vector3 rootPos = snapPoint.position - (rootRot * expectedObjectAnchor.localPosition);
                expectedObjectRoot.SetPositionAndRotation(rootPos, rootRot);
            }
            else
            {
                // Fallback: alignement pivot root.
                expectedObjectRoot.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
            }
        }

        if (lockRigidbodyKinematic && expectedObjectRoot != null)
        {
            // IMPORTANT: figer tous les rigidbodies de la hiérarchie, pas seulement le premier.
            var rbs = expectedObjectRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbs.Length; i++)
            {
                var rb = rbs[i];
                if (rb == null) continue;

                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.detectCollisions = !disableObjectCollidersOnPlaced;
            }
        }

        if (disableObjectCollidersOnPlaced && expectedObjectRoot != null)
        {
            var cols = expectedObjectRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] == null) continue;
                cols[i].enabled = false;
            }
        }

        if (disableGrabAfterPlaced && expectedObjectRoot != null)
        {
            var grabs = expectedObjectRoot.GetComponentsInChildren<XRGrabInteractable>(true);
            for (int i = 0; i < grabs.Length; i++)
            {
                if (grabs[i] == null) continue;
                grabs[i].enabled = false;
            }
        }

        if (behavioursToDisableOnPlaced != null)
        {
            for (int i = 0; i < behavioursToDisableOnPlaced.Length; i++)
            {
                if (behavioursToDisableOnPlaced[i] == null) continue;
                behavioursToDisableOnPlaced[i].enabled = false;
            }
        }

        if (shellPuzzleAudioToStop != null)
            shellPuzzleAudioToStop.StopRewardLoopAudio();

        if (placedObjectRenderer != null)
        {
            var mat = placedObjectRenderer.material;
            if (mat != null)
            {
                if (mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", placedObjectColor);
                else
                    mat.color = placedObjectColor;

                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", placedObjectColor);
            }
        }

        if (faderToActivate != null)
            faderToActivate.SetActive(true);

        if (hideSocketOnPlaced)
        {
            // Désactive le trigger pour éviter toute ré-interaction.
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            if (socketVisualToHide != null)
                socketVisualToHide.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        if (activateAfterPlaced != null)
        {
            for (int i = 0; i < activateAfterPlaced.Length; i++)
            {
                var go = activateAfterPlaced[i];
                if (go == null) continue;
                go.SetActive(true);
                var cols = go.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++)
                {
                    if (cols[c] != null)
                        cols[c].enabled = true;
                }
            }
        }

        // Les triggers sous le root (ex. ShellPlacementZone sur CylinderSeaBlue) sont pilotés par le
        // Rigidbody parent : si detectCollisions a été mis à false avec disableObjectCollidersOnPlaced,
        // réactiver les colliders ne suffit pas — aucun OnTriggerEnter / Stay.
        if (activateAfterPlaced != null && activateAfterPlaced.Length > 0 && expectedObjectRoot != null && lockRigidbodyKinematic)
        {
            var rbsRestore = expectedObjectRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rbsRestore.Length; i++)
            {
                if (rbsRestore[i] == null) continue;
                rbsRestore[i].detectCollisions = true;
            }
        }

        onObjectPlaced?.Invoke();
    }

    private void UpdateUnlockReadyVisual(bool force)
    {
        if (unlockReadyVisualEffect == null)
            return;

        bool shouldBeActive = !_filled && IsRequirementMet();
        if (!force && shouldBeActive == _unlockReadyVisualWasActive)
            return;

        unlockReadyVisualEffect.SetActive(shouldBeActive);
        _unlockReadyVisualWasActive = shouldBeActive;
    }

    private bool IsExpectedObjectHeld()
    {
        if (expectedObjectRoot == null) return false;
        var grabs = expectedObjectRoot.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < grabs.Length; i++)
        {
            var g = grabs[i];
            if (g == null) continue;
            if (g.isSelected) return true;
        }
        return false;
    }
}

