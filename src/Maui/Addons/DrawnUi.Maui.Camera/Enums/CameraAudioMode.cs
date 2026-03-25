namespace DrawnUi.Camera
{
    /// <summary>
    /// Controls the audio session configuration during recording and preview capture.
    /// Affects signal processing applied to the microphone input.
    /// </summary>
    public enum CameraAudioMode
    {
        /// <summary>
        /// Standard system audio processing. No mode-specific tuning.
        /// iOS: AVAudioSessionModeDefault. Android: AudioSource.Mic.
        /// This is the default value.
        /// </summary>
        Default = 0,

        /// <summary>
        /// Optimized for video recording. Matches native iOS Camera app behavior.
        /// Captures ambient/environmental sound naturally without voice-specific processing.
        /// iOS: AVAudioSessionModeVideoRecording. Android: AudioSource.Camcorder.
        /// </summary>
        VideoRecording = 1,

        /// <summary>
        /// Voice processing pipeline: AGC, echo cancellation, and noise suppression.
        /// Suited for voice memos, interviews, or call-style recording.
        /// iOS: AVAudioEngine SetVoiceProcessingEnabled. Android: AudioSource.VoiceCommunication.
        /// </summary>
        Voice = 2,

        /// <summary>
        /// Minimal signal processing, flat frequency response.
        /// Suited for audio analysis, measurement tools, or when raw microphone signal is needed.
        /// iOS: AVAudioSessionModeMeasurement. Android: AudioSource.Unprocessed (API 29+, else Mic).
        /// </summary>
        Flat = 3,
    }
}
