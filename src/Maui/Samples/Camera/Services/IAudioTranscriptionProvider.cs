namespace CameraTests.Services
{
    /// <summary>
    /// Interface for audio transcription providers (OpenAI Whisper, etc.)
    /// </summary>
    public interface IAudioTranscriptionProvider
    {
        /// <summary>
        /// Transcribes audio data to text
        /// </summary>
        /// <param name="audioData">WAV audio data</param>
        /// <param name="language">Language code (e.g., "en", "es"). Null for auto-detect.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Transcribed text</returns>
        Task<string> TranscribeAsync(byte[] audioData, string language, CancellationToken ct);
    }
}
