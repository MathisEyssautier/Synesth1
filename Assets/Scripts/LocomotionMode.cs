using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
    private bool _snapTurnEnabled = true;
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
        // Désactivation de tout
        moveObject.SetActive(false);
        turnObject.SetActive(false);
        teleportationObject.SetActive(false);
        SetTeleportInteractorsActive(false);

        if (_forceDisabled)
            return;

        switch (_currentMode)
        {
            case LocomotionMode.LinearSnapTurn:
                moveObject.SetActive(true);
                turnObject.SetActive(_snapTurnEnabled);
                continuousMoveProvider.forwardSource = _defaultForwardSource;
                break;

            case LocomotionMode.LinearGaze:
                moveObject.SetActive(true);
                turnObject.SetActive(_snapTurnEnabled);
                continuousMoveProvider.forwardSource = xrCamera;
                break;

            case LocomotionMode.Teleport:
                teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(true);
                turnObject.SetActive(_snapTurnEnabled);
                break;

            case LocomotionMode.TeleportBlink:
                teleportationObject.SetActive(true);
                SetTeleportInteractorsActive(true);
                turnObject.SetActive(_snapTurnEnabled);
                teleportationProvider.locomotionStateChanged += OnTeleportStateChanged;
                break;
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