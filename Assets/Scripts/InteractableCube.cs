using System;
using System.Collections;    
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;  
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
public class InteractableCube : MonoBehaviour
{
    public static event Action<InteractableCube> OnFirstDeactivated;

    [Header("FMOD")]
    [SerializeField] private EventReference cubeEventRef;

    [Header("Emission")]
    [SerializeField] private Renderer cubeRenderer;
    [SerializeField] private Color emissionColor = Color.red;
    [SerializeField] private float emissionIntensity = 3f;  // intensit? quand le son joue
    [SerializeField] private float fadeDuration = 0.5f;

    private EventInstance cubeSound;
    private XRGrabInteractable grabInteractable;
    private bool isSoundPlaying = false;
    private Material cubeMaterial;
    private Coroutine emissionCoroutine;
    private bool _hasBeenDeactivatedOnce = false;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.activated.AddListener(OnTriggerPressed);

        // Cr?e une instance du material pour ne pas modifier le material partag?
        cubeMaterial = cubeRenderer.material;
        cubeMaterial.EnableKeyword("_EMISSION");
        SetEmission(0f); // ?teint au d?part
    }

    void OnEnable()
    {
        if (cubeEventRef.IsNull)
        {
            isSoundPlaying = false;
            return;
        }

        cubeSound = RuntimeManager.CreateInstance(cubeEventRef);
        RuntimeManager.AttachInstanceToGameObject(cubeSound, gameObject);

        // Lance le son et l'?mission d?s que le cube appara?t
        cubeSound.start();
        isSoundPlaying = true;
        AnimateEmission(emissionIntensity);
    }

    private void OnGrabbed(SelectEnterEventArgs args) { }

    private void OnReleased(SelectExitEventArgs args) { }

    private void OnTriggerPressed(ActivateEventArgs args)
    {
        if (cubeEventRef.IsNull || !cubeSound.isValid())
        {
            if (!_hasBeenDeactivatedOnce)
            {
                _hasBeenDeactivatedOnce = true;
                OnFirstDeactivated?.Invoke(this);
            }
            return;
        }

        if (isSoundPlaying)
        {
            cubeSound.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            isSoundPlaying = false;
            AnimateEmission(0f);

            if (!_hasBeenDeactivatedOnce)
            {
                _hasBeenDeactivatedOnce = true;
                OnFirstDeactivated?.Invoke(this);
            }
        }
        else
        {
            cubeSound.start();
            isSoundPlaying = true;
            AnimateEmission(emissionIntensity);
        }
    }

    private void AnimateEmission(float targetIntensity)
    {
        if (emissionCoroutine != null)
            StopCoroutine(emissionCoroutine);
        emissionCoroutine = StartCoroutine(FadeEmission(targetIntensity));
    }

    private IEnumerator FadeEmission(float targetIntensity)
    {
        // R?cup?re l'intensit? actuelle depuis le material
        Color currentEmission = cubeMaterial.GetColor("_EmissionColor");
        float currentIntensity = currentEmission.maxColorComponent;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);
            float intensity = Mathf.Lerp(currentIntensity, targetIntensity, t);
            SetEmission(intensity);
            yield return null;
        }

        SetEmission(targetIntensity);
    }

    private void SetEmission(float intensity)
    {
        // HDR color : on multiplie la couleur de base par l'intensit?
        cubeMaterial.SetColor("_EmissionColor", emissionColor * intensity);
    }

    void OnDisable()
    {
        if (cubeSound.isValid())
        {
            cubeSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            cubeSound.release();
        }
    }

    void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
        grabInteractable.activated.RemoveListener(OnTriggerPressed);
    }
}