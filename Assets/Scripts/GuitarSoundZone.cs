using UnityEngine;
using FMODUnity;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class GuitarSoundZone : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GuitarCapoCrankController capoCrankController;
    [SerializeField] private GuitarAssemblyManager guitarAssemblyManager;
    [SerializeField] private PrismFacetPuzzleController prismFacetPuzzleController;

    [Header("Trigger")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float triggerCooldown = 0.15f;

    [Header("FMOD Events")]
    [Tooltip("Son joué si les 6 cordes ne sont pas encore montées sur la guitare.")]
    [SerializeField] private EventReference wrongEvent;
    [Tooltip("Sons par cran de capo (index 0..4 -> crank1..crank5).")]
    [SerializeField] private EventReference[] crankEvents = new EventReference[5];

    private float _nextAllowedTime = 0f;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (prismFacetPuzzleController != null && capoCrankController != null)
            prismFacetPuzzleController.SetGuitarCapoForSolve(capoCrankController);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextAllowedTime) return;
        _nextAllowedTime = Time.time + triggerCooldown;

        if (capoCrankController != null && capoCrankController.IsSolved)
            return;

        bool stringsReady = guitarAssemblyManager != null && guitarAssemblyManager.AreAllStringsPlaced;
        if (!stringsReady)
        {
            if (!wrongEvent.IsNull)
                RuntimeManager.PlayOneShotAttached(wrongEvent, gameObject);
            return;
        }

        int crankIndex = 0;
        if (capoCrankController != null)
            crankIndex = Mathf.Clamp(capoCrankController.CurrentCrankIndex, 0, crankEvents.Length - 1);

        if (crankEvents != null && crankIndex >= 0 && crankIndex < crankEvents.Length)
        {
            var evt = crankEvents[crankIndex];
            if (!evt.IsNull)
                RuntimeManager.PlayOneShotAttached(evt, gameObject);
        }

        // Nouveau puzzle: l'accord joué fait avancer la facette correspondant au cran capot.
        if (prismFacetPuzzleController != null)
        {
            prismFacetPuzzleController.PlayChordMaterialFlashForCapoIndex(crankIndex);
            prismFacetPuzzleController.AdvanceFacetFromCapoIndex(crankIndex);
        }
    }
}

