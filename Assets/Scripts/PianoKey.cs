using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Collider))]
public class PianoKey : MonoBehaviour
{
    [Header("Id de la touche (1..5)")]
    [SerializeField] private int keyId = 1;

    [Header("Déclenchement")]
    [Tooltip("Tag de la main qui touche la touche")]
    [SerializeField] private string handTag = "PlayerHand";
    [Tooltip("Temps mini entre deux presses (évite spam si la main reste dedans)")]
    [SerializeField] private float pressCooldown = 0.25f;

    [Header("Audio FMOD")]
    [SerializeField] private EventReference noteEvent;
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f;

    [Header("Visuel (couleur non-emissive)")]
    [SerializeField] private Renderer keyRenderer;
    [SerializeField] private Color pressColor = Color.white;
    [SerializeField] private float colorFlashDuration = 0.15f;

    [Header("Animation press")]
    [SerializeField] private float pressDepth = 0.01f;
    [SerializeField] private float pressDownTime = 0.05f;
    [SerializeField] private float returnTime = 0.08f;

    [Header("Manager")]
    [SerializeField] private PianoPuzzleManager puzzleManager;

    private Material _matInstance;
    private Color _baseColor = Color.white;
    private Vector3 _initialLocalPos;
    private bool _isInteractable = true;
    private float _nextAllowedPressTime = 0f;
    private Coroutine _animRoutine;
    private Coroutine _colorRoutine;
    private Rigidbody _rb;

    public int KeyId => keyId;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        _initialLocalPos = transform.localPosition;
        _rb = GetComponent<Rigidbody>();

        if (keyRenderer != null)
        {
            _matInstance = keyRenderer.material;
            _baseColor = _matInstance.color;
        }
    }

    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isInteractable) return;
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextAllowedPressTime) return;

        _nextAllowedPressTime = Time.time + pressCooldown;
        Press();
    }

    private void Press()
    {
        // Audio
        if (!noteEvent.IsNull)
        {
            var inst = RuntimeManager.CreateInstance(noteEvent);
            inst.setVolume(volume);
            // Évite le warning FMOD "set3DAttributes" : on attache l'instance au GameObject.
            RuntimeManager.AttachInstanceToGameObject(inst, gameObject);
            inst.start();
            inst.release();
        }

        // Visuels
        if (_animRoutine != null) StopCoroutine(_animRoutine);
        _animRoutine = StartCoroutine(PressAnim());

        if (_matInstance != null)
        {
            if (_colorRoutine != null) StopCoroutine(_colorRoutine);
            _colorRoutine = StartCoroutine(ColorFlash());
        }

        // Puzzle
        if (puzzleManager != null)
            puzzleManager.OnKeyPressed(this);
    }

    private IEnumerator PressAnim()
    {
        // Enfoncement vers le bas (axe local Y négatif)
        Vector3 downPos = _initialLocalPos + new Vector3(0f, -pressDepth, 0f);

        float t = 0f;
        while (t < pressDownTime)
        {
            t += Time.deltaTime;
            float a = pressDownTime <= 0f ? 1f : Mathf.Clamp01(t / pressDownTime);
            transform.localPosition = Vector3.Lerp(_initialLocalPos, downPos, a);
            yield return null;
        }
        transform.localPosition = downPos;

        t = 0f;
        while (t < returnTime)
        {
            t += Time.deltaTime;
            float a = returnTime <= 0f ? 1f : Mathf.Clamp01(t / returnTime);
            transform.localPosition = Vector3.Lerp(downPos, _initialLocalPos, a);
            yield return null;
        }
        transform.localPosition = _initialLocalPos;
    }

    private IEnumerator ColorFlash()
    {
        _matInstance.color = pressColor;
        yield return new WaitForSeconds(colorFlashDuration);
        _matInstance.color = _baseColor;
    }
}

