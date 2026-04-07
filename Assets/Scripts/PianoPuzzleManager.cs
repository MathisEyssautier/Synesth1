using UnityEngine;
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
    [Tooltip("Mesh du piano à teinter en jaune (ou autre) quand la séquence est bonne.")]
    [SerializeField] private Renderer pianoSuccessRenderer;
    [SerializeField] private Color pianoSuccessColor = new Color(1f, 0.92f, 0.016f);
    [SerializeField] private float pianoSuccessEmissionMul = 1.25f;

    [Header("Réussite — audio piano")]
    [SerializeField] private EventReference pianoSuccessSound;
    [SerializeField] private Transform pianoSuccessSoundOrigin;

    [Header("Réussite — radio (potards + son + blanc)")]
    [SerializeField] private RadioManager radioManager;

    [Header("Échec / reset")]
    [Tooltip("Si true: une mauvaise touche remet la séquence à zéro.")]
    [SerializeField] private bool resetOnMistake = true;

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
    }

    private void ApplyPianoSuccessVisual()
    {
        if (pianoSuccessRenderer == null) return;

        var mat = pianoSuccessRenderer.material;
        if (mat == null) return;

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", pianoSuccessColor);
        else
            mat.color = pianoSuccessColor;

        if (mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", pianoSuccessColor * pianoSuccessEmissionMul);
        }
    }

    private void PlayPianoSuccessSound()
    {
        if (pianoSuccessSound.IsNull) return;

        var t = pianoSuccessSoundOrigin != null ? pianoSuccessSoundOrigin : transform;
        RuntimeManager.PlayOneShot(pianoSuccessSound, t.position);
    }
}

