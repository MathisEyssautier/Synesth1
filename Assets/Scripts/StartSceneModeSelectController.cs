using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif

/// <summary>
/// StartScene: choose Sensitive / Intermediate / Expert with left stick + A.
/// Left trigger (via StartSceneController) then loads gameplay with VRSettingsStore applied.
/// </summary>
public class StartSceneModeSelectController : MonoBehaviour
{
    private class ModeCard
    {
        public GameObject Root;
        public Image Background;
        public LocomotionPreset Preset;
    }

    [Header("UI root (optional — auto-finds Step2 / Cards)")]
    [SerializeField] private GameObject panelRoot;

    [Header("Alpha (0–255)")]
    [SerializeField] private float alphaIdle = 40f;
    [SerializeField] private float alphaHoverMin = 40f;
    [SerializeField] private float alphaHoverMax = 90f;
    [SerializeField] private float alphaSelected = 255f;

    [Header("Input")]
    [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
    [SerializeField] private XRNode rightHandNode = XRNode.RightHand;
    [SerializeField] private float axisDeadzone = 0.65f;
    [SerializeField] private float axisRepeatDelay = 0.18f;

    [Header("Hover pulse")]
    [SerializeField] private float pulseSpeed = 10f;

    private readonly List<ModeCard> _cards = new List<ModeCard>();
    private int _hoverIndex = 1;
    private int _selectedIndex = 1;
    private float _nextAxisTime;
    private bool _rightConfirmWasPressed;

    private float AlphaIdle => alphaIdle / 255f;
    private float AlphaHoverMin => alphaHoverMin / 255f;
    private float AlphaHoverMax => alphaHoverMax / 255f;
    private float AlphaSelected => alphaSelected / 255f;

    private void Start()
    {
        BuildCards();
        _selectedIndex = Mathf.Clamp(1, 0, Mathf.Max(0, _cards.Count - 1));
        _hoverIndex = _selectedIndex;
        ApplyPresetToStore();
        ApplyCardVisuals();
    }

    private void Update()
    {
        if (_cards.Count == 0) return;

        HandleStickNavigation();
        HandleConfirm();

        float pulseT = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float hoverAlpha = Mathf.Lerp(AlphaHoverMin, AlphaHoverMax, pulseT);
        UpdateHoverPulse(hoverAlpha);
    }

    private void HandleStickNavigation()
    {
        if (Time.unscaledTime < _nextAxisTime) return;
        if (!TryReadHandPrimary2DAxis(leftHandNode, out Vector2 axis)) return;

        if (axis.x > axisDeadzone)
        {
            _hoverIndex = Mathf.Min(_hoverIndex + 1, _cards.Count - 1);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            ApplyCardVisuals();
        }
        else if (axis.x < -axisDeadzone)
        {
            _hoverIndex = Mathf.Max(_hoverIndex - 1, 0);
            _nextAxisTime = Time.unscaledTime + axisRepeatDelay;
            ApplyCardVisuals();
        }
    }

    private void HandleConfirm()
    {
        if (!ReadPrimaryButtonEdge(rightHandNode, ref _rightConfirmWasPressed)) return;

        _selectedIndex = _hoverIndex;
        ApplyPresetToStore();
        ApplyCardVisuals();
    }

    private void ApplyPresetToStore()
    {
        if (_cards.Count == 0) return;
        VRSettingsStore.ApplyPreset(_cards[_selectedIndex].Preset);
    }

    private void ApplyCardVisuals()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            Image img = _cards[i].Background;
            if (img == null) continue;

            Color c = img.color;
            c.a = i == _selectedIndex ? AlphaSelected : AlphaIdle;
            img.color = c;
        }
    }

    private void UpdateHoverPulse(float hoverAlpha)
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            if (i == _selectedIndex) continue;
            if (i != _hoverIndex) continue;

            Image img = _cards[i].Background;
            if (img == null) continue;

            Color c = img.color;
            c.a = hoverAlpha;
            img.color = c;
        }
    }

    private void BuildCards()
    {
        _cards.Clear();

        Transform searchRoot = panelRoot != null ? panelRoot.transform : transform;
        Transform cardsRoot = FindDeepChild(searchRoot, "Cards") ?? searchRoot;

        TryAddCard(cardsRoot, "Card_sensitive", LocomotionPreset.Sensitive);
        TryAddCard(cardsRoot, "Card_Intermediate", LocomotionPreset.Intermediate);
        TryAddCard(cardsRoot, "Card_intermediate", LocomotionPreset.Intermediate);
        TryAddCard(cardsRoot, "Card_expert", LocomotionPreset.Expert);

        if (_cards.Count == 0)
        {
            foreach (Transform child in cardsRoot.GetComponentsInChildren<Transform>(true))
            {
                string n = child.name.ToLowerInvariant();
                if (n.Contains("sensitive"))
                    TryAddCardFromTransform(child, LocomotionPreset.Sensitive);
                else if (n.Contains("intermediate"))
                    TryAddCardFromTransform(child, LocomotionPreset.Intermediate);
                else if (n.Contains("expert"))
                    TryAddCardFromTransform(child, LocomotionPreset.Expert);
            }
        }

        _cards.Sort((a, b) =>
        {
            float ax = a.Background != null ? a.Background.rectTransform.anchoredPosition.x : 0f;
            float bx = b.Background != null ? b.Background.rectTransform.anchoredPosition.x : 0f;
            return ax.CompareTo(bx);
        });

        // Remove duplicate intermediate entries
        for (int i = _cards.Count - 1; i >= 0; i--)
        {
            for (int j = 0; j < i; j++)
            {
                if (_cards[i].Preset == _cards[j].Preset)
                {
                    _cards.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private void TryAddCard(Transform parent, string objectName, LocomotionPreset preset)
    {
        Transform t = FindDeepChild(parent, objectName);
        if (t != null)
            TryAddCardFromTransform(t, preset);
    }

    private void TryAddCardFromTransform(Transform t, LocomotionPreset preset)
    {
        foreach (var card in _cards)
        {
            if (card.Root == t.gameObject) return;
        }

        Image bg = t.GetComponent<Image>();
        if (bg == null) return;

        _cards.Add(new ModeCard
        {
            Root = t.gameObject,
            Background = bg,
            Preset = preset
        });
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase))
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null) return found;
        }

        return null;
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
