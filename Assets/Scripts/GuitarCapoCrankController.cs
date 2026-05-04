using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using FMODUnity;
using FMOD.Studio;

public class GuitarCapoCrankController : MonoBehaviour
{
    [Header("Guitar input (index trigger while held)")]
    [SerializeField] private XRGrabInteractable guitarGrabInteractable;

    [Header("Snap/cranks positions")]
    [Tooltip("Transforms représentant la position/rotation de chaque cran du capot (un par 'crans').")]
    [SerializeField] private Transform[] crankMarkers;

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

    [Header("Narration")]
    [SerializeField] private UnityEvent onChordSolved;

    [Header("Post-prisme (boucle jusqu’au dépôt socket)")]
    [Tooltip("Event FMOD en boucle (timeline/boucle côté Studio) dès que le prisme est complété ; arrêt via UnlockPlacementSocket (requiredGuitarPuzzle) ou StopPostPrismCompletionLoop().")]
    [SerializeField] private EventReference postPrismCompletionLoopEvent;
    [Tooltip("Suivi 3D de la boucle ; vide = même objet que la guitare XR.")]
    [SerializeField] private Transform postPrismLoopAttachOverride;
    [Tooltip("Particules / VFX : activé en même temps que la boucle FMOD post-prisme ; désactivé à l’arrêt (socket) ou OnDisable.")]
    [SerializeField] private GameObject postPrismLoopParticlesRoot;

    private XRGrabInteractable _guitar;
    private int _currentIndex;
    private bool _guitarHeld;
    private bool _solved;
    private bool _locked;

    private Material _matInstance;
    private Coroutine _successVisualRoutine;

    [Header("Index trigger (les deux mains)")]
    [SerializeField] private float indexTriggerThreshold = 0.65f;
    private bool _leftTriggerWasDown;
    private bool _rightTriggerWasDown;

    private EventInstance _postPrismLoopInstance;

    /// <summary>
    /// Instance FMOD de la boucle post-prisme (<c>BoucleGuitare</c> / <see cref="postPrismCompletionLoopEvent"/>),
    /// valide du démarrage de la boucle jusqu’à <see cref="StopPostPrismCompletionLoop"/>. Pour <see cref="FMODMeteringSource"/>.
    /// </summary>
    public EventInstance EventInstance => _postPrismLoopInstance;

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

