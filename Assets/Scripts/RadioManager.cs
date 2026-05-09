using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using FMODUnity;
using FMOD.Studio;

public class RadioManager : MonoBehaviour
{
    [Header("Potards")]
    public PotardController potard1;
    public PotardController potard2;

    [Header("Piano — verrouillage")]
    [Tooltip("Si activé : les potards sont désactivés au démarrage jusqu'à UnlockAfterPianoSuccess().")]
    [SerializeField] private bool verrouillerPotardsJusquaPiano = true;

    [Header("Piano — déblocage (appelé par PianoPuzzleManager)")]
    [SerializeField] private Renderer partieRadioNoirBlanc;
    [SerializeField] private Color couleurRadioVerrouillee = Color.black;
    [SerializeField] private Color couleurRadioDebloquee = Color.white;
    [SerializeField] private EventReference sonDeblocageRadio;
    [SerializeField] private Transform origineSonRadio;

    [Header("Portes")]
    public DoorController porteA;
    public DoorController porteB;
    public Renderer rendererPorteA;
    public Renderer rendererPorteB;

    [Header("Audio ouverture automatique")]
    [Tooltip("One-shot FMOD quand on entre en alignement AA (porte A) ou BB (porte B).")]
    [SerializeField] private EventReference sonOuverturePorte;
    [SerializeField] private Transform origineSonOuverturePorte;

    [Header("Audio fermeture portes (3D)")]
    [Tooltip("One-shot à la position de la porte A (ex. cuisine) quand on quitte l’alignement AA.")]
    [SerializeField] private EventReference sonFermeturePorteA;
    [Tooltip("One-shot à la position de la porte B (ex. bureau) quand on quitte l’alignement BB.")]
    [SerializeField] private EventReference sonFermeturePorteB;

    [Header("Audio radio (boucle + chaînes AA / BB)")]
    [Tooltip("Grésillement / fond : volume 1 en neutre, volume 0 en AA ou BB.")]
    [SerializeField] private EventReference sonBoucleRadio;
    [Tooltip("Son AA joué en one-shot à l'entrée en alignement AA.")]
    [SerializeField] private EventReference sonChaineAA;
    [Tooltip("Son BB joué en one-shot à l'entrée en alignement BB.")]
    [SerializeField] private EventReference sonChaineBB;
    [SerializeField] private Transform origineSonBoucleRadio;

    [Header("Particules par état radio")]
    [Tooltip("Actif uniquement en AA (porte A ouverte).")]
    [SerializeField] private GameObject particlesEtatAA;
    [Tooltip("Actif uniquement en BB (porte B ouverte).")]
    [SerializeField] private GameObject particlesEtatBB;
    [Tooltip("Actif pour toutes les autres positions (Aucun).")]
    [SerializeField] private GameObject particlesEtatAucun;

    [Header("Emission portes")]
    public Color couleurPorteAinactive = new Color(0.05f, 0.2f, 0.05f);
    public Color couleurPorteAactive = new Color(0f, 1f, 0f);
    public Color couleurPorteBinactive = new Color(0.2f, 0.05f, 0.05f);
    public Color couleurPorteBactive = new Color(1f, 0f, 0f);

    [Header("Entrouverture")]
    public float angleEntrouverte = -8f;
    public float vitesseEntrouverture = 2f;

    [Header("Evenements optionnels")]
    public UnityEvent OnAlignementAA;
    public UnityEvent OnAlignementBB;
    public UnityEvent OnAlignementPerdu;

    [Tooltip("Invoque quand un son exclusif (ex. vocal parents) s'est terminé et avant la restauration des boucles.")]
    public UnityEvent onExclusiveRadioPlaybackEnded;

    public enum EtatAlignement { Aucun, AA, BB }

    private EtatAlignement _etatCourant = EtatAlignement.Aucun;
    private bool _radioDebloquee;
    private bool _isStandby;
    private bool _exclusiveRadioPlaying;
    public bool IsRadioUnlocked => _radioDebloquee;
    public bool IsStandby => _isStandby;
    /// <summary>
    /// Vrai pendant la lecture d'un event exclusif (ex. vocal parents). Pendant cette période,
    /// le bouton standby est verrouillé et la radio est forcée allumée.
    /// </summary>
    public bool IsExclusiveRadioPlaying => _exclusiveRadioPlaying;

