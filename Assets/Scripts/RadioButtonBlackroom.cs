using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Bouton physique ON/OFF pour la radio de la black room (onboarding).
/// Pilote un <see cref="StudioEventEmitter"/> (play/stop) et la teinte du mesh radio (comme le standby du salon).
/// </summary>
[RequireComponent(typeof(Collider))]
public class RadioButtonBlackroom : MonoBehaviour
{
    [Header("Radio")]
    [SerializeField] private StudioEventEmitter radioEmitter;
    [Tooltip("Partie visuelle de la radio (ex. mesh noir/blanc).")]
    [SerializeField] private Renderer radioVisualRenderer;
    [SerializeField] private Color colorRadioOn = Color.white;
    [SerializeField] private Color colorRadioOff = Color.black;

    [Header("Input")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float cooldown = 0.2f;

    [Header("Button animation")]
    [Tooltip("Enfoncement (Y local) quand la radio est allumée.")]
    [SerializeField] private float onPressedDepth = 0.01f;
    [Tooltip("Enfoncement (Y local) quand la radio est éteinte.")]
    [SerializeField] private float offPressedDepth = 0.02f;
    [SerializeField] private float pressSpeed = 12f;

    [Header("Indice (canvas flèche)")]
    [Tooltip("Canvas au-dessus du bouton (souvent frère sous BoutonRadio). Visible + léger bobbing tant que la radio est éteinte.")]
    [SerializeField] private GameObject hintCanvasRoot;
    [SerializeField] private float hintBobAmplitude = 0.015f;
    [SerializeField] private float hintBobSpeed = 2.5f;

    private bool _isRadioOn;
    private float _nextPressTime;
    private Vector3 _initialLocalPos;
    private Vector3 _hintCanvasBaseLocalPos;
    private bool _hintCanvasBaseCaptured;

    public bool IsRadioOn => _isRadioOn;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _initialLocalPos = transform.localPosition;

        if (hintCanvasRoot != null)
        {
            _hintCanvasBaseLocalPos = hintCanvasRoot.transform.localPosition;
            _hintCanvasBaseCaptured = true;
        }
    }

    private void Start()
    {
        ResetRadioToOff();
    }

    private void OnDisable()
    {
        StopRadioEmitterCompletely();
    }

    private void OnDestroy()
    {
        StopRadioEmitterCompletely();
    }

    /// <summary>État initial + après reload de scène : aucune lecture tant que la radio est « off ».</summary>
    private void ResetRadioToOff()
    {
        StopRadioEmitterCompletely();
        _isRadioOn = false;
        ApplyRadioVisual(false);
        UpdateHintCanvas();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextPressTime) return;
        if (radioEmitter == null) return;

        _nextPressTime = Time.time + cooldown;
        ToggleRadio();
    }

    private void Update()
    {
        Vector3 target = GetTargetLocalPosition();
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * pressSpeed);
        UpdateHintCanvas();
    }

    public void ToggleRadio()
    {
        ApplyRadioState(!_isRadioOn);
    }

    public void SetRadioOn(bool on)
    {
        if (_isRadioOn == on) return;
        ApplyRadioState(on);
    }

    private void ApplyRadioState(bool on)
    {
        if (on)
        {
            _isRadioOn = true;
            EnsureSingleEmitterPlayingAtFullVolume();
            ApplyRadioVisual(true);
        }
        else
        {
            _isRadioOn = false;
            StopRadioEmitterCompletely();
            ApplyRadioVisual(false);
        }

        UpdateHintCanvas();
    }

    private void EnsureSingleEmitterPlayingAtFullVolume()
    {
        if (radioEmitter == null) return;

        EventInstance inst = radioEmitter.EventInstance;
        bool needsPlay = true;
        if (inst.isValid())
        {
            inst.getPlaybackState(out PLAYBACK_STATE state);
            needsPlay = state == PLAYBACK_STATE.STOPPED;
        }

        if (needsPlay)
            radioEmitter.Play();

        inst = radioEmitter.EventInstance;
        if (inst.isValid())
            inst.setVolume(1f);
    }

    private void StopRadioEmitterCompletely()
    {
        if (radioEmitter == null) return;
        radioEmitter.Stop();
    }

    private void ApplyRadioVisual(bool on)
    {
        if (radioVisualRenderer == null) return;

        Color c = on ? colorRadioOn : colorRadioOff;
        Material mat = radioVisualRenderer.material;
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
        else
            mat.color = c;

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", c);
    }

    private Vector3 GetTargetLocalPosition()
    {
        float depth = _isRadioOn ? onPressedDepth : offPressedDepth;
        return _initialLocalPos - new Vector3(0f, depth, 0f);
    }

    private void UpdateHintCanvas()
    {
        if (hintCanvasRoot == null) return;

        bool showHint = !_isRadioOn;
        if (hintCanvasRoot.activeSelf != showHint)
            hintCanvasRoot.SetActive(showHint);

        if (!showHint) return;

        if (!_hintCanvasBaseCaptured)
        {
            _hintCanvasBaseLocalPos = hintCanvasRoot.transform.localPosition;
            _hintCanvasBaseCaptured = true;
        }

        float bob = Mathf.Sin(Time.time * hintBobSpeed) * hintBobAmplitude;
        hintCanvasRoot.transform.localPosition = _hintCanvasBaseLocalPos + new Vector3(0f, bob, 0f);
    }
}
