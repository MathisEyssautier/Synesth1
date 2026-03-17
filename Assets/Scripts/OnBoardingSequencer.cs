using System.Collections;
using UnityEngine;

public class OnboardingSequencer : MonoBehaviour
{
    [Header("Mur qui monte")]
    [SerializeField] private GameObject wallSection;
    [SerializeField] private float wallMoveDistance = 4f;
    [SerializeField] private float wallMoveDuration = 2f;

    [Header("Cube interagible")]
    [SerializeField] private GameObject interactableCube;
    [SerializeField] private float cubeAppearDelay = 1f; // délai après que le mur commence à monter

    void OnEnable()
    {
        SubtitleManager.OnVoiceEnded += StartOnboardingSequence;
    }

    void OnDisable()
    {
        SubtitleManager.OnVoiceEnded -= StartOnboardingSequence;
    }

    private void StartOnboardingSequence()
    {
        StartCoroutine(OnboardingRoutine());
    }

    private IEnumerator OnboardingRoutine()
    {
        // 1. Le mur monte
        StartCoroutine(MoveWallUp());

        // 2. Le cube apparaît avec un délai
        yield return new WaitForSeconds(cubeAppearDelay);
        interactableCube.SetActive(true);
    }

    private IEnumerator MoveWallUp()
    {
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