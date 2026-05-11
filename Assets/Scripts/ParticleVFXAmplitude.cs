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

    [Tooltip("Valeur max envoyée au VFX. ATTENTION : dans Particles.vfx la formule est Amplitude*180000 = SpawnRate, et la capacité est 2000 particules / lifetime ~0.4s = ~5000 part/s max. Donc Amplitude max sûre ≈ 0.03. Au-delà, le système overflow et NE RENDS RIEN. 0 = pas de plafond.")]
    [SerializeField] private float maxAmplitudeOutput = 0.05f;

    [Header("Idle (ex. coquillages)")]
    [Tooltip("Ajouté à l'amplitude mesurée : particules légères même quand le metering FMOD est à 0 (objet pas à l'oreille ou event très faible).")]
    [SerializeField] private float baselineAmplitude = 0f;

    [Header("DIAGNOSTIC BUILD (à désactiver en prod)")]
    [Tooltip("DEBUG: si activé, ignore le FMOD metering et envoie cette valeur fixe au VFX. Utile pour tester si le VFX rend bien sur Quest même sans metering FMOD. ATTENTION : utiliser une PETITE valeur (~0.02), sinon le spawn rate VFX overflow et plus rien ne rend !")]
    [SerializeField] private bool forceFixedAmplitudeForBuildTest = false;
    [Tooltip("Valeur d'amplitude forcée. ATTENTION : Particles.vfx fait SpawnRate = Amplitude * 180000. Avec capacité 2000 / lifetime 0.4s, max sûre ≈ 0.02-0.03. NE PAS METTRE 5 ou 10 — overflow garanti et 0 particule rendue.")]
    [SerializeField] private float forcedAmplitudeValue = 0.02f;
    [Tooltip("DEBUG: active des Debug.Log pour voir dans adb logcat ce qui se passe.")]
    [SerializeField] private bool verboseDiagnosticLogs = false;

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
            Debug.Log($"[ParticleVFXAmplitude] {_vfxGraphs.Length} VFX graph(s) détectés sur {gameObject.name}.");

        if (verboseDiagnosticLogs)
        {
            Debug.Log($"[ParticleVFXAmplitudeDIAG] {gameObject.name}: forceFixedAmplitudeForBuildTest={forceFixedAmplitudeForBuildTest}, forcedAmplitudeValue={forcedAmplitudeValue}, vfxGraphsCount={(_vfxGraphs != null ? _vfxGraphs.Length : 0)}, propertyName=\"{vfxPropertyName}\"");
            if (_vfxGraphs != null)
            {
                for (int i = 0; i < _vfxGraphs.Length; i++)
                {
                    var v = _vfxGraphs[i];
                    if (v == null) continue;
                    bool hasProp = v.HasFloat(vfxPropertyName);
                    Debug.Log($"[ParticleVFXAmplitudeDIAG]  - VFX[{i}] '{v.gameObject.name}' (active={v.gameObject.activeInHierarchy}, enabled={v.enabled}, hasFloatProperty={hasProp}, visualEffectAsset={v.visualEffectAsset?.name})");
                }
            }
        }
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

        if (forceFixedAmplitudeForBuildTest)
        {
            PushAmplitudeToVfx(forcedAmplitudeValue);
            return;
        }

        float rawAmplitude = 0f;
        if (musicSource != null)
            rawAmplitude = (musicSource.MeterLeft + musicSource.MeterRight) * 0.5f;

        _smoothedAmplitude = Mathf.Lerp(rawAmplitude, _smoothedAmplitude, smoothing);

        float finalValue = (_smoothedAmplitude + baselineAmplitude) * amplitudeScale;

        PushAmplitudeToVfx(finalValue);
    }

    private void PushAmplitudeToVfx(float value)
    {
        if (_vfxGraphs == null) return;
        if (maxAmplitudeOutput > 0f)
            value = Mathf.Clamp(value, 0f, maxAmplitudeOutput);

        int pushed = 0;
        foreach (VisualEffect vfx in _vfxGraphs)
        {
            if (vfx != null && vfx.HasFloat(vfxPropertyName))
            {
                vfx.SetFloat(vfxPropertyName, value);
                pushed++;
            }
        }

        if (verboseDiagnosticLogs && Time.frameCount % 120 == 0)
        {
            Debug.Log($"[ParticleVFXAmplitudeDIAG] {gameObject.name} push amplitude={value:F2} to {pushed}/{(_vfxGraphs != null ? _vfxGraphs.Length : 0)} VFX (frame {Time.frameCount})");
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