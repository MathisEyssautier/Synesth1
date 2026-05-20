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
    [Tooltip("Déblocage dépôt + arrêt de la boucle FMOD post-prisme sur la guitare (voir GuitarCapoCrankController).")]
    [SerializeField] private GuitarCapoCrankController requiredGuitarPuzzle;
    [Tooltip("Si un prérequis puzzle est défini : ignore un objet déjà dans le trigger au moment du déblocage (ex. lecteur cassette qui reçoit des colliders solides quand les coquillages sont validés). Il faut sortir puis reposer.")]
    [SerializeField] private bool requireFreshOverlapAfterUnlock = true;
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
    [Tooltip("Auto-stoppe tous les events FMOD émis depuis l'objet posé (accord guitare en cours, boucle post-prisme). Filet de sécurité même si les références ci-dessus ne sont pas câblées.")]
    [SerializeField] private bool autoStopGuitarFmodOnPlaced = true;
    [Tooltip("Optionnel : renderer à recolorer au dépôt. Si c'est le même que la cible du bool Success (body shader), la teinte est ignorée pour ne pas écraser le rendu du shader.")]
    [SerializeField] private Renderer placedObjectRenderer;
    [SerializeField] private Color placedObjectColor = Color.yellow;

    [Header("Shader Graph — bool Success au dépôt")]
    [Tooltip("Après pose : met Success=1 sur les materials du renderer (fige la variation du shader). Vide = enfant « body » sous expectedObjectRoot.")]
    [SerializeField] private Renderer rendererForShaderSuccessOnPlaced;
    [SerializeField] private bool applyShaderSuccessWhenPlaced = true;

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
    private bool _placementArmed = true;
    private bool _requirementMetPreviousFrame;

    /// <summary>Socket guitare : prérequis capo, pas de puzzle coquillages.</summary>
    public bool IsGuitarSocket => requiredGuitarPuzzle != null && requiredShellPuzzle == null;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        if (snapPoint == null) snapPoint = transform;

        _requirementMetPreviousFrame = IsRequirementMet();
        RefreshPlacementArmingOnRequirementEdge();

        UpdateUnlockReadyVisual(force: true);
    }

    private void Update()
    {
        if (_filled)
        {
            UpdateUnlockReadyVisual(force: false);
            return;
        }

        bool reqMet = IsRequirementMet();
        if (reqMet != _requirementMetPreviousFrame)
            RefreshPlacementArmingOnRequirementEdge();
        _requirementMetPreviousFrame = reqMet;

        UpdateUnlockReadyVisual(force: false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_filled) return;
        if (expectedObjectRoot == null || other == null) return;
        if (!other.transform.IsChildOf(expectedObjectRoot)) return;
        if (!IsRequirementMet()) return;
        if (!CanAcceptPlacement()) return;
        if (requireObjectReleased && IsExpectedObjectHeld()) return;

        PlaceExpectedObject();
    }

    private void OnTriggerStay(Collider other)
    {
        if (_filled) return;
        if (expectedObjectRoot == null || other == null) return;
        if (!other.transform.IsChildOf(expectedObjectRoot)) return;
        if (!IsRequirementMet()) return;
        if (!CanAcceptPlacement()) return;
        if (requireObjectReleased && IsExpectedObjectHeld()) return;

        PlaceExpectedObject();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_filled) return;
        if (expectedObjectRoot == null || other == null) return;
        if (!other.transform.IsChildOf(expectedObjectRoot)) return;
        if (!IsRequirementMet()) return;

        // L'objet a quitté la zone après déblocage : on autorise un vrai dépôt au prochain passage.
        _placementArmed = true;
    }

    private bool HasUnlockRequirement()
    {
        return requiredShellPuzzle != null || requiredGuitarPuzzle != null;
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

    private bool CanAcceptPlacement()
    {
        if (!requireFreshOverlapAfterUnlock || !HasUnlockRequirement())
            return true;
        if (!IsRequirementMet())
            return false;
        return _placementArmed;
    }

    /// <summary>
    /// Quand le puzzle vient de se résoudre : si l'objet est déjà dans le trigger, on attend qu'il sorte
    /// (évite un « faux dépôt » quand les colliders passent de trigger à solide au Solve).
    /// </summary>
    private void RefreshPlacementArmingOnRequirementEdge()
    {
        if (!requireFreshOverlapAfterUnlock || !HasUnlockRequirement())
        {
            _placementArmed = true;
            return;
        }

        if (!IsRequirementMet())
        {
            _placementArmed = false;
            return;
        }

        _placementArmed = !IsExpectedObjectOverlapping();
    }

    private bool IsExpectedObjectOverlapping()
    {
        if (expectedObjectRoot == null) return false;

        var socketCol = GetComponent<Collider>();
        if (socketCol == null || !socketCol.enabled) return false;

        var objectCols = expectedObjectRoot.GetComponentsInChildren<Collider>(true);
        Bounds socketBounds = socketCol.bounds;

        for (int i = 0; i < objectCols.Length; i++)
        {
            var c = objectCols[i];
            if (c == null || !c.enabled) continue;
            if (socketBounds.Intersects(c.bounds))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Seine Lab : pose la guitare sur le support sans activer le fader guitare ni la narration.
    /// </summary>
    public void ForcePlaceForSeineLab(bool activateFader, bool invokePlacedEvent)
    {
        if (_filled || expectedObjectRoot == null)
            return;

        _placementArmed = true;
        PlaceExpectedObject(activateFader, invokePlacedEvent);
    }

    private void PlaceExpectedObject()
    {
        PlaceExpectedObject(activateFader: true, invokePlacedEvent: true);
    }

    private void PlaceExpectedObject(bool activateFader, bool invokePlacedEvent)
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

        if (requiredGuitarPuzzle != null)
            requiredGuitarPuzzle.StopPostPrismCompletionLoop();

        if (autoStopGuitarFmodOnPlaced && expectedObjectRoot != null)
        {
            var soundZones = expectedObjectRoot.GetComponentsInChildren<GuitarSoundZone>(true);
            for (int i = 0; i < soundZones.Length; i++)
            {
                if (soundZones[i] == null) continue;
                soundZones[i].StopActiveAudio();
            }

            var capoControllers = expectedObjectRoot.GetComponentsInChildren<GuitarCapoCrankController>(true);
            for (int i = 0; i < capoControllers.Length; i++)
            {
                if (capoControllers[i] == null) continue;
                capoControllers[i].StopPostPrismCompletionLoop();
            }
        }

        Renderer shaderSuccessTarget = applyShaderSuccessWhenPlaced ? ResolveRendererForShaderSuccess() : null;
        bool skipLegacyPlacedColor = shaderSuccessTarget != null && placedObjectRenderer == shaderSuccessTarget;

        if (placedObjectRenderer != null && !skipLegacyPlacedColor)
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

        if (applyShaderSuccessWhenPlaced)
            ApplyShaderSuccessOnPlacedObject();

        if (activateFader && faderToActivate != null)
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

        if (invokePlacedEvent)
            onObjectPlaced?.Invoke();
    }

    private void ApplyShaderSuccessOnPlacedObject()
    {
        Renderer r = ResolveRendererForShaderSuccess();
        ShaderGraphSuccessUtility.SetSuccessOnRendererMaterials(r, true);
    }

    private Renderer ResolveRendererForShaderSuccess()
    {
        Renderer r = rendererForShaderSuccessOnPlaced;
        if (r == null && expectedObjectRoot != null)
        {
            Transform body = expectedObjectRoot.Find("body");
            if (body == null)
            {
                var trs = expectedObjectRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < trs.Length; i++)
                {
                    if (trs[i] != null && trs[i].name == "body")
                    {
                        body = trs[i];
                        break;
                    }
                }
            }

            if (body != null)
                r = body.GetComponent<Renderer>();
        }

        return r;
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

