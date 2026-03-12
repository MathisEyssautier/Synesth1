using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class BlockFarGrab : MonoBehaviour, IXRSelectFilter, IXRHoverFilter
{
    public bool canProcess => true;

    public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
    {
        return IsNearInteraction(interactor);
    }

    public bool Process(IXRHoverInteractor interactor, IXRHoverInteractable interactable)
    {
        return IsNearInteraction(interactor);
    }

    private bool IsNearInteraction(object interactor)
    {
        if (interactor is NearFarInteractor nearFar)
        {
            // Bloque si on est en Far, autorise Near et None
            return nearFar.selectionRegion.Value != NearFarInteractor.Region.Far;
        }
        return true; // Autorise tous les autres types d'interactors
    }
}