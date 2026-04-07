using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FMODUnity;

public class GuitarCapoCrankController : MonoBehaviour
{
    [Header("Guitar input (index trigger while held)")]
    [SerializeField] private XRGrabInteractable guitarGrabInteractable;

    [Header("Snap/cranks positions")]
    [Tooltip("Transforms représentant la position/rotation de chaque cran du capot (un par 'crans').")]
    [SerializeField] private Transform[] crankMarkers;

    [Header("Target (rose)")]
    [Tooltip("Index du cran 'rose' (couleur cible).")]
    [SerializeField] private int targetCrankIndex = 0;

    [Header("Start")]
    [Tooltip("Cran de départ du capot. Pour 'position 1' -> index 0.")]
    [SerializeField] private int startCrankIndex = 0;

    [Header("Visuals (spectrum color + emission)")]
    [Tooltip("Mesh à teinter (enfant du capot). Laisser vide : premier Renderer trouvé sous ce GameObject, hors la racine.")]
    [SerializeField] private Renderer capoRenderer;
    [SerializeField] private float emissionIntensity = 3f;
    [Tooltip("Couleurs fixes par cran (taille = nombre de crans, ici 5).")]
    [SerializeField] private Color[] crankColors = new Color[5];
    [Tooltip("Couleur rose (si crankColors est vide ou si index hors plage).")]
    [SerializeField] private Color roseColor = new Color(1f, 0.2f, 0.9f);

    [Header("Success / lock")]
    [Tooltip("GameObject du 3e fader à activer dans la salle principale.")]
    [SerializeField] private GameObject thirdFaderToActivate;
    [SerializeField] private bool lockAfterSuccess = true;
    [SerializeField] private bool activateFaderOnSolve = false;

    [Header("Success visual")]
    [Tooltip("Renderer du body de guitare (ex: enfant 'GuitarBody') à teinter en jaune à la réussite.")]
    [SerializeField] private Renderer guitarBodyRenderer;
    [SerializeField] private Color successBodyColor = Color.yellow;
    [SerializeField] private Color successFlashColor = new Color(1f, 1f, 0.35f);
    [SerializeField] private float successFlashDuration = 0.25f;
    [SerializeField] private float successFadeToSoftDuration = 0.45f;

    [Header("Gameplay")]
    [SerializeField] private bool onlyAdvanceWhenGuitarHeld = true;

    private XRGrabInteractable _guitar;
    private int _currentIndex;
    private bool _guitarHeld;
    private bool _solved;
    private bool _locked;

    private IXRSelectInteractor _holdingInteractor;
    private Material _matInstance;
    private Coroutine _successVisualRoutine;

    private void Awake()
    {
        _guitar = guitarGrabInteractable;
        if (_guitar == null)
            _guitar = FindFirstObjectByType<XRGrabInteractable>();

        // Le capot n'est pas manipulé directement : on évite qu'il tombe.
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        ResolveCapoMaterialRenderer();

        if (capoRenderer != null)
        {
            _matInstance = capoRenderer.material;
            if (_matInstance != null)
                _matInstance.EnableKeyword("_EMISSION");
        }

        int len = crankMarkers != null ? crankMarkers.Length : 0;
        int maxIndex = Mathf.Max(0, len - 1);
        _currentIndex = Mathf.Clamp(startCrankIndex, 0, maxIndex);
        ApplyIndex(_currentIndex, instant: true);
    }

