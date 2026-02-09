namespace DrawnUi.Camera
{
    /// <summary>
    /// Preprocesses raw PCM16 audio: stereo-to-mono downmix, resampling to a target sample rate,
    /// and optional silence gating. Stateful — maintains resampling continuity across calls.
    ///
    /// Each processing step is skipped automatically when not needed:
    /// - Mono input (1 channel): no downmix, input array passed through as-is.
    /// - Source rate == target rate: no resampling, zero-copy passthrough.
    /// - silenceRmsThreshold == 0: no RMS calculation, silence gating disabled entirely.
    /// - When all conditions match (mono, same rate, no silence gate): Process() returns
    ///   the original input array with zero allocations.
    ///
    /// Usage:
    /// <code>
    /// // Create: target 24kHz output, silence gate at 0.003 RMS, mute after 100 silent chunks
    /// var preprocessor = new AudioSampleConverter(targetSampleRate: 24000);
    ///
    /// // Or disable silence gating:
    /// var preprocessor = new AudioSampleConverter(targetSampleRate: 16000, silenceRmsThreshold: 0);
    ///
    /// // Set source format (call again if audio device changes):
    /// preprocessor.SetFormat(sampleRate: 48000, channels: 2);
    ///
    /// // Process audio chunks (call per audio callback):
    /// byte[] result = preprocessor.Process(rawPcm16Data);
    /// if (result != null)
    /// {
    ///     // result is mono PCM16 at target sample rate — send to API, file, etc.
    /// }
    /// // result == null means prolonged silence, skip sending.
    ///
    /// // Reset resampling state when starting a new session:
    /// preprocessor.Reset();
    /// </code>
    /// </summary>
    public class AudioSampleConverter
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

        /// <summary>
        /// Creates a new audio preprocessor.
        /// </summary>
        /// <param name="targetSampleRate">Desired output sample rate in Hz (e.g. 24000 for OpenAI, 16000 for Whisper).</param>
        /// <param name="silenceRmsThreshold">RMS level (0..1) below which audio is considered silence. Set to 0 to disable silence gating.</param>
        /// <param name="silentChunksBeforeMute">Number of consecutive silent chunks before audio is suppressed. At 480 samples/48kHz, 100 chunks is roughly 1 second.</param>
        public AudioSampleConverter(int targetSampleRate, float silenceRmsThreshold = 0.003f, int silentChunksBeforeMute = 100)
        {
            _targetSampleRate = targetSampleRate;
            _silenceRmsThreshold = silenceRmsThreshold;
            _silentChunksBeforeMute = silentChunksBeforeMute;
        }

        /// <summary>
        /// Set the source audio format. Call before Process(), and again if the audio device changes.
        /// </summary>
        /// <param name="sampleRate">Source sample rate in Hz (e.g. 44100, 48000).</param>
        /// <param name="channels">Number of channels (1=mono, 2=stereo).</param>
        public void SetFormat(int sampleRate, int channels)
        {
            _sourceSampleRate = sampleRate;
            _sourceChannels = channels;
            _formatInitialized = true;
        }

        /// <summary>
        /// Resets resampling and silence gate state. Call when starting a new recording/streaming session.
        /// </summary>
        public void Reset()
        {
            _resamplePosition = 0;
            _consecutiveSilentChunks = 0;
        }

        /// <summary>
        /// Process a chunk of raw PCM16 audio. Applies downmix, silence gate, and resampling as needed.
        /// Each step is skipped when input already matches the target (zero-copy passthrough).
        /// </summary>
        /// <param name="pcmData">Raw PCM16 audio bytes (little-endian, interleaved channels).</param>
        /// <returns>Processed mono PCM16 audio at target sample rate, or null if the chunk should be skipped (silence or invalid input).</returns>
        public byte[] Process(byte[] pcmData)
        {
            if (!_formatInitialized || pcmData == null || pcmData.Length == 0)
                return null;

            // Downmix to mono if multi-channel, otherwise zero-copy passthrough
            byte[] monoData = _sourceChannels > 1
                ? DownmixToMono(pcmData, _sourceChannels)
                : pcmData;

            // Silence gate (skipped entirely when threshold is 0)
            if (_silenceRmsThreshold > 0)
            {
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
            }

            // Resample if rates differ, otherwise zero-copy passthrough
            byte[] result = _sourceSampleRate != _targetSampleRate
                ? Resample(monoData, _sourceSampleRate, _targetSampleRate)
                : monoData;

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
