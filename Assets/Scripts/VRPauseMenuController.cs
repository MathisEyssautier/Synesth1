using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// VR pause menu: stack navigation across Menu_pause, menu_settings, Menu_quit, and controllers view.
/// Left X: open/close (main) or back. Right A: confirm. Stick: navigate.
/// </summary>
public class VRPauseMenuController : MonoBehaviour
{
    private enum MenuScreen
    {
        None,
        Main,
        Settings,
        Quit,
        Controllers
    }

    private enum MoveMode
    {
        Linear,
        Teleport
    }

    [Header("Menu root (parent under camera)")]
    [SerializeField] private GameObject menuRoot;

    [Header("Panels (children of menu root or auto-found by name)")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject settingsMenuPanel;
    [SerializeField] private GameObject quitMenuPanel;
    [SerializeField] private GameObject controllersMenuPanel;

    [Header("Gameplay refs")]
    [SerializeField] private LocomotionManager locomotionManager;
    [SerializeField] private Transform playerCameraOffset;

    [Header("Input")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private XRNode rightHandNode = XRNode.RightHand;
    [SerializeField] private float axisDeadzone = 0.65f;
    [SerializeField] private float axisRepeatDelay = 0.18f;
    [SerializeField] private float playerHeightStep = 0.03f;
    [SerializeField] private float minPlayerHeightOffset = -0.35f;
    [SerializeField] private float maxPlayerHeightOffset = 0.35f;

    [Header("Locomotion")]
    [SerializeField] private float snapTurnAngleDegrees = 30f;

    [Header("Restart")]
    [SerializeField] private string restartSceneName = "StartScene";

    private MenuScreen _screen = MenuScreen.None;
    private bool _isOpen;
    private bool _isClosingMenu;
    private bool _leftMenuButtonWasPressed;
    private bool _rightConfirmWasPressed;
    private float _nextAxisTime;

    private readonly List<VRMenuTextHighlight> _mainHighlights = new List<VRMenuTextHighlight>();
    private readonly List<GameObject> _mainButtons = new List<GameObject>();
    private int _mainIndex;

    private readonly List<VRMenuCardSelectable> _quitOptions = new List<VRMenuCardSelectable>();
    private int _quitIndex;

    private VRMenuCardSelectable _controllersBackOption;

    private Transform _settingsMovementLine;
    private VRMenuCardSelectable _settingsFreeOption;
    private VRMenuCardSelectable _settingsTeleportOption;
    private VRMenuCardSelectable _settingsSnapOption;
    private VRMenuCardSelectable _settingsBackOption;
    private VRMenuCardSelectable _lockedTwoHandsOption;
    private VRMenuCardSelectable _lockedFrenchOption;
    private int _settingsMovementIndex;
    private bool _settingsFocusBackButton;

    private Sprite _selectCardHover;
    private Sprite _selectCircleSelected;

    private MoveMode _moveMode = MoveMode.Linear;
    private bool _snapTurnEnabled;
    private float _playerHeightOffset;
    private Vector3 _baseCameraOffsetLocalPos;
    private bool _heightCalibrated;
    private Coroutine _heightCalibrationRoutine;

    private static bool _sessionDefaultsApplied;

    private void Awake()
    {
        if (_sessionDefaultsApplied) return;
        VRSettingsStore.ResetToDefaults();
        _sessionDefaultsApplied = true;
    }

    private void Start()
    {
        ResolvePanelReferences();
        HideAllPanels();
        CacheMenuSprites();

        BuildMainMenuItems();
        BuildQuitMenuItems();
        BuildSettingsMenuItems();
        BuildControllersMenuItems();

        HideDeprecatedSettingsUi();

        _heightCalibrated = false;
        LoadSettingsFromStore();
        ApplyLocomotionSettings();
        ApplyLockedSettingsVisuals();
        RefreshSettingsVisuals();

        if (playerCameraOffset != null)
            _heightCalibrationRoutine = StartCoroutine(CalibrateHeightBaselineAfterXrReady());

        // Keep menuRoot active so canvases under the camera stay valid; hide panels only.
        if (menuRoot != null)
            menuRoot.SetActive(true);
        HideAllPanels();
    }

    private void Update()
    {
        if (ReadLeftPrimaryButtonPressed())
            HandleMenuButtonPress();

        if (!_isOpen) return;
        if (!IsMenuUiAlive()) return;

        if (ReadRightPrimaryButtonPressed())
            HandleConfirmPress();

        if (Time.unscaledTime >= _nextAxisTime)
            HandleStickNavigation();
    }

    private static bool IsAlive(Object obj) => obj != null;

    private bool IsMenuUiAlive() => IsAlive(menuRoot) && IsAlive(pauseMenuPanel);

    private void HandleMenuButtonPress()
    {
        if (!_isOpen)
        {
            OpenMenu();
            return;
        }

        if (_screen == MenuScreen.Main)
            CloseMenu();
        else
            NavigateBack();
    }

    private void HandleConfirmPress()
    {
        switch (_screen)
        {
            case MenuScreen.Main:
                ActivateMainSelection();
                break;
            case MenuScreen.Settings:
                ActivateSettingsSelection();
                break;
            case MenuScreen.Quit:
                ActivateQuitSelection();
                break;
            case MenuScreen.Controllers:
                NavigateBack();
                break;
        }
    }

    private void OpenMenu()
    {
        _isOpen = true;
        _nextAxisTime = 0f;
        _mainIndex = 0;

        EnsureMenuBindings();

        if (menuRoot != null && !menuRoot.activeSelf)
            menuRoot.SetActive(true);
        ShowScreen(MenuScreen.Main);

        Time.timeScale = 0f;
        Bus master = RuntimeManager.GetBus("bus:/");
        if (master.isValid())
            master.setPaused(true);

        DisableLocomotionWhilePaused();
        RefreshAllHighlights();
    }

    private void CloseMenu()
    {
        if (_isClosingMenu) return;
        _isClosingMenu = true;
        _isOpen = false;
        _screen = MenuScreen.None;

        HideAllPanels();

        Bus master = RuntimeManager.GetBus("bus:/");
        if (master.isValid())
            master.setPaused(false);

        Time.timeScale = 1f;
        SaveSettingsToStore();
        StartCoroutine(ApplyLocomotionSettingsNextFrame());
    }

    private void NavigateBack()
    {
        switch (_screen)
        {
            case MenuScreen.Settings:
            case MenuScreen.Quit:
            case MenuScreen.Controllers:
                ShowScreen(MenuScreen.Main);
                break;
            default:
                CloseMenu();
                break;
        }
    }

    private void ShowScreen(MenuScreen screen)
    {
        _screen = screen;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(screen == MenuScreen.Main);
        if (settingsMenuPanel != null) settingsMenuPanel.SetActive(screen == MenuScreen.Settings);
        if (quitMenuPanel != null) quitMenuPanel.SetActive(screen == MenuScreen.Quit);
        if (controllersMenuPanel != null) controllersMenuPanel.SetActive(screen == MenuScreen.Controllers);

        if (screen == MenuScreen.Quit)
            _quitIndex = 0;
        if (screen == MenuScreen.Settings)
        {
            _settingsMovementIndex = _moveMode == MoveMode.Teleport ? 1 : 0;
            _settingsFocusBackButton = false;
        }
        RefreshAllHighlights();
    }

    private void HideAllPanels()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsMenuPanel != null) settingsMenuPanel.SetActive(false);
        if (quitMenuPanel != null) quitMenuPanel.SetActive(false);
        if (controllersMenuPanel != null) controllersMenuPanel.SetActive(false);
    }

