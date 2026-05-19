using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using FMODUnity;
using FMOD.Studio;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class RingingPhone : MonoBehaviour
{
    [Header("FMOD")]
    [SerializeField] private EventReference phoneEventRef;
    [Range(0f, 1f)]
    [SerializeField] private float onVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float offVolume = 0f;

    [Header("Emission (écran / corps rose)")]
    [SerializeField] private Renderer phoneRenderer;
    [SerializeField] private Color emissionColor = Color.magenta;
    [SerializeField] private float emissionOnIntensity = 3f;
    [SerializeField] private float emissionOffIntensity = 0f;
    [SerializeField] private float fadeDuration = 0.3f;

    private EventInstance _phoneInstance;
    private XRGrabInteractable _grab;
    private Rigidbody _rb;
    private Material _phoneMaterial;
    private Coroutine _emissionRoutine;

    private bool _isOn;

    public bool IsOn => _isOn;

    public static event Action<RingingPhone, bool> OnStateChanged;

    private void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();

        _grab.activated.AddListener(OnActivate);

        if (phoneRenderer != null)
        {
            _phoneMaterial = phoneRenderer.material;
            _phoneMaterial.EnableKeyword("_EMISSION");
        }

        // Réglages "classiques" pour un objet qu'on peut prendre.
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void OnEnable()
    {
        if (!phoneEventRef.IsNull)
        {
            _phoneInstance = RuntimeManager.CreateInstance(phoneEventRef);
            RuntimeManager.AttachInstanceToGameObject(_phoneInstance, gameObject);
            _phoneInstance.start();
        }

        // Par défaut: téléphone allumé qui sonne.
        SetState(true, instant: true);
    }

    private void OnDisable()
    {
        if (_phoneInstance.isValid())
        {
            _phoneInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            _phoneInstance.release();
        }
    }

    private void OnDestroy()
    {
        _grab.activated.RemoveListener(OnActivate);
    }

    private void OnActivate(ActivateEventArgs args)
    {
        if (!_grab.isSelected)
            return;

        SetState(!_isOn, instant: false);
    }

    private void SetState(bool turnOn, bool instant)
    {
        bool changed = _isOn != turnOn;
        _isOn = turnOn;

        ApplyStatePresentation(turnOn, instant);

        if (changed)
            OnStateChanged?.Invoke(this, _isOn);
    }

    private void ApplyStatePresentation(bool turnOn, bool instant)
    {
        if (_phoneInstance.isValid())
        {
            float targetVolume = turnOn ? onVolume : offVolume;
            _phoneInstance.setVolume(targetVolume);
        }

        float targetIntensity = turnOn ? emissionOnIntensity : emissionOffIntensity;
        AnimateEmission(targetIntensity, instant);
    }

    private void AnimateEmission(float targetIntensity, bool instant)
    {
        if (_phoneMaterial == null)
            return;

        if (instant || fadeDuration <= 0f)
        {
            SetEmission(targetIntensity);
            return;
        }

        if (_emissionRoutine != null)
            StopCoroutine(_emissionRoutine);
        _emissionRoutine = StartCoroutine(FadeEmission(targetIntensity));
    }

    private IEnumerator FadeEmission(float targetIntensity)
    {
        Color currentEmission = _phoneMaterial.GetColor("_EmissionColor");
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
        _phoneMaterial.SetColor("_EmissionColor", emissionColor * intensity);
    }
}

