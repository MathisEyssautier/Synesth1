using UnityEngine;

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

    [Header("Échec / reset")]
    [Tooltip("Si true: une mauvaise touche remet la séquence à zéro.")]
    [SerializeField] private bool resetOnMistake = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private int _progress = 0;
    private bool _completed = false;

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

        if (onSuccessActivate != null)
            onSuccessActivate.SetActive(false);
    }

    public void OnKeyPressed(PianoKey key)
    {
        if (_completed) return;
        if (key == null) return;
        if (requiredOrder == null || requiredOrder.Length == 0) return;

        int expected = requiredOrder[_progress];
        if (key.KeyId != expected)
        {
            if (debugLogs)
                Debug.Log($"[PianoPuzzle] Wrong key {key.KeyId}, expected {expected}. Reset={resetOnMistake}");
            if (resetOnMistake)
                _progress = 0;
            return;
        }

        _progress++;
        if (debugLogs)
            Debug.Log($"[PianoPuzzle] Correct key {key.KeyId}. Progress {_progress}/{requiredOrder.Length}");
        if (_progress >= requiredOrder.Length)
            Complete();
    }

    private void Complete()
    {
        _completed = true;

        if (debugLogs)
            Debug.Log("[PianoPuzzle] Completed!");

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
    }
}

