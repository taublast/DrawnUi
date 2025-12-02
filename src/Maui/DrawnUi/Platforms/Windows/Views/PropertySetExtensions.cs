using System;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Size = Windows.Foundation.Size;


internal static class PropertySetExtensions
{
    [System.Runtime.InteropServices.DllImport("SkiaSharp.DrawnUi.WinUI.Native.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern void AddSize(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] object propertySet,
        string key,
        double width,
        double height);

    [System.Runtime.InteropServices.DllImport("SkiaSharp.DrawnUi.WinUI.Native.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    public static extern void AddSingle(
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.IUnknown)] object propertySet,
        string key,
        float value);
}

 
