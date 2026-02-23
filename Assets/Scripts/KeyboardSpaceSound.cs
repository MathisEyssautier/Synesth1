using FMODUnity;
using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardSpaceSound : MonoBehaviour
{
    [Header("Audio FMOD")]
    private StudioEventEmitter emitter;
    public bool isPlaying = false;

    private void Start()
    {
        emitter = GetComponent<StudioEventEmitter>();
    }
    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isPlaying = !isPlaying;

            if (isPlaying)
            {
                emitter.Play();
            }
            else
            {
                emitter.Stop();
            }
            
        }
    }
}