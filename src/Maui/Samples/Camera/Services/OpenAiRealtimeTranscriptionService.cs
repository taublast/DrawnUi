using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AppoMobi;

namespace CameraTests.Services
{
    /// <summary>
    /// OpenAI Realtime API transcription service using WebSocket.
    /// Streams PCM audio to OpenAI and receives real-time transcription deltas.
    /// Handles stereo→mono downmix and resampling to required 24kHz.
    /// </summary>
    public class OpenAiRealtimeTranscriptionService : IDisposable
    {
        private const string WebSocketUrl = "wss://api.openai.com/v1/realtime?intent=transcription";
        private const int TargetSampleRate = 24000;

        private readonly string _apiKey;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private Task _receiveTask;
        private Task _sendTask;
        private bool _isRunning;
        private bool _sessionConfigured;

        // Source audio format
        private int _sourceSampleRate;
        private int _sourceBitsPerSample = 16;
        private int _sourceChannels = 1;
        private bool _formatInitialized;

        // Send queue for serialized WebSocket writes
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        // Resampling state for continuity across chunks
        private double _resamplePosition;

        public string Language { get; set; }
        public string Model { get; set; } = "gpt-4o-mini-transcribe";

        /// <summary>
        /// Fired when a transcription delta (partial text) is received.
        /// </summary>
        public event Action<string> TranscriptionDelta;

        /// <summary>
        /// Fired when a complete transcription segment is received.
        /// </summary>
        public event Action<string> TranscriptionCompleted;

        public OpenAiRealtimeTranscriptionService(string apiKey = null)
        {
            _apiKey = apiKey ?? Secrets.OpenAiKey;
        }

        public void SetAudioFormat(int sampleRate, int bitsPerSample, int channels)
        {
            _sourceSampleRate = sampleRate;
            _sourceBitsPerSample = bitsPerSample;
            _sourceChannels = channels;
            _formatInitialized = true;
            Debug.WriteLine($"[RealtimeTranscription] Audio format: {sampleRate}Hz, {bitsPerSample}bit, {channels}ch");
        }

        public async void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _sessionConfigured = false;
            _resamplePosition = 0;

            // Drain any stale send queue
            while (_sendQueue.TryDequeue(out _)) { }

            _cts = new CancellationTokenSource();

            try
            {
                await ConnectAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealtimeTranscription] Connect failed: {ex.Message}");
                _isRunning = false;
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _cts?.Cancel();
            _sendSignal.Release(); // Unblock send loop

            Task.Run(async () =>
            {
                try
                {
                    if (_webSocket?.State == WebSocketState.Open)
                    {
                        using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", closeCts.Token);
                    }
                }
                catch { }
                finally
                {
                    _webSocket?.Dispose();
                    _webSocket = null;
                    _sessionConfigured = false;
                }
            });

            _cts?.Dispose();
            _cts = null;
        }

        private int _feedCount;

        public void FeedAudio(byte[] pcmData)
        {
            if (!_isRunning || !_formatInitialized || !_sessionConfigured ||
                pcmData == null || pcmData.Length == 0)
                return;

            if (_webSocket?.State != WebSocketState.Open)
                return;

            try
            {
                // Downmix to mono if stereo
                byte[] monoData;
                if (_sourceChannels > 1)
                {
                    monoData = DownmixToMono(pcmData, _sourceChannels);
                }
                else
                {
                    monoData = pcmData;
                }

                // Resample to 24kHz if needed
                byte[] audioToSend;
                if (_sourceSampleRate != TargetSampleRate)
                {
                    audioToSend = Resample(monoData, _sourceSampleRate, TargetSampleRate);
                }
                else
                {
                    audioToSend = monoData;
                }

                if (audioToSend.Length == 0)
                    return;

                var base64Audio = Convert.ToBase64String(audioToSend);
                var message = JsonSerializer.Serialize(new
                {
                    type = "input_audio_buffer.append",
                    audio = base64Audio
                });

                var bytes = Encoding.UTF8.GetBytes(message);

                // Enqueue for serialized sending
                _sendQueue.Enqueue(bytes);
                _sendSignal.Release();

                _feedCount++;
                if (_feedCount % 100 == 0)
                {
                    Debug.WriteLine($"[RealtimeTranscription] Fed {_feedCount} chunks, last mono={monoData.Length}b → resampled={audioToSend.Length}b");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealtimeTranscription] FeedAudio error: {ex.Message}");
            }
        }

        private async Task ConnectAsync(CancellationToken ct)
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            _webSocket.Options.SetRequestHeader("OpenAI-Beta", "realtime=v1");

            Debug.WriteLine("[RealtimeTranscription] Connecting...");
            await _webSocket.ConnectAsync(new Uri(WebSocketUrl), ct);
            Debug.WriteLine("[RealtimeTranscription] Connected");