    private void HandleStickNavigation()
    {
        if (!TryReadNavigationAxis(out Vector2 axis))
            return;

        switch (_screen)
        {
            case MenuScreen.Main:
                if (NavigateVerticalList(axis, _mainHighlights.Count, ref _mainIndex))
                {
                    _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
                    RefreshAllHighlights();
                }
                break;
            case MenuScreen.Quit:
                if (NavigateHorizontalList(axis, _quitOptions.Count, ref _quitIndex))
                {
                    _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
                    RefreshAllHighlights();
                }
                break;
            case MenuScreen.Settings:
                NavigateSettings(axis);
                break;
        }
    }

    private void NavigateSettings(Vector2 axis)
    {
        if (_settingsFocusBackButton)
        {
            if (axis.y > axisDeadzone)
            {
                _settingsFocusBackButton = false;
                _settingsMovementIndex = 0;
                _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
                RefreshAllHighlights();
            }
            return;
        }

        if (axis.x > axisDeadzone)
        {
            _settingsMovementIndex = Mathf.Min(_settingsMovementIndex + 1, 2);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            RefreshAllHighlights();
            return;
        }

        if (axis.x < -axisDeadzone)
        {
            _settingsMovementIndex = Mathf.Max(_settingsMovementIndex - 1, 0);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            RefreshAllHighlights();
            return;
        }

        if (axis.y < -axisDeadzone)
        {
            _settingsFocusBackButton = true;
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            RefreshAllHighlights();
        }
    }

