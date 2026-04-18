using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

public enum LocomotionMode
{
    LinearSnapTurn,
    LinearGaze,
    Teleport,
    TeleportBlink
}

public class LocomotionManager : MonoBehaviour
{
    [Header("Locomotion GameObjects (enfants de Locomotion)")]
    public GameObject moveObject;
    public GameObject turnObject;
    public GameObject teleportationObject;

    [Header("Teleport Interactors (sur le controller droit)")]
    public GameObject teleportInteractorRight;

    [Header("Providers (composants sur les objets ci-dessus)")]
    public ContinuousMoveProvider continuousMoveProvider;
    public SnapTurnProvider snapTurnProvider;
    public TeleportationProvider teleportationProvider;

    [Header("XR Camera (pour mode Gaze)")]
    public Transform xrCamera;

    [Header("Blink")]
    public CanvasGroup fadeCanvasGroup;
    public float blinkDuration = 0.15f;

    private LocomotionMode _currentMode;
    private Transform _defaultForwardSource;
    private bool _snapTurnEnabled = false;
    private bool _forceDisabled = false;
    public bool IsForceDisabled => _forceDisabled;

    void Start()
    {
        if (fadeCanvasGroup != null)
            fadeCanvasGroup.alpha = 0f;

        // Délai de téléportation calé sur la durée du fondu aller
        teleportationProvider.delayTime = blinkDuration;

        _defaultForwardSource = continuousMoveProvider.forwardSource;
        SetMode(LocomotionMode.LinearSnapTurn);
    }

    public void SetMode(LocomotionMode mode)
    {
        // Désabonnement préventif
        if (teleportationProvider != null)
            teleportationProvider.locomotionStateChanged -= OnTeleportStateChanged;

        _currentMode = mode;
        ApplyCurrentModeState();
    }

    public void SetSnapTurnEnabled(bool enabled)
    {
        _snapTurnEnabled = enabled;
        ApplyCurrentModeState();
    }

    public void SetForceDisabled(bool disabled)
    {
        _forceDisabled = disabled;
        ApplyCurrentModeState();
    }

    private void ApplyCurrentModeState()
    {
        // Désactivation de tout (null ou objet détruit : ignorer — ex. coroutine menu après reload scène)
        if (moveObject != null) moveObject.SetActive(false);
        if (turnObject != null) turnObject.SetActive(false);
        if (teleportationObject != null) teleportationObject.SetActive(false);
        SetTeleportInteractorsActive(false);

        if (_forceDisabled)
            return;

        switch (_currentMode)
        {
            case LocomotionMode.LinearSnapTurn:
                if (moveObject != null) moveObject.SetActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (continuousMoveProvider != null) continuousMoveProvider.forwardSource = _defaultForwardSource;
                break;

            case LocomotionMode.LinearGaze:
                if (moveObject != null) moveObject.SetActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (continuousMoveProvider != null) continuousMoveProvider.forwardSource = xrCamera;
                break;

            case LocomotionMode.Teleport:
                if (teleportationObject != null) teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                break;

            case LocomotionMode.TeleportBlink:
                if (teleportationObject != null) teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(true);
                if (turnObject != null) turnObject.SetActive(_snapTurnEnabled);
                if (teleportationProvider != null)
                    teleportationProvider.locomotionStateChanged += OnTeleportStateChanged;
                break;
        }

        // Réactive les bonnes InputActions (Move / téléport / snap) sur les manettes. Sans ça, un état bloqué
        // dans ControllerInputActionManager (Near-Far, UI, menu pause) peut laisser Move désactivée alors que
        // la lecture directe du stick (ex. menu) fonctionne encore.
        RefreshControllerInputActionManagers();
    }

    /// <summary>
    /// Rejoue les règles d’activation des actions XRI sur les contrôleurs (Starter Assets).
    /// </summary>
    public void RefreshControllerInputActionManagers()
    {
        if (continuousMoveProvider == null) return;

        var managers = continuousMoveProvider.transform.root.GetComponentsInChildren<ControllerInputActionManager>(true);
        for (int i = 0; i < managers.Length; i++)
        {
            var mgr = managers[i];
            if (mgr == null) continue;
            mgr.smoothMotionEnabled = mgr.smoothMotionEnabled;
            mgr.smoothTurnEnabled = mgr.smoothTurnEnabled;
        }
    }

    private void OnTeleportStateChanged(LocomotionProvider provider, LocomotionState state)
    {
        // Preparing = téléportation demandée mais pas encore exécutée (pendant le delayTime)
        if (state == LocomotionState.Preparing)
        {
            StartCoroutine(BlinkEffect());
        }
    }

    private void SetTeleportInteractorsActive(bool active)
    {
        if (teleportInteractorRight != null) teleportInteractorRight.SetActive(active);
    }

    private IEnumerator BlinkEffect()
    {
        // Fondu au noir (pendant le delayTime, avant le saut)
        float t = 0f;
        while (t < blinkDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / blinkDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1f;

        // La téléportation se produit ici (delayTime écoulé = écran noir)

        // Retour au clair
        t = 0f;
        while (t < blinkDuration)
        {
            fadeCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / blinkDuration);
            t += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = 0f;
    }

    // Appelées par les boutons UI
    public void SwitchToLinearSnap() => SetMode(LocomotionMode.LinearSnapTurn);
    public void SwitchToLinearGaze() => SetMode(LocomotionMode.LinearGaze);
    public void SwitchToTeleport() => SetMode(LocomotionMode.Teleport);
    public void SwitchToTeleportBlink() => SetMode(LocomotionMode.TeleportBlink);
}