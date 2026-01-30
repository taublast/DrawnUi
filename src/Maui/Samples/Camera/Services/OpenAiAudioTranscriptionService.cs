using System.Net.Http.Headers;
using System.Text.Json;

namespace CameraTests.Services
{
    /// <summary>
    /// OpenAI Whisper API implementation for audio transcription
    /// </summary>
    public class OpenAiAudioTranscriptionService : IAudioTranscriptionProvider
    {
        private string ApiKey = "";

        private const string WhisperEndpoint = "https://api.openai.com/v1/audio/transcriptions";
        private const string Model = "whisper-1";

        private readonly HttpClient _httpClient;

        public OpenAiAudioTranscriptionService(string apiKey)
        {
            ApiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<string> TranscribeAsync(byte[] audioData, string language, CancellationToken ct)
        {
            if (audioData == null || audioData.Length == 0)
                return string.Empty;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, WhisperEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                using var content = new MultipartFormDataContent();

                // Add audio file
                var audioContent = new ByteArrayContent(audioData);
                audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                content.Add(audioContent, "file", "audio.wav");

                // Add model
                content.Add(new StringContent(Model), "model");

                // Add language if specified (improves accuracy)
                if (!string.IsNullOrEmpty(language))
                {
                    content.Add(new StringContent(language), "language");
                }

                // Prevent hallucination - tell Whisper to only transcribe, not continue
                content.Add(new StringContent("Transcribe only. Do not add anything."), "prompt");

                // Temperature 0 = deterministic, less creative/hallucination
                content.Add(new StringContent("0"), "temperature");

                // Response format
                content.Add(new StringContent("json"), "response_format");

                request.Content = content;

                using var response = await _httpClient.SendAsync(request, ct);
                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    // Parse error message if available, otherwise return empty
                    return TryParseErrorMessage(responseBody);
                }

                // Parse JSON response: {"text": "..."}
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString()?.Trim() ?? string.Empty;
                }

                return string.Empty;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TryParseErrorMessage(string responseBody)
        {
            // Don't show errors to user - just return empty
            // Errors are typically: rate limit, invalid audio, too short audio, etc.
            // These are not useful to display on screen
            return string.Empty;
        }
    }
}