    private bool NavigateVerticalList(Vector2 axis, int count, ref int index)
    {
        if (count <= 0) return false;
        if (axis.y > axisDeadzone)
        {
            index = (index - 1 + count) % count;
            return true;
        }

        if (axis.y < -axisDeadzone)
        {
            index = (index + 1) % count;
            return true;
        }

        return false;
    }

    private bool NavigateHorizontalList(Vector2 axis, int count, ref int index)
    {
        if (count <= 0) return false;
        if (axis.x > axisDeadzone)
        {
            index = (index + 1) % count;
            return true;
        }

        if (axis.x < -axisDeadzone)
        {
            index = (index - 1 + count) % count;
            return true;
        }

        return false;
    }

    private void ActivateMainSelection()
    {
        PruneDeadMenuBindings();
        if (_mainButtons.Count == 0 || _mainIndex >= _mainButtons.Count) return;

        GameObject selected = _mainButtons[_mainIndex];
        if (!IsAlive(selected)) return;

        string name = selected.name;
        if (name.Contains("RESUME"))
        {
            CloseMenu();
            return;
        }

        if (name.Contains("restart"))
        {
            StartCoroutine(RestartSceneNextFrame());
            return;
        }

        if (name.Contains("SETTINGS"))
        {
            ShowScreen(MenuScreen.Settings);
            return;
        }

        if (name.Contains("Controller") || name.Contains("CONTROLLER") || name.Contains("Controls"))
        {
            ShowScreen(MenuScreen.Controllers);
            return;
        }

        if (name.Contains("Quit"))
            ShowScreen(MenuScreen.Quit);
    }

    private void ActivateSettingsSelection()
    {
        if (_settingsFocusBackButton)
        {
            NavigateBack();
            return;
        }

        switch (_settingsMovementIndex)
        {
            case 0:
                _moveMode = MoveMode.Linear;
                break;
            case 1:
                _moveMode = MoveMode.Teleport;
                break;
            case 2:
                _snapTurnEnabled = !_snapTurnEnabled;
                break;
        }

        ApplyLocomotionSettings();
        SaveSettingsToStore();
        RefreshSettingsVisuals();
    }

    private void ActivateQuitSelection()
    {
        if (_quitOptions.Count < 2) return;

        string name = _quitIndex == 0
            ? FindQuitButtonName(cancel: true)
            : FindQuitButtonName(cancel: false);

        if (name.Contains("cancel") || name.Contains("back") || name.Contains("Back"))
            ShowScreen(MenuScreen.Main);
        else
            QuitApplication();
    }

    private string FindQuitButtonName(bool cancel)
    {
        if (quitMenuPanel == null) return string.Empty;
        Transform buttonRoot = FindDeepChild(quitMenuPanel.transform, cancel ? "btn_cancel" : "btn_exit");
        return buttonRoot != null ? buttonRoot.name : string.Empty;
    }

