using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pilote plusieurs overrides post-processing d'un Volume URP (le global volume du bureau)
/// pour donner un effet "hallucinations". Branche-le directement sur :
///  - GuitarAssemblyManager.onAllStringsPlaced  -> EnableEffect()
///  - PrismFacetPuzzleController.onPrismSolved  -> DisableEffect()
/// Effets supportés : Chromatic Aberration, Bloom, Film Grain. Chacun peut être activé ou
/// désactivé indépendamment dans l'inspector, avec ses valeurs OFF / ON.
/// (Le nom de la classe est conservé pour préserver le câblage UnityEvent existant.)
/// </summary>
[DisallowMultipleComponent]
public class OfficeChromaticAberrationDriver : MonoBehaviour
{
    [Header("Volume URP")]
    [Tooltip("Volume (Global ou local) qui contient les overrides à piloter.")]
    [SerializeField] private Volume targetVolume;

    [Header("Chromatic Aberration")]
    [SerializeField] private bool driveChromaticAberration = true;
    [Range(0f, 1f)]
    [SerializeField] private float chromaticAberrationOff = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float chromaticAberrationOn = 1f;

    [Header("Bloom")]
    [SerializeField] private bool driveBloom = true;
    [Min(0f)]
    [Tooltip("Intensité du bloom à l'état désactivé (généralement la valeur de base de ta scène).")]
    [SerializeField] private float bloomOff = 1f;
    [Min(0f)]
    [Tooltip("Intensité du bloom à l'état activé. L'utilisateur voulait 6.")]
    [SerializeField] private float bloomOn = 6f;

    [Header("Film Grain")]
    [SerializeField] private bool driveFilmGrain = true;
    [Range(0f, 1f)]
    [SerializeField] private float filmGrainOff = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float filmGrainOn = 1f;

    [Header("Transition")]
    [Tooltip("Durée du fondu en secondes pour Enable/Disable.")]
    [Min(0f)]
    [SerializeField] private float transitionDuration = 1.0f;
    [Tooltip("Si vrai, l'effet est forcé à l'état OFF au démarrage.")]
    [SerializeField] private bool forceOffOnStart = true;

    private ChromaticAberration _chromaticAberration;
    private Bloom _bloom;
    private FilmGrain _filmGrain;
    private Coroutine _transitionRoutine;

    private void Awake()
    {
        ResolveOverrides();
    }

    private void Start()
    {
        if (forceOffOnStart)
            ApplyImmediate(0f);
    }

    private void ResolveOverrides()
    {
        if (targetVolume == null)
        {
            Debug.LogWarning("[OfficeChromaticAberrationDriver] Aucun Volume assigné.", this);
            return;
        }
        if (targetVolume.profile == null)
        {
            Debug.LogWarning("[OfficeChromaticAberrationDriver] Le Volume n'a pas de Profile.", this);
            return;
        }

        if (driveChromaticAberration)
        {
            if (!targetVolume.profile.TryGet(out _chromaticAberration) || _chromaticAberration == null)
                Debug.LogWarning("[OfficeChromaticAberrationDriver] Override 'Chromatic Aberration' introuvable. Ajoute-le sur le Volume.", this);
            else
            {
                _chromaticAberration.active = true;
                _chromaticAberration.intensity.overrideState = true;
            }
        }

        if (driveBloom)
        {
            if (!targetVolume.profile.TryGet(out _bloom) || _bloom == null)
                Debug.LogWarning("[OfficeChromaticAberrationDriver] Override 'Bloom' introuvable. Ajoute-le sur le Volume.", this);
            else
            {
                _bloom.active = true;
                _bloom.intensity.overrideState = true;
            }
        }

        if (driveFilmGrain)
        {
            if (!targetVolume.profile.TryGet(out _filmGrain) || _filmGrain == null)
                Debug.LogWarning("[OfficeChromaticAberrationDriver] Override 'Film Grain' introuvable. Ajoute-le sur le Volume.", this);
            else
            {
                _filmGrain.active = true;
                _filmGrain.intensity.overrideState = true;
            }
        }
    }

    /// <summary>Active les effets (lerp jusqu'aux valeurs ON).</summary>
    public void EnableEffect()
    {
        StartTransitionTo(1f);
    }

    /// <summary>Désactive les effets (lerp jusqu'aux valeurs OFF).</summary>
    public void DisableEffect()
    {
        StartTransitionTo(0f);
    }

    /// <summary>
    /// Définit l'état immédiatement (sans fondu). 0 = OFF, 1 = ON.
    /// </summary>
    public void SetStateImmediate(float t01)
    {
        ApplyImmediate(Mathf.Clamp01(t01));
    }

    private void StartTransitionTo(float target01)
    {
        if (!HasAnyResolvedOverride())
            ResolveOverrides();

        if (_transitionRoutine != null)
            StopCoroutine(_transitionRoutine);

        if (transitionDuration <= 0f || !isActiveAndEnabled)
        {
            ApplyImmediate(target01);
            return;
        }

        _transitionRoutine = StartCoroutine(TransitionTo(target01));
    }

    private IEnumerator TransitionTo(float target01)
    {
        float startCa = _chromaticAberration != null ? _chromaticAberration.intensity.value : 0f;
        float startBloom = _bloom != null ? _bloom.intensity.value : 0f;
        float startGrain = _filmGrain != null ? _filmGrain.intensity.value : 0f;

        float endCa = Mathf.Lerp(chromaticAberrationOff, chromaticAberrationOn, target01);
        float endBloom = Mathf.Lerp(bloomOff, bloomOn, target01);
        float endGrain = Mathf.Lerp(filmGrainOff, filmGrainOn, target01);

        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / transitionDuration);

            if (driveChromaticAberration && _chromaticAberration != null)
                _chromaticAberration.intensity.value = Mathf.Lerp(startCa, endCa, k);
            if (driveBloom && _bloom != null)
                _bloom.intensity.value = Mathf.Lerp(startBloom, endBloom, k);
            if (driveFilmGrain && _filmGrain != null)
                _filmGrain.intensity.value = Mathf.Lerp(startGrain, endGrain, k);

            yield return null;
        }

        ApplyImmediate(target01);
    }

    private void ApplyImmediate(float target01)
    {
        if (!HasAnyResolvedOverride())
            ResolveOverrides();

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
            _transitionRoutine = null;
        }

        if (driveChromaticAberration && _chromaticAberration != null)
            _chromaticAberration.intensity.value = Mathf.Lerp(chromaticAberrationOff, chromaticAberrationOn, target01);
        if (driveBloom && _bloom != null)
            _bloom.intensity.value = Mathf.Lerp(bloomOff, bloomOn, target01);
        if (driveFilmGrain && _filmGrain != null)
            _filmGrain.intensity.value = Mathf.Lerp(filmGrainOff, filmGrainOn, target01);
    }

    private bool HasAnyResolvedOverride()
    {
        return _chromaticAberration != null || _bloom != null || _filmGrain != null;
    }
}
