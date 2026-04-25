using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

public enum LocomotionMode
{
    LinearSnapTurn,
    LinearGaze,
    Teleport,
    TeleportBlink
}

public class LocomotionManager : MonoBehaviour
{
    [Header("Locomotion GameObjects (enfants de Locomotion)")]
    public GameObject moveObject;
    public GameObject turnObject;
    public GameObject teleportationObject;

    [Header("Teleport Interactors (sur le controller droit)")]
    public GameObject teleportInteractorRight;

    [Header("Providers (composants sur les objets ci-dessus)")]
    public ContinuousMoveProvider continuousMoveProvider;
    public SnapTurnProvider snapTurnProvider;
    public TeleportationProvider teleportationProvider;

    [Header("XR Camera (pour mode Gaze)")]
    public Transform xrCamera;

    [Header("Blink")]
    public CanvasGroup fadeCanvasGroup;
    public float blinkDuration = 0.15f;

    [Header("Quest Link fallback (legacy XR nodes)")]
    [SerializeField] private bool enableLegacyXrFallback = true;
    [SerializeField] private float fallbackMoveSpeed = 2.5f;
    [SerializeField] private float fallbackMoveDeadzone = 0.15f;
    [SerializeField] private float fallbackSnapThreshold = 0.7f;
    [SerializeField] private float fallbackSnapCooldown = 0.22f;
    [SerializeField] private float fallbackSelectPressThreshold = 0.55f;
    [SerializeField] private float fallbackSelectReleaseThreshold = 0.35f;
    [SerializeField] private Vector3 fallbackLeftRotationOffsetEuler = Vector3.zero;
    [SerializeField] private Vector3 fallbackRightRotationOffsetEuler = Vector3.zero;
    [SerializeField] private float fallbackHoverHapticsAmplitude = 0.15f;
    [SerializeField] private float fallbackHoverHapticsDuration = 0.025f;
    [SerializeField] private Vector3 fallbackRightControllerRotationOffsetEuler = Vector3.zero;

    private LocomotionMode _currentMode;
    private Transform _defaultForwardSource;
    private bool _snapTurnEnabled = false;
    private bool _forceDisabled = false;
    private Transform _rigRoot;
    private CharacterController _rigCharacterController;
    private Transform _leftControllerTransform;
    private Transform _rightControllerTransform;
    private Transform _leftControllerVisualTransform;
    private Transform _rightControllerVisualTransform;
    private Quaternion _leftVisualBaseLocalRotation = Quaternion.identity;
    private Quaternion _rightVisualBaseLocalRotation = Quaternion.identity;
    private Behaviour[] _trackedPoseDrivers = new Behaviour[0];
    private bool _legacyFallbackActive;
    private bool _legacyFallbackStateApplied;
    private bool _legacyFallbackLogDone;
    private float _nextFallbackSnapTime;
    private XRBaseInteractor _leftSelectInteractor;
    private XRBaseInteractor _rightSelectInteractor;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable _leftFallbackSelected;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable _rightFallbackSelected;
    private bool _leftFallbackSelectHeld;
    private bool _rightFallbackSelectHeld;
    private bool _leftFallbackActivateHeld;
    private bool _rightFallbackActivateHeld;
    private int _leftHoveredSelectableCount;
    private int _rightHoveredSelectableCount;
    private XRBaseInteractor _teleportSelectInteractor;
    [SerializeField] private float fallbackTeleportCommitThreshold = 0.7f;
    [SerializeField] private float fallbackTeleportReleaseThreshold = 0.25f;
    private bool _legacyTeleportAimHeld;
    private bool _legacyTeleportRayVisible;
    private bool _legacyOffsetsRuntimeZeroed;
    private bool _legacyTiltCalibrationCaptured;
    private Vector2 _leftTiltReferenceXZ;
    private Vector2 _rightTiltReferenceXZ;
    public bool IsForceDisabled => _forceDisabled;

