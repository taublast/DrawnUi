/*
using System.Diagnostics;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace CameraTests.Services
{



    /// <summary>
    /// Azure Speech Services implementation of IRealtimeTranscriptionService.
    /// Uses the Microsoft.CognitiveServices.Speech SDK with PushAudioInputStream
    /// for real-time continuous recognition.
    ///  <PackageReference Include="Microsoft.CognitiveServices.Speech" Version="1.42.0" />
    /// </summary>
    public class AzureSpeechTranscriptionService : IRealtimeTranscriptionService
    {
        private readonly string _subscriptionKey;
        private readonly string _region;

        private SpeechRecognizer _recognizer;
        private PushAudioInputStream _audioInputStream;
        private AudioConfig _audioConfig;
        private bool _isRunning;

        // Source audio format
        private int _sourceSampleRate;
        private int _sourceBitsPerSample = 16;
        private int _sourceChannels = 1;
        private bool _formatInitialized;

        public string Language { get; set; }

        public event Action<string> TranscriptionDelta;
        public event Action<string> TranscriptionCompleted;

        public AzureSpeechTranscriptionService(string subscriptionKey, string region)
        {
            _subscriptionKey = subscriptionKey;
            _region = region;
        }

        public void SetAudioFormat(int sampleRate, int bitsPerSample, int channels)
        {
            _sourceSampleRate = sampleRate;
            _sourceBitsPerSample = bitsPerSample;
            _sourceChannels = channels;
            _formatInitialized = true;
            Debug.WriteLine($"[AzureSpeech] Audio format: {sampleRate}Hz, {bitsPerSample}bit, {channels}ch");
        }

        public async void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            try
            {
                await StartRecognitionAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AzureSpeech] Start failed: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            Task.Run(async () =>
            {
                try
                {
                    if (_recognizer != null)
                    {
                        await _recognizer.StopContinuousRecognitionAsync();
                        Debug.WriteLine("[AzureSpeech] Recognition stopped");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AzureSpeech] Stop error: {ex.Message}");
                }
                finally
                {
                    CleanupResources();
                }
            });
        }

        public void FeedAudio(byte[] pcmData)
        {
            if (!_isRunning || !_formatInitialized || _audioInputStream == null ||
                pcmData == null || pcmData.Length == 0)
                return;

            try
            {
                _audioInputStream.Write(pcmData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AzureSpeech] FeedAudio error: {ex.Message}");
            }
        }

        private async Task StartRecognitionAsync()
        {
            if (!_formatInitialized)
            {
                Debug.WriteLine("[AzureSpeech] Audio format not set, deferring start");
                return;
            }

            // Create push stream with the source audio format
            // Azure SDK handles format conversion internally — no resampling needed
            var format = AudioStreamFormat.GetWaveFormatPCM(
                (uint)_sourceSampleRate,
                (byte)_sourceBitsPerSample,
                (byte)_sourceChannels);

            _audioInputStream = AudioInputStream.CreatePushStream(format);
            _audioConfig = AudioConfig.FromStreamInput(_audioInputStream);

            var speechConfig = SpeechConfig.FromSubscription(_subscriptionKey, _region);

            if (!string.IsNullOrEmpty(Language))
            {
                speechConfig.SpeechRecognitionLanguage = Language;
            }

            // Enable partial results (intermediate recognition)
            speechConfig.SetProperty(PropertyId.SpeechServiceResponse_RequestSentimentAnalysis, "false");

            _recognizer = new SpeechRecognizer(speechConfig, _audioConfig);

            // Partial/intermediate results (streaming text)
            _recognizer.Recognizing += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizingSpeech)
                {
                    var text = e.Result.Text;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Debug.WriteLine($"[AzureSpeech] Recognizing: {text}");
                        TranscriptionDelta?.Invoke(text);
                    }
                }
            };

            // Final/completed recognition
            _recognizer.Recognized += (s, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech)
                {
                    var text = e.Result.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Debug.WriteLine($"[AzureSpeech] Recognized: {text}");
                        TranscriptionCompleted?.Invoke(text);
                    }
                }
                else if (e.Result.Reason == ResultReason.NoMatch)
                {
                    Debug.WriteLine("[AzureSpeech] No match");
                }
            };

            _recognizer.Canceled += (s, e) =>
            {
                Debug.WriteLine($"[AzureSpeech] Canceled: {e.Reason}, Error: {e.ErrorDetails}");
                if (e.Reason == CancellationReason.Error)
                {
                    _isRunning = false;
                }
            };

            _recognizer.SessionStarted += (s, e) =>
            {
                Debug.WriteLine("[AzureSpeech] Session started");
            };

            _recognizer.SessionStopped += (s, e) =>
            {
                Debug.WriteLine("[AzureSpeech] Session stopped");
            };

            Debug.WriteLine("[AzureSpeech] Starting continuous recognition...");
            await _recognizer.StartContinuousRecognitionAsync();
            Debug.WriteLine("[AzureSpeech] Continuous recognition started");
        }

        private void CleanupResources()
        {
            _recognizer?.Dispose();
            _recognizer = null;

            _audioConfig?.Dispose();
            _audioConfig = null;

            _audioInputStream?.Dispose();
            _audioInputStream = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }


}

*/
