using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FaderController : MonoBehaviour
{
    public enum FaderType { Piano, Guitare }

    [Header("FMOD")]
    public FaderType faderType;
    public MusicManagerScript musicManager;

    [Header("Références")]
    public Transform faderBase;
    public float railHalfLength = 0.30f;

    [Header("Valeur courante (lecture seule)")]
    [Range(0f, 1f)]
    public float value = 0f;

    private XRGrabInteractable _grab;
    private bool _isGrabbed = false;
    private float _lockedLocalY; // on mémorise le Y de départ

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _grab.trackPosition = false;
        _grab.throwOnDetach = false;
        _grab.selectEntered.AddListener(_ => _isGrabbed = true);
        _grab.selectExited.AddListener(_ => { _isGrabbed = false; ConstrainToRail(); });

        // On mémorise le Y local au démarrage
        _lockedLocalY = faderBase.InverseTransformPoint(transform.position).y;
    }

    void Update()
    {
        if (!_isGrabbed) return;

        Vector3 handWorldPos = _grab.interactorsSelecting[0].GetAttachTransform(_grab).position;
        Vector3 localPos = faderBase.InverseTransformPoint(handWorldPos);

        localPos.y = _lockedLocalY; // Y toujours verrouillé
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);

        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);

        if (faderType == FaderType.Piano)
            musicManager.SetVolumePiano(value);
        else
            musicManager.SetVolumeGuitare(value);
    }

    void ConstrainToRail()
    {
        Vector3 localPos = faderBase.InverseTransformPoint(transform.position);
        localPos.y = _lockedLocalY;
        localPos.z = 0f;
        localPos.x = Mathf.Clamp(localPos.x, -railHalfLength, railHalfLength);
        transform.position = faderBase.TransformPoint(localPos);
        value = Mathf.InverseLerp(-railHalfLength, railHalfLength, localPos.x);
    }
}