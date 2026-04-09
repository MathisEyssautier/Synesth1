using UnityEngine;

namespace UnityVFXReactiveAudio
{
    /// <summary>
    /// Version modifiée d'AudioLevelTracker compatible avec AudioCapture ET FMODAudioCapture.
    /// Le champ audioSource accepte n'importe quel MonoBehaviour implémentant IAudioCapture.
    ///
    /// Changement par rapport à l'original :
    ///   - audioSource est maintenant de type Component (sérialisable) avec cast vers IAudioCapture
    ///   - Toute la logique de niveau reste identique
    /// </summary>
    [AddComponentMenu("UnityVFXReactiveAudio/Audio Level Tracker")]
    public sealed class AudioLevelTracker : MonoBehaviour
    {
        #region Editor attributes and public properties

        [SerializeField, Range(0, 15)] int _channel = 0;
        public int channel { get => _channel; set => _channel = value; }

        [SerializeField] FilterType _filterType = FilterType.Bypass;
        public FilterType filterType { get => _filterType; set => _filterType = value; }

        [SerializeField] bool _autoGain = true;
        public bool autoGain { get => _autoGain; set => _autoGain = value; }

        [SerializeField, Range(-10, 40)] float _gain = 6;
        public float gain { get => _gain; set => _gain = value; }

        [SerializeField, Range(1, 40)] float _dynamicRange = 12;
        public float dynamicRange { get => _dynamicRange; set => _dynamicRange = value; }

        [SerializeField] bool _smoothFall = true;
        public bool smoothFall { get => _smoothFall; set => _smoothFall = value; }

        [SerializeField, Range(0, 1)] float _fallSpeed = 0.3f;
        public float fallSpeed { get => _fallSpeed; set => _fallSpeed = value; }

        [SerializeReference] PropertyBinder[] _propertyBinders = null;
        public PropertyBinder[] propertyBinders
        { get => (PropertyBinder[])_propertyBinders.Clone(); set => _propertyBinders = value; }

        #endregion

        #region Audio source — accepte AudioCapture OU FMODAudioCapture

        // Sérialisé comme Component pour l'Inspector, casté en IAudioCapture à l'usage
        [SerializeField] private Component _audioSourceComponent = null;

        private IAudioCapture _captureCache;

        /// <summary>
        /// Accès à la source audio, quel que soit son type.
        /// Assigner audioSource depuis le code : tracker.SetAudioSource(myFMODCapture);
        /// </summary>
        public IAudioCapture AudioSource
        {
            get
            {
                if (_captureCache == null && _audioSourceComponent != null)
                    _captureCache = _audioSourceComponent as IAudioCapture;
                return _captureCache;
            }
        }

        /// <summary>
        /// Permet d'assigner programmatiquement un AudioCapture ou FMODAudioCapture.
        /// </summary>
        public void SetAudioSource(IAudioCapture source)
        {
            _audioSourceComponent = source as Component;
            _captureCache = source;
        }

        // Compatibilité avec l'ancien champ public audioSource de type AudioCapture
        [System.Obsolete("Utilisez SetAudioSource() ou le champ _audioSourceComponent dans l'Inspector.")]
        public AudioCapture audioSource
        {
            get => _audioSourceComponent as AudioCapture;
            set { _audioSourceComponent = value; _captureCache = value; }
        }

        #endregion

        #region Runtime public properties

        public float currentGain => _autoGain ? -_head : _gain;

        public float inputLevel
        {
            get
            {
                var src = AudioSource;
                if (src == null || !src.IsReady) return kSilence;
                return src.GetChannelLevel(_channel, _filterType);
            }
        }

        public float normalizedLevel => _normalizedLevel;

        public Unity.Collections.NativeSlice<float> audioDataSlice
            => AudioSource?.GetChannelDataSlice(_channel)
               ?? default(Unity.Collections.NativeSlice<float>);

        public void ResetAutoGain() => _head = kSilence;

        #endregion

        #region Private

        const float kSilence = -60;
        float _normalizedLevel = 0;
        float _head = kSilence;
        float _fall = 0;

        #endregion

        #region MonoBehaviour

        void Update()
        {
            var src = AudioSource;
            if (src == null || !src.IsReady) return;

            var input = inputLevel;
            var dt = Time.deltaTime;

            if (_autoGain)
            {
                const float kDecaySpeed = 0.6f;
                _head = Mathf.Max(_head - kDecaySpeed * dt, kSilence);
                var room = _dynamicRange * 0.05f;
                _head = Mathf.Clamp(input - room, _head, 0);
            }

            var normalizedInput = Mathf.Clamp01((input + currentGain) / _dynamicRange + 1);

            if (_smoothFall)
            {
                _fall += Mathf.Pow(10, 1 + _fallSpeed * 2) * dt;
                _normalizedLevel -= _fall * dt;
                if (_normalizedLevel < normalizedInput)
                {
                    _normalizedLevel = normalizedInput;
                    _fall = 0;
                }
            }
            else
            {
                _normalizedLevel = normalizedInput;
            }

            if (_propertyBinders != null)
                foreach (var b in _propertyBinders) b.Level = _normalizedLevel;
        }

        #endregion
    }
}