    private static void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void RefreshAllHighlights()
    {
        PruneDeadMenuBindings();

        for (int i = 0; i < _mainHighlights.Count; i++)
        {
            if (!IsAlive(_mainHighlights[i])) continue;
            _mainHighlights[i].SetHighlighted(_screen == MenuScreen.Main && i == _mainIndex);
        }

        for (int i = 0; i < _quitOptions.Count; i++)
        {
            if (!IsAlive(_quitOptions[i])) continue;
            _quitOptions[i].SetHovered(_screen == MenuScreen.Quit && i == _quitIndex);
        }

        if (_screen == MenuScreen.Settings)
            RefreshSettingsVisuals();

        if (IsAlive(_controllersBackOption))
            _controllersBackOption.SetHovered(_screen == MenuScreen.Controllers);
    }

    private void RefreshSettingsVisuals()
    {
        bool focusBack = _settingsFocusBackButton;

        if (IsAlive(_settingsFreeOption))
        {
            _settingsFreeOption.SetHovered(!focusBack && _settingsMovementIndex == 0);
            _settingsFreeOption.SetCircleSelected(_moveMode == MoveMode.Linear);
        }

        if (IsAlive(_settingsTeleportOption))
        {
            _settingsTeleportOption.SetHovered(!focusBack && _settingsMovementIndex == 1);
            _settingsTeleportOption.SetCircleSelected(_moveMode == MoveMode.Teleport);
        }

        if (IsAlive(_settingsSnapOption))
        {
            _settingsSnapOption.SetHovered(!focusBack && _settingsMovementIndex == 2);
            _settingsSnapOption.SetCircleSelected(_snapTurnEnabled);
        }

        if (IsAlive(_settingsBackOption))
            _settingsBackOption.SetHovered(focusBack);
    }

    private void ApplyLockedSettingsVisuals()
    {
        if (IsAlive(_lockedTwoHandsOption))
            _lockedTwoHandsOption.SetCircleSelected(true);
        if (IsAlive(_lockedFrenchOption))
            _lockedFrenchOption.SetCircleSelected(true);
    }

