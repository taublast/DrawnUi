using System;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Size = Windows.Foundation.Size;

/// <summary>
/// Forwards to SkiaSharp's WinUI native PropertySetExtensions via DllImport after ensuring the DLL is loaded.
/// Uses direct interop with proper marshaling.
/// </summary>
public static class PropertySetExtensions
{
    private const string SkiaSharpWinUINative = "SkiaSharp.Views.WinUI.Native";

    static PropertySetExtensions()
    {
        DrawnUi.Infrastructure.NativeLibraryLoader.TryLoadNativeLibrary("SkiaSharp.Views.WinUI.Native.dll");
    }

    [DllImport(SkiaSharpWinUINative, EntryPoint = "PropertySetExtensions_AddSingle")]
    private static extern void AddSingleInternal(IntPtr propertySet, [MarshalAs(UnmanagedType.LPWStr)] string key, float value);

    [DllImport(SkiaSharpWinUINative, EntryPoint = "PropertySetExtensions_AddSize")]
    private static extern void AddSizeInternal(IntPtr propertySet, [MarshalAs(UnmanagedType.LPWStr)] string key, Size value);

    /// <summary>
    /// Adds a single (float) value via SkiaSharp's native helper.
    /// </summary>
    public static void AddSingle(PropertySet propertySet, string key, float value)
    {
        IntPtr ptr = Marshal.GetIUnknownForObject(propertySet);
        try
        {
            AddSingleInternal(ptr, key, value);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    /// <summary>
    /// Adds a Size value via SkiaSharp's native helper.
    /// </summary>
    public static void AddSize(PropertySet propertySet, string key, Size size)
    {
        IntPtr ptr = Marshal.GetIUnknownForObject(propertySet);
        try
        {
            AddSizeInternal(ptr, key, size);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }
}
