using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AppoMobi;
using DrawnUi.Camera;

namespace CameraTests.Services
{
    /// <summary>
    /// OpenAI Realtime API transcription service using WebSocket.
    /// Implements IRealtimeTranscriptionService for pluggable use.
    /// </summary>
    public class OpenAiRealtimeTranscriptionService : IRealtimeTranscriptionService
    {
        private const string WebSocketUrl = "wss://api.openai.com/v1/realtime?intent=transcription";
        private const int TargetSampleRate = 24000;

        private readonly string _apiKey;
        private readonly AudioSampleConverter _preprocessor;
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _sessionConfigured;

        // Send queue for serialized WebSocket writes
        private readonly ConcurrentQueue<byte[]> _sendQueue = new();
        private readonly SemaphoreSlim _sendSignal = new(0);

        private int _feedCount;

        public string Language { get; set; }
        public string Model { get; set; } = "gpt-4o-mini-transcribe";

        public event Action<string> TranscriptionDelta;
        public event Action<string> TranscriptionCompleted;

        public OpenAiRealtimeTranscriptionService(string apiKey = null)
        {
            _apiKey = apiKey ?? Secrets.OpenAiKey;
            _preprocessor = new AudioSampleConverter(TargetSampleRate);
        }

        public void SetAudioFormat(int sampleRate, int bitsPerSample, int channels)
        {
            _preprocessor.SetFormat(sampleRate, channels);
            Debug.WriteLine($"[RealtimeTranscription] Audio format: {sampleRate}Hz, {bitsPerSample}bit, {channels}ch");
        }

        public async void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _sessionConfigured = false;
            _feedCount = 0;
            _preprocessor.Reset();

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
            _sendSignal.Release();

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

        public void FeedAudio(byte[] pcmData)
        {
            if (!_isRunning || !_sessionConfigured || _webSocket?.State != WebSocketState.Open)
                return;

            try
            {
                var processed = _preprocessor.Process(pcmData);
                if (processed == null)
                {
                    //can be empty if silence gating is enabled and audio is below threshold
                    return;
                }

                var base64Audio = Convert.ToBase64String(processed);
                var message = JsonSerializer.Serialize(new
                {
                    type = "input_audio_buffer.append",
                    audio = base64Audio
                });

                _sendQueue.Enqueue(Encoding.UTF8.GetBytes(message));
                _sendSignal.Release();

                _feedCount++;
                if (_feedCount % 100 == 0)
                {
                    Debug.WriteLine($"[RealtimeTranscription] Fed {_feedCount} chunks, last={processed.Length}b");
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

            Task.Run(() => ReceiveLoopAsync(ct), ct);
            Task.Run(() => SendLoopAsync(ct), ct);

            await SendJsonAsync(BuildSessionConfigJson(), ct);
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

        private async Task SendJsonAsync(string json, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        private bool IsSendingData;
        public event Action<bool> SendingData;

        protected void NotifyIsSendingData(bool state)
        {
            IsSendingData = state;
            SendingData?.Invoke(state);
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
                            NotifyIsSendingData(true);

                            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true,
                                ct);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[RealtimeTranscription] Send error: {ex.Message}");
                            return;
                        }
                        finally
                        {
                            NotifyIsSendingData(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
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

        public void Dispose()
        {
            Stop();
        }
    }
}
