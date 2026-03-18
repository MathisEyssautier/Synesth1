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
    private Material _matInstance;
    private Color _baseColor;

    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        // Cache une instance du material pour éviter des allocations en Update().
        _matInstance = _renderer.material;
        _baseColor = _matInstance.color;
    }

    void Update()
    {
        bool isTraversable = MusicManager.CurrentInstrument == wallInstrument;

        _collider.enabled = !isTraversable;

        float targetAlpha = isTraversable ? transparentAlpha : visibleAlpha;
        Color current = _matInstance.color;
        float newAlpha = Mathf.Lerp(current.a, targetAlpha, Time.deltaTime * transitionSpeed);
        _matInstance.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, newAlpha);
    }
}
