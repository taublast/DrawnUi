namespace CameraTests.Services
{
    /// <summary>
    /// Preprocesses raw PCM16 audio: stereo→mono downmix, resampling to target rate,
    /// and silence gating. Stateful — maintains resampling continuity across calls.
    /// </summary>
    public class AudioPreprocessor
    {
        private readonly int _targetSampleRate;
        private readonly float _silenceRmsThreshold;
        private readonly int _silentChunksBeforeMute;

        private int _sourceSampleRate;
        private int _sourceChannels = 1;
        private bool _formatInitialized;

        // Resampling state for continuity across chunks
        private double _resamplePosition;

        // Silence gate state
        private int _consecutiveSilentChunks;

        public AudioPreprocessor(int targetSampleRate, float silenceRmsThreshold = 0.003f, int silentChunksBeforeMute = 100)
        {
            _targetSampleRate = targetSampleRate;
            _silenceRmsThreshold = silenceRmsThreshold;
            _silentChunksBeforeMute = silentChunksBeforeMute;
        }

        public void SetFormat(int sampleRate, int channels)
        {
            _sourceSampleRate = sampleRate;
            _sourceChannels = channels;
            _formatInitialized = true;
        }

        public void Reset()
        {
            _resamplePosition = 0;
            _consecutiveSilentChunks = 0;
        }

        /// <summary>
        /// Process raw PCM16 audio: downmix to mono, apply silence gate, resample to target rate.
        /// Returns null if audio should be skipped (prolonged silence).
        /// </summary>
        public byte[] Process(byte[] pcmData)
        {
            if (!_formatInitialized || pcmData == null || pcmData.Length == 0)
                return null;

            // Downmix to mono if multi-channel
            byte[] monoData;
            if (_sourceChannels > 1)
            {
                monoData = DownmixToMono(pcmData, _sourceChannels);
            }
            else
            {
                monoData = pcmData;
            }

            // Silence gate: skip only after prolonged continuous silence
            if (CalculateRms(monoData) < _silenceRmsThreshold)
            {
                _consecutiveSilentChunks++;
                if (_consecutiveSilentChunks > _silentChunksBeforeMute)
                    return null;
            }
            else
            {
                _consecutiveSilentChunks = 0;
            }

            // Resample if needed
            byte[] result;
            if (_sourceSampleRate != _targetSampleRate)
            {
                result = Resample(monoData, _sourceSampleRate, _targetSampleRate);
            }
            else
            {
                result = monoData;
            }

            return result.Length > 0 ? result : null;
        }

        /// <summary>
        /// Fast RMS calculation on PCM16 mono data. Returns normalized 0..1 value.
        /// </summary>
        private static float CalculateRms(byte[] monoData)
        {
            if (monoData.Length < 2) return 0;

            long sum = 0;
            int sampleCount = monoData.Length / 2;
            for (int i = 0; i < monoData.Length - 1; i += 2)
            {
                short s = (short)(monoData[i] | (monoData[i + 1] << 8));
                sum += s * s;
            }
            return (float)Math.Sqrt((double)sum / sampleCount) / 32768f;
        }

        /// <summary>
        /// Downmixes multi-channel PCM16 to mono by averaging all channels per frame.
        /// </summary>
        private static byte[] DownmixToMono(byte[] input, int channels)
        {
            int bytesPerSample = 2;
            int frameSize = channels * bytesPerSample;
            int frameCount = input.Length / frameSize;
            var output = new byte[frameCount * bytesPerSample];

            for (int f = 0; f < frameCount; f++)
            {
                int sum = 0;
                int offset = f * frameSize;
                for (int ch = 0; ch < channels; ch++)
                {
                    int idx = offset + ch * bytesPerSample;
                    short sample = (short)(input[idx] | (input[idx + 1] << 8));
                    sum += sample;
                }
                short mono = (short)(sum / channels);
                int outIdx = f * bytesPerSample;
                output[outIdx] = (byte)(mono & 0xFF);
                output[outIdx + 1] = (byte)((mono >> 8) & 0xFF);
            }

            return output;
        }

        /// <summary>
        /// Resamples PCM16 mono audio using linear interpolation.
        /// Maintains continuity across calls via _resamplePosition.
        /// </summary>
        private byte[] Resample(byte[] input, int sourceRate, int targetRate)
        {
            int bytesPerSample = 2;
            int sourceSampleCount = input.Length / bytesPerSample;
            if (sourceSampleCount == 0) return Array.Empty<byte>();

            double ratio = (double)sourceRate / targetRate;
            int targetSampleCount = (int)Math.Ceiling(sourceSampleCount / ratio);

            var output = new byte[targetSampleCount * bytesPerSample];
            int outputIndex = 0;

            for (int i = 0; i < targetSampleCount; i++)
            {
                double srcPos = _resamplePosition + i * ratio;
                int srcIndex = (int)srcPos;
                double frac = srcPos - srcIndex;

                short sample;
                if (srcIndex + 1 < sourceSampleCount)
                {
                    short s0 = (short)(input[srcIndex * 2] | (input[srcIndex * 2 + 1] << 8));
                    short s1 = (short)(input[(srcIndex + 1) * 2] | (input[(srcIndex + 1) * 2 + 1] << 8));
                    sample = (short)(s0 + (s1 - s0) * frac);
                }
                else if (srcIndex < sourceSampleCount)
                {
                    sample = (short)(input[srcIndex * 2] | (input[srcIndex * 2 + 1] << 8));
                }
                else
                {
                    break;
                }

                output[outputIndex++] = (byte)(sample & 0xFF);
                output[outputIndex++] = (byte)((sample >> 8) & 0xFF);
            }

            _resamplePosition = (_resamplePosition + targetSampleCount * ratio) - sourceSampleCount;
            if (_resamplePosition < 0) _resamplePosition = 0;

            if (outputIndex < output.Length)
            {
                Array.Resize(ref output, outputIndex);
            }

            return output;
        }
    }
}
