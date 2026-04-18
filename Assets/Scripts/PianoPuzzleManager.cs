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
    [Tooltip("Mesh du piano dont le shader expose un bool 'Success' (ou propriété équivalente).")]
    [SerializeField] private Renderer pianoSuccessRenderer;
    [SerializeField] private string pianoSuccessBoolProperty = "Success";

    [Header("Réussite — audio piano")]
    [SerializeField] private EventReference pianoSuccessSound;
    [SerializeField] private Transform pianoSuccessSoundOrigin;

    [Header("Réussite — radio (potards + son + blanc)")]
    [SerializeField] private RadioManager radioManager;

    [Header("Échec / reset")]
    [Tooltip("Si true: une mauvaise touche remet la séquence à zéro.")]
    [SerializeField] private bool resetOnMistake = true;

    [Header("Narration")]
    [SerializeField] private UnityEvent onPuzzleSolved;

    private int _progress = 0;
    private bool _completed = false;

    public bool IsSolved => _completed;

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

        // Ne force plus l'état au démarrage :
        // l'objet doit conserver son état tel que placé dans la scène / PlayMode.
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
                _progress = 0;
            return;
        }

        _progress++;
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

    private void ApplyPianoSuccessVisual()
    {
        if (pianoSuccessRenderer == null) return;
        Material[] mats = pianoSuccessRenderer.materials;
        if (mats == null || mats.Length == 0) return;

        string configured = pianoSuccessBoolProperty;
        string configuredUnderscore = string.IsNullOrEmpty(configured) ? string.Empty : "_" + configured;

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