            // Start receive loop
            _receiveTask = Task.Run(() => ReceiveLoopAsync(ct), ct);

            // Start serialized send loop
            _sendTask = Task.Run(() => SendLoopAsync(ct), ct);

            // Send session configuration
            await SendDirectAsync(BuildSessionConfigJson(), ct);
        }

        private string BuildSessionConfigJson()
        {
            var sessionConfig = new
            {
                type = "transcription_session.update",
                session = new
                {
                    input_audio_format = "pcm16",
                    input_audio_transcription = BuildTranscriptionConfig(),
                    turn_detection = new
                    {
                        type = "server_vad",
                        threshold = 0.5,
                        prefix_padding_ms = 300,
                        silence_duration_ms = 500
                    },
                    input_audio_noise_reduction = new
                    {
                        type = "near_field"
                    }
                }
            };

            var json = JsonSerializer.Serialize(sessionConfig);
            Debug.WriteLine($"[RealtimeTranscription] Sending config: {json}");
            return json;
        }

        private async Task SendDirectAsync(string json, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                ct);
        }

        private async Task SendLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await _sendSignal.WaitAsync(ct);

                    while (_sendQueue.TryDequeue(out var data))
                    {
                        if (_webSocket?.State != WebSocketState.Open)
                            return;

                        try
                        {
                            await _webSocket.SendAsync(
                                new ArraySegment<byte>(data),
                                WebSocketMessageType.Text,
                                true,
                                ct);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[RealtimeTranscription] Send error: {ex.Message}");
                            return;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
        }

        private object BuildTranscriptionConfig()
        {
            if (!string.IsNullOrEmpty(Language))
            {
                return new
                {
                    model = Model,
                    language = Language,
                    prompt = "Transcribe the speech accurately."
                };
            }
            return new
            {
                model = Model,
                prompt = "Transcribe the speech accurately."
            };
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            var messageBuffer = new StringBuilder();

            try
            {
                while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
                {
                    messageBuffer.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.WriteLine("[RealtimeTranscription] Server closed connection");
                            return;
                        }
                        messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    ProcessMessage(messageBuffer.ToString());
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                Debug.WriteLine($"[RealtimeTranscription] WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealtimeTranscription] Receive error: {ex.Message}");
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeElement))
                    return;

                var eventType = typeElement.GetString();

                switch (eventType)
                {
                    case "transcription_session.created":
                    case "transcription_session.updated":
                        _sessionConfigured = true;
                        Debug.WriteLine($"[RealtimeTranscription] Session configured: {eventType}");
                        break;

                    case "conversation.item.input_audio_transcription.delta":
                        if (root.TryGetProperty("delta", out var delta))
                        {
                            var text = delta.GetString();
                            if (!string.IsNullOrEmpty(text))
                            {
                                TranscriptionDelta?.Invoke(text);
                            }
                        }
                        break;

                    case "conversation.item.input_audio_transcription.completed":
                        if (root.TryGetProperty("transcript", out var transcript))
                        {
                            var text = transcript.GetString();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                Debug.WriteLine($"[RealtimeTranscription] Completed: {text}");
                                TranscriptionCompleted?.Invoke(text);
                            }
                        }
                        break;

                    case "input_audio_buffer.speech_started":
                        Debug.WriteLine("[RealtimeTranscription] Speech started");
                        break;

                    case "input_audio_buffer.speech_stopped":
                        Debug.WriteLine("[RealtimeTranscription] Speech stopped");
                        break;

                    case "input_audio_buffer.committed":
                        Debug.WriteLine("[RealtimeTranscription] Audio committed");
                        break;

                    case "error":
                        if (root.TryGetProperty("error", out var error))
                        {
                            var msg = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown";
                            Debug.WriteLine($"[RealtimeTranscription] ERROR: {msg}");
                        }
                        break;

                    default:
                        Debug.WriteLine($"[RealtimeTranscription] Event: {eventType}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RealtimeTranscription] Parse error: {ex.Message}");
            }
        }

        /// <summary>
        /// Downmixes multi-channel PCM16 to mono by averaging all channels per frame.
        /// </summary>
        private static byte[] DownmixToMono(byte[] input, int channels)
        {
            int bytesPerSample = 2; // 16-bit
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
        /// Resamples PCM16 mono audio from source rate to target rate using linear interpolation.
        /// Maintains continuity across calls via _resamplePosition.
        /// </summary>
        private byte[] Resample(byte[] input, int sourceRate, int targetRate)
        {
            int bytesPerSample = 2; // 16-bit
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

            // Track position for next call continuity
            _resamplePosition = (_resamplePosition + targetSampleCount * ratio) - sourceSampleCount;
            if (_resamplePosition < 0) _resamplePosition = 0;

            if (outputIndex < output.Length)
            {
                Array.Resize(ref output, outputIndex);
            }

            return output;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
