using System;
using System.Runtime.InteropServices;

namespace DrawnUi.Camera.Platforms.Windows;

/// <summary>
/// P/Invoke wrapper for AudioEncoderNative.dll - Native Windows audio encoder.
/// Provides access to CLSID_CResamplerMediaObject which is inaccessible from .NET MAUI.
/// </summary>
internal static class AudioEncoderNative
{
    private const string DllName = "AudioEncoderNative.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr audio_encoder_create(
        IntPtr sinkWriter,
        uint streamIndex,
        int sampleRate,
        int channels,
        int inputIsFloat);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int audio_encoder_write_pcm(
        IntPtr encoder,
        [In] byte[] pcmData,
        uint dataSize,
        long timestampHns);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern int audio_encoder_finalize(IntPtr encoder);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void audio_encoder_destroy(IntPtr encoder);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr audio_encoder_get_error(IntPtr encoder);

    /// <summary>
    /// Helper to get error message as managed string.
    /// </summary>
    internal static string GetError(IntPtr encoder)
    {
        if (encoder == IntPtr.Zero)
            return "Invalid encoder handle";

        IntPtr errorPtr = audio_encoder_get_error(encoder);
        return Marshal.PtrToStringAnsi(errorPtr) ?? "Unknown error";
    }
}
