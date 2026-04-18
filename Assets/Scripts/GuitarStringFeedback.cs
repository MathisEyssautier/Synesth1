using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;

[RequireComponent(typeof(XRGrabInteractable))]
public class GuitarStringFeedback : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference stringEvent;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Color activeColor = Color.cyan;
    [SerializeField] private float blinkDuration = 1f;

    [Header("Canvas")]
    [SerializeField] private GameObject canvasRoot;

    [Header("Placement on guitar")]
    [Tooltip("Index de la corde (0..5). Doit correspondre à l'entrée dans GuitarAssemblyManager.")]
    [SerializeField] private int stringIndex = 0;
    [SerializeField] private GuitarAssemblyManager guitarAssemblyManager;
    [SerializeField] private string guitarRootNameHint = "GUITARE";
    [SerializeField] private ParticleVFXAmplitude vfxAmplitude;


    private XRGrabInteractable _grab;
    private Material _baseMaterial;
    private Material[] _baseMaterials;
    private Material[] _activeMaterials;
    private Material _matInstance;
    private Color _baseColor = Color.white;
    private Coroutine _feedbackRoutine;
    private bool _placedOnGuitar;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.activated.AddListener(OnActivated);

        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (targetRenderer != null)
        {
            _baseMaterial = targetRenderer.sharedMaterial;
            _baseMaterials = targetRenderer.sharedMaterials;
            _matInstance = targetRenderer.material;
            _baseColor = _matInstance.HasProperty("_BaseColor")
                ? _matInstance.GetColor("_BaseColor")
                : _matInstance.color;

            // Prépare un tableau "full active material" pour tous les slots.
            if (activeMaterial != null && _baseMaterials != null && _baseMaterials.Length > 0)
            {
                _activeMaterials = new Material[_baseMaterials.Length];
                for (int i = 0; i < _activeMaterials.Length; i++)
                    _activeMaterials[i] = activeMaterial;
            }
        }

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_grab != null)
            _grab.activated.RemoveListener(OnActivated);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        // On ne réagit que si la corde est réellement tenue.
        if (_placedOnGuitar) return;
        if (_grab == null || !_grab.isSelected) return;

        if (!stringEvent.IsNull)
            RuntimeManager.PlayOneShotAttached(stringEvent, gameObject);

        if (_feedbackRoutine != null)
            StopCoroutine(_feedbackRoutine);
        _feedbackRoutine = StartCoroutine(FeedbackRoutine());
        if (vfxAmplitude != null)
        {
            vfxAmplitude.TriggerAmplitudePulse(50f, 1f);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryPlaceOnGuitar(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryPlaceOnGuitar(collision);
    }

    private void TryPlaceOnGuitar(Collision collision)
    {
        if (_placedOnGuitar) return;
        if (collision == null) return;
        Collider other = collision.collider;
        if (other == null) return;

        var manager = guitarAssemblyManager;
        if (manager == null)
            manager = other.GetComponentInParent<GuitarAssemblyManager>();

        if (manager == null && !string.IsNullOrEmpty(guitarRootNameHint))
        {
            Transform p = other.transform;
            while (p != null)
            {
                if (p.name == guitarRootNameHint)
                {
                    manager = p.GetComponentInChildren<GuitarAssemblyManager>();
                    break;
                }
                p = p.parent;
            }
        }

        if (manager == null) return;

        // IMPORTANT: même si un manager est assigné dans l'inspecteur,
        // on ne valide le placement que si le contact vient bien d'un collider
        // de la guitare (ou d'un enfant de sa hiérarchie).
        if (!IsColliderFromGuitar(other, manager.transform))
            return;

        if (manager.TryPlaceString(stringIndex, gameObject))
            _placedOnGuitar = true;
    }

    private static bool IsColliderFromGuitar(Collider other, Transform guitarRoot)
    {
        if (other == null || guitarRoot == null) return false;
        return other.transform.IsChildOf(guitarRoot);
    }

    private IEnumerator FeedbackRoutine()
    {
        SetVisualState(true);
        if (blinkDuration > 0f)
            yield return new WaitForSeconds(blinkDuration);
        SetVisualState(false);
        _feedbackRoutine = null;
    }

    private void SetVisualState(bool active)
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(active);

        if (_matInstance != null)
        {
            // Priorité : swap de material complet.
            if (activeMaterial != null && _baseMaterials != null && _baseMaterials.Length > 0)
            {
                // Gère aussi le cas multi-submesh (plusieurs slots de materials).
                if (active)
                    targetRenderer.materials = _activeMaterials;
                else
                    targetRenderer.materials = _baseMaterials;
                return;
            }

            // Fallback : teinte couleur si aucun material actif assigné.
            Color c = active ? activeColor : _baseColor;
            if (_matInstance.HasProperty("_BaseColor"))
                _matInstance.SetColor("_BaseColor", c);
            else
                _matInstance.color = c;
        }
    }
}
