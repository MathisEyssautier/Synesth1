using UnityEngine;
using System.Collections;

public class FakeMeteringSource : MonoBehaviour
{
    public float MeterLeft  { get; private set; }
    public float MeterRight { get; private set; }

    [SerializeField] private float fakeAmplitude = 50f;
    [SerializeField] private float defaultDuration = 1f;

    private Coroutine _pulseRoutine;

    // 👉 appel public depuis PianoKey
    // Si aucun paramètre n'est passé, la durée par défaut est 1 seconde
    public void TriggerPulse(float duration = 1f)
    {
        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);

        _pulseRoutine = StartCoroutine(Pulse(duration));
    }

    private IEnumerator Pulse(float duration)
    {
        MeterLeft = fakeAmplitude;
        MeterRight = fakeAmplitude;

        yield return new WaitForSeconds(duration);

        MeterLeft = 0f;
        MeterRight = 0f;
    }
}