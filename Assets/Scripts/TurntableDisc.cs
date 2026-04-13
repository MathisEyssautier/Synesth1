using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class TurntableDisc : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private int discNumber = 1;

    [Header("FMOD")]
    [SerializeField] private EventReference discEvent;

    [Header("Visual")]
    [SerializeField] private Renderer discRenderer;
    [SerializeField] private GameObject numberCanvasRoot;
    [SerializeField] private Color onColor = Color.magenta;
    [SerializeField] private float emissionOnIntensity = 2f;
    [SerializeField] private float emissionOffIntensity = 0f;

    [Header("Spin")]
    [SerializeField] private float spinSpeedDegPerSec = 180f;

    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private EventInstance _eventInstance;
    public EventInstance EventInstance => _eventInstance;
    private Material _mat;

    private Color _baseColor = Color.white;
    private bool _isPlaying = false;
    private bool _eventStarted = false;
    private bool _isDocked = false;
    private TurntableBase _currentBase;
    private Collider[] _physicsColliders;
    private bool _playbackPhysicsLocked;
    private bool _savedUseGravity;
    private bool _savedIsKinematic;
    private bool _savedDetectCollisions;
    private RigidbodyConstraints _savedConstraints;

    public int DiscNumber => discNumber;
    public bool IsPlaying => _isPlaying;
    public bool IsDocked => _isDocked;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        _physicsColliders = GetComponentsInChildren<Collider>(true);

        if (discRenderer == null)
            discRenderer = GetComponentInChildren<Renderer>();

        if (discRenderer != null)
        {
            _mat = discRenderer.material;
            _baseColor = _mat.color;
            _mat.EnableKeyword("_EMISSION");
        }

        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void OnEnable()
    {
        if (!discEvent.IsNull)
        {
            _eventInstance = RuntimeManager.CreateInstance(discEvent);
            RuntimeManager.AttachInstanceToGameObject(_eventInstance, gameObject);
        }

        SetPlaying(false);
    }

    private void OnDisable()
    {
        UnlockPlaybackPhysics();

        if (_eventInstance.isValid())
        {
            _eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _eventInstance.release();
        }
    }

    private void OnDestroy()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void Update()
    {
        if (_isPlaying)
            transform.Rotate(0f, 0f, spinSpeedDegPerSec * Time.deltaTime, Space.Self);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        // Si on attrape le disque, il n'est plus considéré "posé sur la platine".
        if (_currentBase != null)
            _currentBase.NotifyDiscGrabbed(this);
        _isDocked = false;
        _currentBase = null;

        // Retirer / reposer doit relancer le son depuis le début la prochaine fois.
        ResetPlaybackToStart();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Rien de spécial ici : le base trigger le resnap s'il repasse dessus.
    }

    public void SnapToBase(TurntableBase turntableBase, Transform snapPoint)
    {
        if (snapPoint == null) return;

        _currentBase = turntableBase;
        _isDocked = true;

        if (_rb != null && !_rb.isKinematic)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(snapPoint.position, snapPoint.rotation);
    }

    public void TogglePlayPause()
    {
        SetPlaying(!_isPlaying);
    }

    public void SetPlaying(bool play)
    {
        if (!_isDocked && play) return;

        _isPlaying = play;

        if (play)
            LockPlaybackPhysics();
        else
            UnlockPlaybackPhysics();

        if (_eventInstance.isValid())
        {
            if (play)
            {
                // Premier play après pose: start. Ensuite pause/unpause reprend où il s'est arrêté.
                if (!_eventStarted)
                {
                    _eventInstance.start();
                    _eventStarted = true;
                }
                _eventInstance.setPaused(false);
            }
            else
            {
                if (_eventStarted)
                    _eventInstance.setPaused(true);
            }
        }

        if (numberCanvasRoot != null)
            numberCanvasRoot.SetActive(play);

        if (_mat != null)
        {
            Color c = play ? onColor : _baseColor;
            _mat.color = c;
            if (_mat.HasProperty("_BaseColor"))
                _mat.SetColor("_BaseColor", c);
            if (_mat.HasProperty("_EmissionColor"))
                _mat.SetColor("_EmissionColor", c * (play ? emissionOnIntensity : emissionOffIntensity));
        }
    }

    private void SetPhysicsCollidersEnabled(bool enabled)
    {
        if (_physicsColliders == null) return;

        for (int i = 0; i < _physicsColliders.Length; i++)
        {
            Collider c = _physicsColliders[i];
            if (c == null) continue;
            if (c.isTrigger) continue; // conserve les triggers utilitaires
            c.enabled = enabled;
        }
    }

    private void LockPlaybackPhysics()
    {
        if (_playbackPhysicsLocked) return;
        if (_grab != null)
            _grab.enabled = false;
        SetPhysicsCollidersEnabled(false);

        if (_rb != null)
        {
            _savedUseGravity = _rb.useGravity;
            _savedIsKinematic = _rb.isKinematic;
            _savedDetectCollisions = _rb.detectCollisions;
            _savedConstraints = _rb.constraints;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.useGravity = false;
            _rb.isKinematic = true;
            _rb.detectCollisions = false;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        _playbackPhysicsLocked = true;
    }

    private void UnlockPlaybackPhysics()
    {
        if (!_playbackPhysicsLocked)
        {
            if (_grab != null) _grab.enabled = true;
            SetPhysicsCollidersEnabled(true);
            return;
        }

        if (_rb != null)
        {
            _rb.constraints = _savedConstraints;
            _rb.detectCollisions = _savedDetectCollisions;
            _rb.isKinematic = _savedIsKinematic;
            _rb.useGravity = _savedUseGravity;
        }

        SetPhysicsCollidersEnabled(true);
        if (_grab != null)
            _grab.enabled = true;

        _playbackPhysicsLocked = false;
    }

    private void ResetPlaybackToStart()
    {
        if (!_eventInstance.isValid())
            return;

        if (_eventStarted)
        {
            _eventInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _eventStarted = false;
        }

        // Visuel remis à l'état OFF.
        SetPlaying(false);
    }
}

