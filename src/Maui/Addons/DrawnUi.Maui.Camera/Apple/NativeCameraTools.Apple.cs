#if IOS || MACCATALYST
using System.Runtime.InteropServices;
using DrawnUi.Draw;
using SkiaSharp;

namespace DrawnUi.Camera;

public static class SplinesHelper
{


}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SplineAsLUT //we need Half type from net5 but using net 4.. so using 0-255 base byte
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Red;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Green;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Blue;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public byte[] Alpha;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SplinePreset //float 4bytes both metal and c#
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Xs;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] Ys;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] As;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] Bs;


    public byte Tag;
};

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FilterOptions
{
    public float Gamma;
    public byte Tag;
};


[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct SplineTest //float 4bytes both metal and c#
{
    public byte Start;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public float[] Xs;

    public byte Tag;
};
#endif
