using UnityEngine;
using UnityEngine.Events;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System;
using System.Collections;
using System.Reflection;

public class ShellPuzzleManager : MonoBehaviour
{
    [Header("Zones (4)")]
    [SerializeField] private ShellPlacementZone[] zones;

    [Header("Black cube -> white on success")]
    [SerializeField] private Renderer blackCubeRenderer;
    [SerializeField] private Color solvedColor = Color.white;

    [Header("Reward")]
    [Tooltip("GameObject à activer quand tout est correct (ex: 2e fader)")]
    [SerializeField] private GameObject rewardObjectToActivate;
    [Tooltip("Si false: n'active pas le fader ici (activation via socket de dépôt).")]
    [SerializeField] private bool activateRewardObjectOnSolve = false;

    [Header("Reward feedback (sons + grabbable)")]
    [SerializeField] private EventReference rewardUnlockedOneShot;
    [SerializeField] private EventReference rewardContinuousLoop;
    [SerializeField] private Transform rewardAudioOrigin;
    [SerializeField] private bool makeRewardGrabbable = true;

    [Header("Cassette (son + grab)")]
    [Tooltip("Le lecteur cassette (le mesh indiqué par blackCubeRenderer) est en mode statique au début.")]
    [SerializeField] private bool disableCassetteGrabOnStart = true;
    [Tooltip("Quand le puzzle est résolu : on peut aussi désactiver le script HapticContactTrigger sur la cassette.")]
    [SerializeField] private bool disableCassetteHapticsOnSolve = true;

    [Header("State")]
    [SerializeField] private bool setRewardInactiveOnStart = true;

    [Header("Shells after solve")]
    [Tooltip("Quand le puzzle est résolu : désactive la saisie XR des coquillages (ils ne sont plus attrapables).")]
    [SerializeField] private bool disableShellGrabOnSolve = true;

