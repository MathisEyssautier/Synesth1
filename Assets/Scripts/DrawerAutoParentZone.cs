using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class DrawerAutoParentZone : MonoBehaviour
{
    private class TrackedItem
    {
        public Transform root;
        public XRGrabInteractable grab;
        public Transform originalParent;
        public int overlapCount;
    }

    [Header("Parent target")]
    [SerializeField] private Transform drawerParent;

    [Header("Filter")]
    [SerializeField] private LayerMask allowedLayers = ~0;
    [SerializeField] private string ignoreTag = "PlayerHand";
    [SerializeField] private bool requireGrabInteractable = true;

    [Header("Behavior")]
    [SerializeField] private bool onlyWhenNotHeld = true;
    [SerializeField] private bool restoreOriginalParentOnExit = true;

    private readonly Dictionary<Transform, TrackedItem> _items = new Dictionary<Transform, TrackedItem>();

    private void Awake()
    {
        var c = GetComponent<Collider>();
        c.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Transform root = ResolveRoot(other);
        if (root == null) return;

        TrackedItem item = GetOrCreateItem(root);
        item.overlapCount++;

        if (item.overlapCount > 0 && CanParentNow(item))
            ParentToDrawer(item);
    }

    private void OnTriggerExit(Collider other)
    {
        Transform root = ResolveRoot(other);
        if (root == null) return;
        if (!_items.TryGetValue(root, out TrackedItem item)) return;

        item.overlapCount = Mathf.Max(0, item.overlapCount - 1);
        if (item.overlapCount == 0 && restoreOriginalParentOnExit)
            RestoreOriginalParent(item);
    }

    private TrackedItem GetOrCreateItem(Transform root)
    {
        if (_items.TryGetValue(root, out TrackedItem existing))
            return existing;

        var item = new TrackedItem
        {
            root = root,
            grab = root.GetComponentInChildren<XRGrabInteractable>(true),
            originalParent = root.parent,
            overlapCount = 0
        };

        if (item.grab != null)
        {
            item.grab.selectEntered.AddListener(OnAnyItemGrabbed);
            item.grab.selectExited.AddListener(OnAnyItemReleased);
        }

        _items.Add(root, item);
        return item;
    }

    private void OnAnyItemGrabbed(SelectEnterEventArgs args)
    {
        var grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;

        TrackedItem item = FindByGrab(grab);
        if (item == null) return;

        // While held, never remain parented to drawer.
        if (item.root != null && item.root.parent == drawerParent)
            item.root.SetParent(null, true);
    }

    private void OnAnyItemReleased(SelectExitEventArgs args)
    {
        var grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;

        TrackedItem item = FindByGrab(grab);
        if (item == null) return;

        // Re-parent ONLY if currently overlapping zone.
        if (item.overlapCount > 0 && CanParentNow(item))
            ParentToDrawer(item);
        else if (restoreOriginalParentOnExit)
            RestoreOriginalParent(item);
    }

    private TrackedItem FindByGrab(XRGrabInteractable grab)
    {
        foreach (var kv in _items)
        {
            if (kv.Value.grab == grab)
                return kv.Value;
        }
        return null;
    }

    private bool CanParentNow(TrackedItem item)
    {
        if (item == null || item.root == null || drawerParent == null) return false;
        if (!onlyWhenNotHeld) return true;
        return item.grab == null || !item.grab.isSelected;
    }

    private void ParentToDrawer(TrackedItem item)
    {
        if (item.root.parent != drawerParent)
            item.root.SetParent(drawerParent, true);
    }

    private void RestoreOriginalParent(TrackedItem item)
    {
        if (item == null || item.root == null) return;
        if (item.root.parent != drawerParent) return;
        item.root.SetParent(item.originalParent, true);
    }

    private Transform ResolveRoot(Collider other)
    {
        if (other == null) return null;
        if (!string.IsNullOrEmpty(ignoreTag) && other.CompareTag(ignoreTag)) return null;
        if (((1 << other.gameObject.layer) & allowedLayers.value) == 0) return null;
        if (drawerParent != null && other.transform.IsChildOf(drawerParent)) return null;
        if (other.transform.IsChildOf(transform)) return null;

        XRGrabInteractable grab = other.GetComponentInParent<XRGrabInteractable>();
        if (requireGrabInteractable && grab == null)
            return null;

        if (grab != null)
            return grab.transform;

        Rigidbody rb = other.attachedRigidbody;
        return rb != null ? rb.transform : other.transform;
    }

    private void OnDisable()
    {
        foreach (var kv in _items)
        {
            var item = kv.Value;
            if (item == null || item.grab == null) continue;
            item.grab.selectEntered.RemoveListener(OnAnyItemGrabbed);
            item.grab.selectExited.RemoveListener(OnAnyItemReleased);
        }
    }
}
