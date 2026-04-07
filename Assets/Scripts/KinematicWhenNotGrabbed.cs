using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class KinematicWhenNotGrabbed : MonoBehaviour
{
    [Header("On grab")]
    [Tooltip("Quand le plateau est tenu, on remet Rigidbody en dynamique. Active 'Use Gravity' si tu veux qu'il tombe pendant le déplacement.")] 
    [SerializeField] private bool setUseGravityOnGrab = true;

    [Header("Au repos (non grab)")]
    [Tooltip("Si coché : plateau reste kinematic quand on ne le tient pas (empêche les impulses des coquillages).")]
    [SerializeField] private bool setKinematicWhenNotGrabbed = true;
    [Tooltip("Si coché : active la gravité au repos même si le plateau est kinematic. (en pratique, la gravité ne s'applique pas si kinematic=true).")]
    [SerializeField] private bool setUseGravityWhenNotGrabbed = true;

    [Tooltip("Si coché : le plateau reste cinématique pendant le grab (recommandé si les coquillages sont snap en enfants).")]
    [SerializeField] private bool keepKinematicOnGrab = true;

    private Rigidbody _rb;
    private XRGrabInteractable _grab;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _grab = GetComponent<XRGrabInteractable>();

        // Au repos, on fige le plateau pour qu'il ne reçoive pas d'impulsions des coquillages.
        _rb.isKinematic = setKinematicWhenNotGrabbed;
        _rb.useGravity = setUseGravityWhenNotGrabbed && !setKinematicWhenNotGrabbed;
    }

    private void OnEnable()
    {
        _grab.selectEntered.AddListener(OnGrabbed);
        _grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _grab.selectEntered.RemoveListener(OnGrabbed);
        _grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        _rb.isKinematic = keepKinematicOnGrab;
        _rb.useGravity = setUseGravityOnGrab && !keepKinematicOnGrab;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = setKinematicWhenNotGrabbed;
        _rb.useGravity = setUseGravityWhenNotGrabbed && !setKinematicWhenNotGrabbed;
    }
}

