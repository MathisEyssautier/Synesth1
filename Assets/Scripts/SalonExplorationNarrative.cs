using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;

/// <summary>
/// Voix off après l'exploration libre du salon : indices piano, objets, cuisine, bureau, guitare, coquillages, faders.
/// Déclencheurs zone : composant <see cref="SalonNarrativeTriggerZone"/> sur les triggers cuisine / bureau.
/// </summary>
public class SalonExplorationNarrative : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private SubtitleManager subtitleManager;
    [SerializeField] private PianoPuzzleManager pianoPuzzleManager;
    [SerializeField] private ShellPuzzleManager shellPuzzleManager;

    [Header("Indices piano (depuis NotifySalonExplorationStarted)")]
    [SerializeField] private float pianoHintAfterSeconds = 180f;
    [SerializeField] private float pianoSecondHintAfterSeconds = 420f;
    [SerializeField] private EventReference voTherapeuteDisMoiEstceQueTu;
    [SerializeField] private EventReference voNayaOuiCestVraiNumeros;
    [SerializeField] private EventReference voNayaIlMeSembleQueNumeros;

    [Header("Objets (première prise)")]
    [SerializeField] private XRGrabInteractable vinylVinyl1Grab;
    [SerializeField] private EventReference voNayaCesDisquesMesParents;

    [SerializeField] private XRGrabInteractable braceletGrab;
    [SerializeField] private EventReference voNayaCeCollierMaMere;

    [Header("Piano résolu")]
    [SerializeField] private EventReference voTherapeuteBienJoueTuFaisDesProgres;
    [SerializeField] private EventReference voNayaCePianoEst;
    [SerializeField] private EventReference voNayaCestLaRadioQui;

    [Header("Cuisine — objets (première prise)")]
    [SerializeField] private XRGrabInteractable shellOnTableGrab;
    [SerializeField] private EventReference voNayaJaimaisTellementEcouter;

    [SerializeField] private XRGrabInteractable assietteGrab;
    [SerializeField] private EventReference voNayaIlManquaitMonAssiette;

    [Header("Cuisine — indice 5 min (bureau non visité + cassette non débloquée)")]
    [SerializeField] private float kitchenHintAfterSeconds = 300f;
    [SerializeField] private EventReference voTherapeuteSiTuAsBesoinDeSouffler;

    [Header("Cuisine / bureau (lignes jouées par trigger)")]
    [SerializeField] private EventReference voNayaOhCestLaCuisine;
    [SerializeField] private EventReference voNayaOhTiensJeReconnais;

    [Header("Coquillages / cassette résolus")]
    [SerializeField] private EventReference voTherapeuteParfaitK7;
    [SerializeField] private EventReference voNayaTousCesSonsCest;
    [SerializeField] private EventReference voTherapeuteLeTempsPasse;
    [SerializeField] private EventReference voNayaOuiJespere;

    [Header("Guitare")]
    [SerializeField] private EventReference voTherapeuteCestPresqueTermine;
    [Tooltip("Première ligne jouée la 1re fois qu'un accord est joué après l'assemblage des 6 cordes.")]
    [SerializeField] private EventReference voGuitarFirstChordLine1;
    [Tooltip("Deuxième ligne enchaînée juste après la première, à la 1re fois qu'un accord est joué après l'assemblage des 6 cordes.")]
    [SerializeField] private EventReference voGuitarFirstChordLine2;
    [SerializeField] private EventReference voTherapeuteParfaitGuitare;
    [SerializeField] private EventReference voTherapeuteEtLaTuViensDeRajouter;
    [SerializeField] private EventReference voNayaCetteGuitareMaToujours;
    [SerializeField] private EventReference voTherapeuteTuNesPasFigee;

    [Header("Bureau — Post-FX hallucinations (Volume URP)")]
    [Tooltip("Driver du Volume URP du bureau. Activé en même temps que la voix off des cordes posées, désactivé en même temps que la voix off de prisme résolu.")]
    [SerializeField] private OfficeChromaticAberrationDriver officePostFxDriver;

    [Header("Trois faders actifs — son radio (pas une voix off)")]
    [SerializeField] private GameObject faderViolons;
    [SerializeField] private GameObject faderGuitare;
    [SerializeField] private GameObject faderBass;
    [SerializeField] private RadioManager radioManager;
    [Tooltip("Timeline 3D sur la radio ; coupe les boucles radio puis les rétablit à la fin de l'event.")]
    [SerializeField] private EventReference eventVocalParentsSurRadio;
    [Header("Trois faders — voix off après la fin du son radio (parents)")]
    [SerializeField] private EventReference voNayaJeMeSuisToujoursDit;
    [SerializeField] private EventReference voTherapeuteTyEsPresqueEquilibre;

    private bool _postParentsVoQueued;

    private bool _explorationStarted;
    private bool _pianoHint1Done;
    private bool _pianoHint2Done;
    private bool _vinylDone;
    private bool _braceletDone;
    private bool _pianoSolvedVoDone;
    private bool _kitchenEntered;
    private bool _kitchenHintDone;
    private bool _shellGrabDone;
    private bool _assietteDone;
    private bool _officeEntered;
    private bool _shellPuzzleVoDone;
    private bool _guitarStringsVoDone;
    private bool _guitarFirstChordVoDone;
    private bool _guitarSolvedVoDone;
    private bool _guitarPlacedVoDone;
    private bool _threeFadersVoDone;
    private Coroutine _pianoHintRoutine;
    private Coroutine _kitchenTimerRoutine;
    private Coroutine _radioAfterNarrationRoutine;

    private void OnEnable()
    {
        if (radioManager != null)
            radioManager.onExclusiveRadioPlaybackEnded.AddListener(OnParentsRadioSoundEnded);

        if (vinylVinyl1Grab != null)
            vinylVinyl1Grab.selectEntered.AddListener(OnVinylFirstGrab);
        if (braceletGrab != null)
            braceletGrab.selectEntered.AddListener(OnBraceletFirstGrab);
        if (shellOnTableGrab != null)
            shellOnTableGrab.selectEntered.AddListener(OnShellFirstGrab);
        if (assietteGrab != null)
            assietteGrab.selectEntered.AddListener(OnAssietteFirstGrab);
    }

    private void OnDisable()
    {
        if (_radioAfterNarrationRoutine != null)
        {
            StopCoroutine(_radioAfterNarrationRoutine);
            _radioAfterNarrationRoutine = null;
        }

        if (radioManager != null)
            radioManager.onExclusiveRadioPlaybackEnded.RemoveListener(OnParentsRadioSoundEnded);

        if (vinylVinyl1Grab != null)
            vinylVinyl1Grab.selectEntered.RemoveListener(OnVinylFirstGrab);
        if (braceletGrab != null)
            braceletGrab.selectEntered.RemoveListener(OnBraceletFirstGrab);
        if (shellOnTableGrab != null)
            shellOnTableGrab.selectEntered.RemoveListener(OnShellFirstGrab);
        if (assietteGrab != null)
            assietteGrab.selectEntered.RemoveListener(OnAssietteFirstGrab);
    }

    public void NotifySalonExplorationStarted()
    {
        if (_explorationStarted) return;
        _explorationStarted = true;
        if (_pianoHintRoutine != null)
            StopCoroutine(_pianoHintRoutine);
        _pianoHintRoutine = StartCoroutine(PianoHintsRoutine());
    }

    private IEnumerator PianoHintsRoutine()
    {
        if (pianoPuzzleManager == null)
            yield break;

        float start = Time.time;
        while (!pianoPuzzleManager.IsSolved)
        {
            if (!_pianoHint1Done && Time.time - start >= pianoHintAfterSeconds)
            {
                _pianoHint1Done = true;
                if (!pianoPuzzleManager.IsSolved)
                {
                    EnqueueSub(voTherapeuteDisMoiEstceQueTu);
                    EnqueueSub(voNayaOuiCestVraiNumeros);
                }
            }

            if (!_pianoHint2Done && Time.time - start >= pianoSecondHintAfterSeconds)
            {
                _pianoHint2Done = true;
                if (!pianoPuzzleManager.IsSolved)
                    EnqueueSub(voNayaIlMeSembleQueNumeros);
            }

            yield return null;
        }
    }

    public void NotifyKitchenEntered()
    {
        if (_kitchenEntered) return;
        _kitchenEntered = true;
        EnqueueSub(voNayaOhCestLaCuisine);

        if (_kitchenTimerRoutine != null)
            StopCoroutine(_kitchenTimerRoutine);
        _kitchenTimerRoutine = StartCoroutine(KitchenIdleHintRoutine());
    }

    public void NotifyOfficeEntered()
    {
        if (_officeEntered) return;
        _officeEntered = true;
        EnqueueSub(voNayaOhTiensJeReconnais);
    }

    private IEnumerator KitchenIdleHintRoutine()
    {
        float start = Time.time;
        while (Time.time - start < kitchenHintAfterSeconds)
        {
            if (_kitchenHintDone) yield break;
            if (_officeEntered) yield break;
            if (shellPuzzleManager != null && shellPuzzleManager.IsSolved) yield break;
            yield return null;
        }

        if (_kitchenHintDone) yield break;
        if (_officeEntered) yield break;
        if (shellPuzzleManager != null && shellPuzzleManager.IsSolved) yield break;

        _kitchenHintDone = true;
        EnqueueSub(voTherapeuteSiTuAsBesoinDeSouffler);
    }

    public void NotifyPianoPuzzleSolved()
    {
        if (_pianoSolvedVoDone) return;
        _pianoSolvedVoDone = true;
        EnqueueSub(voTherapeuteBienJoueTuFaisDesProgres);
        EnqueueSub(voNayaCePianoEst);
        EnqueueSub(voNayaCestLaRadioQui);
    }

    public void NotifyShellPuzzleSolved()
    {
        if (_shellPuzzleVoDone) return;
        _shellPuzzleVoDone = true;
        EnqueueSub(voTherapeuteParfaitK7);
        EnqueueSub(voNayaTousCesSonsCest);
        EnqueueSub(voTherapeuteLeTempsPasse);
        EnqueueSub(voNayaOuiJespere);
    }

    public void NotifyAllGuitarStringsPlaced()
    {
        if (_guitarStringsVoDone) return;
        _guitarStringsVoDone = true;
        EnqueueSub(voTherapeuteCestPresqueTermine);

        if (officePostFxDriver != null)
            officePostFxDriver.EnableEffect();
    }

    /// <summary>
    /// À appeler la première fois qu'un accord est joué après que toutes les cordes
    /// de guitare ont été montées. Enchaîne deux lignes de dialogue.
    /// </summary>
    public void NotifyFirstGuitarChordPlayed()
    {
        if (_guitarFirstChordVoDone) return;
        _guitarFirstChordVoDone = true;
        EnqueueSub(voGuitarFirstChordLine1);
        EnqueueSub(voGuitarFirstChordLine2);
    }

    public void NotifyGuitarChordSolved()
    {
        if (_guitarSolvedVoDone) return;
        _guitarSolvedVoDone = true;
        EnqueueSub(voTherapeuteParfaitGuitare);

        if (officePostFxDriver != null)
            officePostFxDriver.DisableEffect();
    }

    public void NotifyGuitarPlacedOnStand()
    {
        if (_guitarPlacedVoDone) return;
        _guitarPlacedVoDone = true;
        EnqueueSub(voTherapeuteEtLaTuViensDeRajouter);
        EnqueueSub(voNayaCetteGuitareMaToujours);
        EnqueueSub(voTherapeuteTuNesPasFigee);
    }

    private void Update()
    {
        if (_threeFadersVoDone) return;
        if (faderViolons == null || faderGuitare == null || faderBass == null) return;
        if (!faderViolons.activeInHierarchy || !faderGuitare.activeInHierarchy || !faderBass.activeInHierarchy)
            return;

        _threeFadersVoDone = true;

        _postParentsVoQueued = false;
        if (_radioAfterNarrationRoutine != null)
            StopCoroutine(_radioAfterNarrationRoutine);
        _radioAfterNarrationRoutine = StartCoroutine(PlayRadioParentsAfterNarrationIdle());
    }

    private IEnumerator PlayRadioParentsAfterNarrationIdle()
    {
        while (subtitleManager != null && !subtitleManager.IsNarrationIdle())
            yield return null;

        if (radioManager != null && !eventVocalParentsSurRadio.IsNull)
            radioManager.PlayExclusiveEventStoppingRadioStreams(eventVocalParentsSurRadio);
        else
            OnParentsRadioSoundEnded();

        _radioAfterNarrationRoutine = null;
    }

    private void OnParentsRadioSoundEnded()
    {
        if (_postParentsVoQueued) return;
        _postParentsVoQueued = true;
        EnqueueSub(voNayaJeMeSuisToujoursDit);
        EnqueueSub(voTherapeuteTyEsPresqueEquilibre);
    }

    private void EnqueueSub(EventReference er)
    {
        if (subtitleManager == null || er.IsNull) return;
        subtitleManager.EnqueueSubtitledLine(er);
    }

    private void OnVinylFirstGrab(SelectEnterEventArgs e)
    {
        if (_vinylDone) return;
        _vinylDone = true;
        if (vinylVinyl1Grab != null)
            vinylVinyl1Grab.selectEntered.RemoveListener(OnVinylFirstGrab);
        EnqueueSub(voNayaCesDisquesMesParents);
    }

    private void OnBraceletFirstGrab(SelectEnterEventArgs e)
    {
        if (_braceletDone) return;
        _braceletDone = true;
        if (braceletGrab != null)
            braceletGrab.selectEntered.RemoveListener(OnBraceletFirstGrab);
        EnqueueSub(voNayaCeCollierMaMere);
    }

    private void OnShellFirstGrab(SelectEnterEventArgs e)
    {
        if (_shellGrabDone) return;
        _shellGrabDone = true;
        if (shellOnTableGrab != null)
            shellOnTableGrab.selectEntered.RemoveListener(OnShellFirstGrab);
        EnqueueSub(voNayaJaimaisTellementEcouter);
    }

    private void OnAssietteFirstGrab(SelectEnterEventArgs e)
    {
        if (_assietteDone) return;
        _assietteDone = true;
        if (assietteGrab != null)
            assietteGrab.selectEntered.RemoveListener(OnAssietteFirstGrab);
        EnqueueSub(voNayaIlManquaitMonAssiette);
    }
}
