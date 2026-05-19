using UnityEngine;

/// <summary>
/// Léger mouvement sinusoïdal sur un axe local (Y par défaut).
/// </summary>
public class LocalAxisBobbing : MonoBehaviour
{
    [SerializeField] private float amplitude = 0.015f;
    [SerializeField] private float speed = 2.5f;
    [SerializeField] private Vector3 localAxis = Vector3.up;

    private Vector3 _baseLocalPosition;

    private void Awake()
    {
        _baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        float bob = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localPosition = _baseLocalPosition + localAxis.normalized * bob;
    }
}
