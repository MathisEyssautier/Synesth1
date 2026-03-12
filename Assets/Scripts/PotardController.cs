using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class PotardController : MonoBehaviour
{
    [Header("Crans")]
    public int nombreCrans = 12;
    [Tooltip("Degres de rotation de main necessaires pour avancer d'un cran")]
    public float seuilDegresCran = 8f;

    [Header("Positions valides (index de cran, 0 a nombreCrans-1)")]
    public int cranPositionA = 3;
    public int cranPositionB = 9;

    [Header("Haptics")]
    [Range(0f, 1f)] public float intensiteCran = 0.15f;
    public float dureeCran = 0.05f;
    [Range(0f, 1f)] public float intensitePositionValide = 0.8f;
    public float dureePositionValide = 0.15f;

    [Header("Indicateur visuel")]
    public Renderer cubeIndicateur;
    public Color couleurNeutre = Color.white;
    public Color couleurA = Color.green;
    public Color couleurB = Color.red;

    private int _cranActuel = 0;
    private bool _estSaisi = false;
    private IXRSelectInteractor _interactorCourant;
    private float _angleMainPrecedent;
    private float _accumulateurDelta = 0f;
    private Quaternion _rotationBaseLocale;

    private XRGrabInteractable _grabInteractable;
    private Rigidbody _rb;

    public int CranActuel => _cranActuel;
    public bool EstSurA => _cranActuel == cranPositionA;
    public bool EstSurB => _cranActuel == cranPositionB;

    public System.Action<int> OnCranChange;
    public System.Action<bool> OnPositionValide;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        _rotationBaseLocale = Quaternion.Euler(
            transform.localEulerAngles.x,
            0f,
            transform.localEulerAngles.z
        );
        _rb.constraints = RigidbodyConstraints.FreezeAll;
        AppliquerRotationCran(_cranActuel);
        MettreAJourCouleur();
    }

    private void OnEnable()
    {
        _grabInteractable.selectEntered.AddListener(OnGrab);
        _grabInteractable.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrab);
        _grabInteractable.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        _estSaisi = true;
        _interactorCourant = args.interactorObject;
        _angleMainPrecedent = GetAngleController(_interactorCourant);
        _accumulateurDelta = 0f;
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        _estSaisi = false;
        _interactorCourant = null;
        _accumulateurDelta = 0f;

        _rb.constraints = RigidbodyConstraints.FreezeAll;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        AppliquerRotationCran(_cranActuel);
    }

    private void Update()
    {
        if (!_estSaisi || _interactorCourant == null) return;

        float angleActuel = GetAngleController(_interactorCourant);
        float delta = Mathf.DeltaAngle(_angleMainPrecedent, angleActuel);
        _angleMainPrecedent = angleActuel;

        _accumulateurDelta += delta;

        while (_accumulateurDelta >= seuilDegresCran)
        {
            _accumulateurDelta -= seuilDegresCran;
            ChangerCran(1);
        }
        while (_accumulateurDelta <= -seuilDegresCran)
        {
            _accumulateurDelta += seuilDegresCran;
            ChangerCran(-1);
        }
    }

    private void ChangerCran(int direction)
    {
        _cranActuel = (_cranActuel + direction + nombreCrans) % nombreCrans;

        AppliquerRotationCran(_cranActuel);
        OnCranChange?.Invoke(_cranActuel);
        EnvoyerHapticsCran();
        MettreAJourCouleur();

        if (_cranActuel == cranPositionA)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(true);
        }
        else if (_cranActuel == cranPositionB)
        {
            EnvoyerHapticsPositionValide();
            OnPositionValide?.Invoke(false);
        }
    }

    private void AppliquerRotationCran(int cran)
    {
        float degresParCran = 360f / nombreCrans;
        float angleY = cran * degresParCran;
        transform.localRotation = _rotationBaseLocale * Quaternion.Euler(0f, angleY, 0f);
    }

    private void MettreAJourCouleur()
    {
        if (cubeIndicateur == null) return;

        Color cible = couleurNeutre;
        if (_cranActuel == cranPositionA) cible = couleurA;
        else if (_cranActuel == cranPositionB) cible = couleurB;

        cubeIndicateur.material.SetColor("_EmissionColor", cible);
        cubeIndicateur.material.color = cible;
    }

    // Lit le "roll" du controller, c'est-a-dire la rotation autour de son axe forward
    // Beaucoup plus direct et sensible que de projeter le vecteur up
    private float GetAngleController(IXRSelectInteractor interactor)
    {
        Quaternion rotController = interactor.GetAttachTransform(_grabInteractable).rotation;

        // On extrait le vecteur right du controller et on le projette
        // dans le plan perpendiculaire au forward du controller
        // pour isoler uniquement le roll (twist de la main)
        Vector3 rightController = rotController * Vector3.right;
        Vector3 upController = rotController * Vector3.up;

        // Angle du vecteur right autour de l'axe forward world (Z)
        return Mathf.Atan2(upController.x, upController.y) * Mathf.Rad2Deg;
    }

    private void EnvoyerHapticsCran()
    {
        if (_interactorCourant is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(intensiteCran, dureeCran);
    }

    private void EnvoyerHapticsPositionValide()
    {
        if (_interactorCourant is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(intensitePositionValide, dureePositionValide);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float degresParCran = 360f / nombreCrans;
        for (int i = 0; i < nombreCrans; i++)
        {
            float angle = i * degresParCran * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            Vector3 pos = transform.position + dir * 0.06f;
            Gizmos.color = i == cranPositionA ? Color.green : i == cranPositionB ? Color.blue : Color.gray;
            Gizmos.DrawSphere(pos, 0.005f);
        }
    }
#endif
}