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

    public void SetVolumeViolons(float value)
    {
        _musicInstance.setParameterByName("ViolonsVolume", value);
    }

    public void SetVolumeGuitare(float value)
    {
        _musicInstance.setParameterByName("GuitarVolume", value);
    }

    public void SetVolumeBass(float value)
    {
        _musicInstance.setParameterByName("BassVolume", value);
    }

    void OnDestroy()
    {
        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();
    }
}