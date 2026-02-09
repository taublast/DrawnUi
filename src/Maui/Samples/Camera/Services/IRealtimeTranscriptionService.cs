namespace CameraTests.Services
{
    /// <summary>
    /// Interface for real-time audio transcription services.
    /// Accepts raw PCM audio, handles format conversion internally,
    /// and delivers transcription results via events.
    /// </summary>
    public interface IRealtimeTranscriptionService : IDisposable
    {
        /// <summary>
        /// Language hint for transcription (e.g. "en", "es"). Null for auto-detect.
        /// </summary>
        string Language { get; set; }

        /// <summary>
        /// Set the source audio format. Must be called before FeedAudio.
        /// Called again if format changes (e.g. user switches audio device).
        /// </summary>
        void SetAudioFormat(int sampleRate, int bitsPerSample, int channels);

        /// <summary>
        /// Start the transcription session (connect, authenticate, etc.).
        /// </summary>
        void Start();

        /// <summary>
        /// Stop the transcription session and release resources.
        /// </summary>
        void Stop();

        /// <summary>
        /// Feed raw PCM audio data. Format must match the last SetAudioFormat call.
        /// </summary>
        void FeedAudio(byte[] pcmData);

        /// <summary>
        /// Fired when a partial transcription delta is available (streaming text).
        /// </summary>
        event Action<string> TranscriptionDelta;

        /// <summary>
        /// Fired when a complete transcription segment is finalized.
        /// </summary>
        event Action<string> TranscriptionCompleted;
    }
}
