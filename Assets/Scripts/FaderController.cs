using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FaderController : MonoBehaviour
{
    public enum FaderType { Violons, Guitare, Bass }

    [Header("FMOD")]
    public FaderType faderType;
    public MusicManagerScript musicManager;

    [Header("Rfrences")]
    public Transform faderBase;
    public float railHalfLength = 0.30f;

    [Header("Valeur courante du fader")]
    [Range(0f, 1f)]
    public float value = 0f;

    [Header("Haptique cible")]
    [SerializeField] private bool enableTargetHaptics = true;
    [Range(0f, 1f)] [SerializeField] private float targetValue = 0.5f;
    [Range(0.001f, 0.2f)] [SerializeField] private float targetTolerance = 0.03f;
    [Range(0f, 1f)] [SerializeField] private float hapticAmplitude = 0.75f;
    [SerializeField] private float hapticDuration = 0.06f;

    [Header("Feedback visuel cible")]
    [SerializeField] private bool enableTargetVisualFeedback = true;
    [Tooltip("Si dfini (ou trouv sur un parent) : la teinte  proche de la cible  nest active que quand les trois faders du groupe sont actifs.")]
    [SerializeField] private FinalSequenceController finalSequenceController;
    [SerializeField] private bool gateTargetVisualFeedbackUntilAllFadersActive = true;
    [Tooltip("Renderer  teinter (si vide, premier Renderer trouv sous ce fader).")]
    [SerializeField] private Renderer targetFeedbackRenderer;
    [Tooltip("Base de teinte quand on est loin de la cible.")]
    [SerializeField] private Color feedbackBaseColor = Color.white;
    [Tooltip("Couleur ddie  ce fader uniquement (ex: vert pour le fader vert).")]
    [SerializeField] private Color targetFeedbackColor = Color.green;
    [Tooltip("Distance (en valeur de fader 0..1)  partir de laquelle le feedback commence  devenir visible.")]
    [Range(0.01f, 1f)] [SerializeField] private float visualFeedbackRange = 0.25f;
    [Range(0f, 5f)] [SerializeField] private float visualEmissionIntensity = 1.2f;

    private XRGrabInteractable _grab;
    private bool _isGrabbed = false;
    private float _lockedLocalY;
    private IXRSelectInteractor _activeInteractor;
    private bool _wasInsideTarget;
    private Material _feedbackMaterial;
    private Color _feedbackBaseCapturedColor = Color.white;
    private bool _isBroken;

    public void SetBrokenState(bool broken)
    {
        _isBroken = broken;
    }

    public void ConfigureTargetHaptics(float desiredTargetValue, float desiredTolerance)
    {
        targetValue = Mathf.Clamp01(desiredTargetValue);
        targetTolerance = Mathf.Clamp(desiredTolerance, 0.001f, 0.2f);
        _wasInsideTarget = Mathf.Abs(value - targetValue) <= targetTolerance;
        UpdateTargetVisualFeedback();
    }

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.trackPosition = false;
        _grab.throwOnDetach = false;
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);

        _lockedLocalY = faderBase.InverseTransformPoint(transform.position).y;

        if (finalSequenceController == null)
            finalSequenceController = FindFirstObjectByType<FinalSequenceController>();

        ResolveFeedbackRendererAndMaterial();
    }

    public void RefreshTargetVisualFeedback() => UpdateTargetVisualFeedback();

    private void OnEnable()
    {
        // Quand le fader apparat (SetActive true), on pousse tout de suite sa valeur vers FMOD
        // pour viter d'avoir une piste muette tant qu'on ne l'a pas grab.
        ConstrainToRail();
        ApplyValueToMusic();
        UpdateTargetVisualFeedback();
    }

    void Update()
    {
        if (!_isGrabbed) return;

        Vector3 handWorldPos = _grab.interactorsSelecting[0].GetAttachTransform(_grab).position;
        Vector3 localPos = faderBase.InverseTransformPoint(handWorldPos);

        localPos.y = _lockedLocalY;
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);

        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);

        UpdateTargetHaptics();
        UpdateTargetVisualFeedback();
        ApplyValueToMusic();
    }

    void ConstrainToRail()
    {
        Vector3 localPos = faderBase.InverseTransformPoint(transform.position);
        localPos.y = _lockedLocalY;
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);
        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);
        UpdateTargetVisualFeedback();
    }

    private void ApplyValueToMusic()
    {
        if (_isBroken) return;
        if (musicManager == null) return;

        switch (faderType)
        {
            case FaderType.Violons:
                musicManager.SetVolumeViolons(value);
                break;
            case FaderType.Guitare:
                musicManager.SetVolumeGuitare(value);
                break;
            case FaderType.Bass:
                musicManager.SetVolumeBass(value);
                break;
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _isGrabbed = true;
        _activeInteractor = args.interactorObject;
        _wasInsideTarget = Mathf.Abs(value - targetValue) <= targetTolerance;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _isGrabbed = false;
        _activeInteractor = null;
        _wasInsideTarget = false;
        ConstrainToRail();
        ApplyValueToMusic();
        UpdateTargetVisualFeedback();
    }

    private void UpdateTargetHaptics()
    {
        if (_isBroken) return;
        if (!enableTargetHaptics || !_isGrabbed) return;

        bool inside = Mathf.Abs(value - targetValue) <= targetTolerance;
        if (inside && !_wasInsideTarget)
            SendHapticToCurrentInteractor(hapticAmplitude, hapticDuration);

        _wasInsideTarget = inside;
    }

    private void SendHapticToCurrentInteractor(float amplitude, float duration)
    {
        float amp = Mathf.Clamp01(amplitude);
        float dur = Mathf.Max(0.01f, duration);

        if (_activeInteractor is XRBaseInputInteractor inputInteractor)
            inputInteractor.SendHapticImpulse(amp, dur);

        Transform t = _activeInteractor != null ? _activeInteractor.transform : null;
        if (t == null)
        {
            // Last resort: try both hands to avoid silent failure on fallback runtimes.
            if (TrySendToNode(XRNode.LeftHand, amp, dur))
                return;
            TrySendToNode(XRNode.RightHand, amp, dur);
            return;
        }

        if (NameLooksLeft(t))
        {
            TrySendToNode(XRNode.LeftHand, amp, dur);
            return;
        }

        if (NameLooksRight(t))
        {
            TrySendToNode(XRNode.RightHand, amp, dur);
            return;
        }

        if (TrySendToNode(XRNode.LeftHand, amp, dur))
            return;
        TrySendToNode(XRNode.RightHand, amp, dur);
    }

    private static bool NameLooksLeft(Transform t)
    {
        if (t == null) return false;
        if (ContainsInHierarchy(t, "Left Controller")) return true;
        return false;
    }

    private static bool NameLooksRight(Transform t)
    {
        if (t == null) return false;
        if (ContainsInHierarchy(t, "Right Controller")) return true;
        return false;
    }

    private static bool ContainsInHierarchy(Transform t, string token)
    {
        Transform current = t;
        while (current != null)
        {
            if (current.name.Contains(token))
                return true;
            current = current.parent;
        }
        return false;
    }

    private static bool TrySendToNode(XRNode node, float amp, float dur)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        if (!device.TryGetHapticCapabilities(out HapticCapabilities caps)) return false;
        if (!caps.supportsImpulse || caps.numChannels <= 0) return false;
        return device.SendHapticImpulse(0u, amp, dur);
    }

    private void ResolveFeedbackRendererAndMaterial()
    {
        if (!Application.isPlaying)
            return;
        if (targetFeedbackRenderer == null)
            targetFeedbackRenderer = GetComponentInChildren<Renderer>(true);
        if (targetFeedbackRenderer == null)
            return;

        _feedbackMaterial = targetFeedbackRenderer.material;
        if (_feedbackMaterial == null)
            return;

        if (_feedbackMaterial.HasProperty("_BaseColor"))
            _feedbackBaseCapturedColor = _feedbackMaterial.GetColor("_BaseColor");
        else
            _feedbackBaseCapturedColor = _feedbackMaterial.color;
    }

    private bool ShouldApplyTargetVisualFeedback()
    {
        if (!gateTargetVisualFeedbackUntilAllFadersActive)
            return true;
        if (finalSequenceController == null)
            finalSequenceController = FindFirstObjectByType<FinalSequenceController>();
        if (finalSequenceController == null)
            return false;
        return finalSequenceController.AreAllMixFadersActiveInHierarchy;
    }

    private void RestoreTargetVisualBaseColor()
    {
        if (_feedbackMaterial == null)
            return;

        Color baseColor = feedbackBaseColor;
        if (baseColor == default)
            baseColor = _feedbackBaseCapturedColor;

        if (_feedbackMaterial.HasProperty("_BaseColor"))
            _feedbackMaterial.SetColor("_BaseColor", baseColor);
        else
            _feedbackMaterial.color = baseColor;

        if (_feedbackMaterial.HasProperty("_EmissionColor"))
            _feedbackMaterial.SetColor("_EmissionColor", Color.black);
    }

    private void UpdateTargetVisualFeedback()
    {
        if (!Application.isPlaying)
            return;
        if (_isBroken)
            return;
        if (!enableTargetVisualFeedback)
            return;
        if (_feedbackMaterial == null)
            ResolveFeedbackRendererAndMaterial();
        if (_feedbackMaterial == null)
            return;

        if (!ShouldApplyTargetVisualFeedback())
        {
            RestoreTargetVisualBaseColor();
            return;
        }

        float distance = Mathf.Abs(value - targetValue);
        float range = Mathf.Max(targetTolerance, visualFeedbackRange);
        float t = 1f - Mathf.Clamp01(distance / range);
        Color targetColor = targetFeedbackColor;
        Color baseColor = feedbackBaseColor;

        if (baseColor == default)
            baseColor = _feedbackBaseCapturedColor;

        Color c = Color.Lerp(baseColor, targetColor, t);
        if (_feedbackMaterial.HasProperty("_BaseColor"))
            _feedbackMaterial.SetColor("_BaseColor", c);
        else
            _feedbackMaterial.color = c;

        if (_feedbackMaterial.HasProperty("_EmissionColor"))
        {
            _feedbackMaterial.EnableKeyword("_EMISSION");
            _feedbackMaterial.SetColor("_EmissionColor", c * (t * visualEmissionIntensity));
        }
    }

}