    void Start()
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        // Délai de téléportation calé sur la durée du fondu aller
        teleportationProvider.delayTime = blinkDuration;

        _defaultForwardSource = continuousMoveProvider.forwardSource;
        _rigRoot = continuousMoveProvider != null ? continuousMoveProvider.transform.root : transform;
        _rigCharacterController = _rigRoot != null ? _rigRoot.GetComponent<CharacterController>() : null;
        CacheControllerTransforms();
        SetMode(LocomotionMode.LinearSnapTurn);
    }

    private void Update()
    {
        UpdateLegacyFallbackState();
        ApplyLegacyLocomotionFallback();
        ApplyLegacyGrabFallback();
    }

    private void LateUpdate()
    {
        ApplyLegacyTrackingFallback();
    }

    public void SetMode(LocomotionMode mode)
    {
        // Désabonnement préventif
        if (teleportationProvider != null)
            teleportationProvider.locomotionStateChanged -= OnTeleportStateChanged;

        _currentMode = mode;
        ApplyCurrentModeState();
    }

    public void SetSnapTurnEnabled(bool enabled)
    {
        _snapTurnEnabled = enabled;
        ApplyCurrentModeState();
    }

    public void SetForceDisabled(bool disabled)
    {
        _forceDisabled = disabled;
        ApplyCurrentModeState();
    }

    private void ApplyCurrentModeState()
    {
        // Désactivation de tout (null ou objet détruit : ignorer — ex. coroutine menu après reload scène)
        if (moveObject != null) moveObject.SetActive(false);
        if (turnObject != null) turnObject.SetActive(false);
        if (teleportationObject != null) teleportationObject.SetActive(false);
        SetTeleportInteractorsActive(false);

        if (_forceDisabled)
            return;

        switch (_currentMode)
        {
            case LocomotionMode.LinearSnapTurn:
                if (moveObject != null) moveObject.SetActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (continuousMoveProvider != null) continuousMoveProvider.forwardSource = _defaultForwardSource;
                break;

            case LocomotionMode.LinearGaze:
                if (moveObject != null) moveObject.SetActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (continuousMoveProvider != null) continuousMoveProvider.forwardSource = xrCamera;
                break;

            case LocomotionMode.Teleport:
                if (teleportationObject != null) teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(!_legacyFallbackActive);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                break;

            case LocomotionMode.TeleportBlink:
                if (teleportationObject != null) teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(!_legacyFallbackActive);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (teleportationProvider != null)
                    teleportationProvider.locomotionStateChanged += OnTeleportStateChanged;
                break;
        }

        // Réactive les bonnes InputActions (Move / téléport / snap) sur les manettes. Sans ça, un état bloqué
        // dans ControllerInputActionManager (Near-Far, UI, menu pause) peut laisser Move désactivée alors que
        // la lecture directe du stick (ex. menu) fonctionne encore.
        RefreshControllerInputActionManagers();
    }

    /// <summary>
    /// Rejoue les règles d’activation des actions XRI sur les contrôleurs (Starter Assets).
    /// </summary>
    public void RefreshControllerInputActionManagers()
    {
        if (continuousMoveProvider == null) return;

        var managers = continuousMoveProvider.transform.root.GetComponentsInChildren<ControllerInputActionManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            var mgr = managers[i];
            if (mgr == null) continue;
            mgr.smoothMotionEnabled = mgr.smoothMotionEnabled;
            mgr.smoothTurnEnabled = mgr.smoothTurnEnabled;
        }
    }

    private void OnTeleportStateChanged(LocomotionProvider provider, LocomotionState state)
    {
        // Preparing = téléportation demandée mais pas encore exécutée (pendant le delayTime)
        if (state == LocomotionState.Preparing)
        {
            StartCoroutine(BlinkEffect());
        }
    }

    private void SetTeleportInteractorsActive(bool active)
    {
        if (teleportInteractorRight != null) teleportInteractorRight.SetActive(active);
        _legacyTeleportRayVisible = active;
        if (!active)
            _legacyTeleportAimHeld = false;
    }

    private IEnumerator BlinkEffect()
    {
        // Fondu au noir (pendant le delayTime, avant le saut)
        float t = 0f;
        while (t < blinkDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / blinkDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        // La téléportation se produit ici (delayTime écoulé = écran noir)

        // Retour au clair
        t = 0f;
        while (t < blinkDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / blinkDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
    }

    // Appelées par les boutons UI
    public void SwitchToLinearSnap() => SetMode(LocomotionMode.LinearSnapTurn);
    public void SwitchToLinearGaze() => SetMode(LocomotionMode.LinearGaze);
    public void SwitchToTeleport() => SetMode(LocomotionMode.Teleport);
    public void SwitchToTeleportBlink() => SetMode(LocomotionMode.TeleportBlink);

    private void UpdateLegacyFallbackState()
    {
        if (!enableLegacyXrFallback || _forceDisabled)
        {
            _legacyFallbackActive = false;
            return;
        }

        bool hasLegacyControllers = IsLegacyControllerAvailable(XRNode.LeftHand) || IsLegacyControllerAvailable(XRNode.RightHand);
        bool hasInputSystemControllers = HasInputSystemXrControllers();
        _legacyFallbackActive = hasLegacyControllers && !hasInputSystemControllers;

        if (_legacyFallbackStateApplied != _legacyFallbackActive)
        {
            _legacyFallbackStateApplied = _legacyFallbackActive;
            SetTrackedPoseDriversEnabled(!_legacyFallbackActive);
            ApplyCurrentModeState();
            if (_legacyFallbackActive)
            {
                _legacyOffsetsRuntimeZeroed = false;
                _legacyTiltCalibrationCaptured = false;
                _leftTiltReferenceXZ = Vector2.zero;
                _rightTiltReferenceXZ = Vector2.zero;
            }
        }

        if (_legacyFallbackActive && !_legacyOffsetsRuntimeZeroed)
        {
            // Force-zero runtime offsets to ignore stale serialized values from scene/prefab.
            fallbackLeftRotationOffsetEuler = Vector3.zero;
            fallbackRightRotationOffsetEuler = Vector3.zero;
            fallbackRightControllerRotationOffsetEuler = Vector3.zero;
            _legacyOffsetsRuntimeZeroed = true;
        }

        if (_legacyFallbackActive && !_legacyFallbackLogDone)
        {
            _legacyFallbackLogDone = true;
            Debug.Log("LocomotionManager: Input System XR controllers not detected, enabling legacy XR fallback for tracking + locomotion.");
        }
    }

    private void ApplyLegacyTrackingFallback()
    {
        if (!_legacyFallbackActive)
            return;

        Vector3 leftPos = Vector3.zero;
        Quaternion leftRot = Quaternion.identity;
        bool hasLeftPos = false;
        bool hasLeftRot = false;
        bool hasLeftPose = _leftControllerTransform != null &&
            TryGetNodePose(XRNode.LeftHand, out leftPos, out leftRot, out hasLeftPos, out hasLeftRot);

        Vector3 rightPos = Vector3.zero;
        Quaternion rightRot = Quaternion.identity;
        bool hasRightPos = false;
        bool hasRightRot = false;
        bool hasRightPose = _rightControllerTransform != null &&
            TryGetNodePose(XRNode.RightHand, out rightPos, out rightRot, out hasRightPos, out hasRightRot);

        if (!_legacyTiltCalibrationCaptured && hasLeftPose && hasRightPose && hasLeftRot && hasRightRot)
        {
            _leftTiltReferenceXZ = ReadTiltXZ(leftRot);
            _rightTiltReferenceXZ = ReadTiltXZ(rightRot);
            _legacyTiltCalibrationCaptured = true;
        }

        if (hasLeftPose)
        {
            if (!_leftControllerTransform.gameObject.activeSelf)
                _leftControllerTransform.gameObject.SetActive(true);
            if (hasLeftPos) _leftControllerTransform.localPosition = leftPos;
            if (hasLeftRot)
                _leftControllerTransform.localRotation = ApplyTiltCalibration(leftRot, _leftTiltReferenceXZ, _legacyTiltCalibrationCaptured);
        }

        if (hasRightPose)
        {
            if (!_rightControllerTransform.gameObject.activeSelf)
                _rightControllerTransform.gameObject.SetActive(true);
            if (hasRightPos) _rightControllerTransform.localPosition = rightPos;
            if (hasRightRot)
                _rightControllerTransform.localRotation = ApplyTiltCalibration(rightRot, _rightTiltReferenceXZ, _legacyTiltCalibrationCaptured);
        }

        ApplyControllerVisualRotationOffsets();
    }

    private void ApplyLegacyLocomotionFallback()
    {
        if (!_legacyFallbackActive || Time.timeScale <= 0f)
            return;

        if (_currentMode != LocomotionMode.LinearSnapTurn && _currentMode != LocomotionMode.LinearGaze)
        {
            if (_currentMode == LocomotionMode.Teleport || _currentMode == LocomotionMode.TeleportBlink)
                ApplyLegacyTeleportFallback();
            return;
        }

        if (moveObject == null || !moveObject.activeInHierarchy || _rigRoot == null)
            return;

        if (!TryReadStick(XRNode.LeftHand, out Vector2 leftAxis))
            leftAxis = Vector2.zero;

        if (leftAxis.sqrMagnitude > fallbackMoveDeadzone * fallbackMoveDeadzone)
        {
            // Keep fallback movement aligned with where the player looks.
            Transform forwardSource = xrCamera != null
                ? xrCamera
                : (continuousMoveProvider != null && continuousMoveProvider.forwardSource != null
                    ? continuousMoveProvider.forwardSource
                    : null);

            Vector3 forward = forwardSource != null ? forwardSource.forward : _rigRoot.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = _rigRoot.forward;
            forward.Normalize();

            Vector3 right = forwardSource != null ? forwardSource.right : _rigRoot.right;
            right.y = 0f;
            if (right.sqrMagnitude < 0.0001f) right = _rigRoot.right;
            right.Normalize();

            Vector3 worldMove = (forward * leftAxis.y) + (right * leftAxis.x);
            worldMove *= fallbackMoveSpeed * Time.deltaTime;

            if (_rigCharacterController != null && _rigCharacterController.enabled)
                _rigCharacterController.Move(worldMove);
            else
                _rigRoot.position += worldMove;
        }

        if (!_snapTurnEnabled || turnObject == null || !turnObject.activeInHierarchy)
            return;

        if (Time.unscaledTime < _nextFallbackSnapTime)
            return;

        if (!TryReadStick(XRNode.RightHand, out Vector2 rightAxis))
            return;

        if (Mathf.Abs(rightAxis.x) < fallbackSnapThreshold)
            return;

        float angle = snapTurnProvider != null ? snapTurnProvider.turnAmount : 45f;
        _rigRoot.Rotate(0f, Mathf.Sign(rightAxis.x) * angle, 0f, Space.World);
        _nextFallbackSnapTime = Time.unscaledTime + fallbackSnapCooldown;
    }

    private void ApplyLegacyTeleportFallback()
    {
        if (teleportationObject == null || !teleportationObject.activeInHierarchy)
            return;
        if (_teleportSelectInteractor == null)
            _teleportSelectInteractor = teleportInteractorRight != null
                ? teleportInteractorRight.GetComponentInChildren<XRBaseInteractor>(true)
                : null;
        if (_teleportSelectInteractor == null)
            return;

        if (!TryReadStick(XRNode.RightHand, out Vector2 rightAxis))
            rightAxis = Vector2.zero;

        bool pressedForward = rightAxis.y >= fallbackTeleportCommitThreshold;
        bool released = rightAxis.y <= fallbackTeleportReleaseThreshold;

        if (pressedForward)
        {
            _legacyTeleportAimHeld = true;
            if (!_legacyTeleportRayVisible)
                SetTeleportInteractorsActive(true);
            return;
        }

        if (_legacyTeleportAimHeld && released)
        {
            _legacyTeleportAimHeld = false;
            TryCommitLegacyTeleportOnHoveredTarget();
            if (_legacyTeleportRayVisible)
                SetTeleportInteractorsActive(false);
            return;
        }

        if (_legacyTeleportRayVisible)
            SetTeleportInteractorsActive(false);

        // Keep snap turn available in teleport mode when not actively aiming teleport.
        if (!_snapTurnEnabled || turnObject == null || !turnObject.activeInHierarchy)
            return;
        if (Time.unscaledTime < _nextFallbackSnapTime)
            return;
        if (Mathf.Abs(rightAxis.x) < fallbackSnapThreshold)
            return;

        float angle = snapTurnProvider != null ? snapTurnProvider.turnAmount : 45f;
        _rigRoot.Rotate(0f, Mathf.Sign(rightAxis.x) * angle, 0f, Space.World);
        _nextFallbackSnapTime = Time.unscaledTime + fallbackSnapCooldown;
    }

    private void TryCommitLegacyTeleportOnHoveredTarget()
    {
        var interactor = _teleportSelectInteractor;
        if (interactor == null)
            return;
        var mgr = interactor.interactionManager;
        if (mgr == null)
            return;

        var hovered = interactor.interactablesHovered;
        if (hovered == null || hovered.Count == 0)
            return;

        IXRSelectInteractable target = null;
        for (int i = 0; i < hovered.Count; i++)
        {
            if (hovered[i] is IXRSelectInteractable selectable)
            {
                target = selectable;
                break;
            }
        }

        if (target == null)
            return;

        mgr.SelectEnter(interactor, target);
        mgr.SelectExit(interactor, target);
    }

    private void ApplyLegacyGrabFallback()
    {
        if (!_legacyFallbackActive)
            return;

        ProcessLegacyGrabForHand(
            XRNode.LeftHand,
            _leftSelectInteractor,
            ref _leftFallbackSelected,
            ref _leftFallbackSelectHeld);
        TryPulseHoverHaptics(XRNode.LeftHand, _leftSelectInteractor, ref _leftHoveredSelectableCount);

        ProcessLegacyGrabForHand(
            XRNode.RightHand,
            _rightSelectInteractor,
            ref _rightFallbackSelected,
            ref _rightFallbackSelectHeld);
        TryPulseHoverHaptics(XRNode.RightHand, _rightSelectInteractor, ref _rightHoveredSelectableCount);

        ProcessLegacyActivateForHand(XRNode.LeftHand, _leftSelectInteractor, ref _leftFallbackActivateHeld);
        ProcessLegacyActivateForHand(XRNode.RightHand, _rightSelectInteractor, ref _rightFallbackActivateHeld);
    }

    private void ProcessLegacyGrabForHand(
        XRNode node,
        XRBaseInteractor interactor,
        ref UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable cachedSelected,
        ref bool held)
    {
        if (interactor == null)
            return;

        var mgr = interactor.interactionManager;
        if (mgr == null)
            return;

        bool pressed = ReadLegacySelectPressed(node, held);

        if (pressed && !held)
        {
            var alreadySelectedList = interactor.interactablesSelected;
            if (alreadySelectedList != null && alreadySelectedList.Count > 0)
            {
                cachedSelected = alreadySelectedList[0];
                held = true;
                return;
            }

            UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable candidate = null;
            var hovered = interactor.interactablesHovered;
            for (int i = 0; i < hovered.Count; i++)
            {
                if (hovered[i] is not UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable selectable)
                    continue;
                candidate = selectable;
                break;
            }

            if (candidate != null)
            {
                cachedSelected = candidate;
                bool alreadySelected = false;
                var selected = interactor.interactablesSelected;
                for (int i = 0; i < selected.Count; i++)
                {
                    if (ReferenceEquals(selected[i], candidate))
                    {
                        alreadySelected = true;
                        break;
                    }
                }

                if (!alreadySelected)
                    mgr.SelectEnter(interactor, candidate);

                held = true;
            }
        }
        else if (!pressed && held)
        {
            if (cachedSelected != null)
            {
                var selected = interactor.interactablesSelected;
                bool isActuallySelected = false;
                for (int i = 0; i < selected.Count; i++)
                {
                    if (ReferenceEquals(selected[i], cachedSelected))
                    {
                        isActuallySelected = true;
                        break;
                    }
                }

                if (isActuallySelected)
                    mgr.SelectExit(interactor, cachedSelected);
            }

            cachedSelected = null;
            held = false;
        }
    }

    private void ProcessLegacyActivateForHand(XRNode node, XRBaseInteractor interactor, ref bool held)
    {
        if (interactor == null)
            return;

        if (interactor is not UnityEngine.XR.Interaction.Toolkit.Interactors.IXRActivateInteractor activateInteractor)
            return;

        bool pressed = ReadLegacyActivatePressed(node);
        if (pressed == held)
            return;

        held = pressed;

        var selected = interactor.interactablesSelected;
        if (selected == null || selected.Count == 0)
            return;

        for (int i = 0; i < selected.Count; i++)
        {
            var selectable = selected[i];
            if (selectable is not UnityEngine.XR.Interaction.Toolkit.Interactables.IXRActivateInteractable activateInteractable)
                continue;

            var args = new ActivateEventArgs
            {
                interactorObject = activateInteractor,
                interactableObject = activateInteractable
            };

            if (pressed)
                activateInteractable.OnActivated(args);
            else
            {
                var deactivateArgs = new DeactivateEventArgs
                {
                    interactorObject = activateInteractor,
                    interactableObject = activateInteractable
                };
                activateInteractable.OnDeactivated(deactivateArgs);
            }
        }
    }

    private void CacheControllerTransforms()
    {
        if (_rigRoot == null)
            return;

        var trackedPoseDrivers = new List<Behaviour>();
        var transforms = _rigRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            var t = transforms[i];
            if (t == null) continue;
            if (_leftControllerTransform == null && t.name == "Left Controller")
            {
                _leftControllerTransform = t;
                AddTrackedPoseDrivers(_leftControllerTransform, trackedPoseDrivers);
            }
            else if (_rightControllerTransform == null && t.name == "Right Controller")
            {
                _rightControllerTransform = t;
                AddTrackedPoseDrivers(_rightControllerTransform, trackedPoseDrivers);
            }
        }

        _trackedPoseDrivers = trackedPoseDrivers.ToArray();

        _leftControllerVisualTransform = FindChildByName(_leftControllerTransform, "Left Controller Visual");
        _rightControllerVisualTransform = FindChildByName(_rightControllerTransform, "Right Controller Visual");
        if (_leftControllerVisualTransform != null)
            _leftVisualBaseLocalRotation = _leftControllerVisualTransform.localRotation;
        if (_rightControllerVisualTransform != null)
            _rightVisualBaseLocalRotation = _rightControllerVisualTransform.localRotation;

        if (_leftSelectInteractor == null)
            _leftSelectInteractor = FindSelectInteractorOnController(_leftControllerTransform);
        if (_rightSelectInteractor == null)
            _rightSelectInteractor = FindSelectInteractorOnController(_rightControllerTransform);
        if (_teleportSelectInteractor == null && teleportInteractorRight != null)
            _teleportSelectInteractor = teleportInteractorRight.GetComponentInChildren<XRBaseInteractor>(true);
    }

    private void ApplyControllerVisualRotationOffsets()
    {
        // Keep visual corrections configurable per hand for Quest Link fallback.
        Quaternion leftVisualOffset = Quaternion.Euler(fallbackLeftRotationOffsetEuler);
        Quaternion rightVisualOffset = Quaternion.Euler(fallbackRightRotationOffsetEuler);

        if (_leftControllerVisualTransform == null && _leftControllerTransform != null)
        {
            _leftControllerVisualTransform = FindChildByName(_leftControllerTransform, "Left Controller Visual");
            if (_leftControllerVisualTransform != null)
                _leftVisualBaseLocalRotation = _leftControllerVisualTransform.localRotation;
        }

        if (_rightControllerVisualTransform == null && _rightControllerTransform != null)
        {
            _rightControllerVisualTransform = FindChildByName(_rightControllerTransform, "Right Controller Visual");
            if (_rightControllerVisualTransform != null)
                _rightVisualBaseLocalRotation = _rightControllerVisualTransform.localRotation;
        }

        if (_leftControllerVisualTransform != null)
        {
            _leftControllerVisualTransform.localRotation = _legacyFallbackActive
                ? _leftVisualBaseLocalRotation * leftVisualOffset
                : _leftVisualBaseLocalRotation;
        }

        if (_rightControllerVisualTransform != null)
        {
            _rightControllerVisualTransform.localRotation = _legacyFallbackActive
                ? _rightVisualBaseLocalRotation * rightVisualOffset
                : _rightVisualBaseLocalRotation;
        }
    }

    private static XRBaseInteractor FindSelectInteractorOnController(Transform controllerTransform)
    {
        if (controllerTransform == null)
            return null;

        var nearFar = controllerTransform.GetComponentInChildren<NearFarInteractor>(true);
        if (nearFar != null)
            return nearFar;

        var anyInteractor = controllerTransform.GetComponentInChildren<XRBaseInteractor>(true);
        if (anyInteractor != null)
            return anyInteractor;

        return null;
    }

    private static void AddTrackedPoseDrivers(Transform controllerTransform, List<Behaviour> result)
    {
        if (controllerTransform == null) return;
        var behaviours = controllerTransform.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;
            var typeName = b.GetType().Name;
            if (typeName.IndexOf("TrackedPoseDriver", System.StringComparison.OrdinalIgnoreCase) >= 0)
                result.Add(b);
        }
    }

    private void SetTrackedPoseDriversEnabled(bool enabled)
    {
        for (int i = 0; i < _trackedPoseDrivers.Length; i++)
        {
            var b = _trackedPoseDrivers[i];
            if (b == null) continue;
            b.enabled = enabled;
        }
    }

    private static bool IsLegacyControllerAvailable(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked))
            return tracked;

        return true;
    }

    private static bool TryReadStick(XRNode node, out Vector2 axis)
    {
        axis = Vector2.zero;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        return device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out axis);
    }

    private bool ReadLegacySelectPressed(XRNode node, bool currentlyHeld)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        float threshold = currentlyHeld ? fallbackSelectReleaseThreshold : fallbackSelectPressThreshold;

        // Keep original gameplay mechanic: grab only with the grip (middle finger trigger),
        // not index trigger.
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out bool gripPressed) && gripPressed)
            return true;
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip) && grip >= threshold)
            return true;

        return false;
    }

    private static bool ReadLegacyActivatePressed(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool triggerButtonPressed) &&
            triggerButtonPressed)
            return true;

        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float triggerValue) &&
            triggerValue >= 0.6f)
            return true;

        return false;
    }

    private void TryPulseHoverHaptics(XRNode node, XRBaseInteractor interactor, ref int previousSelectableCount)
    {
        if (interactor == null)
            return;

        int selectableCount = 0;
        var hovered = interactor.interactablesHovered;
        for (int i = 0; i < hovered.Count; i++)
        {
            if (hovered[i] is UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable)
                selectableCount++;
        }

        if (selectableCount > 0 && previousSelectableCount == 0)
            SendLegacyHapticImpulse(node, fallbackHoverHapticsAmplitude, fallbackHoverHapticsDuration);

        previousSelectableCount = selectableCount;
    }

    private static void SendLegacyHapticImpulse(XRNode node, float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return;

        if (device.TryGetHapticCapabilities(out HapticCapabilities caps) &&
            caps.supportsImpulse &&
            caps.numChannels > 0)
        {
            device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), Mathf.Max(0f, duration));
        }
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        Transform containsMatch = null;
        var children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (t == null)
                continue;

            if (t.name == targetName)
                return t;

            if (containsMatch == null &&
                t.name.IndexOf(targetName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                containsMatch = t;
        }

        return containsMatch;
    }

    private static bool TryGetNodePose(
        XRNode node,
        out Vector3 localPosition,
        out Quaternion localRotation,
        out bool hasPosition,
        out bool hasRotation)
    {
        localPosition = default;
        localRotation = Quaternion.identity;
        hasPosition = false;
        hasRotation = false;

        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
            return false;

        var gripPositionUsage = new InputFeatureUsage<Vector3>("gripPosition");
        var pointerPositionUsage = new InputFeatureUsage<Vector3>("pointerPosition");
        var gripRotationUsage = new InputFeatureUsage<Quaternion>("gripRotation");
        var pointerRotationUsage = new InputFeatureUsage<Quaternion>("pointerRotation");

        hasPosition = device.TryGetFeatureValue(gripPositionUsage, out localPosition);
        if (!hasPosition)
            hasPosition = device.TryGetFeatureValue(pointerPositionUsage, out localPosition);
        if (!hasPosition)
            hasPosition = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out localPosition);

        // For Quest Link fallback, prioritize deviceRotation to better match real controller posture.
        // grip/pointer rotations can include controller-model ergonomic offsets and look "tilted" in-game.
        hasRotation = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out localRotation);
        if (!hasRotation)
            hasRotation = device.TryGetFeatureValue(pointerRotationUsage, out localRotation);
        if (!hasRotation)
            hasRotation = device.TryGetFeatureValue(gripRotationUsage, out localRotation);

        return hasPosition || hasRotation;
    }

    private static Vector2 ReadTiltXZ(Quaternion q)
    {
        Vector3 e = q.eulerAngles;
        return new Vector2(NormalizeAngle(e.x), NormalizeAngle(e.z));
    }

    private static Quaternion ApplyTiltCalibration(Quaternion raw, Vector2 tiltRefXZ, bool hasCalibration)
    {
        if (!hasCalibration)
            return raw;

        Vector3 e = raw.eulerAngles;
        float x = NormalizeAngle(e.x) - tiltRefXZ.x;
        float y = NormalizeAngle(e.y); // keep yaw raw to avoid drift while physically turning
        float z = NormalizeAngle(e.z) - tiltRefXZ.y;
        return Quaternion.Euler(x, y, z);
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    private static bool HasInputSystemXrControllers()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (var d in InputSystem.devices)
        {
            if (d == null || !d.added || !d.enabled)
                continue;
            if (d is UnityEngine.InputSystem.XR.XRController)
                return true;

            bool isHandUsage = false;
            foreach (var usage in d.usages)
            {
                if (usage == UnityEngine.InputSystem.CommonUsages.LeftHand ||
                    usage == UnityEngine.InputSystem.CommonUsages.RightHand)
                {
                    isHandUsage = true;
                    break;
                }
            }

            if (!isHandUsage)
                continue;

            string layout = d.layout ?? string.Empty;
            if (layout.Contains("XR", System.StringComparison.OrdinalIgnoreCase) ||
                layout.Contains("Quest", System.StringComparison.OrdinalIgnoreCase) ||
                layout.Contains("Touch", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }
#endif
        return false;
    }
}