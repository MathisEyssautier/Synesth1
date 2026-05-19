using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;

/// <summary>
/// Ignore les collisions entre les objets saisis (XRGrabInteractable) et le corps du joueur
/// (PlayerBlocker, capsule du rig, etc.) pour éviter d'être repoussé en arrière en marchant.
/// </summary>
public class GrabbedObjectPlayerCollisionIgnorer : MonoBehaviour
{
    [SerializeField] private XROrigin xrOrigin;
    [Tooltip("Racine du corps joueur. Par défaut = XR Origin (inclut PlayerBlocker).")]
    [SerializeField] private Transform playerBodyRoot;
    [SerializeField] private bool ignoreObjectTriggerColliders;

    private Collider[] _playerColliders;
    private readonly HashSet<XRGrabInteractable> _subscribedGrabs = new();
    private readonly Dictionary<XRGrabInteractable, List<(Collider obj, Collider player)>> _ignoredPairsByGrab = new();

    private void Awake()
    {
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (playerBodyRoot == null && xrOrigin != null)
            playerBodyRoot = xrOrigin.transform;

        CachePlayerColliders();
    }

    private void OnEnable()
    {
        DiscoverAndSubscribeGrabbables();
    }

    private void Start()
    {
        // Objets activés après ce composant (ordre d'initialisation).
        DiscoverAndSubscribeGrabbables();
    }

    private void OnDisable()
    {
        UnsubscribeAllGrabbables();
        RestoreAll();
    }

    private void DiscoverAndSubscribeGrabbables()
    {
        XRGrabInteractable[] grabbables = FindObjectsByType<XRGrabInteractable>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < grabbables.Length; i++)
            SubscribeGrabbable(grabbables[i]);
    }

    private void SubscribeGrabbable(XRGrabInteractable grab)
    {
        if (grab == null || !_subscribedGrabs.Add(grab))
            return;

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        if (grab.isSelected)
            ApplyIgnoreCollisions(grab);
    }

    private void UnsubscribeGrabbable(XRGrabInteractable grab)
    {
        if (grab == null || !_subscribedGrabs.Remove(grab))
            return;

        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
    }

    private void UnsubscribeAllGrabbables()
    {
        var grabs = new List<XRGrabInteractable>(_subscribedGrabs);
        for (int i = 0; i < grabs.Count; i++)
            UnsubscribeGrabbable(grabs[i]);
    }

    private void CachePlayerColliders()
    {
        if (playerBodyRoot == null)
        {
            _playerColliders = new Collider[0];
            return;
        }

        Collider[] all = playerBodyRoot.GetComponentsInChildren<Collider>(true);
        var list = new List<Collider>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Collider c = all[i];
            if (c == null || !c.enabled || c.isTrigger)
                continue;
            list.Add(c);
        }

        _playerColliders = list.ToArray();
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable grab)
            ApplyIgnoreCollisions(grab);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactableObject is XRGrabInteractable grab)
            RestoreGrab(grab);
    }

    private void ApplyIgnoreCollisions(XRGrabInteractable grab)
    {
        if (grab == null || _ignoredPairsByGrab.ContainsKey(grab))
            return;

        var pairs = new List<(Collider, Collider)>();
        Collider[] objectColliders = grab.GetComponentsInChildren<Collider>(true);

        for (int o = 0; o < objectColliders.Length; o++)
        {
            Collider objCol = objectColliders[o];
            if (objCol == null || !objCol.enabled)
                continue;
            if (objCol.isTrigger && !ignoreObjectTriggerColliders)
                continue;

            for (int p = 0; p < _playerColliders.Length; p++)
            {
                Collider playerCol = _playerColliders[p];
                if (playerCol == null)
                    continue;

                Physics.IgnoreCollision(objCol, playerCol, true);
                pairs.Add((objCol, playerCol));
            }
        }

        if (pairs.Count > 0)
            _ignoredPairsByGrab[grab] = pairs;
    }

    private void RestoreGrab(XRGrabInteractable grab)
    {
        if (!_ignoredPairsByGrab.TryGetValue(grab, out List<(Collider obj, Collider player)> pairs))
            return;

        for (int i = 0; i < pairs.Count; i++)
        {
            (Collider obj, Collider player) = pairs[i];
            if (obj != null && player != null)
                Physics.IgnoreCollision(obj, player, false);
        }

        _ignoredPairsByGrab.Remove(grab);
    }

    private void RestoreAll()
    {
        var grabs = new List<XRGrabInteractable>(_ignoredPairsByGrab.Keys);
        for (int i = 0; i < grabs.Count; i++)
            RestoreGrab(grabs[i]);
    }
}
