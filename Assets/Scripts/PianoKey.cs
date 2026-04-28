using System.Collections;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(Collider))]
public class PianoKey : MonoBehaviour
{
    public static event System.Action<PianoKey> OnAnyKeyPressed;
    [Header("Id de la touche (1..5)")]
    [SerializeField] private int keyId = 1;

    [Header("Déclenchement")]
    [Tooltip("Tag de la main qui touche la touche")]
    [SerializeField] private string handTag = "PlayerHand";
    [Tooltip("Temps mini entre deux presses (évite spam si la main reste dedans). Minimum forcé: 1 seconde.")]
    [SerializeField] private float pressCooldown = 0.25f;

    [Header("Audio FMOD")]
    [SerializeField] private EventReference noteEvent;
    [SerializeField] private ParticleVFXAmplitude vfxAmplitude;
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
    private Collider _keyCollider;
    private readonly Collider[] _overlapResults = new Collider[16];
    private const float MinPressCooldown = 1f;
    private EventInstance _lastEventInstance;

    public int KeyId => keyId;
    public EventInstance EventInstance => _lastEventInstance;

    private void Awake()
    {
        _keyCollider = GetComponent<Collider>();
        _initialLocalPos = transform.localPosition;
        _rb = GetComponent<Rigidbody>();

        if (keyRenderer != null)
        {
            _matInstance = keyRenderer.material;
            _baseColor = _matInstance.color;
        }
    }

    private void Update()
    {
        // Fallback robuste: détecte la main par overlap physique même si les callbacks Trigger/Collision
        // ne sont pas envoyés (cas XR + rigidbodies kinematic).
        TryPressFromOverlap();
    }

    public void SetInteractable(bool interactable)
    {
        _isInteractable = interactable;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryPressFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryPressFromCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null) return;
        TryPressFromCollider(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision == null) return;
        TryPressFromCollider(collision.collider);
    }

    private void TryPressFromCollider(Collider other)
    {
        if (other == null) return;
        if (!_isInteractable) return;
        if (!IsHandCollider(other)) return;
        if (Time.time < _nextAllowedPressTime) return;

        _nextAllowedPressTime = Time.time + Mathf.Max(MinPressCooldown, pressCooldown);
        Press();
    }

    private bool IsHandCollider(Collider other)
    {
        if (other == null || string.IsNullOrEmpty(handTag)) return false;
        if (other.CompareTag(handTag)) return true;

        Rigidbody rb = other.attachedRigidbody;
        if (rb != null)
        {
            if (rb.CompareTag(handTag)) return true;
            if (rb.transform != null && rb.transform.root != null && rb.transform.root.CompareTag(handTag))
                return true;
        }

        if (other.transform != null)
        {
            if (other.transform.CompareTag(handTag)) return true;
            if (other.transform.root != null && other.transform.root.CompareTag(handTag)) return true;
        }

        return false;
    }

    private void TryPressFromOverlap()
    {
        if (_keyCollider == null) return;
        if (!_isInteractable) return;
        if (Time.time < _nextAllowedPressTime) return;

        Bounds b = _keyCollider.bounds;
        if (b.extents.sqrMagnitude <= 0f) return;

        int count = Physics.OverlapBoxNonAlloc(
            b.center,
            b.extents * 0.95f,
            _overlapResults,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapResults[i];
            if (c == null) continue;
            if (c == _keyCollider) continue;
            if (c.transform.IsChildOf(transform)) continue;
            if (!IsHandCollider(c)) continue;

            _nextAllowedPressTime = Time.time + Mathf.Max(MinPressCooldown, pressCooldown);
            Press();
            break;
        }
    }

    private void Press()
    {
        // Audio
        if (!noteEvent.IsNull)
        {
            var inst = RuntimeManager.CreateInstance(noteEvent);
            _lastEventInstance = inst;
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

        OnAnyKeyPressed?.Invoke(this);
        if (vfxAmplitude != null)
        {
            vfxAmplitude.TriggerAmplitudePulse(50f, 1f);
        }
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

