using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;

/// <summary>
/// À placer sur n'importe quel objet grabable (téléphone, billet, ticket de métro,
/// livre, carnet, badge, parapluie, clés, etc.) pour jouer une (ou plusieurs)
/// ligne(s) de narration FMOD via le <see cref="SubtitleManager"/>.
///
/// - Détecte automatiquement le <see cref="XRGrabInteractable"/> du même GameObject.
/// - Joue la narration au premier grab (par défaut) ou à chaque grab si <c>playOnce</c> est faux.
/// - Si plusieurs lignes sont fournies, elles sont enfilées dans l'ordre.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class NarrativeFirstGrabTrigger : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("Laisser vide : sera trouvé automatiquement dans la scène au premier grab si besoin.")]
    [SerializeField] private SubtitleManager subtitleManager;
    [Tooltip("Optionnel : XRGrabInteractable cible. Sinon utilise celui du même GameObject.")]
    [SerializeField] private XRGrabInteractable grabInteractable;

    [Header("Narration")]
    [Tooltip("Une ou plusieurs lignes de narration FMOD jouées dans l'ordre.")]
    [SerializeField] private EventReference[] narrationLines;

    [Header("Behaviour")]
    [Tooltip("Si true : la narration ne se déclenche qu'au premier grab puis le listener est retiré.")]
    [SerializeField] private bool playOnce = true;
    [Tooltip("Délai avant l'enfilement de la première ligne (en secondes). Utile pour laisser une autre VO finir.")]
    [SerializeField] private float delayBeforeFirstLine = 0f;

    private bool _alreadyTriggered;

    private void Awake()
    {
        if (grabInteractable == null)
            grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnGrabbed);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (playOnce && _alreadyTriggered) return;
        _alreadyTriggered = true;

        if (playOnce && grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);

        if (delayBeforeFirstLine > 0f)
            Invoke(nameof(EnqueueAllLines), delayBeforeFirstLine);
        else
            EnqueueAllLines();
    }

    private void EnqueueAllLines()
    {
        if (narrationLines == null || narrationLines.Length == 0)
            return;

        SubtitleManager mgr = ResolveSubtitleManager();
        if (mgr == null)
        {
            Debug.LogWarning($"[NarrativeFirstGrabTrigger] Aucun SubtitleManager trouvé pour {gameObject.name}.", this);
            return;
        }

        for (int i = 0; i < narrationLines.Length; i++)
        {
            var line = narrationLines[i];
            if (line.IsNull) continue;
            mgr.EnqueueSubtitledLine(line);
        }
    }

    private SubtitleManager ResolveSubtitleManager()
    {
        if (subtitleManager != null)
            return subtitleManager;

        subtitleManager = FindFirstObjectByType<SubtitleManager>();
        return subtitleManager;
    }
}
