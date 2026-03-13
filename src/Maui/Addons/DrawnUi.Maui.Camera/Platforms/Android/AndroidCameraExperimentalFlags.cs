#if ANDROID
namespace DrawnUi.Camera
{
    internal static class AndroidCameraExperimentalFlags
    {
        // Test switch: true restores the older Android dual-stream mode where preview stays on
        // ImageReader while recording continues on the GPU encoder surface.
        internal const bool UseLegacyDualStreamPreviewDuringRecording = false;

        internal static bool IsLegacyDualStreamPreviewDuringRecordingEnabled()
        {
            return UseLegacyDualStreamPreviewDuringRecording;
        }
    }
}
#endif
