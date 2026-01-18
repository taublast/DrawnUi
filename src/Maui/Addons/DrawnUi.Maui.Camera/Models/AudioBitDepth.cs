namespace DrawnUi.Camera
{
    /// <summary>
    /// Supported audio bit depths for recording.
    /// </summary>
    public enum AudioBitDepth
    {
        Pcm8Bit = 8,      // Low quality, smallest size
        Pcm16Bit = 16,    // Default - good quality, standard size
        Pcm24Bit = 24,    // High quality, larger size
        Float32Bit = 32   // Professional quality, largest size
    }
}