    private EventInstance _instBoucle;
    private EventInstance _instAA;
    private EventInstance _instBB;
    private EventInstance _instExclusiveOverride;
    private Coroutine _restoreRadioAfterOverrideRoutine;

    /// <summary>
    /// Instance FMOD représentative de l'état radio courant (Aucun/AA/BB) pour FMODMeteringSource.
    /// Si un override exclusif est en cours, il est prioritaire.
    /// </summary>
    public EventInstance EventInstance
    {
        get
        {
            if (_instExclusiveOverride.isValid())
                return _instExclusiveOverride;

            switch (_etatCourant)
            {
                case EtatAlignement.AA:
                    return _instAA;
                case EtatAlignement.BB:
                    return _instBB;
                default:
                    return _instBoucle;
            }
        }
    }

    private Transform OrigineAudio => origineSonBoucleRadio != null ? origineSonBoucleRadio : transform;

    private void Awake()
    {
        potard1.OnCranChange += _ => VerifierAlignement();
        potard2.OnCranChange += _ => VerifierAlignement();
    }

    private void Start()
    {
        if (verrouillerPotardsJusquaPiano)
        {
            potard1?.SetInteractable(false);
            potard2?.SetInteractable(false);
            AppliquerCouleurPartieRadio(couleurRadioVerrouillee);
        }

        BlockerPorte(porteA);
        BlockerPorte(porteB);
        SetEmissionPorte(rendererPorteA, couleurPorteAinactive);
        SetEmissionPorte(rendererPorteB, couleurPorteBinactive);
        ApplyParticlesForEtat(_etatCourant);
    }

    /// <summary>
    /// Arrête et libère les boucles AA/BB/grésillement, joue un event FMOD 3D attaché à la radio (ex. vocal parents),
    /// puis rétablit les boucles une fois l'event terminé (si la radio était déjà débloquée).
    /// </summary>
    public void PlayExclusiveEventStoppingRadioStreams(EventReference eventReference)
    {
        if (eventReference.IsNull) return;

        if (_restoreRadioAfterOverrideRoutine != null)
        {
            StopCoroutine(_restoreRadioAfterOverrideRoutine);
            _restoreRadioAfterOverrideRoutine = null;
        }

        LibererSiValide(ref _instBoucle);
        LibererSiValide(ref _instAA);
        LibererSiValide(ref _instBB);
        LibererSiValide(ref _instExclusiveOverride);

        // Si la radio était en veille, on la rallume automatiquement pour que le vocal ait du sens.
        // On ne recrée pas les boucles puisqu'elles sont remplacées par l'override exclusif.
        if (_isStandby)
        {
            _isStandby = false;
            AppliquerCouleurPartieRadio(GetCurrentRadioColor());
        }

        _exclusiveRadioPlaying = true;
        RefreshPotardInteractivity();

        _instExclusiveOverride = CreateFmodInstance(eventReference);
        if (!_instExclusiveOverride.isValid())
        {
            _exclusiveRadioPlaying = false;
            return;
        }

        GameObject go = OrigineAudio.gameObject;
        RuntimeManager.AttachInstanceToGameObject(_instExclusiveOverride, go);
        _instExclusiveOverride.start();

        _restoreRadioAfterOverrideRoutine = StartCoroutine(RestoreRadioStreamsAfterExclusiveEnds());
    }

    private IEnumerator RestoreRadioStreamsAfterExclusiveEnds()
    {
        while (_instExclusiveOverride.isValid())
        {
            _instExclusiveOverride.getPlaybackState(out PLAYBACK_STATE st);
            if (st == PLAYBACK_STATE.STOPPED)
                break;
            yield return null;
        }

        LibererSiValide(ref _instExclusiveOverride);
        _restoreRadioAfterOverrideRoutine = null;
        _exclusiveRadioPlaying = false;
        RefreshPotardInteractivity();

        onExclusiveRadioPlaybackEnded?.Invoke();

        if (_radioDebloquee)
        {
            CreerEtDemarrerToutesLesInstancesRadio();
            AppliquerVolumesRadio(_etatCourant);
        }
    }

