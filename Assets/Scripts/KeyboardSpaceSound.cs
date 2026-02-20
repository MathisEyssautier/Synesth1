using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardSpaceSound : MonoBehaviour
{
    public AudioSource audioSource;
    public bool isPlaying = false;

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isPlaying = !isPlaying;

            if (isPlaying)
            {
                audioSource.Play();
            }
            else
            {
                audioSource.Stop();
            }
            
        }
    }
}