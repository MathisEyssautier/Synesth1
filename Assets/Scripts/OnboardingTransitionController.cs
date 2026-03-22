using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using Unity.XR.CoreUtils;

public class OnboardingTransitionController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private SubtitleManager subtitleManager;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [Tooltip("Optionnel: référence directe au cube d'onboarding (évite Find au runtime).")]
    [SerializeField] private InteractableCube onboardingCube;
    [Tooltip("Optionnel: XRGrabInteractable du cube si tu veux forcer un release sûr.")]
    [SerializeField] private XRGrabInteractable onboardingCubeGrab;

    [Header("Teleport")]
    [Tooltip("Root du rig (souvent XROrigin / XR Rig)")]
    [SerializeField] private Transform rigRoot;
    [Tooltip("Point d'arrivée dans la pièce principale")]
    [SerializeField] private Transform mainRoomSpawnPoint;

    [Header("Onboarding room")]
    [Tooltip("Root à désactiver après téléportation (pour ne pas la voir via fenêtres)")]
    [SerializeField] private GameObject onboardingRoomRoot;

    [Header("Lighting")]
    [Tooltip("Ex: 2e Directional Light (diffusion) à activer après l'onboarding.")]
    [SerializeField] private GameObject directionalLightToEnableOnEnd;
    [SerializeField] private bool disableDirectionalLightOnStart = false;

    [Header("Timing")]
    [SerializeField] private float fadeOutDuration = 1.0f;
    [SerializeField] private float fadeInDuration = 0.6f;
    [SerializeField] private float voiceReplayDelay = 0f;

    [Header("Blink override (optional)")]
    [Tooltip("Si true: utilise blinkDuration pour un clignement rapide (fade out/in).")]
    [SerializeField] private bool useBlink = true;
    [SerializeField] private float blinkDuration = 0.15f;

    private bool _waitingForOutroVoiceEnd = false;
    private bool _transitionStarted = false;

    private void Awake()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
            fadeCanvasGroup.interactable = false;
        }
        // On ne désactive plus automatiquement la light au démarrage:
        // tu veux pouvoir piloter son état directement dans la scène/PlayMode.
    }

    private void OnEnable()
    {
        InteractableCube.OnFirstDeactivated += OnCubeFirstDeactivated;
        SubtitleManager.OnVoiceEnded += OnVoiceEnded;
    }

    private void OnDisable()
    {
        InteractableCube.OnFirstDeactivated -= OnCubeFirstDeactivated;
        SubtitleManager.OnVoiceEnded -= OnVoiceEnded;
    }

    private void OnCubeFirstDeactivated(InteractableCube cube)
    {
        if (_transitionStarted) return;
        if (_waitingForOutroVoiceEnd) return;

        _waitingForOutroVoiceEnd = true;
        if (subtitleManager != null)
            subtitleManager.ReplayVoice(voiceReplayDelay);
    }

    private void OnVoiceEnded()
    {
        if (!_waitingForOutroVoiceEnd) return;
        if (_transitionStarted) return;
        StartCoroutine(TransitionRoutine());
    }

    private IEnumerator TransitionRoutine()
    {
        _transitionStarted = true;

        float outDur = useBlink ? blinkDuration : fadeOutDuration;
        float inDur = useBlink ? blinkDuration : fadeInDuration;

        yield return Fade(0f, 1f, outDur);

        // Laisser 1 frame pour stabiliser XR/physique avant release/téléport.
        yield return null;

        ReleaseCubeIfHeld();
        TeleportToMainRoom();

        if (onboardingRoomRoot != null)
            onboardingRoomRoot.SetActive(false);

        if (directionalLightToEnableOnEnd != null)
            directionalLightToEnableOnEnd.SetActive(true);

        yield return Fade(1f, 0f, inDur);
    }

    private void TeleportToMainRoom()
    {
        if (rigRoot == null || mainRoomSpawnPoint == null)
            return;

        // Si on a XROrigin, on l'utilise pour préserver l'offset caméra proprement.
        var origin = rigRoot.GetComponent<XROrigin>();
        if (origin != null)
        {
            // Protection: certains setups peuvent ne pas avoir de caméra assignée à l'instant T.
            if (origin.Camera != null)
            {
                origin.MoveCameraToWorldLocation(mainRoomSpawnPoint.position);
                origin.transform.rotation = mainRoomSpawnPoint.rotation;
                return;
            }
            origin.MoveCameraToWorldLocation(mainRoomSpawnPoint.position);
            origin.transform.rotation = mainRoomSpawnPoint.rotation;
            return;
        }

        rigRoot.SetPositionAndRotation(mainRoomSpawnPoint.position, mainRoomSpawnPoint.rotation);
    }

    private void ReleaseCubeIfHeld()
    {
        var cube = onboardingCube != null ? onboardingCube : FindFirstObjectByType<InteractableCube>();
        if (cube == null) return;

        var grab = onboardingCubeGrab != null ? onboardingCubeGrab : cube.GetComponent<XRGrabInteractable>();
        if (grab == null) return;

        var mgr = grab.interactionManager;
        if (mgr == null) return;

        // Copie stable pour éviter les soucis si la liste change pendant les SelectExit.
        var selecting = grab.interactorsSelecting;
        if (selecting == null || selecting.Count == 0) return;

        var snapshot = new IXRSelectInteractor[selecting.Count];
        for (int i = 0; i < selecting.Count; i++)
            snapshot[i] = selecting[i];

        for (int i = snapshot.Length - 1; i >= 0; i--)
        {
            var interactor = snapshot[i];
            if (interactor == null) continue;
            mgr.SelectExit(interactor, grab);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null)
            yield break;

        fadeCanvasGroup.blocksRaycasts = true;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = duration <= 0f ? 1f : Mathf.Clamp01(t / duration);
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, a);
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = to > 0.01f;
    }
}

