using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Collider))]
public class DrawerItemParenting : MonoBehaviour
{
    [Header("Drawer refs")]
    [Tooltip("Parent du tiroir (souvent l'objet du tiroir qui bouge avec DrawerGrab).")]
    [SerializeField] private Transform drawerParent;
    [Tooltip("Zone trigger du tiroir (empty/collider) dans laquelle l'objet doit être pour être re-parenté au release.")]
    [SerializeField] private Transform drawerDropZoneRoot;

    [Header("Re-parent behavior")]
    [SerializeField] private bool detachOnGrab = true;
    [SerializeField] private bool reparentOnReleaseIfInsideZone = true;
    [SerializeField] private bool snapToAnchorOnReparent = false;
    [SerializeField] private Transform drawerAnchor;

    private XRGrabInteractable _grab;
    private int _insideZoneCount;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (!detachOnGrab) return;
        transform.SetParent(null, true);
    }

    private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        if (!reparentOnReleaseIfInsideZone) return;
        if (_insideZoneCount <= 0) return;
        if (drawerParent == null) return;

        transform.SetParent(drawerParent, true);
        if (snapToAnchorOnReparent && drawerAnchor != null)
            transform.SetPositionAndRotation(drawerAnchor.position, drawerAnchor.rotation);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (drawerDropZoneRoot == null || other == null) return;
        if (!other.transform.IsChildOf(drawerDropZoneRoot)) return;
        _insideZoneCount++;
    }

    private void OnTriggerExit(Collider other)
    {
        if (drawerDropZoneRoot == null || other == null) return;
        if (!other.transform.IsChildOf(drawerDropZoneRoot)) return;
        _insideZoneCount = Mathf.Max(0, _insideZoneCount - 1);
    }
}
