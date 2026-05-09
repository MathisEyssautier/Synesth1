using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class GuitarSoundZone : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GuitarCapoCrankController capoCrankController;
    [SerializeField] private GuitarAssemblyManager guitarAssemblyManager;
    [SerializeField] private PrismFacetPuzzleController prismFacetPuzzleController;
    [Tooltip("Optionnel : pour déclencher la voix off « premier accord joué après les cordes posées ». Si non assigné, recherché automatiquement dans la scène.")]
    [SerializeField] private SalonExplorationNarrative salonExplorationNarrative;

    [Header("Trigger")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float triggerCooldown = 0.15f;

    [Header("FMOD Events")]
    [Tooltip("Son joué si les 6 cordes ne sont pas encore montées sur la guitare.")]
    [SerializeField] private EventReference wrongEvent;
    [Tooltip("Sons par cran de capo (index 0..4 -> crank1..crank5).")]
    [SerializeField] private EventReference[] crankEvents = new EventReference[5];

    [Header("Effets par cran de capo")]
    [Tooltip("5 GameObjects (ex. enfants avec particules / VFX) : index = position du capo (0..4). Un seul actif ; tous désactivés si cordes pas montées ou énigme guitare résolue.")]
    [SerializeField] private GameObject[] capoPositionEffectRoots = new GameObject[5];

    private float _nextAllowedTime = 0f;
    private int _capoParticlesShownIndex = int.MinValue;

    /// <summary>
    /// Instance FMOD de l’accord (ou du « wrong ») en cours — pour <see cref="FMODMeteringSource"/> / particules.
    /// </summary>
    public EventInstance EventInstance => _activeChordInstance;

    private EventInstance _activeChordInstance;

    private void OnDisable()
    {
        StopActiveChordInstance();
    }

    private void OnDestroy()
    {
        StopActiveChordInstance();
    }

    private void StopActiveChordInstance()
    {
        if (!_activeChordInstance.isValid())
            return;

        _activeChordInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _activeChordInstance.release();
        _activeChordInstance.clearHandle();
    }

    private void PlayChordEventAttached(EventReference evt)
    {
        StopActiveChordInstance();
        if (evt.IsNull || !RuntimeManager.IsInitialized)
            return;

        _activeChordInstance = RuntimeManager.CreateInstance(evt);
        RuntimeManager.AttachInstanceToGameObject(_activeChordInstance, gameObject);
        _activeChordInstance.start();
    }

    private void Update()
    {
        if (prismFacetPuzzleController == null)
            return;
        bool stringsReady = guitarAssemblyManager != null && guitarAssemblyManager.AreAllStringsPlaced;
        prismFacetPuzzleController.SetFacetAudioEnabled(stringsReady);

        SyncCapoPositionParticles(stringsReady);
    }

    private void SyncCapoPositionParticles(bool stringsReady)
    {
        if (capoPositionEffectRoots == null || capoPositionEffectRoots.Length == 0)
            return;
        if (capoCrankController == null)
            return;

        if (!stringsReady || capoCrankController.IsSolved)
        {
            if (_capoParticlesShownIndex != int.MinValue)
            {
                ApplyCapoEffectActiveIndex(-1);
                _capoParticlesShownIndex = int.MinValue;
            }
            return;
        }

        int idx = Mathf.Clamp(capoCrankController.CurrentCrankIndex, 0, capoPositionEffectRoots.Length - 1);
        if (idx == _capoParticlesShownIndex)
            return;

        ApplyCapoEffectActiveIndex(idx);
        _capoParticlesShownIndex = idx;
    }

    private void ApplyCapoEffectActiveIndex(int activeIndex)
    {
        for (int i = 0; i < capoPositionEffectRoots.Length; i++)
        {
            var root = capoPositionEffectRoots[i];
            if (root == null)
                continue;

            bool on = activeIndex >= 0 && i == activeIndex;
            if (root.activeSelf != on)
                root.SetActive(on);
        }
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        if (prismFacetPuzzleController != null && capoCrankController != null)
            prismFacetPuzzleController.SetGuitarCapoForSolve(capoCrankController);

        ApplyCapoEffectActiveIndex(-1);
        _capoParticlesShownIndex = int.MinValue;
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
            PlayChordEventAttached(wrongEvent);
            return;
        }

        int crankIndex = 0;
        if (capoCrankController != null)
            crankIndex = Mathf.Clamp(capoCrankController.CurrentCrankIndex, 0, crankEvents.Length - 1);

        if (crankEvents != null && crankIndex >= 0 && crankIndex < crankEvents.Length)
        {
            var evt = crankEvents[crankIndex];
            PlayChordEventAttached(evt);
        }

        if (salonExplorationNarrative == null)
        {
#if UNITY_2023_1_OR_NEWER
            salonExplorationNarrative = FindFirstObjectByType<SalonExplorationNarrative>(FindObjectsInactive.Include);
#else
            salonExplorationNarrative = FindObjectOfType<SalonExplorationNarrative>(true);
#endif
        }

        if (salonExplorationNarrative != null)
            salonExplorationNarrative.NotifyFirstGuitarChordPlayed();

        // Nouveau puzzle: l'accord joué fait avancer la facette correspondant au cran capot.
        if (prismFacetPuzzleController != null)
        {
            prismFacetPuzzleController.PlayChordMaterialFlashForCapoIndex(crankIndex);
            prismFacetPuzzleController.AdvanceFacetFromCapoIndex(crankIndex);
        }
    }
}

