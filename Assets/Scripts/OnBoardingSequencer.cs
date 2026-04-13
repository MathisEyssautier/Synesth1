using System.Collections;
using UnityEngine;

/// <summary>
/// Ancien flux : salle d'accueil avec mur + cube aprùs N fins de voix off.
/// Dùsactivù par dùfaut : le jeu dùmarre directement dans le salon (voir SalonOnboardingController).
/// </summary>
public class OnBoardingSequencer : MonoBehaviour
{
    [Header("Legacy (dùsactivù par dùfaut)")]
    [Tooltip("Si faux : ce script ne s'abonne pas aux voix off et ne fait rien (flux salon actuel).")]
    [SerializeField] private bool enableLegacyWallAndCubeFlow = false;

    [Header("Mur qui monte")]
    [SerializeField] private GameObject wallSection;
    [SerializeField] private float wallMoveDistance = 4f;
    [SerializeField] private float wallMoveDuration = 2f;

    [Header("Cube interagible")]
    [SerializeField] private GameObject interactableCube;
    [SerializeField] private float cubeAppearDelay = 1f;

    [Tooltip("Nombre de fins de ligne VO avant le mur (ancien onboarding).")]
    [SerializeField] private int voiceEndCountBeforeWall = 3;

    private bool _hasStarted;
    private int _voiceEndsRemaining;

    private void OnEnable()
    {
        if (!enableLegacyWallAndCubeFlow)
            return;

        _voiceEndsRemaining = Mathf.Max(1, voiceEndCountBeforeWall);
        SubtitleManager.OnVoiceEnded += OnSubtitleVoiceEnded;
    }

    private void OnDisable()
    {
        if (!enableLegacyWallAndCubeFlow)
            return;

        SubtitleManager.OnVoiceEnded -= OnSubtitleVoiceEnded;
    }

    private void OnSubtitleVoiceEnded()
    {
        if (!enableLegacyWallAndCubeFlow) return;
        if (_hasStarted) return;

        _voiceEndsRemaining--;
        if (_voiceEndsRemaining > 0) return;

        _hasStarted = true;
        StartCoroutine(OnboardingRoutine());
    }

    private IEnumerator OnboardingRoutine()
    {
        if (wallSection != null)
            StartCoroutine(MoveWallUp());

        yield return new WaitForSeconds(cubeAppearDelay);

        if (interactableCube != null)
            interactableCube.SetActive(true);
    }

    private IEnumerator MoveWallUp()
    {
        if (wallSection == null) yield break;

        Vector3 startPos = wallSection.transform.position;
        Vector3 endPos = startPos + Vector3.up * wallMoveDistance;
        float elapsed = 0f;

        while (elapsed < wallMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / wallMoveDuration);
            wallSection.transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        wallSection.transform.position = endPos;
    }
}
