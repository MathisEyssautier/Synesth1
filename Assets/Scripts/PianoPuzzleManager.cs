using UnityEngine;
using UnityEngine.Events;
using FMODUnity;

public class PianoPuzzleManager : MonoBehaviour
{
    [Header("Touches (dans l'ordre 1..5)")]
    [SerializeField] private PianoKey[] keys;

    [Header("Séquence à jouer (KeyId)")]
    [Tooltip("Par défaut: 2 puis 4 puis 3 puis 5 puis 1")]
    [SerializeField] private int[] requiredOrder = new[] { 2, 4, 3, 5, 1 };

    [Header("Réussite")]
    [SerializeField] private GameObject onSuccessActivate;
    [SerializeField] private bool disableKeysOnSuccess = true;

    [Header("Réussite — visuel piano")]
    [Tooltip("Tous les meshes du piano (caisse, couvercle, etc.) dont le shader expose le bool Success.")]
    [SerializeField] private Renderer[] pianoSuccessRenderers;
    [SerializeField] private string pianoSuccessBoolProperty = "Success";

    [Header("Réussite — audio piano")]
    [SerializeField] private EventReference pianoSuccessSound;
    [SerializeField] private Transform pianoSuccessSoundOrigin;

    [Header("Réussite — radio (potards + son + blanc)")]
    [SerializeField] private RadioManager radioManager;

    [Header("Feedback visuel séquence (boules)")]
    [Tooltip("Boules d'étape dans l'ordre de la séquence (ex: 4 boules pour 4 notes).")]
    [SerializeField] private Renderer[] sequenceIndicators;
    [Tooltip("Couleur affichée pour chaque étape validée. Exemple: jaune, bleu, rose, rouge.")]
    [SerializeField] private Color[] sequenceStepColors = new[]
    {
        new Color(1f, 0.92f, 0.2f), // jaune
        new Color(0.25f, 0.55f, 1f), // bleu
        new Color(1f, 0.35f, 0.8f), // rose
        new Color(1f, 0.25f, 0.25f) // rouge
    };
    [SerializeField] private Color sequenceOffColor = Color.white;
    [Tooltip("Si activé : les boules sont désactivées au démarrage et n'apparaissent qu'après RevealSequenceIndicators() (ex: avec EnableIpod du salon).")]
    [SerializeField] private bool hideSequenceIndicatorsUntilIpodReveal = true;

    [Header("Échec / reset")]
    [Tooltip("Si true: une mauvaise touche remet la séquence à zéro.")]
    [SerializeField] private bool resetOnMistake = true;

