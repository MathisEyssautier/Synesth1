using UnityEngine;
using FMODUnity;

public class LightSwitch : MonoBehaviour
{
    [Header("Lumieres")]
    public Light[] lights;

    [Header("Etat initial")]
    public bool allumeeAuDepart = true;

    [Header("Animation enfoncement")]
    public float distanceEnfoncement = 0.02f;
    public float vitesseEnfoncement = 10f;

    [Header("Objets X-Ray")]
    [Tooltip("Les objets qui passent en mode X-Ray quand les lumieres sont eteintes")]
    public Renderer[] objetsXRay;
    [Tooltip("Material X-Ray de base (sera instancie par objet)")]
    public Material materialXRay;
    [Tooltip("Couleur emissive de chaque objet en mode X-Ray (doit avoir le meme nombre d'elements que Objets X Ray)")]
    public Color[] couleursXRay;

    private Material[] _materialsNormaux;
    private Material[] _materialsXRayInstancies;
    [SerializeField] private EventReference sonBouton;

    private bool _allumee;
    private bool _enfonce = false;
    private Vector3 _positionRepos;
    private Vector3 _positionEnfoncee;

    private void Start()
    {
        _allumee = allumeeAuDepart;
        _positionRepos = transform.localPosition;
        _positionEnfoncee = _positionRepos + Vector3.right * distanceEnfoncement;

        // Récupère les materials normaux et crée une instance X-Ray par objet
        if (objetsXRay != null)
        {
            _materialsNormaux = new Material[objetsXRay.Length];
            _materialsXRayInstancies = new Material[objetsXRay.Length];
            for (int i = 0; i < objetsXRay.Length; i++)
            {
                if (objetsXRay[i] == null) continue;
                _materialsNormaux[i] = objetsXRay[i].material;

                // Instancie un material X-Ray unique par objet
                _materialsXRayInstancies[i] = new Material(materialXRay);

                // Applique la couleur si elle est renseignée
                if (couleursXRay != null && i < couleursXRay.Length)
                {
                    _materialsXRayInstancies[i].SetColor("_Emissive", couleursXRay[i]);
                }
            }
        }

        AppliquerEtat();
    }

    private void Update()
    {
        Vector3 cible = _enfonce ? _positionEnfoncee : _positionRepos;
        transform.localPosition = Vector3.Lerp(transform.localPosition, cible, Time.deltaTime * vitesseEnfoncement);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignore les colliders qui ne sont pas les mains
        if (!other.CompareTag("PlayerHand")) return;

        _enfonce = true;
        _allumee = !_allumee;
        AppliquerEtat();

        if (!sonBouton.IsNull)
            RuntimeManager.PlayOneShot(sonBouton, transform.position);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("PlayerHand")) return;
        _enfonce = false;
    }

    private void AppliquerEtat()
    {
        foreach (Light l in lights)
        {
            if (l != null) l.enabled = _allumee;
        }

        if (objetsXRay == null) return;
        for (int i = 0; i < objetsXRay.Length; i++)
        {
            if (objetsXRay[i] == null) continue;
            objetsXRay[i].material = _allumee ? _materialsNormaux[i] : _materialsXRayInstancies[i];
        }
    }
}