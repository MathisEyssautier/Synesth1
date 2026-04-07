using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RadioStandbyButton : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RadioManager radioManager;

    [Header("Input")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float cooldown = 0.2f;

    [Header("Button Animation")]
    [SerializeField] private float pressDepth = 0.01f;
    [SerializeField] private float pressSpeed = 12f;

    private float _nextTime;
    private bool _isAnimating;
    private Vector3 _initialLocalPos;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        _initialLocalPos = transform.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextTime) return;
        if (radioManager == null) return;

        _nextTime = Time.time + cooldown;
        radioManager.ToggleStandby();

        if (!_isAnimating)
            StartCoroutine(PressAnimation());
    }

    private IEnumerator PressAnimation()
    {
        _isAnimating = true;
        Vector3 pressed = _initialLocalPos - new Vector3(0f, pressDepth, 0f);

        while (Vector3.Distance(transform.localPosition, pressed) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, pressed, Time.deltaTime * pressSpeed);
            yield return null;
        }

        while (Vector3.Distance(transform.localPosition, _initialLocalPos) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, _initialLocalPos, Time.deltaTime * pressSpeed);
            yield return null;
        }

        transform.localPosition = _initialLocalPos;
        _isAnimating = false;
    }
}
