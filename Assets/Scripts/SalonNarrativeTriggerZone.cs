using UnityEngine;

/// <summary>
/// À placer sur un GameObject avec Collider isTrigger (cuisine, bureau, etc.).
/// </summary>
public class SalonNarrativeTriggerZone : MonoBehaviour
{
    public enum ZoneType
    {
        Kitchen,
        Office
    }

    [SerializeField] private ZoneType zone;
    [SerializeField] private SalonExplorationNarrative narrative;
    [Tooltip("Tag du rig joueur (souvent sur XR Origin ou CharacterController). Laisser vide = tout collider.")]
    [SerializeField] private string requiredTag = "";

    private void OnTriggerEnter(Collider other)
    {
        if (narrative == null) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag))
            return;

        if (zone == ZoneType.Kitchen)
            narrative.NotifyKitchenEntered();
        else
            narrative.NotifyOfficeEntered();
    }
}
