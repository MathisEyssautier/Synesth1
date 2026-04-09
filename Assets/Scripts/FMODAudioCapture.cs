using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using FMOD.Studio;
using FMODUnity;
using UnityVFXReactiveAudio;

namespace UnityVFXReactiveAudio
{
    /// <summary>
    /// Remplace AudioCapture pour les GameObjects utilisant FMOD Studio Event Emitter.
    /// Capture les samples PCM directement depuis le DSP graph FMOD et les expose
    /// dans la même interface que AudioCapture (NativeSlice, LevelMeter, etc.)
    /// Compatible avec AudioLevelTracker et SpectrumAnalyzer sans modification.
    /// </summary>
    [AddComponentMenu("UnityVFXReactiveAudio/FMOD Audio Capture")]
    public sealed class FMODAudioCapture : MonoBehaviour, IAudioCapture
    {
        #region Inspector

        [Tooltip("L'Event Emitter FMOD attaché à ce GameObject (ou un autre).")]
        [SerializeField] private StudioEventEmitter _emitter = null;

        [Tooltip("Si coché, tente de récupérer automatiquement le StudioEventEmitter sur ce GameObject.")]
        [SerializeField] private bool _autoFindEmitter = true;

        #endregion

        #region Public API — même interface qu'AudioCapture

        public bool IsReady { get; private set; }
        public int ChannelCount { get; private set; }
        public int SampleRate { get; private set; }

        public float GetChannelLevel(int channel)
        {
            if (!IsReady || _audioLevels == null) return 0f;
            return MathUtils.dBFS(_audioLevels.GetLevel(channel).x);
        }

        public float GetChannelLevel(int channel, FilterType filter)
        {
            if (!IsReady || _audioLevels == null) return 0f;
            return MathUtils.dBFS(_audioLevels.GetLevel(channel)[(int)filter]);
        }

        public NativeSlice<float> InterleavedDataSlice
            => new NativeSlice<float>(_readingBuffer.AsArray());

        public NativeSlice<float> GetChannelDataSlice(int channel)
        {
            if (!IsReady || _readingBuffer.Length < channel + 1) return default;
            return new NativeSlice<float>(_readingBuffer.AsArray(), channel)
                       .GetNativeSlice(channel, ChannelCount);
        }

        #endregion

        #region Private — buffers (même pattern double-buffer qu'AudioCapture)

        private NativeList<float> _readingBuffer;
        private NativeList<float> _fillBuffer;
        private readonly object _bufferLock = new object();

        private LevelMeter _audioLevels;

        #endregion

        #region Private — FMOD DSP

        private DSP _dsp;
        private bool _dspAttached;

        // GCHandle pour éviter que le delegate soit collecté
        private GCHandle _callbackHandle;
        private DSP_READ_CALLBACK _dspCallback;

        // Infos récupérées depuis FMOD
        private int _fmodChannels;
        private int _fmodSampleRate;

        #endregion

        #region Unity lifecycle

        private void Awake()
        {
            if (_autoFindEmitter && _emitter == null)
                _emitter = GetComponent<StudioEventEmitter>();
        }

        private void OnEnable()
        {
            const int kInitialCapacity = 1024 * 4 * 2;
            lock (_bufferLock)
            {
                _readingBuffer = new NativeList<float>(kInitialCapacity, Allocator.Persistent);
                _fillBuffer    = new NativeList<float>(kInitialCapacity, Allocator.Persistent);
            }
            IsReady = false;
        }

        private void OnDisable()
        {
            DetachDsp();

            lock (_bufferLock)
            {
                if (_readingBuffer.IsCreated) _readingBuffer.Dispose();
                if (_fillBuffer.IsCreated)    _fillBuffer.Dispose();
                _audioLevels = null;
            }
            IsReady = false;
        }

        private void LateUpdate()
        {
            // Tente d'attacher le DSP si l'event vient de démarrer
            if (!_dspAttached)
                TryAttachDsp();

            // Swap des buffers (même logique qu'AudioCapture.LateUpdate)
            lock (_bufferLock)
            {
                if (!_readingBuffer.IsCreated) return;
                (_readingBuffer, _fillBuffer) = (_fillBuffer, _readingBuffer);
                _fillBuffer.Clear();
            }

            if (_fmodChannels > 0 && !IsReady)
            {
                ChannelCount = _fmodChannels;
                SampleRate   = _fmodSampleRate;
                IsReady      = true;
            }

            if (IsReady)
            {
                if (_audioLevels == null)
                {
                    _audioLevels = new LevelMeter(ChannelCount);
                    _audioLevels.SampleRate = SampleRate;
                }
                _audioLevels.ProcessAudioData(_readingBuffer.AsArray());
            }
        }

        #endregion

        #region DSP attachment

