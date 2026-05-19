using System;
using UnityEngine;

/// <summary>
/// À placer sur un GameObject avec Collider isTrigger (cuisine, bureau, etc.).
/// </summary>
public class SalonNarrativeTriggerZone : MonoBehaviour
{
    /// <summary>Déclenché quand le joueur entre dans une zone (cuisine ou bureau).</summary>
    public static event Action<ZoneType> PlayerEnteredZone;
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

        PlayerEnteredZone?.Invoke(zone);

        if (zone == ZoneType.Kitchen)
            narrative.NotifyKitchenEntered();
        else
            narrative.NotifyOfficeEntered();
    }
}
