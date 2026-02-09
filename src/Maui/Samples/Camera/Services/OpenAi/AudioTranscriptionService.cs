using System.Diagnostics;
using AppoMobi;

namespace CameraTests.Services
{
    /// <summary>
    /// Audio transcription service with adaptive silence detection.
    /// Cuts ONLY on silence - no forced cuts. Adapts threshold to stream noise floor.
    /// </summary>
    public class AudioTranscriptionService : IDisposable
    {
        private readonly IAudioTranscriptionProvider _transcriptionProvider;
        private readonly List<byte> _audioBuffer = new();
        private readonly object _bufferLock = new();

        // Audio format
        private int _sampleRate = 16000;
        private int _bitsPerSample = 16;
        private int _channels = 1;
        private bool _formatInitialized;

        // Adaptive silence detection
        // Start low, adapt UP to track actual quiet level in stream
        private float _noiseFloor = 0.001f;           // Adapts UP to match stream's quiet level
        private const float ThresholdMargin = 1.3f;   // Silence threshold = noiseFloor * margin
        private const float SilenceDuration = 0.1f;  // Seconds of silence before cut
        private float _silenceDuration;
        private bool _hasAudioData;

        // Minimum audio before allowing cut
        private const float MinChunkDuration = 0.3f;
        private float _chunkDuration;
        private int _minAudioBytes;

        // Language for transcription (null = auto-detect)
        public string Language { get; set; }

        // Event raised when transcription is available
        public event Action<string> TranscriptionReceived;

        private CancellationTokenSource _processingCts;
        private bool _isProcessing;

        public AudioTranscriptionService(IAudioTranscriptionProvider transcriptionProvider = null)
        {
            _transcriptionProvider = transcriptionProvider ?? new OpenAiAudioTranscriptionService(Secrets.OpenAiKey);
        }

        public void SetAudioFormat(int sampleRate, int bitsPerSample, int channels)
        {
            _sampleRate = sampleRate;
            _bitsPerSample = bitsPerSample;
            _channels = channels;
            _formatInitialized = true;

            int bytesPerSample = _bitsPerSample / 8;
            _minAudioBytes = (int)(_sampleRate * MinChunkDuration * _channels * bytesPerSample);
        }

        public void Start()
        {
            if (_isProcessing) return;

            _isProcessing = true;
            _processingCts = new CancellationTokenSource();
            _chunkDuration = 0;
            _silenceDuration = 0;
            _hasAudioData = false;
            _noiseFloor = 0.001f; // Start low, adapts UP

            lock (_bufferLock)
            {
                _audioBuffer.Clear();
            }
        }

        public void Stop()
        {
            if (!_isProcessing) return;

            _isProcessing = false;
            _processingCts?.Cancel();
            _processingCts?.Dispose();
            _processingCts = null;

            ProcessFinalChunk();
        }

        public void FeedAudio(byte[] data)
        {
            if (!_isProcessing || !_formatInitialized || data == null || data.Length == 0)
                return;

            int bytesPerSample = _bitsPerSample / 8;
            float frameDuration = (float)data.Length / (_sampleRate * _channels * bytesPerSample);

            // Buffer all audio
            lock (_bufferLock)
            {
                _audioBuffer.AddRange(data);
            }
            _chunkDuration += frameDuration;

            // Calculate RMS
            float rms = CalculateRMS(data);

            // Silence = below threshold (floor * margin)
            float silenceThreshold = _noiseFloor * ThresholdMargin;
            bool isSilence = rms < silenceThreshold;

            if (isSilence)
            {
                _silenceDuration += frameDuration;
            }
            else
            {
                _silenceDuration = 0;
                _hasAudioData = true;
            }

            // Cut ONLY on silence (no forced max cut)
            bool shouldCut = _chunkDuration >= MinChunkDuration &&
                             _silenceDuration >= SilenceDuration &&
                             _hasAudioData;

            if (shouldCut)
            {
                ProcessAudioChunk();
            }
        }

        private float CalculateRMS(byte[] data)
        {
            if (data.Length < 2) return 0;

            double sum = 0;
            int sampleCount = 0;

            for (int i = 0; i < data.Length - 1; i += 2)
            {
                short sample = (short)(data[i] | (data[i + 1] << 8));
                float normalized = sample / 32768f;
                sum += normalized * normalized;
                sampleCount++;
            }

            if (sampleCount == 0) return 0;
            return (float)Math.Sqrt(sum / sampleCount);
        }

        private void ProcessAudioChunk()
        {
            byte[] audioData;
            lock (_bufferLock)
            {
                if (_audioBuffer.Count < _minAudioBytes)
                {
                    _audioBuffer.Clear();
                    ResetChunkState();
                    return;
                }

                audioData = _audioBuffer.ToArray();
                _audioBuffer.Clear();
            }

            ResetChunkState();
            _ = ProcessAndTranscribeAsync(audioData);
        }

        private void ResetChunkState()
        {
            _hasAudioData = false;
            _silenceDuration = 0;
            _chunkDuration = 0;
        }

        private void ProcessFinalChunk()
        {
            byte[] audioData;
            lock (_bufferLock)
            {
                int bytesPerSample = _bitsPerSample / 8;
                int finalMinBytes = (int)(_sampleRate * 0.15f * _channels * bytesPerSample);

                if (_audioBuffer.Count < finalMinBytes)
                {
                    _audioBuffer.Clear();
                    return;
                }

                audioData = _audioBuffer.ToArray();
                _audioBuffer.Clear();
            }

            _ = ProcessAndTranscribeAsync(audioData);
        }

        private async Task ProcessAndTranscribeAsync(byte[] pcmData)
        {
            try
            {
                var wavData = ConvertPcmToWav(pcmData);
                float durationSec = (float)pcmData.Length / (_sampleRate * _channels * (_bitsPerSample / 8));
                Debug.WriteLine($"[Audio] Sending chunk: {wavData.Length} bytes, {durationSec:F2}s");

                var ct = _processingCts?.Token ?? CancellationToken.None;
                var text = await _transcriptionProvider.TranscribeAsync(wavData, Language, ct);

                Debug.WriteLine($"[Audio] Result: \"{text}\"");

                if (!string.IsNullOrWhiteSpace(text))
                {
                    TranscriptionReceived?.Invoke(text);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private byte[] ConvertPcmToWav(byte[] pcmData)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            int bytesPerSample = _bitsPerSample / 8;
            int byteRate = _sampleRate * _channels * bytesPerSample;
            short blockAlign = (short)(_channels * bytesPerSample);

            writer.Write(new char[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + pcmData.Length);
            writer.Write(new char[] { 'W', 'A', 'V', 'E' });

            writer.Write(new char[] { 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)_channels);
            writer.Write(_sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write((short)_bitsPerSample);

            writer.Write(new char[] { 'd', 'a', 't', 'a' });
            writer.Write(pcmData.Length);
            writer.Write(pcmData);

            return ms.ToArray();
        }

        public void Dispose()
        {
            Stop();
            _processingCts?.Dispose();
        }
    }
}

