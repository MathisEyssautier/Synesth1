using UnityEngine;
using System.Collections;
using FMODUnity;

public class MusicButton : MonoBehaviour
{
    [Header("Audio FMOD")]
    private StudioEventEmitter emitter;

    [Header("Visual")]
    public Renderer cubeRenderer;
    public Color offColor = Color.red;
    public Color onColor = Color.green;

    [Header("Button Movement")]
    public float pressDepth = 0.02f;
    public float pressSpeed = 10f;

    private bool isPlaying = false;
    private bool isAnimating = false;
    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.localPosition;
        cubeRenderer.material.color = offColor;
        emitter = GetComponent<StudioEventEmitter>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PlayerHand"))
        {
            if (!isAnimating)
            {
                ToggleMusic();
                StartCoroutine(PressAnimation());
            }
        }
    }

    void ToggleMusic()
    {
        isPlaying = !isPlaying;

        if (isPlaying)
        {
            emitter.Play();
            cubeRenderer.material.color = onColor;
        }
        else
        {
            emitter.Stop();
            cubeRenderer.material.color = offColor;
        }
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