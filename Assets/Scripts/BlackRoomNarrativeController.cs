using System.Collections;
using FMODUnity;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Séquence d'intro narrative de <c>SynesthesiaBlackRoom</c> : voix off + sous-titres (FMOD + marqueurs)
/// puis son du téléphone dans le tiroir, puis dialogue jusqu'à la phase d'exploration libre.
/// </summary>
public class BlackRoomNarrativeController : MonoBehaviour
{
    private enum LineId
    {
        IntroNarrateur = 0,
        NarrateurTelephone,
        NayaAlarme,
        NarrateurAucunSoucis,
        NayaParentsAnxiete,
        NarrateurDaccordJeVois,
        NarrateurObjetsTable,
        NayaOkJeMyMets,
        Count
    }

    [Header("Voice / subtitles")]
    [SerializeField] private SubtitleManager subtitleManager;
    [Tooltip("Délai avant la première réplique.")]
    [SerializeField] private float delayBeforeFirstLineSeconds = 0.5f;

    [Header("Lignes (events FMOD DARKROOMEVENTS — un event par phrase)")]
    [Tooltip("DRNayaTransition — Naya, Naya, tu m'entends ?…")]
    [SerializeField] private EventReference voIntroNarrateur;
    [Tooltip("DRAhTelephone — Oh, c'est ton téléphone qui sonne ?")]
    [SerializeField] private EventReference voNarrateurTelephone;
    [Tooltip("AhCestUneAlarme")]
    [SerializeField] private EventReference voNayaAlarme;
    [Tooltip("DRAucunSoucis")]
    [SerializeField] private EventReference voNarrateurAucunSoucis;
    [Tooltip("AlorsCommentDireExplications")]
    [SerializeField] private EventReference voNayaParentsAnxiete;
    [Tooltip("DRDaccordJeVois")]
    [SerializeField] private EventReference voNarrateurDaccordJeVois;
    [Tooltip("DRTuReconnais — observe / manipule / porte au fond")]
    [SerializeField] private EventReference voNarrateurObjetsTable;
    [Tooltip("OkJeMyMets")]
    [SerializeField] private EventReference voNayaOkJeMyMets;

    [Header("Téléphone (tiroir bureau)")]
    [Tooltip("Root du téléphone (RingingPhone). Désactivé au démarrage, activé après l'intro.")]
    [SerializeField] private GameObject phoneInDrawerRoot;

    [Header("Locomotion (optionnel)")]
    [SerializeField] private bool lockLocomotionUntilDialogueEnds = true;
    [SerializeField] private LocomotionManager locomotionManager;

    [Header("Fin intro")]
    [SerializeField] private UnityEvent onExplorationPhaseStarted;

    private int _currentLine = -1;
    private bool _dialogueComplete;
    private Coroutine _dialogueRoutine;

    public bool IsDialogueComplete => _dialogueComplete;

    private void Awake()
    {
        if (phoneInDrawerRoot != null)
            phoneInDrawerRoot.SetActive(false);

        if (lockLocomotionUntilDialogueEnds && locomotionManager != null)
            locomotionManager.SetForceDisabled(true);
    }

    private void OnDisable()
    {
        if (_dialogueRoutine != null)
        {
            StopCoroutine(_dialogueRoutine);
            _dialogueRoutine = null;
        }
    }

    private void Start()
    {
        if (subtitleManager == null)
            subtitleManager = FindFirstObjectByType<SubtitleManager>();

        _dialogueRoutine = StartCoroutine(RunDialogueSequence());
    }

    private IEnumerator RunDialogueSequence()
    {
        if (subtitleManager == null)
        {
            Debug.LogError("[BlackRoomNarrative] SubtitleManager manquant.", this);
            yield break;
        }

        float d = Mathf.Max(0f, delayBeforeFirstLineSeconds);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        yield return PlayLineAndWait(LineId.IntroNarrateur);
        if (_dialogueComplete)
            yield break;

        ActivatePhoneInDrawer();

        yield return PlayLineAndWait(LineId.NarrateurTelephone);
        yield return PlayLineAndWait(LineId.NayaAlarme);
        yield return PlayLineAndWait(LineId.NarrateurAucunSoucis);
        yield return PlayLineAndWait(LineId.NayaParentsAnxiete);
        yield return PlayLineAndWait(LineId.NarrateurDaccordJeVois);
        yield return PlayLineAndWait(LineId.NarrateurObjetsTable);
        yield return PlayLineAndWait(LineId.NayaOkJeMyMets);

        _dialogueRoutine = null;
        EnterExplorationPhase();
    }

    private IEnumerator PlayLineAndWait(LineId line)
    {
        if (_dialogueComplete || subtitleManager == null)
            yield break;

        EventReference er = GetEventForLine(line);
        if (er.IsNull)
        {
            Debug.LogWarning($"[BlackRoomNarrative] Event FMOD manquant pour {line}.", this);
            yield break;
        }

        _currentLine = (int)line;
        subtitleManager.EnqueueSubtitledLine(er);

        yield return null;

        const float startTimeoutSeconds = 4f;
        float waited = 0f;
        while (subtitleManager.IsNarrationIdle() && waited < startTimeoutSeconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (subtitleManager.IsNarrationIdle())
        {
            Debug.LogWarning(
                $"[BlackRoomNarrative] La ligne {line} n'a pas démarré (bank FMOD / GUID ?).",
                this);
            yield break;
        }

        while (!subtitleManager.IsNarrationIdle())
            yield return null;
    }

    private void ActivatePhoneInDrawer()
    {
        if (phoneInDrawerRoot == null) return;
        if (!phoneInDrawerRoot.activeSelf)
            phoneInDrawerRoot.SetActive(true);
    }

    private EventReference GetEventForLine(LineId line)
    {
        switch (line)
        {
            case LineId.IntroNarrateur: return voIntroNarrateur;
            case LineId.NarrateurTelephone: return voNarrateurTelephone;
            case LineId.NayaAlarme: return voNayaAlarme;
            case LineId.NarrateurAucunSoucis: return voNarrateurAucunSoucis;
            case LineId.NayaParentsAnxiete: return voNayaParentsAnxiete;
            case LineId.NarrateurDaccordJeVois: return voNarrateurDaccordJeVois;
            case LineId.NarrateurObjetsTable: return voNarrateurObjetsTable;
            case LineId.NayaOkJeMyMets: return voNayaOkJeMyMets;
            default: return default;
        }
    }

    private void EnterExplorationPhase()
    {
        if (_dialogueComplete) return;
        _dialogueComplete = true;
        _currentLine = -1;

        if (lockLocomotionUntilDialogueEnds && locomotionManager != null)
            locomotionManager.SetForceDisabled(false);

        onExplorationPhaseStarted?.Invoke();
    }
}
