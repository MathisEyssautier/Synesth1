using UnityEngine;
using FMODUnity;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Collider))]
public class GuitarSoundZone : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GuitarCapoCrankController capoCrankController;

    [Header("Trigger")]
    [SerializeField] private string handTag = "PlayerHand";
    [SerializeField] private float triggerCooldown = 0.15f;

    [Header("FMOD Events")]
    [SerializeField] private EventReference cleanEvent;
    [SerializeField] private EventReference wrongEvent;

    private float _nextAllowedTime = 0f;

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(handTag)) return;
        if (Time.time < _nextAllowedTime) return;
        _nextAllowedTime = Time.time + triggerCooldown;

        bool inTune = capoCrankController != null && capoCrankController.IsOnTargetCrankEvenIfLocked;
        var evt = inTune ? cleanEvent : wrongEvent;
        if (!evt.IsNull)
            RuntimeManager.PlayOneShotAttached(evt, gameObject);

        if (capoCrankController != null && inTune)
            capoCrankController.TrySolveFromSoundZone();
    }
}

