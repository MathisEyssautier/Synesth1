using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Collider))]
public class HapticContactTrigger : MonoBehaviour
{
    [Header("Contact")]
    [SerializeField] private string handTag = "PlayerHand";

    [Header("Optional puzzle gating (stop haptics when solved)")]
    [SerializeField] private ShellPuzzleManager shellPuzzleManager;

    [Header("Haptics")]
    [Range(0f, 1f)]
    [SerializeField] private float intensity = 1f;
    [Tooltip("Multiplier d'intensité (valeur finale clampée à 1).")]
    [Range(0.1f, 3f)]
    [SerializeField] private float intensityMultiplier = 2f;
    [Tooltip("Durée d'une impulsion (on la renvoie en boucle pour faire une vibration continue).")]
    [SerializeField] private float pulseDuration = 0.08f;
    [Tooltip("Intervalle entre impulsions. Doit être >= pulseDuration.")]
    [SerializeField] private float pulseInterval = 0.09f;

    private Coroutine _loop;
    private Collider _currentHandCollider;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        if (shellPuzzleManager == null)
            shellPuzzleManager = FindFirstObjectByType<ShellPuzzleManager>();
    }

    private void Update()
    {
        if (_loop != null && IsPuzzleSolved())
            StopLoop();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        _currentHandCollider = other;
        StartLoopIfNeeded();
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        _currentHandCollider = other;
        StartLoopIfNeeded();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_currentHandCollider == null) return;
        if (other != _currentHandCollider) return;
        StopLoop();
    }

    private void StartLoopIfNeeded()
    {
        if (_loop != null) return;
        if (IsPuzzleSolved()) return;
        if (_currentHandCollider == null) return;

        _loop = StartCoroutine(HapticLoop());
    }

    private void StopLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        _currentHandCollider = null;
    }

    private IEnumerator HapticLoop()
    {
        float interval = Mathf.Max(0.01f, pulseInterval);
        float duration = Mathf.Clamp(pulseDuration, 0.01f, interval);
        float amp = Mathf.Clamp01(intensity * intensityMultiplier);

        while (_currentHandCollider != null && !IsPuzzleSolved())
        {
            SendHapticToHand(_currentHandCollider, amp, duration);
            yield return new WaitForSeconds(interval);
        }

        _loop = null;
    }

    private bool IsPuzzleSolved()
    {
        if (shellPuzzleManager != null && shellPuzzleManager.IsSolved) return true;
        return false;
    }

    private void SendHapticToHand(Collider handCollider, float amp, float dur)
    {
        // Best case: we are inside an XRBaseInputInteractor hierarchy.
        var inputInteractor = handCollider.GetComponentInParent<XRBaseInputInteractor>();
        if (inputInteractor != null)
        {
            inputInteractor.SendHapticImpulse(amp, dur);
            return;
        }

        // Fallback: try sending to devices (best-effort).
        TrySendToDeviceHeuristic(handCollider.transform, amp, dur);
    }

    private void TrySendToDeviceHeuristic(Transform handTransform, float amp, float dur)
    {
        // Ton setup: les colliders sont sur ces objets.
        // On mappe directement "Left Controller"/"Right Controller" -> XRNode.
        string n = handTransform.name;
        if (n == "Left Controller" || n.Contains("Left Controller"))
        {
            TrySendToNode(XRNode.LeftHand, amp, dur);
            return;
        }
        if (n == "Right Controller" || n.Contains("Right Controller"))
        {
            TrySendToNode(XRNode.RightHand, amp, dur);
            return;
        }

        // Sécurité: si le collider est sur un enfant, on regarde 2 parents max.
        var p = handTransform.parent;
        int depth = 0;
        while (p != null && depth < 2)
        {
            string pn = p.name;
            if (pn == "Left Controller" || pn.Contains("Left Controller"))
            {
                TrySendToNode(XRNode.LeftHand, amp, dur);
                return;
            }
            if (pn == "Right Controller" || pn.Contains("Right Controller"))
            {
                TrySendToNode(XRNode.RightHand, amp, dur);
                return;
            }
            p = p.parent;
            depth++;
        }
    }

    private bool TrySendToNode(XRNode node, float amp, float dur)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return false;
        if (!device.TryGetHapticCapabilities(out var caps)) return false;
        if (!caps.supportsImpulse || caps.numChannels <= 0) return false;
        return device.SendHapticImpulse(0u, amp, dur);
    }
}