    /// <summary>
    /// Le script est souvent sur la racine du capot (empty) : le mesh visible est sur un enfant.
    /// Si aucun renderer n'est assigné, on prend le premier Renderer descendant qui n'est pas sur la racine.
    /// </summary>
    private void ResolveCapoMaterialRenderer()
    {
        if (capoRenderer != null && capoRenderer.transform != transform)
            return;

        capoRenderer = null;
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r.transform == transform) continue;
            capoRenderer = r;
            return;
        }
    }

    private void OnEnable()
    {
        if (_guitar != null)
        {
            _guitar.selectEntered.AddListener(OnGuitarGrabbed);
            _guitar.selectExited.AddListener(OnGuitarReleased);
            _guitar.activated.AddListener(OnGuitarActivated);
        }
    }

    private void OnDisable()
    {
        if (_guitar != null)
        {
            _guitar.selectEntered.RemoveListener(OnGuitarGrabbed);
            _guitar.selectExited.RemoveListener(OnGuitarReleased);
            _guitar.activated.RemoveListener(OnGuitarActivated);
        }
    }

    private void OnGuitarGrabbed(SelectEnterEventArgs args)
    {
        _guitarHeld = true;
        _holdingInteractor = args.interactorObject;
    }

    private void OnGuitarReleased(SelectExitEventArgs args)
    {
        _guitarHeld = false;
        _holdingInteractor = null;
    }

    private void OnGuitarActivated(ActivateEventArgs args)
    {
        if (_solved && _locked) return;
        if (crankMarkers == null || crankMarkers.Length == 0) return;

        // Ne réagit qu'aux activations faites par l'interactor qui tient la guitare.
        if (onlyAdvanceWhenGuitarHeld && !_guitarHeld) return;

        if (_locked) return;

        // Avance au cran suivant à chaque pression.
        int next = (_currentIndex + 1) % crankMarkers.Length;
        SetCrankIndex(next);
    }

    public bool IsOnTargetCrank => !_locked && _currentIndex == targetCrankIndex;
    public bool IsOnTargetCrankEvenIfLocked => _currentIndex == targetCrankIndex;
    public int CurrentCrankIndex => _currentIndex;
    public bool IsSolved => _solved;

    public void TrySolveFromSoundZone()
    {
        if (_solved) return;
        if (lockAfterSuccess == false)
        {
            // Si tu veux juste un feedback sans lock, laisse lockAfterSuccess=false.
        }

        bool isTarget = _currentIndex == targetCrankIndex;
        bool allowed = (!_guitarHeld && onlyAdvanceWhenGuitarHeld) ? false : true;

        if (isTarget && allowed)
        {
            _solved = true;
            if (lockAfterSuccess) _locked = true;
            if (activateFaderOnSolve && thirdFaderToActivate != null)
                thirdFaderToActivate.SetActive(true);
            PlaySuccessBodyFlash();
        }
    }

    private void PlaySuccessBodyFlash()
    {
        if (guitarBodyRenderer == null) return;
        if (_successVisualRoutine != null)
            StopCoroutine(_successVisualRoutine);
        _successVisualRoutine = StartCoroutine(SuccessBodyFlashRoutine());
    }

    private System.Collections.IEnumerator SuccessBodyFlashRoutine()
    {
        if (guitarBodyRenderer == null) yield break;

        var mat = guitarBodyRenderer.material;
        if (mat == null) yield break;

        // Flash vif immédiat.
        SetBodyMaterialColor(mat, successFlashColor);
        if (successFlashDuration > 0f)
            yield return new WaitForSeconds(successFlashDuration);

        // Transition vers jaune plus doux.
        float t = 0f;
        float duration = Mathf.Max(0.01f, successFadeToSoftDuration);
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / duration);
            Color c = Color.Lerp(successFlashColor, successBodyColor, a);
            SetBodyMaterialColor(mat, c);
            yield return null;
        }

        SetBodyMaterialColor(mat, successBodyColor);
        _successVisualRoutine = null;
    }

    private void SetBodyMaterialColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", color);
    }

    public void SetCrankIndex(int index, bool instant = false)
    {
        if (crankMarkers == null || crankMarkers.Length == 0) return;
        _currentIndex = Mathf.Clamp(index, 0, crankMarkers.Length - 1);
        ApplyIndex(_currentIndex, instant);
    }

    private void ApplyIndex(int index, bool instant)
    {
        if (crankMarkers == null || index < 0 || index >= crankMarkers.Length) return;
        var m = crankMarkers[index];
        if (m == null) return;

        // On copie pose du marker.
        transform.position = m.position;
        transform.rotation = m.rotation;

        UpdateColor(index);
    }

    private void UpdateColor(int index)
    {
        if (_matInstance == null) return;

        bool hasEmission = _matInstance.HasProperty("_EmissionColor");
        bool hasBaseColor = _matInstance.HasProperty("_BaseColor");

        Color c = roseColor;
        if (crankColors != null && index >= 0 && index < crankColors.Length)
            c = crankColors[index];

        ApplyMaterialColor(c, hasBaseColor, hasEmission);
    }

    private void ApplyMaterialColor(Color c, bool hasBaseColor, bool hasEmission)
    {
        // Base color (URP/HDRP peuvent utiliser _BaseColor au lieu de .color).
        if (hasBaseColor)
            _matInstance.SetColor("_BaseColor", c);
        else
            _matInstance.color = c;

        if (hasEmission)
            _matInstance.SetColor("_EmissionColor", c * emissionIntensity);
    }
}

