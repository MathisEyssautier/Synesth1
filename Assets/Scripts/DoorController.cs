using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DoorController : MonoBehaviour
{
    [Header("References")]
    public Transform doorPivot;
    public Transform handle1;
    public Transform handle2;

    [Header("Door Settings")]
    public float maxOpenAngle = 90f;
    public float closedAngleThreshold = 2f;

    [Header("Inertie")]
    [Tooltip("Facteur de freinage apres le release (0 = pas de frein, 1 = arret immediat)")]
    public float damping = 0.85f;
    [Tooltip("Vitesse angulaire max en degres par seconde")]
    public float vitesseMax = 200f;

    [Header("FMOD")]
    public float transitionSpeed = 1f;

    [Header("Shell ambience control")]
    [Tooltip("Tous les coquillages de la pièce 2 dont le volume doit suivre l'état de la porte.")]
    [SerializeField] private ShellProximityFeedback[] shellSources;

    [Header("Player / Room 2")]
    [Tooltip("Transform de la tête XR / caméra du joueur (pour savoir de quel côté de la porte il est).")]
    [SerializeField] private Transform playerHead;
    [Tooltip("Volume de la pièce 2 (BoxCollider) : si la tête du joueur est dedans, il est considéré dans la pièce.")]
    [SerializeField] private BoxCollider room2Bounds;

    private XRSimpleInteractable simpleInteractable;
    private XRSimpleInteractable simpleInteractable2;

    private bool isGrabbed = false;
    private bool _inertieActive = false;
    public float currentYAngle = 0f;
    private IXRSelectInteractor currentInteractor;
    private float grabAngleOffset = 0f;
    private float angleAtGrab = 0f;

    // Pour le calcul de velocite
    private float _anglePrecedent = 0f;
    private float _velociteAngulaire = 0f;

    private bool _isClosed = true;
    private float _closedReferenceAngle = 0f;
    private bool _lastShouldMute = false;

    private float GetDoorYAngle()
    {
        // Normalise l'angle Y entre -180 et 180 pour des comparaisons stables.
        float y = doorPivot.rotation.eulerAngles.y;
        if (y > 180f) y -= 360f;
        return y;
    }

    void Start()
    {
        simpleInteractable = handle1.GetComponent<XRSimpleInteractable>();
        simpleInteractable.selectEntered.AddListener(OnGrab);
        simpleInteractable.selectExited.AddListener(OnRelease);

        simpleInteractable2 = handle2.GetComponent<XRSimpleInteractable>();
        simpleInteractable2.selectEntered.AddListener(OnGrab);
        simpleInteractable2.selectExited.AddListener(OnRelease);

        // Angle de référence "porte fermée" pris au démarrage, depuis la pose actuelle dans la scène.
        _closedReferenceAngle = GetDoorYAngle();
        currentYAngle = _closedReferenceAngle;
        _anglePrecedent = currentYAngle;

        // Calcule l'état initial fermé / ouvert et applique aux coquillages.
        _isClosed = true;
        _lastShouldMute = false; // force un premier apply
        RefreshShellMuteState(forceApply: true);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        currentInteractor = args.interactorObject;
        angleAtGrab = currentYAngle;
        grabAngleOffset = GetHandAngle();
        _anglePrecedent = currentYAngle;
        _velociteAngulaire = 0f;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;
        _inertieActive = Mathf.Abs(_velociteAngulaire) > 0.1f;
    }

    float GetHandAngle()
    {
        Vector3 handPosition = currentInteractor.transform.position;
        Vector3 directionToHand = handPosition - doorPivot.position;
        directionToHand.y = 0f;
        if (directionToHand.magnitude < 0.01f) return 0f;
        return Vector3.SignedAngle(Vector3.forward, directionToHand.normalized, Vector3.up);
    }

    void LateUpdate()
    {
        if (isGrabbed && currentInteractor != null)
        {
            UpdateDoorRotation();

            // Calcule la velocite angulaire en temps reel pendant le grab
            _velociteAngulaire = (currentYAngle - _anglePrecedent) / Time.deltaTime;
            _velociteAngulaire = Mathf.Clamp(_velociteAngulaire, -vitesseMax, vitesseMax);
            _anglePrecedent = currentYAngle;
        }
        else if (_inertieActive)
        {
            _velociteAngulaire *= Mathf.Pow(1f - damping, Time.deltaTime * 60f);

            currentYAngle += _velociteAngulaire * Time.deltaTime;
            currentYAngle = Mathf.Clamp(currentYAngle, -maxOpenAngle, 0f);

            doorPivot.rotation = Quaternion.Euler(0f, currentYAngle, 0f);

            if (currentYAngle <= -maxOpenAngle || currentYAngle >= 0f || Mathf.Abs(_velociteAngulaire) < 0.1f)
            {
                _velociteAngulaire = 0f;
                _inertieActive = false;
            }
        }

        RefreshShellMuteState(forceApply: false);
    }

    void UpdateDoorRotation()
    {
        float handAngle = GetHandAngle();
        float angleDelta = handAngle - grabAngleOffset;
        float targetAngle = angleAtGrab + angleDelta;
        targetAngle = Mathf.Clamp(targetAngle, -maxOpenAngle, 0f);
        currentYAngle = targetAngle;
        doorPivot.rotation = Quaternion.Euler(0f, currentYAngle, 0f);
    }

    public void ForceClose()
    {
        currentYAngle = _closedReferenceAngle;
        grabAngleOffset = 0f;
        angleAtGrab = 0f;
        isGrabbed = false;
        _inertieActive = false;
        currentInteractor = null;
        _velociteAngulaire = 0f;
        doorPivot.rotation = Quaternion.Euler(0f, currentYAngle, 0f);

        if (!_isClosed)
        {
            _isClosed = true;
            UpdateShellDoorState(true);
        }
    }

    private void RefreshShellMuteState(bool forceApply)
    {
        // Porte considérée fermée tant que l'angle reste proche de l'angle de référence pris au Start().
        _isClosed = Mathf.Abs(currentYAngle - _closedReferenceAngle) <= closedAngleThreshold;

        // Si la porte est fermée mais que le joueur est DANS la pièce 2, on laisse les sons actifs.
        bool playerInsideRoom2 = IsPlayerInsideRoom2();
        bool shouldMute = _isClosed && !playerInsideRoom2;

        if (forceApply || shouldMute != _lastShouldMute)
        {
            _lastShouldMute = shouldMute;
            UpdateShellDoorState(shouldMute);
        }
    }

    private void UpdateShellDoorState(bool shouldMute)
    {
        if (shellSources == null) return;
        for (int i = 0; i < shellSources.Length; i++)
        {
            if (shellSources[i] == null) continue;
            shellSources[i].SetDoorClosed(shouldMute);
        }
    }

    private bool IsPlayerInsideRoom2()
    {
        if (playerHead == null || room2Bounds == null)
            return false;

        // bounds est en world space, parfait pour un test simple.
        return room2Bounds.bounds.Contains(playerHead.position);
    }

    void OnDestroy()
    {
        if (simpleInteractable != null)
        {
            simpleInteractable.selectEntered.RemoveListener(OnGrab);
            simpleInteractable.selectExited.RemoveListener(OnRelease);
            simpleInteractable2.selectEntered.RemoveListener(OnGrab);
            simpleInteractable2.selectExited.RemoveListener(OnRelease);
        }
    }
}