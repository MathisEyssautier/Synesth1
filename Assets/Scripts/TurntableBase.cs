using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TurntableBase : MonoBehaviour
{
    [Header("Snap")]
    [SerializeField] private Transform discSnapPoint;

    [Header("Filter")]
    [SerializeField] private string discTag = "";

    public TurntableDisc CurrentDisc { get; private set; }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (discSnapPoint == null)
            discSnapPoint = transform;
    }

    private void OnTriggerEnter(Collider other)
    {
        var disc = other.GetComponentInParent<TurntableDisc>();
        if (disc == null) return;
        if (!string.IsNullOrEmpty(discTag) && !disc.CompareTag(discTag)) return;
        if (CurrentDisc != null && CurrentDisc != disc) return; // Un seul disque à la fois sur la platine.

        SetCurrentDisc(disc);
    }

    private void OnTriggerStay(Collider other)
    {
        // Intentionnellement vide.
        // Le re-snap continu empêchait la rotation visuelle du disque en mode play.
    }

    private void OnTriggerExit(Collider other)
    {
        var disc = other.GetComponentInParent<TurntableDisc>();
        if (disc == null) return;
        if (disc != CurrentDisc) return;
        if (disc.IsPlaying) return;

        CurrentDisc = null;
    }

    public void NotifyDiscGrabbed(TurntableDisc disc)
    {
        if (disc == CurrentDisc)
            CurrentDisc = null;
    }

    public void ToggleCurrentDiscPlayPause()
    {
        if (CurrentDisc == null) return;
        CurrentDisc.TogglePlayPause();
    }

    private void SetCurrentDisc(TurntableDisc disc)
    {
        if (disc == null) return;
        CurrentDisc = disc;
        disc.SnapToBase(this, discSnapPoint);
    }
}

