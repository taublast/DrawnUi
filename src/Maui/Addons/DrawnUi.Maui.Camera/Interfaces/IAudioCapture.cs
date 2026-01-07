using System;
using System.Threading.Tasks;

namespace DrawnUi.Camera
{
    public interface IAudioCapture : IDisposable
    {
        bool IsCapturing { get; }
        int SampleRate { get; }
        int Channels { get; }

        event EventHandler<AudioSample> SampleAvailable;

        Task<bool> StartAsync(int sampleRate = 44100, int channels = 1, AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit);
        Task StopAsync();
    }
}