    [Header("Narration")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private bool _solved = false;
    public bool IsSolved => _solved;

    private FmodAttachedEventCleanup _rewardLoopCleanup;

    private XRGrabInteractable _cassetteGrab;
    private Rigidbody _cassetteRb;
    private HapticContactTrigger _cassetteHaptics;
    private Transform _cassetteRoot;
    private bool _cassetteWasKinematic;
    private bool _cassetteWasUseGravity;
    private RigidbodyConstraints _cassetteWasConstraints;
    private Collider[] _cassetteColliders;

    [Header("Cassette (hiérarchie)")]
    [Tooltip("Root de la hiérarchie cassette à scanner pour trouver XRGrabInteractable + HapticContactTrigger. Laisse vide pour utiliser blackCubeRenderer.parent.")]
    [SerializeField] private Transform cassetteRootOverride;

    [Header("Cassette (spotlight)")]
    [Tooltip("Spotlight (GameObject avec Light ou parent) activée quand le lecteur cassette est débloqué.")]
    [SerializeField] private GameObject cassetteSpotlightOnSolve;

    private void OnDestroy()
    {
        // EventInstance CreateInstance : pas liée à la scène — arrêt explicite au déchargement / destruction.
        StopRewardLoopAudio();
    }

    private void Start()
    {
        if (setRewardInactiveOnStart && rewardObjectToActivate != null)
            rewardObjectToActivate.SetActive(false);

        if (cassetteSpotlightOnSolve != null)
            cassetteSpotlightOnSolve.SetActive(false);

        InitCassetteRefs();

        // Assure-toi que XRGrabInteractable sait quels colliders utiliser.
        EnsureXRGrabInteractableCollidersAssigned();

        if (disableCassetteGrabOnStart)
            SetCassetteGrabbersEnabled(false);

        if (disableCassetteGrabOnStart && _cassetteRb != null)
        {
            // Mettre le rigidbody en état "statique" évite qu'il bouge tant qu'il n'est pas grab.
            _cassetteWasKinematic = _cassetteRb.isKinematic;
            _cassetteWasUseGravity = _cassetteRb.useGravity;
            _cassetteWasConstraints = _cassetteRb.constraints;
            _cassetteRb.isKinematic = true;
            _cassetteRb.useGravity = false;

            // Tant qu'on veut juste du haptique : les colliders doivent rester des triggers.
            if (_cassetteColliders != null)
            {
                for (int i = 0; i < _cassetteColliders.Length; i++)
                {
                    var c = _cassetteColliders[i];
                    if (c == null) continue;
                    c.isTrigger = true;
                }
            }
        }

        NotifyZoneChanged();
    }

    private void InitCassetteRefs()
    {
        // On considère la cassette = l'objet parent du renderer fourni.
        if (blackCubeRenderer == null) return;

        _cassetteRoot = cassetteRootOverride != null
            ? cassetteRootOverride
            : (blackCubeRenderer.transform.parent != null ? blackCubeRenderer.transform.parent : blackCubeRenderer.transform);

        _cassetteGrab = _cassetteRoot.GetComponentInChildren<XRGrabInteractable>(true);
        _cassetteRb = _cassetteRoot.GetComponentInChildren<Rigidbody>(true);
        _cassetteHaptics = _cassetteRoot.GetComponentInChildren<HapticContactTrigger>(true);
        _cassetteColliders = _cassetteRoot.GetComponentsInChildren<Collider>(true);

        if (_cassetteRb != null)
        {
            _cassetteWasKinematic = _cassetteRb.isKinematic;
            _cassetteWasUseGravity = _cassetteRb.useGravity;
            _cassetteWasConstraints = _cassetteRb.constraints;
        }
    }

    public void NotifyZoneChanged()
    {
        if (_solved) return;
        if (zones == null || zones.Length == 0) return;

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] == null) return;
            if (!zones[i].IsCorrectlyOccupied) return;
        }

        Solve();
    }

    private void Solve()
    {
        _solved = true;

        // 1) Fader = le GO séparé (rewardObjectToActivate)
        if (activateRewardObjectOnSolve && rewardObjectToActivate != null)
            rewardObjectToActivate.SetActive(true);

        // 2) Matériau de la cassette (couleur du noir->blanc)
        if (blackCubeRenderer != null)
            blackCubeRenderer.material.color = solvedColor;

        if (cassetteSpotlightOnSolve != null)
            cassetteSpotlightOnSolve.SetActive(true);

        // 3) Audio de la cassette (one-shot puis loop)
        Transform origin = rewardAudioOrigin != null ? rewardAudioOrigin : (_cassetteRoot != null ? _cassetteRoot : transform);

        if (!rewardUnlockedOneShot.IsNull)
            RuntimeManager.PlayOneShot(rewardUnlockedOneShot, origin.position);

        if (!rewardContinuousLoop.IsNull)
        {
            GameObject attachGo = _cassetteRoot != null
                ? _cassetteRoot.gameObject
                : (rewardObjectToActivate != null ? rewardObjectToActivate : gameObject);

            EventInstance loopInst = RuntimeManager.CreateInstance(rewardContinuousLoop);
            RuntimeManager.AttachInstanceToGameObject(loopInst, attachGo);

            _rewardLoopCleanup = attachGo.GetComponent<FmodAttachedEventCleanup>();
            if (_rewardLoopCleanup == null)
                _rewardLoopCleanup = attachGo.AddComponent<FmodAttachedEventCleanup>();
            _rewardLoopCleanup.TakeOwnership(loopInst);

            loopInst.start();
        }

        // 4) Cassette devient grabable
        if (disableCassetteHapticsOnSolve && _cassetteHaptics != null)
            _cassetteHaptics.enabled = false;

        if (makeRewardGrabbable)
            SetCassetteGrabbersEnabled(true);

        // Revalide la liste de colliders côté XR, au cas où.
        EnsureXRGrabInteractableCollidersAssigned();

        // 5) Désactiver la saisie des coquillages une fois résolus
        if (disableShellGrabOnSolve && zones != null)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z == null) continue;
                if (z.CurrentShell == null) continue;

                var grab = z.CurrentShell.GetComponent<XRGrabInteractable>();
                if (grab != null)
                    grab.enabled = false;
            }
        }

        // Au solve : rendre la cassette réellement grabable (physique dynamique).
        if (_cassetteRb != null)
        {
            _cassetteRb.isKinematic = false;
            _cassetteRb.useGravity = true;
            _cassetteRb.constraints = RigidbodyConstraints.None;
        }

        if (_cassetteColliders != null)
        {
            for (int i = 0; i < _cassetteColliders.Length; i++)
            {
                var c = _cassetteColliders[i];
                if (c == null) continue;
                c.isTrigger = false;
            }
        }

        onPuzzleSolved?.Invoke();
    }

    private void EnsureXRGrabInteractableCollidersAssigned()
    {
        if (_cassetteRoot == null) return;

        var grabs = _cassetteRoot.GetComponentsInChildren<XRGrabInteractable>(true);
        if (grabs == null || grabs.Length == 0) return;

        var candidates = _cassetteRoot.GetComponentsInChildren<Collider>(true);
        if (candidates == null || candidates.Length == 0) return;

        for (int i = 0; i < grabs.Length; i++)
        {
            var grab = grabs[i];
            if (grab == null) continue;

            // XRGrabInteractable a une property publique "colliders" (List<Collider>).
            // On la remplit via reflection pour être robuste selon versions de XRIT.
            var prop = grab.GetType().GetProperty("colliders", BindingFlags.Instance | BindingFlags.Public);
            object value = null;

            if (prop != null)
                value = prop.GetValue(grab);
            else
            {
                // Fallback : parfois c'est un champ sérialisé.
                var field = grab.GetType().GetField("m_Colliders", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    value = field.GetValue(grab);
            }

            if (!(value is IList list)) continue;

            list.Clear();
            for (int c = 0; c < candidates.Length; c++)
            {
                var col = candidates[c];
                if (col == null) continue;
                list.Add(col);
            }
        }
    }

    private void SetCassetteGrabbersEnabled(bool enabled)
    {
        if (_cassetteRoot == null) return;

        // Children
        XRGrabInteractable[] inChildren = _cassetteRoot.GetComponentsInChildren<XRGrabInteractable>(true);
        for (int i = 0; i < inChildren.Length; i++)
        {
            if (inChildren[i] == null) continue;
            inChildren[i].enabled = enabled;
        }
    }

    private void OnDisable()
    {
        StopRewardLoopAudio();
    }

    public void StopRewardLoopAudio()
    {
        if (_rewardLoopCleanup == null) return;

        _rewardLoopCleanup.StopAndRelease();
        _rewardLoopCleanup = null;
    }
}

