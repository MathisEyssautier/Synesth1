using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class ColorRoomButton : MonoBehaviour
{
    [Header("Volume")]
    public Volume globalVolume;
    public float speed = 200f;
    public float returnSpeed = 100f;

    private ColorAdjustments colorAdjustments;
    private bool isActive = false;
    private bool isAnimating = false;

    private Coroutine colorCoroutine;

    private float initialHue;

    [Header("Visual")]
    public Renderer cubeRenderer;
    public Color offColor = Color.red;
    public Color onColor = Color.green;

    [Header("Button Movement")]
    public float pressDepth = 0.02f;
    public float pressSpeed = 10f;

    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.localPosition;

        if (globalVolume.profile.TryGet(out colorAdjustments))
        {
            colorAdjustments.hueShift.overrideState = true;
            initialHue = colorAdjustments.hueShift.value;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("PlayerHand")) return;
        if (isAnimating) return;

        ToggleColorEffect();
        StartCoroutine(PressAnimation());
    }

    public void ToggleColorEffect()
    {
        isActive = !isActive;

        if (isActive)
        {
            cubeRenderer.material.color = onColor;
            colorCoroutine = StartCoroutine(ChangeColors());
        }
        else
        {
            cubeRenderer.material.color = offColor;

            if (colorCoroutine != null)
                StopCoroutine(colorCoroutine);

            StartCoroutine(ReturnToInitial());
        }
    }

    IEnumerator ChangeColors()
    {
        while (true)
        {
            colorAdjustments.hueShift.value = Random.Range(-180f, 180f);
            yield return new WaitForSeconds(0.05f);
        }
    }

    IEnumerator ReturnToInitial()
    {
        while (Mathf.Abs(colorAdjustments.hueShift.value - initialHue) > 0.5f)
        {
            colorAdjustments.hueShift.value = Mathf.MoveTowards(
                colorAdjustments.hueShift.value,
                initialHue,
                returnSpeed * Time.deltaTime);

            yield return null;
        }

        colorAdjustments.hueShift.value = initialHue;
    }

    IEnumerator PressAnimation()
    {
        isAnimating = true;

        Vector3 pressedPosition = initialPosition - new Vector3(0, pressDepth, 0);

        while (Vector3.Distance(transform.localPosition, pressedPosition) > 0.001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition,pressedPosition, Time.deltaTime * pressSpeed);
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
