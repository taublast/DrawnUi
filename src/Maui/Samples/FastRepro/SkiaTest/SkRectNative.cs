using System.Runtime.InteropServices;

namespace Sandbox
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SkRectNative
    {
        public float Left;
        public float Top;
        public float Right;
        public float Bottom;

        public static SkRectNative FromSize(float width, float height)
            => new()
            {
                Left = 0,
                Top = 0,
                Right = width,
                Bottom = height
            };
    }
}
