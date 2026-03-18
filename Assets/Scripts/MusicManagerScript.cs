using UnityEngine;
using FMODUnity;
using FMOD.Studio;

public class MusicManagerScript : MonoBehaviour
{
    [Header("FMOD Event")]
    public EventReference musicEvent;

    private EventInstance _musicInstance;
    private bool _initialized = false;

    private float _violonsVolume = 0f;
    private float _guitarVolume = 0f;
    private float _bassVolume = 0f;

    private void Awake()
    {
        _musicInstance = RuntimeManager.CreateInstance(musicEvent);
        RuntimeManager.AttachInstanceToGameObject(_musicInstance, gameObject);
        _initialized = true;
    }

    void Start()
    {
        _musicInstance.start();
        ApplyCachedVolumes();
    }

    public void SetVolumeViolons(float value)
    {
        _violonsVolume = value;
        if (_initialized)
            _musicInstance.setParameterByName("ViolonsVolume", value);
    }

    public void SetVolumeGuitare(float value)
    {
        _guitarVolume = value;
        if (_initialized)
            _musicInstance.setParameterByName("GuitarVolume", value);
    }

    public void SetVolumeBass(float value)
    {
        _bassVolume = value;
        if (_initialized)
            _musicInstance.setParameterByName("BassVolume", value);
    }

    private void ApplyCachedVolumes()
    {
        if (!_initialized) return;
        _musicInstance.setParameterByName("ViolonsVolume", _violonsVolume);
        _musicInstance.setParameterByName("GuitarVolume", _guitarVolume);
        _musicInstance.setParameterByName("BassVolume", _bassVolume);
    }

    void OnDestroy()
    {
        _musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        _musicInstance.release();
    }
}