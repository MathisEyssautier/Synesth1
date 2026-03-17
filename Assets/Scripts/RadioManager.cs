using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class RadioManager : MonoBehaviour
{
    [Header("Potards")]
    public PotardController potard1;
    public PotardController potard2;

    [Header("Portes")]
    public DoorController porteA;
    public DoorController porteB;
    public Renderer rendererPorteA;
    public Renderer rendererPorteB;

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

    public enum EtatAlignement { Aucun, AA, BB }
    private EtatAlignement _etatCourant = EtatAlignement.Aucun;

    private void Awake()
    {
        potard1.OnCranChange += _ => VerifierAlignement();
        potard2.OnCranChange += _ => VerifierAlignement();
    }

    private void Start()
    {
        BlockerPorte(porteA);
        BlockerPorte(porteB);
        SetEmissionPorte(rendererPorteA, couleurPorteAinactive);
        SetEmissionPorte(rendererPorteB, couleurPorteBinactive);
    }

    private void VerifierAlignement()
    {
        EtatAlignement nouvelEtat = EtatAlignement.Aucun;

        if (potard1.EstSurA && potard2.EstSurA) nouvelEtat = EtatAlignement.AA;
        else if (potard1.EstSurB && potard2.EstSurB) nouvelEtat = EtatAlignement.BB;

        if (nouvelEtat == _etatCourant) return;

        if (_etatCourant != EtatAlignement.Aucun)
        {
            BlockerPorte(porteA);
            BlockerPorte(porteB);
            OnAlignementPerdu?.Invoke();
            Debug.Log("[RadioManager] Alignement perdu - portes bloquees.");
        }

        _etatCourant = nouvelEtat;

        if (nouvelEtat == EtatAlignement.AA)
        {
            DebloquetEtEntrouvrir(porteA);
            SetEmissionPorte(rendererPorteA, couleurPorteAactive);
            Debug.Log("[RadioManager] A+A - Porte A deverrouillee");
            OnAlignementAA?.Invoke();
        }
        else if (nouvelEtat == EtatAlignement.BB)
        {
            DebloquetEtEntrouvrir(porteB);
            SetEmissionPorte(rendererPorteB, couleurPorteBactive);
            Debug.Log("[RadioManager] B+B - Porte B deverrouillee");
            OnAlignementBB?.Invoke();
        }
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
        // Snap final - RadioManager ne touche plus jamais a cette porte
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

#if UNITY_EDITOR
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 320, 80));
        GUILayout.Label("Potard1 - cran " + potard1?.CranActuel + " | A:" + potard1?.EstSurA + " B:" + potard1?.EstSurB);
        GUILayout.Label("Potard2 - cran " + potard2?.CranActuel + " | A:" + potard2?.EstSurA + " B:" + potard2?.EstSurB);
        GUILayout.Label("Etat : " + _etatCourant);
        GUILayout.EndArea();
    }
#endif
}