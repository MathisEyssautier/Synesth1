using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif
using FMODUnity;
using FMOD.Studio;
using UnityEngine.SceneManagement;

public class VRPauseMenuController : MonoBehaviour
{
    private enum MoveMode
    {
        Linear,
        Teleport
    }

    [Header("Menu root (full view panel)")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;

    [Header("UI text")]
    [SerializeField] private TextMeshProUGUI optionsText;
    [SerializeField] private TextMeshProUGUI controlsHintText;

    [Header("Controls image")]
    [SerializeField] private Image controlsImage;
    [SerializeField] private Sprite placeholderControlsSprite;

    [Header("Gameplay refs")]
    [SerializeField] private LocomotionManager locomotionManager;
    [SerializeField] private Transform playerCameraOffset;

    [Header("Input")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private float axisDeadzone = 0.65f;
    [SerializeField] private float axisRepeatDelay = 0.18f;
    [SerializeField] private float playerHeightStep = 0.03f;
    [SerializeField] private float minPlayerHeightOffset = -0.35f;
    [SerializeField] private float maxPlayerHeightOffset = 0.35f;

    private bool _isOpen;
    private bool _isControlsPage;
    private bool _yWasPressed;
    private float _nextAxisTime;
    private bool _isClosingMenu;
    private int _selected = 0;
    private Vector3 _baseCameraOffsetLocalPos;
    private bool _heightCalibrated;
    private float _playerHeightOffset;
    private Coroutine _heightCalibrationRoutine;

    private MoveMode _moveMode = MoveMode.Linear;
    private bool _snapTurnEnabled = false;
    private readonly float[] _snapAngles = { 30f, 45f, 60f };
    private int _snapAngleIndex = 1;
    private bool _restartConfirmArmed;
    private float _restartConfirmExpireAt;
    [SerializeField] private float restartConfirmWindowSeconds = 1.2f;

    private const int OptionCount = 7;

    private void Start()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
        if (controlsImage != null && placeholderControlsSprite != null)
            controlsImage.sprite = placeholderControlsSprite;

        _heightCalibrated = false;

        if (locomotionManager != null && locomotionManager.snapTurnProvider != null)
        {
            float current = locomotionManager.snapTurnProvider.turnAmount;
            for (int i = 0; i < _snapAngles.Length; i++)
            {
                if (Mathf.Abs(_snapAngles[i] - current) < 0.1f)
                {
                    _snapAngleIndex = i;
                    break;
                }
            }
        }

        LoadSettingsFromStore();
        ApplyLocomotionSettings();

        if (playerCameraOffset != null)
            _heightCalibrationRoutine = StartCoroutine(CalibrateHeightBaselineAfterXrReady());

        RefreshUi();
    }

    private IEnumerator CalibrateHeightBaselineAfterXrReady()
    {
        // Laisse XR/Tracking stabiliser ses transforms avant de capturer la hauteur de base.
        yield return null;
        yield return new WaitForEndOfFrame();

        const int maxFrames = 120;
        int frame = 0;
        while (frame < maxFrames)
        {
            var centerEye = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            bool tracked = centerEye.isValid
                && centerEye.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked)
                && isTracked;

            if (tracked)
                break;

            frame++;
            yield return null;
        }

        if (playerCameraOffset == null)
            yield break;

        _baseCameraOffsetLocalPos = playerCameraOffset.localPosition;
        _heightCalibrated = true;
        ApplyPlayerHeightOffset();
        RefreshUi();
        _heightCalibrationRoutine = null;
    }

    private void Update()
    {
        bool yPressed = ReadLeftSecondaryButtonPressed();

        if (yPressed && !_yWasPressed)
        {
            if (_isOpen) CloseMenu();
            else OpenMenu();
        }
        _yWasPressed = yPressed;

        if (!_isOpen) return;
        HandleNavigation();
    }

#if ENABLE_INPUT_SYSTEM
    // Touch Plus (OpenXR) peut exposer la manette sans matcher XRController.leftHand ; on retombe sur l'usage LeftHand.
    private static UnityEngine.InputSystem.InputDevice FindLeftHandInputDevice()
    {
        var x = XRController.leftHand;
        if (x != null)
            return x;

        foreach (var d in InputSystem.devices)
        {
            if (d == null || !d.added || !d.enabled)
                continue;
            foreach (var usage in d.usages)
            {
                if (usage == UnityEngine.InputSystem.CommonUsages.LeftHand)
                    return d;
            }
        }

        return null;
    }
#endif

    private bool ReadLeftSecondaryButtonPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var leftXr = FindLeftHandInputDevice();
        if (leftXr != null)
        {
            var btn = leftXr.TryGetChildControl<ButtonControl>("secondaryButton");
            if (btn != null && btn.isPressed)
                return true;
        }
#endif
        var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(leftHandNode);
        return left.isValid
            && left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool legacy)
            && legacy;
    }

    private bool TryReadLeftPrimary2DAxis(out Vector2 axis)
    {
#if ENABLE_INPUT_SYSTEM
        var leftXr = FindLeftHandInputDevice();
        if (leftXr != null)
        {
            var stick = leftXr.TryGetChildControl<Vector2Control>("primary2DAxis")
                        ?? leftXr.TryGetChildControl<Vector2Control>("thumbstick");
            if (stick != null)
            {
                axis = stick.ReadValue();
                return true;
            }
        }
#endif
        var left = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(leftHandNode);
        if (left.isValid && left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out axis))
            return true;
        axis = default;
        return false;
    }

    private void OpenMenu()
    {
        _isOpen = true;
        _isControlsPage = false;
        _nextAxisTime = 0f;

        if (menuRoot != null) menuRoot.SetActive(true);
        ApplyPanels();
        RefreshUi();

        Time.timeScale = 0f;

        Bus master = RuntimeManager.GetBus("bus:/");
        if (master.isValid())
            master.setPaused(true);

        if (locomotionManager != null)
        {
            if (locomotionManager.moveObject != null) locomotionManager.moveObject.SetActive(false);
            if (locomotionManager.turnObject != null) locomotionManager.turnObject.SetActive(false);
            if (locomotionManager.teleportationObject != null) locomotionManager.teleportationObject.SetActive(false);
            if (locomotionManager.teleportInteractorRight != null) locomotionManager.teleportInteractorRight.SetActive(false);
        }

    }

    private void CloseMenu()
    {
        if (_isClosingMenu) return;
        _isClosingMenu = true;
        _isOpen = false;
        _isControlsPage = false;

        if (menuRoot != null) menuRoot.SetActive(false);

        Bus master = RuntimeManager.GetBus("bus:/");
        if (master.isValid())
            master.setPaused(false);

        Time.timeScale = 1f;
        StartCoroutine(ApplyLocomotionSettingsNextFrame());

    }

    private void HandleNavigation()
    {
        if (_restartConfirmArmed && Time.unscaledTime > _restartConfirmExpireAt)
            _restartConfirmArmed = false;

        if (Time.unscaledTime < _nextAxisTime) return;

        if (!TryReadLeftPrimary2DAxis(out Vector2 axis))
            return;

        if (_isControlsPage)
        {
            if (Mathf.Abs(axis.x) > axisDeadzone || axis.y < -axisDeadzone)
            {
                _isControlsPage = false;
                ApplyPanels();
                RefreshUi();
                _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            }
            return;
        }

        if (axis.y > axisDeadzone)
        {
            _selected = (_selected - 1 + OptionCount) % OptionCount;
            RefreshUi();
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            return;
        }
        if (axis.y < -axisDeadzone)
        {
            _selected = (_selected + 1) % OptionCount;
            RefreshUi();
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            return;
        }

        if (Mathf.Abs(axis.x) > axisDeadzone)
        {
            if (_selected == 0)
            {
                _moveMode = axis.x > 0f ? MoveMode.Teleport : MoveMode.Linear;
                RefreshUi();
            }
            else if (_selected == 1)
            {
                _snapTurnEnabled = axis.x > 0f;
                RefreshUi();
            }
            else if (_selected == 2)
            {
                if (axis.x > 0f) _snapAngleIndex = (_snapAngleIndex + 1) % _snapAngles.Length;
                else _snapAngleIndex = (_snapAngleIndex - 1 + _snapAngles.Length) % _snapAngles.Length;
                RefreshUi();
            }
            else if (_selected == 3)
            {
                _playerHeightOffset += axis.x > 0f ? playerHeightStep : -playerHeightStep;
                _playerHeightOffset = Mathf.Clamp(_playerHeightOffset, minPlayerHeightOffset, maxPlayerHeightOffset);
                ApplyPlayerHeightOffset();
                RefreshUi();
            }
            else if (_selected == 4)
            {
                _playerHeightOffset = 0f;
                ApplyPlayerHeightOffset();
                RefreshUi();
            }
            else if (_selected == 5 && axis.x > 0f)
            {
                _isControlsPage = true;
                ApplyPanels();
                RefreshUi();
            }
            else if (_selected == 6 && axis.x > 0f)
            {
                // Safety: require a second right input shortly after the first one
                // to avoid accidental scene restarts caused by noisy stick values.
                if (_restartConfirmArmed && Time.unscaledTime <= _restartConfirmExpireAt)
                {
                    _restartConfirmArmed = false;
                    CloseMenu();
                    StartCoroutine(RestartSceneNextFrame());
                }
                else
                {
                    _restartConfirmArmed = true;
                    _restartConfirmExpireAt = Time.unscaledTime + Mathf.Max(0.25f, restartConfirmWindowSeconds);
                    RefreshUi();
                }
            }
            else if (_selected != 6)
            {
                _restartConfirmArmed = false;
            }
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
        }
    }

    private void ApplyLocomotionSettings()
    {
        if (locomotionManager == null) return;
        if (locomotionManager.IsForceDisabled) return;

        locomotionManager.SetMode(_moveMode == MoveMode.Linear ? LocomotionMode.LinearSnapTurn : LocomotionMode.Teleport);
        locomotionManager.SetSnapTurnEnabled(_snapTurnEnabled);
        if (locomotionManager.snapTurnProvider != null)
            locomotionManager.snapTurnProvider.turnAmount = _snapAngles[_snapAngleIndex];
    }

    private IEnumerator ApplyLocomotionSettingsNextFrame()
    {
        yield return null;
        // Après reload / destruction du rig, le manager ou ses GameObjects peuvent être invalides.
        if (this != null && locomotionManager != null)
        {
            ApplyLocomotionSettings();
            // Une frame après réactivation du Move : réapplique Enable/Disable des InputActions XRI.
            locomotionManager.RefreshControllerInputActionManagers();
        }
        _isClosingMenu = false;
    }

    private void ApplyPanels()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!_isControlsPage);
        if (controlsPanel != null) controlsPanel.SetActive(_isControlsPage);
    }

    private void RefreshUi()
    {
        if (optionsText != null)
        {
            string line0 = $"{(_selected == 0 ? "> " : "  ")}Mode de deplacement : {(_moveMode == MoveMode.Linear ? "Lineaire" : "Teleportation")}";
            string line1 = $"{(_selected == 1 ? "> " : "  ")}Snap turn joystick droit : {(_snapTurnEnabled ? "ON" : "OFF")}";
            string line2 = $"{(_selected == 2 ? "> " : "  ")}Angle snap turn : {_snapAngles[_snapAngleIndex]:0} deg";
            string line3 = $"{(_selected == 3 ? "> " : "  ")}Hauteur joueur : {_playerHeightOffset:+0.00;-0.00;0.00} m";
            string line4 = $"{(_selected == 4 ? "> " : "  ")}Recentrer hauteur";
            string line5 = $"{(_selected == 5 ? "> " : "  ")}Controles (ouvrir)";
            string restartLabel = _restartConfirmArmed ? "Relancer la scene (confirmer >)" : "Relancer la scene";
            string line6 = $"{(_selected == 6 ? "> " : "  ")}{restartLabel}";
            optionsText.text = $"{line0}\n{line1}\n{line2}\n{line3}\n{line4}\n{line5}\n{line6}\n\nJoystick gauche: Haut/Bas = selection, Gauche/Droite = modifier";
        }

        if (controlsHintText != null)
        {
            controlsHintText.text = _isControlsPage
                ? "Page Controles (image placeholder). Gauche/Droite ou Bas pour revenir."
                : "Bouton Y (gauche) : ouvrir/fermer le menu";
        }
    }

    private void ApplyPlayerHeightOffset()
    {
        if (playerCameraOffset == null) return;
        if (!_heightCalibrated) return;

        Vector3 lp = _baseCameraOffsetLocalPos;
        // Offset is applied directly in local meters relative to scene-start baseline.
        // This keeps 0.10 predictable and ensures reset returns exactly to launch height.
        lp.y = _baseCameraOffsetLocalPos.y + _playerHeightOffset;
        playerCameraOffset.localPosition = lp;
    }

    private IEnumerator RestartSceneNextFrame()
    {
        yield return null;
        SaveSettingsToStore();
        Time.timeScale = 1f;
        FinalSequenceController.StopOutsideFinalMusicIfPlaying();
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.buildIndex);
    }

    private void OnDisable()
    {
        SaveSettingsToStore();
    }

    private void LoadSettingsFromStore()
    {
        _moveMode = VRSettingsStore.MoveMode == 1 ? MoveMode.Teleport : MoveMode.Linear;
        _snapTurnEnabled = VRSettingsStore.SnapEnabled;
        _playerHeightOffset = Mathf.Clamp(VRSettingsStore.HeightOffset, minPlayerHeightOffset, maxPlayerHeightOffset);

        float savedAngle = VRSettingsStore.SnapAngle;
        int best = 1;
        float bestDist = Mathf.Infinity;
        for (int i = 0; i < _snapAngles.Length; i++)
        {
            float d = Mathf.Abs(_snapAngles[i] - savedAngle);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }
        _snapAngleIndex = best;
    }

    private void SaveSettingsToStore()
    {
        VRSettingsStore.MoveMode = _moveMode == MoveMode.Teleport ? 1 : 0;
        VRSettingsStore.SnapEnabled = _snapTurnEnabled;
        VRSettingsStore.SnapAngle = _snapAngles[_snapAngleIndex];
        VRSettingsStore.HeightOffset = _playerHeightOffset;
    }
}
