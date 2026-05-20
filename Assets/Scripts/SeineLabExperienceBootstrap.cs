using UnityEngine;

/// <summary>
/// Après la première touche piano en Seine Lab : résout guitare (porte cuisine via potards AA), sans narration guitare.
/// Peut vivre dans la scène avec des refs explicites ; sinon résolution automatique au runtime.
/// </summary>
public class SeineLabExperienceBootstrap : MonoBehaviour
{
    private static SeineLabExperienceBootstrap _instance;

    [Header("Guitare (optionnel — auto si vide)")]
    [SerializeField] private GuitarAssemblyManager guitarAssembly;
    [SerializeField] private GuitarCapoCrankController guitarCapo;
    [SerializeField] private UnlockPlacementSocket guitarPlacementSocket;

    [Header("Radio (optionnel)")]
    [SerializeField] private RadioManager radioManager;

    [Header("Narration (optionnel)")]
    [SerializeField] private SalonExplorationNarrative explorationNarrative;

    private void Awake()
    {
        if (!ExperienceProfile.IsSeineLab)
        {
            enabled = false;
            return;
        }

        _instance = this;
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    public static void OnPianoInstantSolved()
    {
        if (!ExperienceProfile.IsSeineLab)
            return;

        if (_instance != null)
            _instance.ApplyGuitarAndKitchenUnlock();
        else
            CreateRuntimeFallback().ApplyGuitarAndKitchenUnlock();
    }

    private static SeineLabExperienceBootstrap CreateRuntimeFallback()
    {
        var go = new GameObject(nameof(SeineLabExperienceBootstrap));
        var bootstrap = go.AddComponent<SeineLabExperienceBootstrap>();
        bootstrap.ResolveReferences();
        return bootstrap;
    }

    private void ResolveReferences()
    {
        if (guitarAssembly == null)
            guitarAssembly = FindFirstObjectByType<GuitarAssemblyManager>();
        if (guitarCapo == null)
            guitarCapo = FindFirstObjectByType<GuitarCapoCrankController>();
        if (radioManager == null)
            radioManager = FindFirstObjectByType<RadioManager>();
        if (explorationNarrative == null)
            explorationNarrative = FindFirstObjectByType<SalonExplorationNarrative>();
        if (guitarPlacementSocket == null)
            guitarPlacementSocket = FindGuitarPlacementSocket();
    }

    private static UnlockPlacementSocket FindGuitarPlacementSocket()
    {
        var sockets = FindObjectsByType<UnlockPlacementSocket>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sockets.Length; i++)
        {
            var s = sockets[i];
            if (s != null && s.IsGuitarSocket)
                return s;
        }

        return null;
    }

    private void ApplyGuitarAndKitchenUnlock()
    {
        explorationNarrative?.MarkSeineLabGuitarResolvedSilently();

        guitarAssembly?.ForceCompleteAllStringsSilently();
        guitarCapo?.TrySolveFromPrismPuzzle();
        guitarCapo?.ActivateRewardFader();
        guitarPlacementSocket?.ForcePlaceForSeineLab(activateFader: false, invokePlacedEvent: false);
    }
}
