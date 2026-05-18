using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;

/// <summary>
/// Trigger derrière la porte de la black room : fondu au noir puis chargement de la scène principale.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BlackRoomExitTrigger : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] private string targetSceneName = "SynesthesiaMain";

    [Header("Fondu")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeOutDuration = 1.1f;

    [Header("Joueur (VR)")]
    [Tooltip("Racine du rig (souvent XR Origin). Si vide : premier XROrigin trouvé en scène.")]
    [SerializeField] private Transform playerRoot;
    [Tooltip("Caméra / tête XR. Recommandé : détection par position dans le BoxCollider (sans collider sur le joueur).")]
    [SerializeField] private Transform playerHead;
    [SerializeField] private bool useHeadPositionInTriggerBounds = true;

    [Header("Prérequis")]
    [Tooltip("Si assigné : le départ n'est possible qu'après la fin du dialogue d'intro.")]
    [SerializeField] private BlackRoomNarrativeController narrativeController;
    [SerializeField] private bool requireDialogueComplete = true;

    private bool _transitionStarted;
    private Collider _triggerCollider;

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        _triggerCollider.isTrigger = true;

        if (playerRoot == null)
        {
            var origin = FindFirstObjectByType<XROrigin>();
            if (origin != null)
                playerRoot = origin.transform;
        }

        if (playerHead == null && playerRoot != null)
        {
            var origin = playerRoot.GetComponent<XROrigin>();
            if (origin != null && origin.Camera != null)
                playerHead = origin.Camera.transform;
        }
    }

    private void Update()
    {
        if (!useHeadPositionInTriggerBounds || playerHead == null || _triggerCollider == null)
            return;

        if (_triggerCollider.bounds.Contains(playerHead.position))
            TryStartTransition();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        TryStartTransition();
    }

    private void TryStartTransition()
    {
        if (_transitionStarted) return;

        if (requireDialogueComplete && narrativeController != null && !narrativeController.IsDialogueComplete)
            return;

        StartCoroutine(TransitionRoutine());
    }

    private bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;

        if (playerRoot != null)
            return other.transform == playerRoot || other.transform.IsChildOf(playerRoot);

        return other.CompareTag("Player") || other.CompareTag("PlayerHand");
    }

    private IEnumerator TransitionRoutine()
    {
        _transitionStarted = true;

        if (fadeCanvasGroup != null)
        {
            EnsureFadeCanvasHierarchyActive();
            fadeCanvasGroup.blocksRaycasts = true;
            yield return FadeCanvas(0f, 1f, fadeOutDuration);
        }

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    private IEnumerator FadeCanvas(float from, float to, float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float t = 0f;
        fadeCanvasGroup.alpha = from;

        while (t < d)
        {
            t += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / d));
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
    }

    private void EnsureFadeCanvasHierarchyActive()
    {
        Transform tr = fadeCanvasGroup.transform;
        while (tr != null)
        {
            if (!tr.gameObject.activeSelf)
                tr.gameObject.SetActive(true);
            tr = tr.parent;
        }
    }
}