    /// <summary>
    /// Si la radio était en veille (bouton), la rallume ; remet les potards au cran 0 ;
    /// réévalue l’alignement AA/BB et ferme les portes si besoin (son fermeture 3D sur la porte concernée).
    /// À brancher sur <c>UnlockPlacementSocket.onObjectPlaced</c> (cassette et guitare).
    /// </summary>
    public void ResetPotardsAuCranZeroEtFermerPortesSiBesoin()
    {
        if (_isStandby)
            SetStandby(false);

        potard1?.SetCranSansInteraction(0);
        potard2?.SetCranSansInteraction(0);
        AppliquerChangementAlignementSiDifferent(ComputeAlignementDepuisPotards());
    }

    /// <summary>Appelé par PianoPuzzleManager quand la séquence piano est réussie.</summary>
    public void UnlockAfterPianoSuccess()
    {
        // La radio s'allume automatiquement à la résolution du piano :
        // un éventuel toggle accidentel du bouton avant la résolution est annulé.
        _isStandby = false;

        potard1?.SetInteractable(true);
        potard2?.SetInteractable(true);
        AppliquerCouleurPartieRadio(couleurRadioDebloquee);

        CreerEtDemarrerToutesLesInstancesRadio();
        _radioDebloquee = true;

        EtatAlignement lu = LireEtatDepuisPotards();
        AppliquerVolumesRadio(lu);
        _etatCourant = lu;
        ApplyParticlesForEtat(_etatCourant);
        RefreshPotardInteractivity();

        if (!sonDeblocageRadio.IsNull && !MemeEventReference(sonDeblocageRadio, sonBoucleRadio))
        {
            Transform t = origineSonRadio != null ? origineSonRadio : transform;
            PlayOneShotFmod(sonDeblocageRadio, t.position);
        }
    }

    public void ToggleStandby()
    {
        SetStandby(!_isStandby);
    }

    public void SetStandby(bool standby)
    {
        // Pendant la lecture exclusive (vocal parents…), la radio est verrouillée allumée.
        if (_exclusiveRadioPlaying && standby) return;
        if (_isStandby == standby) return;
        _isStandby = standby;
        RefreshPotardInteractivity();
        AppliquerCouleurPartieRadio(GetCurrentRadioColor());

        // Ne touche ni aux portes, ni à l'état logique des crans.
        if (_isStandby)
        {
            ForceMuteAllRadioAudio();
        }
        else
        {
            AppliquerVolumesRadio(_etatCourant);
        }
    }

    private Color GetCurrentRadioColor()
    {
        if (!_radioDebloquee) return couleurRadioVerrouillee;
        return _isStandby ? couleurRadioVerrouillee : couleurRadioDebloquee;
    }

    private void CreerEtDemarrerToutesLesInstancesRadio()
    {
        // Libère d’éventuelles instances (double Unlock, etc.).
        LibererSiValide(ref _instBoucle);
        LibererSiValide(ref _instAA);
        LibererSiValide(ref _instBB);

        GameObject go = OrigineAudio.gameObject;

        if (!sonBoucleRadio.IsNull)
        {
            _instBoucle = CreateFmodInstance(sonBoucleRadio);
            if (_instBoucle.isValid())
            {
                RuntimeManager.AttachInstanceToGameObject(_instBoucle, go);
                _instBoucle.start();
            }
        }

        // AA/BB sont désormais des one-shots joués lors d'une entrée d'état,
        // donc pas d'instances persistantes à créer ici.
    }

    /// <summary>Création d'instance FMOD compatible desktop + Android.</summary>
    private static EventInstance CreateFmodInstance(EventReference er)
    {
        if (er.IsNull) return default;

        try
        {
            return RuntimeManager.CreateInstance(er);
        }
        catch
        {
            return default;
        }
    }

    private static void PlayOneShotFmod(EventReference er, Vector3 position)
    {
        if (er.IsNull) return;

        try
        {
            RuntimeManager.PlayOneShot(er, position);
        }
        catch
        {
            //
        }
    }

    /// <summary>
    /// Neutre : boucle 1. AA/BB : boucle 0.
    /// Les sons AA/BB sont joués en one-shot à l'entrée d'alignement.
    /// </summary>
    private void AppliquerVolumesRadio(EtatAlignement etat)
    {
        if (!_radioDebloquee) return;

        if (_isStandby)
        {
            ForceMuteAllRadioAudio();
            return;
        }

        if (_instBoucle.isValid())
            _instBoucle.setVolume(etat == EtatAlignement.Aucun ? 1f : 0f);
    }

