using System.Collections;    
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;  
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
public class InteractableCube : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference cubeEventRef;

    [Header("Emission")]
    [SerializeField] private Renderer cubeRenderer;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private float emissionIntensity = 3f;  // intensité quand le son joue
    [SerializeField] private float fadeDuration = 0.5f;

    private EventInstance cubeSound;
    private XRGrabInteractable grabInteractable;
    private bool isSoundPlaying = false;
    private Material cubeMaterial;
    private Coroutine emissionCoroutine;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);
        grabInteractable.activated.AddListener(OnTriggerPressed);

        // Crée une instance du material pour ne pas modifier le material partagé
        cubeMaterial = cubeRenderer.material;
        cubeMaterial.EnableKeyword("_EMISSION");
        SetEmission(0f); // éteint au départ
    }

    void OnEnable()
    {
        cubeSound = RuntimeManager.CreateInstance(cubeEventRef);
        RuntimeManager.AttachInstanceToGameObject(cubeSound, gameObject);

        // Lance le son et l'émission dès que le cube apparaît
        cubeSound.start();
        isSoundPlaying = true;
        AnimateEmission(emissionIntensity);
    }

    private void OnGrabbed(SelectEnterEventArgs args) { }

    private void OnReleased(SelectExitEventArgs args) { }

    private void OnTriggerPressed(ActivateEventArgs args)
    {
        if (isSoundPlaying)
        {
            cubeSound.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            isSoundPlaying = false;
            AnimateEmission(0f);
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
        // Récupère l'intensité actuelle depuis le material
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
        // HDR color : on multiplie la couleur de base par l'intensité
        cubeMaterial.SetColor("_EmissionColor", emissionColor * intensity);
    }

    void OnDisable()
    {
        cubeSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        cubeSound.release();
    }

    void OnDestroy()
    {
        grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        grabInteractable.selectExited.RemoveListener(OnReleased);
        grabInteractable.activated.RemoveListener(OnTriggerPressed);
    }
}