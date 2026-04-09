using UnityEngine;
using Unity.Mathematics;

namespace UnityVFXReactiveAudio
{
    /// <summary>
    /// Version modifiée de SpectrumAnalyzer compatible avec AudioCapture ET FMODAudioCapture.
    /// Seul changement : audioSource est maintenant un Component casté via IAudioCapture.
    /// </summary>
    [AddComponentMenu("UnityVFXReactiveAudio/Spectrum Analyzer")]
    public sealed class SpectrumAnalyzer : MonoBehaviour
    {
        #region Editor attributes

        [SerializeField, Range(0, 15)] int _channel = 0;
        public int channel { get => _channel; set => _channel = value; }

        [SerializeField] int _resolution = 512;
        public int resolution
        { get => _resolution; set => _resolution = ValidateResolution(value); }

        [SerializeField] bool _autoGain = true;
        public bool autoGain { get => _autoGain; set => _autoGain = value; }

        [SerializeField, Range(-10, 120)] float _gain = 0;
        public float gain { get => _gain; set => _gain = value; }

        [SerializeField, Range(1, 120)] float _dynamicRange = 80;
        public float dynamicRange { get => _dynamicRange; set => _dynamicRange = value; }

        #endregion

        #region Audio source — accepte AudioCapture OU FMODAudioCapture

        [SerializeField] private Component _audioSourceComponent = null;

        private IAudioCapture _captureCache;

        public IAudioCapture AudioSource
        {
            get
            {
                if (_captureCache == null && _audioSourceComponent != null)
                    _captureCache = _audioSourceComponent as IAudioCapture;
                return _captureCache;
            }
        }

        public void SetAudioSource(IAudioCapture source)
        {
            _audioSourceComponent = source as Component;
            _captureCache = source;
        }

        [System.Obsolete("Utilisez SetAudioSource() ou le champ _audioSourceComponent dans l'Inspector.")]
        public AudioCapture audioSource
        {
            get => _audioSourceComponent as AudioCapture;
            set { _audioSourceComponent = value; _captureCache = value; }
        }

        #endregion

        #region Runtime public properties

        public float currentGain => _autoGain ? -_head : _gain;

        public Unity.Collections.NativeArray<float> spectrumArray    => Fft.Spectrum;
        public Unity.Collections.NativeArray<float> logSpectrumArray => LogScaler.Resample(Fft.Spectrum);
        public System.ReadOnlySpan<float> spectrumSpan    => Fft.Spectrum.GetReadOnlySpan();
        public System.ReadOnlySpan<float> logSpectrumSpan => logSpectrumArray.GetReadOnlySpan();

        public void ResetAutoGain() => _head = kSilence;

        #endregion

        #region Private

        const float kSilence = -240;
        float _head = kSilence;

        FftBuffer Fft => _fft ?? (_fft = new FftBuffer(_resolution * 2));
        FftBuffer _fft;

        LogScaler LogScaler => _logScaler ?? (_logScaler = new LogScaler());
        LogScaler _logScaler;

        static int ValidateResolution(int x)
        {
            if (x > 0 && (x & (x - 1)) == 0) return x;
            Debug.LogError("Spectrum resolution must be a power of 2.");
            return 1 << (int)math.max(1, math.round(math.log2(x)));
        }

        #endregion

        #region MonoBehaviour

        private void OnEnable()
        {
            _head = kSilence + _dynamicRange;
            Update();
        }

        void OnDisable()
        {
            _fft?.Dispose(); _fft = null;
            _logScaler?.Dispose(); _logScaler = null;
        }

        void Update()
        {
            var src = AudioSource;
            float input = kSilence;

            if (src != null && src.IsReady)
                input = src.GetChannelLevel(_channel);

            var dt = Time.deltaTime;

            if (_autoGain)
            {
                const float kDecaySpeed = 0.6f;
                _head -= kDecaySpeed * dt;
                _head = Mathf.Max(_head, kSilence + _dynamicRange);
                var room = _dynamicRange * 0.05f;
                _head = Mathf.Clamp(input - room, _head, 0);
            }

            if (src != null && src.IsReady && src.InterleavedDataSlice.Length > 0)
                _fft?.Push(src.GetChannelDataSlice(_channel));
            else
                _fft?.PushEmptyData(0);

            _fft?.Analyze(-currentGain - _dynamicRange, -currentGain);
        }

        #endregion
    }
}
