using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TurntablePlayButton : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TurntableBase turntableBase;

    [Header("Input")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float cooldown = 0.2f;

    [Header("Button Animation")]
    [Tooltip("Enfoncement le long de l'axe Z local (négatif = vers l'intérieur du bouton).")]
    [SerializeField] private float pressDepth = 0.01f;
    [SerializeField] private float pressSpeed = 12f;
    [Tooltip("Si coché : enfonce dans +Z local au lieu de -Z.")]
    [SerializeField] private bool invertPressLocalZ;

    [Header("Indice play (clignotement jaune)")]
    [Tooltip("Mesh du bouton. Vide = Renderer sur ce GameObject.")]
    [SerializeField] private Renderer buttonRenderer;
    [SerializeField] private Color hintPulseColor = new Color(1f, 0.92f, 0.2f);
    [SerializeField] private float hintEmissionIntensity = 1.2f;
    [SerializeField] private float hintPulseDuration = 0.35f;
    [SerializeField] private float hintPulseGap = 0.18f;

    private float _nextTime = 0f;
    private bool _isAnimating;
    private Vector3 _initialLocalPos;
    private Material _buttonMatInstance;
    private Color _buttonBaseColor = Color.white;
    private Color _buttonBaseEmission = Color.black;
    private bool _buttonHasBaseColor;
    private bool _buttonHasEmission;
    private Coroutine _hintPulseRoutine;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _initialLocalPos = transform.localPosition;

        if (turntableBase == null)
            turntableBase = GetComponentInParent<TurntableBase>();

        if (buttonRenderer == null)
            buttonRenderer = GetComponent<Renderer>();

        CacheButtonMaterial();
    }

    private void OnEnable()
    {
        if (_hintPulseRoutine != null)
            StopCoroutine(_hintPulseRoutine);
        _hintPulseRoutine = StartCoroutine(HintPulseLoop());
    }

    private void OnDisable()
    {
        if (_hintPulseRoutine != null)
        {
            StopCoroutine(_hintPulseRoutine);
            _hintPulseRoutine = null;
        }

        RestoreButtonVisualColor();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextTime) return;
        if (turntableBase == null) return;

        _nextTime = Time.time + cooldown;
        turntableBase.ToggleCurrentDiscPlayPause();

        if (!_isAnimating)
            StartCoroutine(PressAnimation());
    }

    private bool ShouldShowPlayHint()
    {
        if (turntableBase == null) return false;

        TurntableDisc disc = turntableBase.CurrentDisc;
        if (disc == null || !disc.IsDocked) return false;

        return !disc.HasStartedPlayback;
    }

    private IEnumerator HintPulseLoop()
    {
        var waitGap = new WaitForSeconds(Mathf.Max(0f, hintPulseGap));

        while (true)
        {
            if (ShouldShowPlayHint())
            {
                yield return PulseButtonColorOnce();
                if (hintPulseGap > 0f)
                    yield return waitGap;
            }
            else
            {
                RestoreButtonVisualColor();
                yield return null;
            }
        }
    }

    private IEnumerator PulseButtonColorOnce()
    {
        SetButtonVisualColor(hintPulseColor, hintEmissionIntensity);
        if (hintPulseDuration > 0f)
            yield return new WaitForSeconds(hintPulseDuration);
        RestoreButtonVisualColor();
    }

    private void CacheButtonMaterial()
    {
        if (buttonRenderer == null) return;

        _buttonMatInstance = buttonRenderer.material;
        _buttonHasBaseColor = _buttonMatInstance.HasProperty("_BaseColor");
        _buttonHasEmission = _buttonMatInstance.HasProperty("_EmissionColor");

        _buttonBaseColor = _buttonHasBaseColor
            ? _buttonMatInstance.GetColor("_BaseColor")
            : _buttonMatInstance.color;

        if (_buttonHasEmission)
        {
            _buttonBaseEmission = _buttonMatInstance.GetColor("_EmissionColor");
            _buttonMatInstance.EnableKeyword("_EMISSION");
        }
    }

    private void SetButtonVisualColor(Color c, float emissionMul)
    {
        if (_buttonMatInstance == null) return;

        if (_buttonHasBaseColor)
            _buttonMatInstance.SetColor("_BaseColor", c);
        else
            _buttonMatInstance.color = c;

        if (_buttonHasEmission)
            _buttonMatInstance.SetColor("_EmissionColor", c * emissionMul);
    }

    private void RestoreButtonVisualColor()
    {
        if (_buttonMatInstance == null) return;

        if (_buttonHasBaseColor)
            _buttonMatInstance.SetColor("_BaseColor", _buttonBaseColor);
        else
            _buttonMatInstance.color = _buttonBaseColor;

        if (_buttonHasEmission)
            _buttonMatInstance.SetColor("_EmissionColor", _buttonBaseEmission);
    }

    private IEnumerator PressAnimation()
    {
        _isAnimating = true;
        float sign = invertPressLocalZ ? 1f : -1f;
        Vector3 pressed = _initialLocalPos + Vector3.forward * (sign * pressDepth);

        while (Vector3.Distance(transform.localPosition, pressed) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, pressed, Time.deltaTime * pressSpeed);
            yield return null;
        }

        while (Vector3.Distance(transform.localPosition, _initialLocalPos) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, _initialLocalPos, Time.deltaTime * pressSpeed);
            yield return null;
        }

        transform.localPosition = _initialLocalPos;
        _isAnimating = false;
    }
}