    private void ForceMuteAllRadioAudio()
    {
        if (_instBoucle.isValid()) _instBoucle.setVolume(0f);
    }

    private void AppliquerCouleurPartieRadio(Color couleur)
    {
        if (partieRadioNoirBlanc == null) return;

        var mat = partieRadioNoirBlanc.material;
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", couleur);
        else
            mat.color = couleur;

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", couleur);
    }

    private void VerifierAlignement()
    {
        AppliquerChangementAlignementSiDifferent(ComputeAlignementDepuisPotards());
    }

    private EtatAlignement ComputeAlignementDepuisPotards()
    {
        if (potard1 == null || potard2 == null)
            return EtatAlignement.Aucun;
        if (potard1.EstSurA && potard2.EstSurA)
            return EtatAlignement.AA;
        if (potard1.EstSurB && potard2.EstSurB)
            return EtatAlignement.BB;
        return EtatAlignement.Aucun;
    }

    private void AppliquerChangementAlignementSiDifferent(EtatAlignement nouvelEtat)
    {
        if (nouvelEtat == _etatCourant) return;

        EtatAlignement etatAvant = _etatCourant;

        if (_etatCourant != EtatAlignement.Aucun)
        {
            BlockerPorte(porteA);
            BlockerPorte(porteB);
            OnAlignementPerdu?.Invoke();
            JouerSonFermeturePortePourAlignementPerdu(etatAvant);
        }

        _etatCourant = nouvelEtat;
        AppliquerVolumesRadio(nouvelEtat);
        ApplyParticlesForEtat(nouvelEtat);

        bool entreeAA = nouvelEtat == EtatAlignement.AA && etatAvant != EtatAlignement.AA;
        bool entreeBB = nouvelEtat == EtatAlignement.BB && etatAvant != EtatAlignement.BB;

        // Si on quitte AA ou BB (ou on en change), on coupe immédiatement
        // l'event correspondant pour retourner au grésillement.
        if (etatAvant == EtatAlignement.AA && nouvelEtat != EtatAlignement.AA)
            LibererSiValide(ref _instAA);
        if (etatAvant == EtatAlignement.BB && nouvelEtat != EtatAlignement.BB)
            LibererSiValide(ref _instBB);

        if (nouvelEtat == EtatAlignement.AA)
        {
            DebloquetEtEntrouvrir(porteA);
            SetEmissionPorte(rendererPorteA, couleurPorteAactive);
            if (entreeAA) JouerSonOuverturePorte(porteA);
            if (entreeAA) DemarrerSonEtatRadio(sonChaineAA, ref _instAA);
            OnAlignementAA?.Invoke();
        }
        else if (nouvelEtat == EtatAlignement.BB)
        {
            DebloquetEtEntrouvrir(porteB);
            SetEmissionPorte(rendererPorteB, couleurPorteBactive);
            if (entreeBB) JouerSonOuverturePorte(porteB);
            if (entreeBB) DemarrerSonEtatRadio(sonChaineBB, ref _instBB);
            OnAlignementBB?.Invoke();
        }
    }

    private void DemarrerSonEtatRadio(EventReference evt, ref EventInstance instance)
    {
        // Crée et démarre une instance attachée à la radio. Stoppable quand on quitte l'état.
        LibererSiValide(ref instance);
        if (evt.IsNull) return;

        instance = CreateFmodInstance(evt);
        if (!instance.isValid()) return;

        RuntimeManager.AttachInstanceToGameObject(instance, OrigineAudio.gameObject);
        instance.start();
    }

    private void ApplyParticlesForEtat(EtatAlignement etat)
    {
        SetParticleState(particlesEtatAA, etat == EtatAlignement.AA);
        SetParticleState(particlesEtatBB, etat == EtatAlignement.BB);
        SetParticleState(particlesEtatAucun, etat == EtatAlignement.Aucun);
    }

    private static void SetParticleState(GameObject go, bool active)
    {
        if (go == null) return;
        if (go.activeSelf != active)
            go.SetActive(active);
    }

    private void JouerSonOuverturePorte(DoorController porte)
    {
        if (sonOuverturePorte.IsNull) return;

        Vector3 pos = transform.position;
        if (porte != null && porte.doorPivot != null)
            pos = porte.doorPivot.position;
        else if (origineSonOuverturePorte != null)
            pos = origineSonOuverturePorte.position;

        PlayOneShotFmod(sonOuverturePorte, pos);
    }

