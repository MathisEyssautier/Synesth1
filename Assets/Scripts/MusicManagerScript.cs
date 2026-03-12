using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MusicManagerScript : MonoBehaviour
{
    [Header("FMOD Event")]
    public EventReference musicEvent;

    private EventInstance _musicInstance;

    void Start()
    {
        _musicInstance = RuntimeManager.CreateInstance(musicEvent);
        RuntimeManager.AttachInstanceToGameObject(_musicInstance, gameObject);
        _musicInstance.start();
    }

    public void SetVolumePiano(float value)
    {
        _musicInstance.setParameterByName("PianoVolume", value);
    }

    public void SetVolumeGuitare(float value)
    {
        _musicInstance.setParameterByName("GuitarVolume", value);
    }

    void OnDestroy()
    {
        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();
    }
}