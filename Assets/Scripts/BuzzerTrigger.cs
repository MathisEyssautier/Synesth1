using UnityEngine;
using FMODUnity;
using System.Collections;

public class BuzzerTrigger : MonoBehaviour
{
    [Header("FMOD")]
    public EventReference musicEvent;

    private bool isAnimating = false;
    private Vector3 initialPosition;

    [Header("Button Movement")]
    public float pressDepth = 0.02f;
    public float pressSpeed = 10f;

    private void Start()
    {
        initialPosition = transform.localPosition;
    }
    void OnTriggerEnter(Collider other)
    {
        StartCoroutine(PressAnimation());

        if (!other.CompareTag("PlayerHand")) return;

        if (MusicManager.Instance.IsPlaying())
            MusicManager.Instance.StopMusic();
        else
            MusicManager.Instance.StartMusic(musicEvent);
    }

    IEnumerator PressAnimation()
    {
        isAnimating = true;

        Vector3 pressedPosition = initialPosition - new Vector3(0, pressDepth, 0);

        while (Vector3.Distance(transform.localPosition, pressedPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, pressedPosition, Time.deltaTime * pressSpeed);
            yield return null;
        }

        while (Vector3.Distance(transform.localPosition, initialPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, initialPosition, Time.deltaTime * pressSpeed);
            yield return null;
        }

        transform.localPosition = initialPosition;
        isAnimating = false;
    }
}