    /// <summary>Alignement AA = porte A (ex. cuisine), BB = porte B (ex. bureau).</summary>
    private void JouerSonFermeturePortePourAlignementPerdu(EtatAlignement alignementQuOnQuitte)
    {
        if (alignementQuOnQuitte == EtatAlignement.AA)
        {
            if (!sonFermeturePorteA.IsNull && porteA != null)
                PlayOneShotFmod(sonFermeturePorteA, GetPosition3DPorte(porteA));
        }
        else if (alignementQuOnQuitte == EtatAlignement.BB)
        {
            if (!sonFermeturePorteB.IsNull && porteB != null)
                PlayOneShotFmod(sonFermeturePorteB, GetPosition3DPorte(porteB));
        }
    }

    private static Vector3 GetPosition3DPorte(DoorController porte)
    {
        if (porte != null && porte.doorPivot != null)
            return porte.doorPivot.position;
        return porte != null ? porte.transform.position : Vector3.zero;
    }

    private EtatAlignement LireEtatDepuisPotards() => ComputeAlignementDepuisPotards();

    private static bool MemeEventReference(EventReference a, EventReference b)
    {
        if (a.IsNull || b.IsNull) return false;
        return a.Guid.Equals(b.Guid);
    }

    private void BlockerPorte(DoorController porte)
    {
        if (porte == null) return;
        StopAllCoroutines();
        porte.ForceClose();
        SetPoigneesActives(porte, false);
        if (porte == porteA) SetEmissionPorte(rendererPorteA, couleurPorteAinactive);
        if (porte == porteB) SetEmissionPorte(rendererPorteB, couleurPorteBinactive);
    }

    private void DebloquetEtEntrouvrir(DoorController porte)
    {
        if (porte == null) return;
        SetPoigneesActives(porte, true);
        StartCoroutine(EntroouvrirPorte(porte));
    }

    private IEnumerator EntroouvrirPorte(DoorController porte)
    {
        while (Mathf.Abs(porte.currentYAngle - angleEntrouverte) > 0.1f)
        {
            porte.currentYAngle = Mathf.Lerp(porte.currentYAngle, angleEntrouverte, Time.deltaTime * vitesseEntrouverture);
            porte.doorPivot.rotation = Quaternion.Euler(0f, porte.currentYAngle, 0f);
            yield return null;
        }
        porte.currentYAngle = angleEntrouverte;
        porte.doorPivot.rotation = Quaternion.Euler(0f, angleEntrouverte, 0f);
    }

    private void SetPoigneesActives(DoorController porte, bool actif)
    {
        if (porte.handle1 != null) porte.handle1.gameObject.SetActive(actif);
        if (porte.handle2 != null) porte.handle2.gameObject.SetActive(actif);
    }

    private void SetEmissionPorte(Renderer r, Color couleur)
    {
        if (r == null) return;
        r.material.SetColor("_EmissionColor", couleur);
        r.material.color = couleur;
    }

    private void RefreshPotardInteractivity()
    {
        // Pendant la lecture d'un event exclusif (ex. vocal parents), les potards sont verrouillés
        // pour empêcher l'ouverture/fermeture de portes pendant le vocal.
        if (_isStandby || _exclusiveRadioPlaying)
        {
            potard1?.SetInteractable(false);
            potard2?.SetInteractable(false);
            return;
        }

        bool shouldEnable = _radioDebloquee || !verrouillerPotardsJusquaPiano;
        potard1?.SetInteractable(shouldEnable);
        potard2?.SetInteractable(shouldEnable);
    }

    private void OnDisable()
    {
        if (_restoreRadioAfterOverrideRoutine != null)
        {
            StopCoroutine(_restoreRadioAfterOverrideRoutine);
            _restoreRadioAfterOverrideRoutine = null;
        }

        LibererSiValide(ref _instBoucle);
        LibererSiValide(ref _instAA);
        LibererSiValide(ref _instBB);
        LibererSiValide(ref _instExclusiveOverride);
        _radioDebloquee = false;
    }

    private static void LibererSiValide(ref EventInstance inst)
    {
        if (!inst.isValid()) return;
        inst.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        inst.release();
        inst.clearHandle();
    }
}
