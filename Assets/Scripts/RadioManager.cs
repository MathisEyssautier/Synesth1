using UnityEngine;
using UnityEngine.Events;

public class RadioManager : MonoBehaviour
{
    [Header("Potards")]
    public PotardController potard1;
    public PotardController potard2;

    [Header("Portes")]
    public DoorController porteA;
    public DoorController porteB;

    [Header("Entrouverture")]
    public float angleEntrouverte = -8f;
    public float vitesseEntrouverture = 2f;

    [Header("Evenements optionnels")]
    public UnityEvent OnAlignementAA;
    public UnityEvent OnAlignementBB;
    public UnityEvent OnAlignementPerdu;

    public enum EtatAlignement { Aucun, AA, BB }
    private EtatAlignement _etatCourant = EtatAlignement.Aucun;

    private float _cibleAnglePorteA = 0f;
    private float _cibleAnglePorteB = 0f;

    private void OnEnable()
    {
        potard1.OnCranChange += _ => VerifierAlignement();
        potard2.OnCranChange += _ => VerifierAlignement();
    }

    private void OnDisable()
    {
        potard1.OnCranChange -= _ => VerifierAlignement();
        potard2.OnCranChange -= _ => VerifierAlignement();
    }

    private void Start()
    {
        BlockerPorte(porteA);
        BlockerPorte(porteB);
    }

    private void Update()
    {
        AnimerPorte(porteA, ref _cibleAnglePorteA);
        AnimerPorte(porteB, ref _cibleAnglePorteB);
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
            DebloquetEtEntrouvrir(porteA, ref _cibleAnglePorteA);
            Debug.Log("[RadioManager] A+A - Porte A deverrouillee");
            OnAlignementAA?.Invoke();
        }
        else if (nouvelEtat == EtatAlignement.BB)
        {
            DebloquetEtEntrouvrir(porteB, ref _cibleAnglePorteB);
            Debug.Log("[RadioManager] B+B - Porte B deverrouillee");
            OnAlignementBB?.Invoke();
        }
    }

    private void BlockerPorte(DoorController porte)
    {
        if (porte == null) return;
        porte.ForceClose();
        SetPoigneesActives(porte, false);

        if (porte == porteA) _cibleAnglePorteA = 0f;
        if (porte == porteB) _cibleAnglePorteB = 0f;
    }

    private void DebloquetEtEntrouvrir(DoorController porte, ref float cibleAngle)
    {
        if (porte == null) return;
        SetPoigneesActives(porte, true);
        cibleAngle = angleEntrouverte;
    }

    private void SetPoigneesActives(DoorController porte, bool actif)
    {
        if (porte.handle1 != null) porte.handle1.gameObject.SetActive(actif);
        if (porte.handle2 != null) porte.handle2.gameObject.SetActive(actif);
    }

    private void AnimerPorte(DoorController porte, ref float cibleAngle)
    {
        if (porte == null) return;
        if (cibleAngle == 0f) return;

        if (Mathf.Abs(porte.currentYAngle - cibleAngle) < 0.1f)
        {
            cibleAngle = 0f;
            return;
        }

        porte.currentYAngle = Mathf.Lerp(porte.currentYAngle, cibleAngle, Time.deltaTime * vitesseEntrouverture);
        porte.doorPivot.rotation = Quaternion.Euler(0f, porte.currentYAngle, 0f);
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