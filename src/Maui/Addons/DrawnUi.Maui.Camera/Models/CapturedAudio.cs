using System;
using System.Collections.Generic;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Represents a captured audio recording (M4A file).
    /// Returned from audio-only recording when RecordVideo=false.
    /// </summary>
    public class CapturedAudio : IDisposable
    {
        /// <summary>
        /// Path to the recorded M4A file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Duration of the audio recording.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Sample rate in Hz (e.g., 44100, 48000).
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Number of audio channels (1=mono, 2=stereo).
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Timestamp when recording was created.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Optional metadata dictionary.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        public void Dispose()
        {
            // No resources to dispose by default
        }
    }
}
