using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
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

    private MoveMode _moveMode = MoveMode.Linear;
    private bool _snapTurnEnabled = false;
    private readonly float[] _snapAngles = { 30f, 45f, 60f };
    private int _snapAngleIndex = 1;

    private const int OptionCount = 7;

    private void Start()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
        if (controlsImage != null && placeholderControlsSprite != null)
            controlsImage.sprite = placeholderControlsSprite;

        if (playerCameraOffset != null)
        {
            _baseCameraOffsetLocalPos = playerCameraOffset.localPosition;
            _heightCalibrated = true;
        }

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
        ApplyPlayerHeightOffset();
        ApplyLocomotionSettings();

        RefreshUi();
    }

    private void Update()
    {
        InputDevice left = InputDevices.GetDeviceAtXRNode(leftHandNode);

        bool yPressed = false;
        if (left.isValid)
            left.TryGetFeatureValue(CommonUsages.secondaryButton, out yPressed);

        if (yPressed && !_yWasPressed)
        {
            if (_isOpen) CloseMenu();
            else OpenMenu();
        }
        _yWasPressed = yPressed;

        if (!_isOpen) return;
        HandleNavigation(left);
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

    private void HandleNavigation(InputDevice left)
    {
        if (!left.isValid) return;
        if (Time.unscaledTime < _nextAxisTime) return;

        if (!left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 axis))
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
                CloseMenu();
                StartCoroutine(RestartSceneNextFrame());
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
        ApplyLocomotionSettings();
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
            string line6 = $"{(_selected == 6 ? "> " : "  ")}Relancer la scene";
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
        if (!_heightCalibrated)
        {
            _baseCameraOffsetLocalPos = playerCameraOffset.localPosition;
            _heightCalibrated = true;
        }

        float parentScaleY = 1f;
        if (playerCameraOffset.parent != null)
        {
            float s = playerCameraOffset.parent.lossyScale.y;
            if (Mathf.Abs(s) > 0.0001f) parentScaleY = s;
        }

        // _playerHeightOffset is in world meters; convert to local according to parent scale.
        float localDeltaY = _playerHeightOffset / parentScaleY;

        Vector3 lp = _baseCameraOffsetLocalPos;
        lp.y = _baseCameraOffsetLocalPos.y + localDeltaY;
        playerCameraOffset.localPosition = lp;
    }

    private IEnumerator RestartSceneNextFrame()
    {
        yield return null;
        SaveSettingsToStore();
        Time.timeScale = 1f;
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
