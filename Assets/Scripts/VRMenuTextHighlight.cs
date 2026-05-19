using TMPro;
using UnityEngine;

/// <summary>
/// TMP label that scales up and brightens when focused in VR menu navigation.
/// </summary>
public class VRMenuTextHighlight : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI label;

    [SerializeField] private float highlightFontScale = 1.35f;
    [SerializeField] private float highlightScaleMultiplier = 1.12f;
    [SerializeField] private Color highlightColor = Color.white;

    private float _normalFontSize = 24f;
    private float _highlightFontSize = 32f;
    private Color _normalColor = new Color(1f, 1f, 1f, 0.55f);
    private Vector3 _normalLocalScale = Vector3.one;
    private FontStyles _normalFontStyle = FontStyles.Normal;

    private bool _highlighted;
    private bool _capturedDefaults;

    public TextMeshProUGUI Label
    {
        get
        {
            if (!IsAlive(this)) return null;
            if (label != null) return label;
            label = GetComponentInChildren<TextMeshProUGUI>(true);
            return label;
        }
    }

    private static bool IsAlive(Object obj) => obj != null;

    private void Awake()
    {
        CaptureDefaults();
        ApplyVisual();
    }

    public void CaptureDefaults()
    {
        if (!IsAlive(this)) return;
        var tmp = Label;
        if (tmp == null) return;

        if (!_capturedDefaults)
        {
            _normalFontSize = tmp.fontSize;
            _highlightFontSize = _normalFontSize * highlightFontScale;
            _normalColor = tmp.color;
            _normalFontStyle = tmp.fontStyle;
            _normalLocalScale = tmp.rectTransform.localScale;
            _capturedDefaults = true;
        }
    }

    public void SetHighlighted(bool highlighted)
    {
        if (!IsAlive(this)) return;
        if (_highlighted == highlighted) return;
        _highlighted = highlighted;
        ApplyVisual();
    }

    public void ForceRefresh()
    {
        if (!IsAlive(this)) return;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (!IsAlive(this)) return;
        var tmp = Label;
        if (tmp == null) return;

        if (!_capturedDefaults)
            CaptureDefaults();

        tmp.fontSize = _highlighted ? _highlightFontSize : _normalFontSize;
        tmp.color = _highlighted ? highlightColor : _normalColor;
        tmp.fontStyle = _highlighted ? FontStyles.Bold : _normalFontStyle;
        tmp.rectTransform.localScale = _highlighted
            ? _normalLocalScale * highlightScaleMultiplier
            : _normalLocalScale;
        tmp.ForceMeshUpdate();
    }
}
