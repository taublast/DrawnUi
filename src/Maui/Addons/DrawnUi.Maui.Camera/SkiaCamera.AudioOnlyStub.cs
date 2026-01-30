#if !WINDOWS && !ANDROID && !IOS && !MACCATALYST

namespace DrawnUi.Camera;

// Stub implementations for non-platform-specific builds
public partial class SkiaCamera
{
    private partial void CreateAudioOnlyEncoder(out IAudioOnlyEncoder encoder)
    {
        encoder = null;
    }

    private partial void StartAudioOnlyCapture(int sampleRate, int channels, out System.Threading.Tasks.Task task)
    {
        task = System.Threading.Tasks.Task.CompletedTask;
    }

    private partial void StopAudioOnlyCapture(out System.Threading.Tasks.Task task)
    {
        task = System.Threading.Tasks.Task.CompletedTask;
    }
}

#endif
