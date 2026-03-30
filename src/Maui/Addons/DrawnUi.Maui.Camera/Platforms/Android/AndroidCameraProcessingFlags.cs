#if ANDROID
namespace DrawnUi.Camera
{
    internal static class AndroidCameraProcessingFlags
    {
        // Keep the two-encoder handoff available for experimentation, but default to the single-
        // encoder deferred-flush path so Android never needs to rebuild the camera session at the seam.
        internal static bool UseTwoEncoderPrerecordTransition => false;

        /// <summary>
        /// Doesn't apply to RS path
        /// </summary>
        internal static bool UsePreviewGlScalerOnGpuPathWhenRecording => true;

        // Test switch: true restores the older Android dual-stream mode where preview stays on
        // ImageReader while recording continues on the GPU encoder surface.
        internal static bool UseLegacyDualStreamPreviewDuringRecording => false;//Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Q;

        internal static bool IsLegacyDualStreamPreviewDuringRecordingEnabled()
        {
            return UseLegacyDualStreamPreviewDuringRecording;
        }

        internal static bool IsTwoEncoderPrerecordTransitionEnabled()
        {
            return UseTwoEncoderPrerecordTransition;
        }
    }
}
#endif
