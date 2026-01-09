using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Represents an available audio input device
    /// </summary>
    public class AudioDeviceInfo
    {
        public int Index { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public bool IsDefault { get; set; }
    }

    public interface IAudioCapture : IDisposable
    {
        bool IsCapturing { get; }
        int SampleRate { get; }
        int Channels { get; }

        event EventHandler<AudioSample> SampleAvailable;

        /// <summary>
        /// Start audio capture with optional device selection
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hz</param>
        /// <param name="channels">Number of channels (1=mono, 2=stereo)</param>
        /// <param name="bitDepth">Audio bit depth</param>
        /// <param name="deviceIndex">Audio device index (-1 for default device)</param>
        Task<bool> StartAsync(int sampleRate = 44100, int channels = 1, AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit, int deviceIndex = -1);
        Task StopAsync();

        /// <summary>
        /// Get list of available audio input devices
        /// </summary>
        Task<List<AudioDeviceInfo>> GetAvailableDevicesAsync();
    }
}
