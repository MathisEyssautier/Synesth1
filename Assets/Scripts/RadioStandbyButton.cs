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
    [Tooltip("Profondeur (Y) quand la radio est allumée.")]
    [SerializeField] private float onPressedDepth = 0.01f;
    [Tooltip("Profondeur (Y) quand la radio est en veille (OFF).")]
    [SerializeField] private float offPressedDepth = 0.02f;
    [SerializeField] private float pressSpeed = 12f;

    private float _nextTime;
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
    }

    private void Update()
    {
        Vector3 target = GetTargetLocalPosition();
        transform.localPosition = Vector3.Lerp(transform.localPosition, target, Time.deltaTime * pressSpeed);
    }

    private Vector3 GetTargetLocalPosition()
    {
        if (radioManager == null)
            return _initialLocalPos;

        // Avant résolution piano : bouton en position haute.
        if (!radioManager.IsRadioUnlocked)
            return _initialLocalPos;

        float depth = radioManager.IsStandby ? offPressedDepth : onPressedDepth;
        return _initialLocalPos - new Vector3(0f, depth, 0f);
    }
}
