using UnityEngine;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif

/// <summary>
/// StartScene : choix du parcours (Black Room vs Seine Lab) si les deux scènes sont dans le build.
/// Joystick gauche haut/bas pour changer ; par défaut Black Room quand les deux sont disponibles.
/// </summary>
public class StartSceneRouteSelectController : MonoBehaviour
{
    public enum GameplayRoute
    {
        BlackRoom,
        SeineLab
    }

    [Header("Scenes")]
    [SerializeField] private string blackRoomSceneName = "SynesthesiaBlackRoom";
    [SerializeField] private string seineLabSceneName = ExperienceProfile.SeineLabSceneName;

    [Header("UI (optionnel)")]
    [Tooltip("Texte / panneau « Version SeineLab » affiché quand le parcours démo est sélectionné.")]
    [SerializeField] private GameObject seineLabInfoRoot;

    [Header("Input")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private float axisDeadzone = 0.65f;
    [SerializeField] private float axisRepeatDelay = 0.22f;

    private bool _blackRoomInBuild;
    private bool _seineLabInBuild;
    private GameplayRoute _selectedRoute = GameplayRoute.BlackRoom;
    private float _nextAxisTime;

    public GameplayRoute SelectedRoute => _selectedRoute;

    public bool SeineLabRouteAvailable => _seineLabInBuild;
    public bool BlackRoomRouteAvailable => _blackRoomInBuild;
    public bool CanChooseRoute => _blackRoomInBuild && _seineLabInBuild;

    private void Start()
    {
        _blackRoomInBuild = ExperienceProfile.IsSceneInBuild(blackRoomSceneName);
        _seineLabInBuild = ExperienceProfile.IsSceneInBuild(seineLabSceneName);

        if (_seineLabInBuild && !_blackRoomInBuild)
            _selectedRoute = GameplayRoute.SeineLab;
        else if (_blackRoomInBuild)
            _selectedRoute = GameplayRoute.BlackRoom;
        else if (_seineLabInBuild)
            _selectedRoute = GameplayRoute.SeineLab;

        ApplyRouteUi();
    }

    private void Update()
    {
        if (!CanChooseRoute) return;

        if (Time.unscaledTime < _nextAxisTime) return;
        if (!TryReadHandPrimary2DAxis(leftHandNode, out Vector2 axis)) return;

        if (axis.y > axisDeadzone)
        {
            SetRoute(GameplayRoute.BlackRoom);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
        }
        else if (axis.y < -axisDeadzone)
        {
            SetRoute(GameplayRoute.SeineLab);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
        }
    }

    public void SetRoute(GameplayRoute route)
    {
        if (route == GameplayRoute.SeineLab && !_seineLabInBuild) return;
        if (route == GameplayRoute.BlackRoom && !_blackRoomInBuild) return;

        _selectedRoute = route;
        ApplyRouteUi();
    }

    /// <summary>Scène à charger selon le build et le parcours sélectionné.</summary>
    public string ResolveSceneName(string fallbackSceneName)
    {
        if (_selectedRoute == GameplayRoute.SeineLab && _seineLabInBuild)
            return seineLabSceneName;

        if (_blackRoomInBuild)
            return blackRoomSceneName;

        if (_seineLabInBuild)
            return seineLabSceneName;

        return fallbackSceneName;
    }

    private void ApplyRouteUi()
    {
        if (seineLabInfoRoot == null) return;

        bool showSeineLabInfo = _seineLabInBuild && _selectedRoute == GameplayRoute.SeineLab;
        if (seineLabInfoRoot.activeSelf != showSeineLabInfo)
            seineLabInfoRoot.SetActive(showSeineLabInfo);
    }

#if ENABLE_INPUT_SYSTEM
    private static UnityEngine.InputSystem.InputDevice FindHandInputDevice(XRNode node)
    {
        if (node == XRNode.LeftHand)
        {
            var left = XRController.leftHand;
            if (left != null) return left;
        }
        else if (node == XRNode.RightHand)
        {
            var right = XRController.rightHand;
            if (right != null) return right;
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

    private static bool TryReadHandPrimary2DAxis(XRNode node, out Vector2 axis)
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
}
