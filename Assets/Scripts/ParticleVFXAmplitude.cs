using UnityEngine;
using UnityEngine.VFX;
using System.Collections;

/// <summary>
/// À attacher sur ParticleObject (enfant direct de l'iPod).
/// Lit les valeurs de metering FMOD exposées par GrabbableMusicObject
/// sur le parent, calcule la moyenne L+R et la pousse à tous les
/// VisualEffect enfants sous la propriété "Amplitude".
/// </summary>
public class ParticleVFXAmplitude : MonoBehaviour
{
    [Header("Références")]
    [Tooltip("Laisser vide : sera cherché automatiquement sur le parent.")]
    [SerializeField] private FMODMeteringSource musicSource;

    [Header("Amplitude")]
    [Tooltip("Nom de la propriété float exposée dans chaque VFX Graph.")]
    [SerializeField] private string vfxPropertyName = "Amplitude";

    [Tooltip("Lissage (0 = aucun, valeurs proches de 1 = très lent).")]
    [Range(0f, 0.99f)]
    [SerializeField] private float smoothing = 0.1f;

    [Tooltip("Multiplicateur appliqué avant d'envoyer la valeur au VFX.")]
    [SerializeField] private float amplitudeScale = 1f;

    [Tooltip("Valeur max envoyée au VFX (metering FMOD peut dépasser 1000). 0 = pas de plafond.")]
    [SerializeField] private float maxAmplitudeOutput = 20f;

    [Header("Idle (ex. coquillages)")]
    [Tooltip("Ajouté à l'amplitude mesurée : particules légères même quand le metering FMOD est à 0 (objet pas à l'oreille ou event très faible).")]
    [SerializeField] private float baselineAmplitude = 0f;

    // Tous les VFX graphs dans les enfants (les 3 systèmes de particules)
    private VisualEffect[] _vfxGraphs;
    private float _smoothedAmplitude;
    private Coroutine _pulseRoutine;

    private void Awake()
    {
        // Récupération automatique du musicSource sur le parent si non assigné

        if (musicSource == null)
            musicSource = GetComponentInParent<FMODMeteringSource>();

        if (musicSource == null)
            Debug.LogWarning($"[ParticleVFXAmplitude] Aucun FMODMeteringSource sur les parents de {gameObject.name} — metering désactivé ; utilisez TriggerAmplitudePulse ou assignez un provider avec EventInstance.");

        CacheVfxGraphs();

        if (_vfxGraphs == null || _vfxGraphs.Length == 0)
            Debug.LogWarning($"[ParticleVFXAmplitude] Aucun VisualEffect trouvé dans les enfants de {gameObject.name}.");
        else
            Debug.Log($"[ParticleVFXAmplitude] {_vfxGraphs.Length} VFX graph(s) détectés.");
    }

    private void CacheVfxGraphs()
    {
        if (_vfxGraphs == null || _vfxGraphs.Length == 0)
            _vfxGraphs = GetComponentsInChildren<VisualEffect>(includeInactive: true);
    }

    private void Update()
    {
        // Pendant un pulse, ne pas écraser avec le metering (PlayOneShot / one-shots = souvent 0).
        if (_pulseRoutine != null)
            return;

        CacheVfxGraphs();
        if (_vfxGraphs == null || _vfxGraphs.Length == 0)
            return;

        float rawAmplitude = 0f;
        if (musicSource != null)
            rawAmplitude = (musicSource.MeterLeft + musicSource.MeterRight) * 0.5f;

        // Lissage exponentiel
        _smoothedAmplitude = Mathf.Lerp(rawAmplitude, _smoothedAmplitude, smoothing);

        float finalValue = (_smoothedAmplitude + baselineAmplitude) * amplitudeScale;

        PushAmplitudeToVfx(finalValue);
    }

    private void PushAmplitudeToVfx(float value)
    {
        if (_vfxGraphs == null) return;
        if (maxAmplitudeOutput > 0f)
            value = Mathf.Clamp(value, 0f, maxAmplitudeOutput);

        foreach (VisualEffect vfx in _vfxGraphs)
        {
            if (vfx != null && vfx.HasFloat(vfxPropertyName))
                vfx.SetFloat(vfxPropertyName, value);
        }
    }

    public void TriggerAmplitudePulse(float amplitude = 50f, float duration = 1f)
    {
        if (_pulseRoutine != null)
            StopCoroutine(_pulseRoutine);

        _pulseRoutine = StartCoroutine(PulseRoutine(amplitude, duration));
    }

    private IEnumerator PulseRoutine(float amplitude, float duration)
    {
        CacheVfxGraphs();
        if (_vfxGraphs == null || _vfxGraphs.Length == 0)
        {
            _pulseRoutine = null;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float value = amplitude * amplitudeScale;
            PushAmplitudeToVfx(value);

            yield return null;
        }

        _pulseRoutine = null;
    }
}