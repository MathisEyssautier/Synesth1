using UnityEngine;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]
public class WallController : MonoBehaviour
{
    [Header("Quel instrument rend ce mur traversable ?")]
    public string wallInstrument = "guitar"; // "guitar" ou "piano"

    [Header("Transparence")]
    [Range(0f, 1f)] public float visibleAlpha = 1f;
    [Range(0f, 1f)] public float transparentAlpha = 0.15f;
    public float transitionSpeed = 3f;

    private Renderer _renderer;
    private Collider _collider;
    private Color _baseColor;

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        _baseColor = _renderer.material.color;
    }

    void Update()
    {
        bool isTraversable = MusicManager.CurrentInstrument == wallInstrument;

        _collider.enabled = !isTraversable;

        float targetAlpha = isTraversable ? transparentAlpha : visibleAlpha;
        Color current = _renderer.material.color;
        float newAlpha = Mathf.Lerp(current.a, targetAlpha, Time.deltaTime * transitionSpeed);
        _renderer.material.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, newAlpha);
    }
}