        SetPostPrismLoopParticlesActive(false);
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
        }
    }

    private void OnDisable()
    {
        if (_guitar != null)
        {
            _guitar.selectEntered.RemoveListener(OnGuitarGrabbed);
            _guitar.selectExited.RemoveListener(OnGuitarReleased);
        }

        StopPostPrismCompletionLoop();
    }

    private void OnDestroy()
    {
        StopPostPrismCompletionLoop();
    }

    private void Update()
    {
        if (_solved && _locked) return;
        if (crankMarkers == null || crankMarkers.Length == 0) return;
        if (onlyAdvanceWhenGuitarHeld && !_guitarHeld) return;
        if (_locked) return;

        // Index trigger gauche ou droite : front montant pour avancer d'un cran.
        // (Les deux appels sont nécessaires chaque frame pour mettre à jour l'état wasDown.)
        bool leftEdge = TryIndexTriggerRisingEdge(XRNode.LeftHand, ref _leftTriggerWasDown);
        bool rightEdge = TryIndexTriggerRisingEdge(XRNode.RightHand, ref _rightTriggerWasDown);
        if (leftEdge || rightEdge)
        {
            int next = (_currentIndex + 1) % crankMarkers.Length;
            SetCrankIndex(next);
        }
    }

    private void OnGuitarGrabbed(SelectEnterEventArgs args)
    {
        _guitarHeld = true;
    }

    private void OnGuitarReleased(SelectExitEventArgs args)
    {
        _guitarHeld = false;
        _leftTriggerWasDown = false;
        _rightTriggerWasDown = false;
    }

    private bool TryIndexTriggerRisingEdge(XRNode node, ref bool wasDown)
    {
        InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
        if (!dev.isValid) return false;

        bool down = false;
        if (dev.TryGetFeatureValue(CommonUsages.triggerButton, out bool triggerButton))
            down |= triggerButton;

        if (dev.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
            down |= triggerValue >= indexTriggerThreshold;

        bool rising = down && !wasDown;
        wasDown = down;
        return rising;
    }

    public int CurrentCrankIndex => _currentIndex;
    public bool IsSolved => _solved;

    public void TrySolveFromPrismPuzzle()
    {
        // La résolution peut arriver à la fin d'une anim : la main n'est pas forcément sur la guitare.
        if (!TrySolveInternal(bypassGuitarHeldCheck: true))
            return;

        StartPostPrismCompletionLoopIfConfigured();
    }

    public void TrySolveFromSoundZone()
    {
        // Compatibilité rétro si un ancien UnityEvent appelle encore cette méthode.
        TrySolveInternal(bypassGuitarHeldCheck: false);
    }

    /// <summary>
    /// Appelé quand la guitare est posée sur sa socket (ex. <see cref="UnlockPlacementSocket"/>).
    /// </summary>
    public void StopPostPrismCompletionLoop()
    {
        SetPostPrismLoopParticlesActive(false);

        if (!_postPrismLoopInstance.isValid())
            return;

        _postPrismLoopInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _postPrismLoopInstance.release();
        _postPrismLoopInstance.clearHandle();
    }

    private void StartPostPrismCompletionLoopIfConfigured()
    {
        StopPostPrismCompletionLoop();
        if (postPrismCompletionLoopEvent.IsNull || !RuntimeManager.IsInitialized)
            return;

        _postPrismLoopInstance = RuntimeManager.CreateInstance(postPrismCompletionLoopEvent);
        GameObject attach = ResolvePostPrismLoopAttachGameObject();
        if (attach != null)
            RuntimeManager.AttachInstanceToGameObject(_postPrismLoopInstance, attach);
        _postPrismLoopInstance.start();
        SetPostPrismLoopParticlesActive(true);
    }

    private void SetPostPrismLoopParticlesActive(bool active)
    {
        if (postPrismLoopParticlesRoot == null)
            return;
        if (postPrismLoopParticlesRoot.activeSelf != active)
            postPrismLoopParticlesRoot.SetActive(active);
    }

    private GameObject ResolvePostPrismLoopAttachGameObject()
    {
        if (postPrismLoopAttachOverride != null)
            return postPrismLoopAttachOverride.gameObject;
        if (_guitar != null)
            return _guitar.gameObject;
        return gameObject;
    }

    /// <returns><see langword="true"/> si la résolution a été appliquée (première fois).</returns>
    private bool TrySolveInternal(bool bypassGuitarHeldCheck)
    {
        if (_solved)
            return false;

        bool allowed = bypassGuitarHeldCheck || (!onlyAdvanceWhenGuitarHeld || _guitarHeld);
        if (!allowed)
            return false;

        _solved = true;
        if (lockAfterSuccess) _locked = true;
        if (activateFaderOnSolve && thirdFaderToActivate != null)
            thirdFaderToActivate.SetActive(true);
        PlaySuccessBodyFlash();
        onChordSolved?.Invoke();
        return true;
    }

    private void OnValidate()
    {
        // Sécurité: toujours forcer la taille attendue des couleurs de cran
        // pour éviter les erreurs d'index dans l'inspecteur.
        if (crankColors == null || crankColors.Length != 5)
        {
            var old = crankColors;
            crankColors = new Color[5];
            if (old != null)
            {
                int len = Mathf.Min(old.Length, crankColors.Length);
                for (int i = 0; i < len; i++)
                    crankColors[i] = old[i];
            }
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
        ApplyCapoColorToMaterial(_matInstance, index);
    }

    /// <summary>
    /// Pour le puzzle prisme : matériau distinct avec la même apparence que le capot sur un cran donné (à Destroy après usage).
    /// </summary>
    public Material CreateCapoVisualMaterialForCrankIndex(int crankIndex)
    {
        ResolveCapoMaterialRenderer();
        if (capoRenderer == null)
            return null;

        Material src = capoRenderer.sharedMaterial;
        if (src == null)
            return null;

        var m = new Material(src);
        ApplyCapoColorToMaterial(m, crankIndex);
        return m;
    }

    private void ApplyCapoColorToMaterial(Material m, int index)
    {
        if (m == null)
            return;

        Color c = roseColor;
        if (crankColors != null && index >= 0 && index < crankColors.Length)
            c = crankColors[index];

        bool hasEmission = m.HasProperty("_EmissionColor");
        bool hasBaseColor = m.HasProperty("_BaseColor");

        if (hasBaseColor)
            m.SetColor("_BaseColor", c);
        else
            m.color = c;

        if (hasEmission)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emissionIntensity);
        }
    }
}