    private void CacheMenuSprites()
    {
        if (settingsMenuPanel == null) return;

        foreach (var img in settingsMenuPanel.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null) continue;
            string spriteName = img.sprite.name;
            if (spriteName == "SelectCard_selected_2x")
                _selectCardHover = img.sprite;
            else if (spriteName == "SelectableCircle_selected_2x")
                _selectCircleSelected = img.sprite;
        }
    }

    private void BuildMainMenuItems()
    {
        _mainHighlights.Clear();
        _mainButtons.Clear();
        if (pauseMenuPanel == null) return;

        string[] names =
        {
            "Btn_RESUME",
            "Btn_restart",
            "Btn_SETTINGS",
            "Btn_Controllers",
            "Btn_CONTROLLERS",
            "Btn_Controls",
            "Btn_Quit"
        };

        var found = new List<(float y, GameObject go)>();
        foreach (string n in names)
        {
            Transform t = FindDeepChild(pauseMenuPanel.transform, n);
            if (t == null) continue;
            var rt = t as RectTransform;
            float y = rt != null ? rt.anchoredPosition.y : 0f;
            found.Add((y, t.gameObject));
        }

        found.Sort((a, b) => b.y.CompareTo(a.y));
        foreach (var entry in found)
        {
            _mainButtons.Add(entry.go);
            _mainHighlights.Add(EnsureHighlight(entry.go));
        }
    }

    private void BuildQuitMenuItems()
    {
        _quitOptions.Clear();
        if (quitMenuPanel == null) return;

        AddQuitCardOption("btn_cancel");
        AddQuitCardOption("btn_exit");
    }

    private void AddQuitCardOption(string childName)
    {
        Transform t = FindDeepChild(quitMenuPanel.transform, childName);
        if (t != null)
            _quitOptions.Add(EnsureCardSelectable(t.gameObject, withCircle: false));
    }

    private void BuildSettingsMenuItems()
    {
        if (settingsMenuPanel == null) return;

        _settingsMovementLine = FindDeepChild(settingsMenuPanel.transform, "Line_movement");
        _settingsFreeOption = EnsureCardOnDirectChild(_settingsMovementLine, "free", withCircle: true);
        _settingsTeleportOption = EnsureCardOnDirectChild(_settingsMovementLine, "Teleport", withCircle: true);
        _settingsSnapOption = EnsureCardOnNamedChild("btn_snapturn", withCircle: true);
        _settingsBackOption = EnsureCardOnNamedChild("Btn_back", withCircle: false);
        _lockedTwoHandsOption = EnsureCardOnNamedChild("btn_twoHands", withCircle: true);
        _lockedFrenchOption = EnsureCardOnFrench();

        HideObjectNamed("Smooth");
        HideObjectNamed("Btn_close");
    }

    private VRMenuCardSelectable EnsureCardOnNamedChild(string childName, bool withCircle)
    {
        if (settingsMenuPanel == null) return null;
        Transform t = FindDeepChild(settingsMenuPanel.transform, childName);
        return t != null ? EnsureCardSelectable(t.gameObject, withCircle) : null;
    }

    private VRMenuCardSelectable EnsureCardOnDirectChild(Transform parent, string childName, bool withCircle)
    {
        if (parent == null) return null;
        Transform t = FindDirectChild(parent, childName);
        return t != null ? EnsureCardSelectable(t.gameObject, withCircle) : null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
        }

        return null;
    }

    private VRMenuCardSelectable EnsureCardOnFrench()
    {
        if (settingsMenuPanel == null) return null;
        Transform lineLanguage = FindDeepChild(settingsMenuPanel.transform, "Line_language");
        if (lineLanguage == null) return null;

        foreach (var tmp in lineLanguage.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.text.IndexOf("French", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                GameObject go = tmp.transform.parent != null ? tmp.transform.parent.gameObject : tmp.gameObject;
                return EnsureCardSelectable(go, withCircle: true);
            }
        }

        return null;
    }

    private void BuildControllersMenuItems()
    {
        if (controllersMenuPanel == null) return;

        Transform back = FindDeepChild(controllersMenuPanel.transform, "Btn_back");
        if (back != null)
            _controllersBackOption = EnsureCardSelectable(back.gameObject, withCircle: false);

        Transform next = FindDeepChild(controllersMenuPanel.transform, "Btn_next");
        if (next != null) next.gameObject.SetActive(false);

        Transform indicator = FindDeepChild(controllersMenuPanel.transform, "Stepindicator");
        if (indicator != null) indicator.gameObject.SetActive(false);
    }

    private void HideDeprecatedSettingsUi()
    {
        HideObjectNamed("Smooth");
        HideObjectNamed("Btn_close");
    }

    private void HideObjectNamed(string objectName)
    {
        if (settingsMenuPanel == null) return;
        Transform t = FindDeepChild(settingsMenuPanel.transform, objectName);
        if (t != null) t.gameObject.SetActive(false);
    }

    private VRMenuCardSelectable EnsureCardSelectable(GameObject go, bool withCircle)
    {
        var card = go.GetComponent<VRMenuCardSelectable>();
        if (card == null)
            card = go.AddComponent<VRMenuCardSelectable>();

        card.Initialize(_selectCardHover, _selectCircleSelected, withCircle);
        return card;
    }

    private void EnsureMenuBindings()
    {
        if (_mainButtons.Count > 0 && IsAlive(_mainButtons[0]))
            return;

        CacheMenuSprites();
        BuildMainMenuItems();
        BuildQuitMenuItems();
        BuildSettingsMenuItems();
        BuildControllersMenuItems();
        ApplyLockedSettingsVisuals();
    }

    private void PruneDeadMenuBindings()
    {
        for (int i = _mainHighlights.Count - 1; i >= 0; i--)
        {
            if (IsAlive(_mainHighlights[i]) && IsAlive(_mainButtons[i])) continue;
            _mainHighlights.RemoveAt(i);
            _mainButtons.RemoveAt(i);
        }

        for (int i = _quitOptions.Count - 1; i >= 0; i--)
        {
            if (IsAlive(_quitOptions[i])) continue;
            _quitOptions.RemoveAt(i);
        }

        if (_mainIndex >= _mainButtons.Count)
            _mainIndex = Mathf.Max(0, _mainButtons.Count - 1);
        if (_quitIndex >= _quitOptions.Count)
            _quitIndex = Mathf.Max(0, _quitOptions.Count - 1);
    }

    private VRMenuTextHighlight EnsureHighlight(GameObject buttonRoot)
    {
        if (!IsAlive(buttonRoot)) return null;

        // Legacy: highlights were on TMP children and broke when TMP refreshed.
        foreach (var tmp in buttonRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (tmp.gameObject == buttonRoot) continue;
            var misplaced = tmp.GetComponent<VRMenuTextHighlight>();
            if (misplaced != null)
                Destroy(misplaced);
        }

        var highlight = buttonRoot.GetComponent<VRMenuTextHighlight>();
        if (highlight == null)
            highlight = buttonRoot.AddComponent<VRMenuTextHighlight>();

        highlight.CaptureDefaults();
        return highlight;
    }

    private void ResolvePanelReferences()
    {
        if (menuRoot == null)
            menuRoot = gameObject;

        Transform root = menuRoot.transform;
        pauseMenuPanel ??= FindPanelObject(root, "Menu_pause");
        settingsMenuPanel ??= FindPanelObject(root, "menu_settings");
        quitMenuPanel ??= FindPanelObject(root, "Menu_quit");
        controllersMenuPanel ??= FindPanelObject(root, "Step3_onboarding_controlersView");

        // Legacy scene wiring: old settings/controls panels under same root
        if (pauseMenuPanel == null)
        {
            Transform legacy = FindDeepChild(root, "MenuCanva");
            if (legacy != null) pauseMenuPanel = legacy.gameObject;
        }
    }

    private static GameObject FindPanelObject(Transform root, string panelName)
    {
        Transform t = FindDeepChild(root, panelName);
        return t != null ? t.gameObject : null;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
    }

    private void DisableLocomotionWhilePaused()
    {
        if (locomotionManager == null) return;
        if (locomotionManager.moveObject != null) locomotionManager.moveObject.SetActive(false);
        if (locomotionManager.turnObject != null) locomotionManager.turnObject.SetActive(false);
        if (locomotionManager.teleportationObject != null) locomotionManager.teleportationObject.SetActive(false);
        if (locomotionManager.teleportInteractorRight != null)
            locomotionManager.teleportInteractorRight.SetActive(false);
    }

    private void ApplyLocomotionSettings()
    {
        if (locomotionManager == null) return;
        if (locomotionManager.IsForceDisabled) return;

        locomotionManager.SetMode(_moveMode == MoveMode.Linear ? LocomotionMode.LinearSnapTurn : LocomotionMode.Teleport);
        locomotionManager.SetSnapTurnEnabled(_snapTurnEnabled);
        if (locomotionManager.snapTurnProvider != null)
            locomotionManager.snapTurnProvider.turnAmount = snapTurnAngleDegrees;
    }

    private IEnumerator ApplyLocomotionSettingsNextFrame()
    {
        yield return null;
        if (this != null && locomotionManager != null)
        {
            ApplyLocomotionSettings();
            locomotionManager.RefreshControllerInputActionManagers();
        }

        _isClosingMenu = false;
    }

    private IEnumerator CalibrateHeightBaselineAfterXrReady()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        const int maxFrames = 120;
        int frame = 0;
        while (frame < maxFrames)
        {
            var centerEye = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            bool tracked = centerEye.isValid
                && centerEye.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool isTracked)
                && isTracked;
            if (tracked) break;
            frame++;
            yield return null;
        }

        if (playerCameraOffset == null) yield break;

        _baseCameraOffsetLocalPos = playerCameraOffset.localPosition;
        _heightCalibrated = true;
        ApplyPlayerHeightOffset();
        _heightCalibrationRoutine = null;
    }

    private void ApplyPlayerHeightOffset()
    {
        if (playerCameraOffset == null || !_heightCalibrated) return;

        Vector3 lp = _baseCameraOffsetLocalPos;
        lp.y = _baseCameraOffsetLocalPos.y + _playerHeightOffset;
        playerCameraOffset.localPosition = lp;
    }

    private IEnumerator RestartSceneNextFrame()
    {
        yield return null;
        _isOpen = false;
        SaveSettingsToStore();
        Time.timeScale = 1f;
        FinalSequenceController.StopOutsideFinalMusicIfPlaying();
        if (!string.IsNullOrEmpty(restartSceneName))
            SceneManager.LoadScene(restartSceneName, LoadSceneMode.Single);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnDisable()
    {
        _isOpen = false;
        SaveSettingsToStore();
    }

    private void LoadSettingsFromStore()
    {
        _moveMode = VRSettingsStore.MoveMode == 1 ? MoveMode.Teleport : MoveMode.Linear;
        _snapTurnEnabled = VRSettingsStore.SnapEnabled;
        _playerHeightOffset = Mathf.Clamp(VRSettingsStore.HeightOffset, minPlayerHeightOffset, maxPlayerHeightOffset);
    }

    private void SaveSettingsToStore()
    {
        VRSettingsStore.MoveMode = _moveMode == MoveMode.Teleport ? 1 : 0;
        VRSettingsStore.SnapEnabled = _snapTurnEnabled;
        VRSettingsStore.SnapAngle = snapTurnAngleDegrees;
        VRSettingsStore.HeightOffset = _playerHeightOffset;
    }

    private bool TryReadNavigationAxis(out Vector2 axis)
    {
        axis = default;
        bool got = false;

        if (TryReadHandPrimary2DAxis(leftHandNode, out Vector2 left))
        {
            axis = left;
            got = true;
        }

        if (TryReadHandPrimary2DAxis(rightHandNode, out Vector2 right))
        {
            if (!got || right.sqrMagnitude > axis.sqrMagnitude)
                axis = right;
            got = true;
        }

        if (!got) return false;

        if (Mathf.Abs(axis.x) > axisDeadzone || Mathf.Abs(axis.y) > axisDeadzone)
        {
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            return true;
        }

        return false;
    }

#if ENABLE_INPUT_SYSTEM
    private static UnityEngine.InputSystem.InputDevice FindHandInputDevice(XRNode node)
    {
        if (node == XRNode.LeftHand)
        {
            var x = XRController.leftHand;
            if (x != null) return x;
        }
        else if (node == XRNode.RightHand)
        {
            var x = XRController.rightHand;
            if (x != null) return x;
        }

        var usage = node == XRNode.LeftHand
            ? UnityEngine.InputSystem.CommonUsages.LeftHand
            : UnityEngine.InputSystem.CommonUsages.RightHand;

        foreach (var d in InputSystem.devices)
        {
            if (d == null || !d.added || !d.enabled) continue;
            foreach (var u in d.usages)
            {
                if (u == usage) return d;
            }
        }

        return null;
    }
#endif

    private bool TryReadHandPrimary2DAxis(XRNode node, out Vector2 axis)
    {
#if ENABLE_INPUT_SYSTEM
        var device = FindHandInputDevice(node);
        if (device != null)
        {
            var stick = device.TryGetChildControl<Vector2Control>("primary2DAxis")
                        ?? device.TryGetChildControl<Vector2Control>("thumbstick");
            if (stick != null)
            {
                axis = stick.ReadValue();
                return true;
            }
        }
#endif
        var xrDevice = InputDevices.GetDeviceAtXRNode(node);
        if (xrDevice.isValid && xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out axis))
            return true;

        axis = default;
        return false;
    }

    private bool ReadLeftPrimaryButtonPressed() => ReadPrimaryButtonEdge(leftHandNode, ref _leftMenuButtonWasPressed);

    private bool ReadRightPrimaryButtonPressed() => ReadPrimaryButtonEdge(rightHandNode, ref _rightConfirmWasPressed);

    private static bool ReadPrimaryButtonEdge(XRNode node, ref bool wasDown)
    {
        bool isDown = ReadPrimaryButtonHeld(node);
        bool edge = isDown && !wasDown;
        wasDown = isDown;
        return edge;
    }

    private static bool ReadPrimaryButtonHeld(XRNode node)
    {
#if ENABLE_INPUT_SYSTEM
        var device = FindHandInputDevice(node);
        if (device != null)
        {
            var btn = device.TryGetChildControl<ButtonControl>("primaryButton");
            if (btn != null)
                return btn.isPressed;
        }
#endif
        var xrDevice = InputDevices.GetDeviceAtXRNode(node);
        return xrDevice.isValid
            && xrDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pressed)
            && pressed;
    }
}
