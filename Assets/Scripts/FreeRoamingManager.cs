using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class FreeRoamingManager : MonoBehaviour
{
    [Header("Locomotion")]
    public GameObject moveObject; // L'enfant "Move" de Locomotion

    [Header("Input")]
    public InputActionReference toggleAction; // Bouton A ou B du contrôleur

    private bool _freeRoaming = true; // Free roaming par défaut

    void Start()
    {
        ApplyMode();
    }

    void OnEnable()
    {
        toggleAction.action.performed += OnToggle;
        toggleAction.action.Enable();
    }

    void OnDisable()
    {
        toggleAction.action.performed -= OnToggle;
    }

    private void OnToggle(InputAction.CallbackContext ctx)
    {
        _freeRoaming = !_freeRoaming;
        ApplyMode();
        Debug.Log($"[FreeRoaming] Mode : {(_freeRoaming ? "Free Roaming" : "Joystick")}");
    }

    private void ApplyMode()
    {
        moveObject.SetActive(!_freeRoaming);
    }
}