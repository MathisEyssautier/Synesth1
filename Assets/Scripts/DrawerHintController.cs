using UnityEngine;

/// <summary>
/// Indice flèche (canvas) sur un tiroir : visible + bobbing tant que le tiroir est fermé,
/// masqué dès qu'il est tiré, réaffiché à la fermeture.
/// </summary>
public class DrawerHintController : MonoBehaviour
{
    public enum BobLocalAxis
    {
        LocalX = 0,
        LocalY = 1,
        LocalZ = 2
    }

    [Header("Refs")]
    [SerializeField] private DrawerGrab drawerGrab;
    [Tooltip("Root du canvas flèche (souvent enfant du bureau ou du tiroir).")]
    [SerializeField] private GameObject hintCanvasRoot;

    [Header("Bobbing")]
    [SerializeField] private float hintBobAmplitude = 0.015f;
    [SerializeField] private float hintBobSpeed = 2.5f;
    [Tooltip("Axe local du canvas (gizmo Unity) : Y = vert, Z = bleu, X = rouge.")]
    [SerializeField] private BobLocalAxis bobLocalAxis = BobLocalAxis.LocalY;

    private Vector3 _hintBaseLocalPos;
    private bool _hintBaseCaptured;

    private void Awake()
    {
        if (drawerGrab == null)
            drawerGrab = GetComponentInParent<DrawerGrab>();

        CaptureHintBase();
    }

    private void CaptureHintBase()
    {
        if (hintCanvasRoot == null) return;

        _hintBaseLocalPos = hintCanvasRoot.transform.localPosition;
        _hintBaseCaptured = true;
    }

    private void Update()
    {
        UpdateHintCanvas();
    }

    private void UpdateHintCanvas()
    {
        if (hintCanvasRoot == null) return;

        bool showHint = drawerGrab == null || drawerGrab.IsClosed;
        if (hintCanvasRoot.activeSelf != showHint)
            hintCanvasRoot.SetActive(showHint);

        if (!showHint) return;

        if (!_hintBaseCaptured)
            CaptureHintBase();

        float bob = Mathf.Sin(Time.time * hintBobSpeed) * hintBobAmplitude;
        Vector3 offset = GetBobOffsetInParentLocal(bob);
        hintCanvasRoot.transform.localPosition = _hintBaseLocalPos + offset;
    }

    /// <summary>
    /// Déplacement le long d'un axe du canvas (pas du parent), converti en local du parent.
    /// </summary>
    private Vector3 GetBobOffsetInParentLocal(float bob)
    {
        Transform t = hintCanvasRoot.transform;
        Vector3 axisInCanvasLocal = bobLocalAxis switch
        {
            BobLocalAxis.LocalX => Vector3.right,
            BobLocalAxis.LocalZ => Vector3.forward,
            _ => Vector3.up
        };

        Vector3 offsetWorld = t.TransformVector(axisInCanvasLocal * bob);

        if (t.parent != null)
            return t.parent.InverseTransformVector(offsetWorld);

        return offsetWorld;
    }
}