    [Header("Narration")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private int _progress = 0;
    private bool _completed = false;
    private Material[] _sequenceIndicatorMaterials;

    public bool IsSolved => _completed;

    /// <summary>
    /// À appeler en même temps que le déblocage de l'iPod (ex. <see cref="SalonOnboardingController"/>).
    /// Active les GameObjects des indicateurs et réinitialise leur couleur.
    /// </summary>
    public void RevealSequenceIndicators()
    {
        SetSequenceIndicatorRootsActive(true);
        CacheSequenceIndicatorMaterials();
        ResetSequenceIndicators();
    }

    private void Awake()
    {
        // Auto-wire optionnel si tu as oublié de brancher le manager sur les keys.
        if (keys != null)
        {
            foreach (var k in keys)
            {
                if (k == null) continue;
                // rien à faire ici, le PianoKey appelle le manager si sa ref est set
            }
        }

        if (hideSequenceIndicatorsUntilIpodReveal)
            SetSequenceIndicatorRootsActive(false);
        else
        {
            CacheSequenceIndicatorMaterials();
            ResetSequenceIndicators();
        }
    }

    private void SetSequenceIndicatorRootsActive(bool active)
    {
        if (sequenceIndicators == null)
            return;
        for (int i = 0; i < sequenceIndicators.Length; i++)
        {
            var r = sequenceIndicators[i];
            if (r != null)
                r.gameObject.SetActive(active);
        }
    }

    public void OnKeyPressed(PianoKey key)
    {
        if (_completed) return;
        if (key == null) return;
        if (requiredOrder == null || requiredOrder.Length == 0) return;

        int expected = requiredOrder[_progress];
        if (key.KeyId != expected)
        {
            if (resetOnMistake)
            {
                _progress = 0;
                ResetSequenceIndicators();
            }
            return;
        }

        _progress++;
        RefreshSequenceIndicators();
        if (_progress >= requiredOrder.Length)
            Complete();
    }

    private void Complete()
    {
        _completed = true;

        if (onSuccessActivate != null)
            onSuccessActivate.SetActive(true);

        if (disableKeysOnSuccess && keys != null)
        {
            foreach (var k in keys)
            {
                if (k == null) continue;
                k.SetInteractable(false);
            }
        }

        ApplyPianoSuccessVisual();
        PlayPianoSuccessSound();
        radioManager?.UnlockAfterPianoSuccess();
        onPuzzleSolved?.Invoke();
    }

    private void CacheSequenceIndicatorMaterials()
    {
        if (sequenceIndicators == null || sequenceIndicators.Length == 0)
        {
            _sequenceIndicatorMaterials = null;
            return;
        }

        _sequenceIndicatorMaterials = new Material[sequenceIndicators.Length];
        for (int i = 0; i < sequenceIndicators.Length; i++)
        {
            var r = sequenceIndicators[i];
            if (r == null) continue;
            _sequenceIndicatorMaterials[i] = r.material;
        }
    }

    private void ResetSequenceIndicators()
    {
        if (_sequenceIndicatorMaterials == null) return;
        for (int i = 0; i < _sequenceIndicatorMaterials.Length; i++)
            SetMaterialColor(_sequenceIndicatorMaterials[i], sequenceOffColor);
    }

    private void RefreshSequenceIndicators()
    {
        if (_sequenceIndicatorMaterials == null) return;

        int totalIndicators = _sequenceIndicatorMaterials.Length;
        for (int i = 0; i < totalIndicators; i++)
        {
            bool isCompletedStep = i < _progress;
            Color c = isCompletedStep ? GetStepColor(i) : sequenceOffColor;
            SetMaterialColor(_sequenceIndicatorMaterials[i], c);
        }
    }

    private Color GetStepColor(int stepIndex)
    {
        if (sequenceStepColors == null || sequenceStepColors.Length == 0)
            return sequenceOffColor;
        int idx = Mathf.Clamp(stepIndex, 0, sequenceStepColors.Length - 1);
        return sequenceStepColors[idx];
    }

    private static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.color = color;

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", color);
    }

    private void ApplyPianoSuccessVisual()
    {
        if (pianoSuccessRenderers == null || pianoSuccessRenderers.Length == 0) return;

        string configured = pianoSuccessBoolProperty;
        string configuredUnderscore = string.IsNullOrEmpty(configured) ? string.Empty : "_" + configured;

        for (int r = 0; r < pianoSuccessRenderers.Length; r++)
        {
            var renderer = pianoSuccessRenderers[r];
            if (renderer == null) continue;

            Material[] mats = renderer.materials;
            if (mats == null || mats.Length == 0) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null) continue;

                if (TrySetSuccessProperty(mat, configured)) continue;
                if (TrySetSuccessProperty(mat, configuredUnderscore)) continue;
                if (TrySetSuccessProperty(mat, "Success")) continue;
                TrySetSuccessProperty(mat, "_Success");
            }
        }
    }

    private static bool TrySetSuccessProperty(Material mat, string propertyName)
    {
        if (mat == null) return false;
        if (string.IsNullOrEmpty(propertyName)) return false;
        if (!mat.HasProperty(propertyName)) return false;

        // Toggle shader properties are represented as float 0/1.
        mat.SetFloat(propertyName, 1f);
        mat.SetInt(propertyName, 1);
        return true;
    }

    private void PlayPianoSuccessSound()
    {
        if (pianoSuccessSound.IsNull) return;

        var t = pianoSuccessSoundOrigin != null ? pianoSuccessSoundOrigin : transform;
        RuntimeManager.PlayOneShot(pianoSuccessSound, t.position);
    }
}

