using System;

namespace DrawnUi.Camera
{
    public struct AudioSample
    {
        public byte[] Data;           // Raw PCM data (16-bit default)
        public long TimestampNs;      // Nanoseconds since capture epoch
        public int SampleRate;        // e.g., 44100
        public int Channels;          // 1 = Mono, 2 = Stereo
        public AudioBitDepth BitDepth;// Bit depth of the sample

        /// <summary>
        /// Gets bytes per sample based on BitDepth. Fallback to 2 bytes (16-bit).
        /// </summary>
        public int BytesPerSample => BitDepth switch
        {
            AudioBitDepth.Pcm8Bit => 1,
            AudioBitDepth.Pcm16Bit => 2,
            AudioBitDepth.Pcm24Bit => 3,
            AudioBitDepth.Float32Bit => 4,
            _ => 2
        };

        public TimeSpan Timestamp => TimeSpan.FromTicks(TimestampNs / 100);
        
        /// <summary>
        /// Calculates number of samples in the Data buffer.
        /// </summary>
        public int SampleCount => Data != null ? Data.Length / (Math.Max(1, Channels) * Math.Max(1, BytesPerSample)) : 0;
    }
}
