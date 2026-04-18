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
            Debug.LogWarning($"[ParticleVFXAmplitude] Aucun GrabbableMusicObject trouvé sur le parent de {gameObject.name}.");

        // Récupère tous les VFX graphs dans les enfants (incluant profondeur > 1)
        _vfxGraphs = GetComponentsInChildren<VisualEffect>(includeInactive: true);

        if (_vfxGraphs.Length == 0)
            Debug.LogWarning($"[ParticleVFXAmplitude] Aucun VisualEffect trouvé dans les enfants de {gameObject.name}.");
        else
            Debug.Log($"[ParticleVFXAmplitude] {_vfxGraphs.Length} VFX graph(s) détectés.");
    }

    private void Update()
    {
        if (musicSource == null || _vfxGraphs == null) return;

        // Moyenne des deux canaux
        float rawAmplitude = (musicSource.MeterLeft + musicSource.MeterRight) * 0.5f;

        // Lissage exponentiel
        _smoothedAmplitude = Mathf.Lerp(rawAmplitude, _smoothedAmplitude, smoothing);

        float finalValue = _smoothedAmplitude * amplitudeScale;

        // Push vers chaque VFX graph
        foreach (VisualEffect vfx in _vfxGraphs)
        {
            if (vfx.HasFloat(vfxPropertyName))
                vfx.SetFloat(vfxPropertyName, finalValue);
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
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float value = amplitude * amplitudeScale;

            foreach (VisualEffect vfx in _vfxGraphs)
            {
                if (vfx.HasFloat(vfxPropertyName))
                    vfx.SetFloat(vfxPropertyName, value);
            }

            yield return null;
        }
    }
}