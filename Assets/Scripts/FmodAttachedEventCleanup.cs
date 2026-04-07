using UnityEngine;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Possède une EventInstance FMOD attachée à ce GameObject : stop + release au Disable/Destroy.
/// Les instances créées avec RuntimeManager.CreateInstance ne sont pas liées au cycle de vie Unity ;
/// les arrêter depuis le même GO que AttachInstanceToGameObject évite les boucles qui survivent au reload de scène.
/// </summary>
[DisallowMultipleComponent]
public class FmodAttachedEventCleanup : MonoBehaviour
{
    private EventInstance _instance;

    public void TakeOwnership(EventInstance instance)
    {
        StopAndRelease();
        _instance = instance;
    }

    private void OnDisable()
    {
        StopAndRelease();
    }

    private void OnDestroy()
    {
        StopAndRelease();
    }

    public void StopAndRelease()
    {
        if (!_instance.isValid()) return;

        RuntimeManager.DetachInstanceFromGameObject(_instance);
        _instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        _instance.release();
        _instance.clearHandle();
    }
}
