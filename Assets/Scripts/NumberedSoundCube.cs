using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
public class NumberedSoundCube : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference cubeEventRef;
    [Tooltip("Volume quand le cube est allumé.")]
    [Range(0f, 1f)]
    [SerializeField] private float unmutedVolume = 1f;

    [Header("Emission")]
    [SerializeField] private Renderer cubeRenderer;
    [SerializeField] private Color emissionColor = Color.white;
    [SerializeField] private float emissionIntensity = 3f;
    [SerializeField] private float fadeDuration = 0.25f;

    [Header("Chiffre (Canvas enfant)")]
    [SerializeField] private GameObject numberCanvasRoot;

    private XRGrabInteractable _grabInteractable;
    private EventInstance _cubeSound;
    private Material _cubeMaterial;
    private Coroutine _emissionCoroutine;

    private bool _isMuted = false;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _grabInteractable.activated.AddListener(OnActivated);

        if (cubeRenderer != null)
        {
            _cubeMaterial = cubeRenderer.material;
            _cubeMaterial.EnableKeyword("_EMISSION");
        }
    }

    private void OnEnable()
    {
        _cubeSound = RuntimeManager.CreateInstance(cubeEventRef);
        RuntimeManager.AttachInstanceToGameObject(_cubeSound, gameObject);
        _cubeSound.start();

        SetMuted(false, instant: true);
    }

    private void OnDisable()
    {
        _cubeSound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _cubeSound.release();
    }

    private void OnDestroy()
    {
        if (_grabInteractable != null)
            _grabInteractable.activated.RemoveListener(OnActivated);
    }

    private void OnActivated(ActivateEventArgs args)
    {
        // activé uniquement quand il est sélectionné (tenu)
        SetMuted(!_isMuted, instant: false);
    }

    private void SetMuted(bool muted, bool instant)
    {
        _isMuted = muted;

        float targetVolume = muted ? 0f : unmutedVolume;
        _cubeSound.setVolume(targetVolume);

        if (numberCanvasRoot != null)
            numberCanvasRoot.SetActive(!muted);

        float targetEmission = muted ? 0f : emissionIntensity;
        if (_cubeMaterial != null)
        {
            if (instant || fadeDuration <= 0f)
            {
                SetEmission(targetEmission);
            }
            else
            {
                if (_emissionCoroutine != null) StopCoroutine(_emissionCoroutine);
                _emissionCoroutine = StartCoroutine(FadeEmission(targetEmission));
            }
        }
    }

    private IEnumerator FadeEmission(float targetIntensity)
    {
        Color currentEmission = _cubeMaterial.GetColor("_EmissionColor");
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
        _cubeMaterial.SetColor("_EmissionColor", emissionColor * intensity);
    }
}

