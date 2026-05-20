using FMOD.Studio;
using FMODUnity;
using UnityEngine;

/// <summary>
/// Rétablit Time.timeScale et le bus FMOD master après pause menu / reload de scène.
/// </summary>
public static class GameAudioBootstrap
{
    public static void EnsureUnpausedForGameplay()
    {
        Time.timeScale = 1f;

        Bus master = RuntimeManager.GetBus("bus:/");
        if (master.isValid())
            master.setPaused(false);
    }
}
