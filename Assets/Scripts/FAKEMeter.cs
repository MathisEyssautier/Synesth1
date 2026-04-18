using UnityEngine;
using System.Collections;

public class FakeMeteringSource : MonoBehaviour
{
    public float MeterLeft  { get; private set; }
    public float MeterRight { get; private set; }

    [SerializeField] private float fakeAmplitude = 50f;
    [SerializeField] private float duration = 1f;

    private Coroutine _pulseRoutine;

    // 👉 appel public depuis PianoKey
    public void TriggerPulse()
    {
        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);

        _pulseRoutine = StartCoroutine(Pulse());
    }

    private IEnumerator Pulse()
    {
        MeterLeft = fakeAmplitude;
        MeterRight = fakeAmplitude;

        yield return new WaitForSeconds(duration);

        MeterLeft = 0f;
        MeterRight = 0f;
    }
}