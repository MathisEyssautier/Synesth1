using Unity.Collections;
using UnityEngine;

namespace UnityVFXReactiveAudio
{
    /// <summary>
    /// Interface commune entre AudioCapture (Unity AudioSource) et FMODAudioCapture (FMOD).
    /// Permet à AudioLevelTracker et SpectrumAnalyzer d'accepter les deux types
    /// sans modification de leur logique interne.
    ///
    /// Usage : dans l'Inspector, glisser soit un AudioCapture soit un FMODAudioCapture
    /// dans le champ "Audio Source" des trackers.
    /// </summary>
    public interface IAudioCapture
    {
        bool IsReady { get; }
        int ChannelCount { get; }
        int SampleRate { get; }

        float GetChannelLevel(int channel);
        float GetChannelLevel(int channel, FilterType filter);

        NativeSlice<float> InterleavedDataSlice { get; }
        NativeSlice<float> GetChannelDataSlice(int channel);
    }

    /// <summary>
    /// Extension de AudioCapture pour implémenter IAudioCapture.
    /// Ajoutez ce partial à AudioCapture.cs, ou utilisez ce fichier séparé.
    /// </summary>
    public partial class AudioCapture : IAudioCapture { }

    /// <summary>
    /// Extension de FMODAudioCapture pour implémenter IAudioCapture.
    /// </summary>
    public partial class FMODAudioCapture : IAudioCapture { }
}
