using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hover = card background (SelectCard_selected). Selected = circle fill (SelectableCircle_selected).
/// </summary>
public class VRMenuCardSelectable : MonoBehaviour
{
    [SerializeField] private bool useCircleIndicator = true;
    [SerializeField] private string circleChildName = "Image";

    private Image _background;
    private Image _circle;
    private Sprite _backgroundDefault;
    private Sprite _backgroundHover;
    private Sprite _circleDefault;
    private Sprite _circleSelected;
    private bool _hovered;
    private bool _circleOn;

    public void Initialize(Sprite cardHover, Sprite circleSelected, bool withCircle = true)
    {
        useCircleIndicator = withCircle;
        _background = GetComponent<Image>();
        if (_background != null)
            _backgroundDefault = _background.sprite;

        _backgroundHover = cardHover != null ? cardHover : _backgroundDefault;

        if (useCircleIndicator)
        {
            Transform circleT = transform.Find(circleChildName);
            if (circleT != null)
            {
                _circle = circleT.GetComponent<Image>();
                if (_circle != null)
                    _circleDefault = _circle.sprite;
            }

            _circleSelected = circleSelected != null ? circleSelected : _circleDefault;
        }

        ApplyVisuals();
    }

    private static bool IsAlive(Object obj) => obj != null;

    public void SetHovered(bool hovered)
    {
        if (!IsAlive(this)) return;
        if (_hovered == hovered) return;
        _hovered = hovered;
        ApplyVisuals();
    }

    public void SetCircleSelected(bool selected)
    {
        if (!IsAlive(this)) return;
        if (!useCircleIndicator) return;
        if (_circleOn == selected) return;
        _circleOn = selected;
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (!IsAlive(this)) return;
        if (_background != null)
            _background.sprite = _hovered ? _backgroundHover : _backgroundDefault;

        if (useCircleIndicator && _circle != null)
            _circle.sprite = _circleOn ? _circleSelected : _circleDefault;
    }
}
