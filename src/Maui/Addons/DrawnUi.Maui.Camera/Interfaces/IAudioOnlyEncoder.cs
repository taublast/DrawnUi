using System;
using System.Threading.Tasks;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Interface for audio-only recording encoders.
    /// Used when EnableVideoRecording=false to create M4A files containing only audio.
    /// </summary>
    public interface IAudioOnlyEncoder : IDisposable
    {
        /// <summary>
        /// Initialize the encoder with output path and audio parameters.
        /// </summary>
        Task InitializeAsync(string outputPath, int sampleRate, int channels, AudioBitDepth bitDepth);

        /// <summary>
        /// Start the encoder. Call after InitializeAsync.
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// Write an audio sample to the encoder.
        /// </summary>
        void WriteAudio(AudioSample sample);

        /// <summary>
        /// Stop recording and finalize the output file.
        /// </summary>
        Task<CapturedAudio> StopAsync();

        /// <summary>
        /// Abort recording without finalizing (discard output).
        /// </summary>
        Task AbortAsync();

        /// <summary>
        /// Whether the encoder is currently recording.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Current recording duration.
        /// </summary>
        TimeSpan RecordingDuration { get; }
    }
}
