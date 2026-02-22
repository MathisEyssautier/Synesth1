using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class HeartbeatHaptic : MonoBehaviour
{
    [Header("Heartbeat Settings")]
    [SerializeField] private float bpm = 50f;
    [SerializeField] private float firstBeatAmplitude = 1.0f;
    [SerializeField] private float secondBeatAmplitude = 0.6f;
    [SerializeField] private float firstBeatDuration = 0.08f;
    [SerializeField] private float secondBeatDuration = 0.06f;
    [SerializeField] private float timeBetweenDoubleBeats = 0.40f;

    [Header("Hand References")]
    [SerializeField] private Transform leftHandTransform;
    [SerializeField] private Transform rightHandTransform;

    [Header("Pulse Visual")]
    [SerializeField] private float expandSpeed = 8f;
    [SerializeField] private float shrinkSpeed = 3f;
    [SerializeField] private float pulseScaleAmount = 0.8f;
    [SerializeField] private float pulseSpeed = 10f;

    private Vector3 _baseScale;
    private Coroutine _heartbeatCoroutine;
    private Coroutine _pulseCoroutine;
    private readonly HashSet<XRNode> _activeHands = new HashSet<XRNode>();

    private void Awake()
    {
        _baseScale = transform.localScale;
    }

    private void Start()
    {
        // Le pulse visuel tourne en permanence dès le début
        StartCoroutine(VisualLoop());
    }

    private XRNode GetHandNode(Collider handCollider)
    {
        Vector3 handPos = handCollider.transform.position;

        float distLeft = leftHandTransform != null
            ? Vector3.Distance(handPos, leftHandTransform.position)
            : float.MaxValue;

        float distRight = rightHandTransform != null
            ? Vector3.Distance(handPos, rightHandTransform.position)
            : float.MaxValue;

        return distLeft <= distRight ? XRNode.LeftHand : XRNode.RightHand;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("PlayerHand")) return;

        XRNode node = GetHandNode(other);
        _activeHands.Add(node);

        if (_heartbeatCoroutine == null)
            _heartbeatCoroutine = StartCoroutine(HapticLoop());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("PlayerHand")) return;

        XRNode node = GetHandNode(other);
        _activeHands.Remove(node);

        if (_activeHands.Count == 0 && _heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _heartbeatCoroutine = null;
        _pulseCoroutine = null;
        _activeHands.Clear();
        transform.localScale = _baseScale;
    }

    // Boucle visuelle — tourne toujours
    private IEnumerator VisualLoop()
    {
        float beatInterval = 60f / bpm;

        while (true)
        {
            TriggerPulse(firstBeatAmplitude);
            yield return new WaitForSeconds(timeBetweenDoubleBeats);

            TriggerPulse(secondBeatAmplitude);
            yield return new WaitForSeconds(beatInterval - timeBetweenDoubleBeats - firstBeatDuration);
        }
    }

    // Boucle haptique — uniquement quand une main est dans la sphère
    private IEnumerator HapticLoop()
    {
        float beatInterval = 60f / bpm;

        while (true)
        {
            SendHapticToActiveHands(firstBeatAmplitude, firstBeatDuration);
            yield return new WaitForSeconds(timeBetweenDoubleBeats);

            SendHapticToActiveHands(secondBeatAmplitude, secondBeatDuration);
            yield return new WaitForSeconds(beatInterval - timeBetweenDoubleBeats - firstBeatDuration);
        }
    }

    private void TriggerPulse(float amplitude)
    {
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        _pulseCoroutine = StartCoroutine(PulseScale(amplitude));
    }

    private IEnumerator PulseScale(float amplitude)
    {
        Vector3 targetScale = _baseScale + Vector3.one * pulseScaleAmount * amplitude;

        yield return StartCoroutine(ScaleTo(targetScale, expandSpeed));
        yield return StartCoroutine(ScaleTo(_baseScale, shrinkSpeed));
    }

    private IEnumerator ScaleTo(Vector3 target, float speed)
    {
        while (Vector3.Distance(transform.localScale, target) > 0.001f)
        {
            transform.localScale = Vector3.MoveTowards(transform.localScale, target, Time.deltaTime * speed);
            yield return null;
        }
        transform.localScale = target;
    }

    private void SendHapticToActiveHands(float amplitude, float duration)
    {
        foreach (XRNode node in _activeHands)
            SendHaptic(node, amplitude, duration);
    }

    private void SendHaptic(XRNode node, float amplitude, float duration)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
            device.SendHapticImpulse(0, amplitude, duration);
    }
}