        private void TryAttachDsp()
        {
            if (_emitter == null) return;

            // L'EventInstance n'est valide qu'après que l'event ait démarré
            if (!_emitter.IsPlaying()) return;

            var instance = _emitter.EventInstance;
            if (!instance.isValid()) return;

            // Récupère le channel group associé à l'event
            var result = instance.getChannelGroup(out var channelGroup);
            if (result != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogWarning($"[FMODAudioCapture] getChannelGroup failed: {result}");
                return;
            }

            // Récupère le sample rate et les channels depuis le system FMOD
            FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(
                out _fmodSampleRate, out var speakerMode, out _);

            // Le nombre de channels dépend du speaker mode
            _fmodChannels = SpeakerModeToChannelCount(speakerMode);

            // Crée un DSP de type FMOD.DSP_TYPE.FADER (transparent, juste pour le callback)
            // On utilise un custom DSP de type UTILITY pour capturer les samples
            FMODUnity.RuntimeManager.CoreSystem.createDSPByType(
                FMOD.DSP_TYPE.MIXER, out _dsp);

            // On préfère un DSP custom via FMOD.DSP_DESCRIPTION pour avoir le callback PCM
            CreateCaptureDsp(out _dsp);

            result = channelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, _dsp);
            if (result != FMOD.RESULT.OK)
            {
                UnityEngine.Debug.LogWarning($"[FMODAudioCapture] addDSP failed: {result}");
                _dsp.release();
                return;
            }

            _dspAttached = true;
            UnityEngine.Debug.Log($"[FMODAudioCapture] DSP attaché sur {_emitter.name} " +
                                  $"({_fmodChannels}ch @ {_fmodSampleRate}Hz)");
        }

        private void DetachDsp()
        {
            if (!_dspAttached) return;

            if (_emitter != null && _emitter.IsPlaying())
            {
                var instance = _emitter.EventInstance;
                if (instance.isValid())
                {
                    instance.getChannelGroup(out var cg);
                    cg.removeDSP(_dsp);
                }
            }

            _dsp.release();
            _dspAttached = false;

            if (_callbackHandle.IsAllocated)
                _callbackHandle.Free();
        }

        /// <summary>
        /// Crée un DSP custom FMOD avec un read callback qui copie les samples PCM.
        /// </summary>
        private void CreateCaptureDsp(out DSP dsp)
        {
            _dspCallback = OnDspRead;
            _callbackHandle = GCHandle.Alloc(_dspCallback);

            var desc = new DSP_DESCRIPTION();
            desc.version           = 0x00010000;
            desc.numinputbuffers   = 1;
            desc.numoutputbuffers  = 1;
            desc.read              = _dspCallback;
            // Laisse passer l'audio sans modification
            desc.userdata          = IntPtr.Zero;

            FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out dsp);
        }

        /// <summary>
        /// DSP read callback — appelé sur le thread audio FMOD.
        /// On copie les samples dans _fillBuffer de façon thread-safe.
        /// </summary>
        [AOT.MonoPInvokeCallback(typeof(DSP_READ_CALLBACK))]
        private FMOD.RESULT OnDspRead(
            ref DSP_STATE dspState,
            IntPtr inBufferRaw,
            IntPtr outBufferRaw,
            uint length,
            int inChannels,
            ref int outChannels)
        {
            // Nombre de floats dans le buffer interleaved
            int totalSamples = (int)length * inChannels;

            // Pass-through : copie in → out
            if (outBufferRaw != IntPtr.Zero && inBufferRaw != IntPtr.Zero)
                unsafe
                {
                    Buffer.MemoryCopy(
                        inBufferRaw.ToPointer(),
                        outBufferRaw.ToPointer(),
                        totalSamples * sizeof(float),
                        totalSamples * sizeof(float));
                }

            // Mise à jour du channel count détecté
            if (_fmodChannels != inChannels)
                _fmodChannels = inChannels;

            // Injection dans le fill buffer (thread-safe)
            lock (_bufferLock)
            {
                if (!_fillBuffer.IsCreated) return FMOD.RESULT.OK;

                unsafe
                {
                    var nativeArr = NativeArrayUnsafeUtility
                        .ConvertExistingDataToNativeArray<float>(
                            inBufferRaw.ToPointer(), totalSamples, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    var safety = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArr, safety);
#endif
                    _fillBuffer.AddRange(nativeArr);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    AtomicSafetyHandle.Release(safety);
#endif
                    // Sécurité si le jeu est en pause
                    if (_fillBuffer.Length > _fmodChannels * 48000 / 4)
                        _fillBuffer.Clear();
                }
            }

            return FMOD.RESULT.OK;
        }

        #endregion

        #region Helpers

        private static int SpeakerModeToChannelCount(FMOD.SPEAKERMODE mode) => mode switch
        {
            FMOD.SPEAKERMODE.MONO     => 1,
            FMOD.SPEAKERMODE.STEREO   => 2,
            FMOD.SPEAKERMODE._5POINT1 => 6,
            FMOD.SPEAKERMODE._7POINT1 => 8,
            _                         => 2   // fallback stéréo
        };

        #endregion
    }
}
