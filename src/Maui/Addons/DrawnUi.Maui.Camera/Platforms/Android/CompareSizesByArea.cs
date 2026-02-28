using Java.Lang;
using Java.Util;
using Object = Java.Lang.Object;

namespace DrawnUi.Camera
{
    public class CompareSizesByArea : Java.Lang.Object, IComparator
    {
        public CompareSizesByArea(IntPtr handle, Android.Runtime.JniHandleOwnership transfer)
            : base(handle, transfer) { }

        public CompareSizesByArea() { }

        public int Compare(Object lhs, Object rhs)
        {
            var lhsSize = (Android.Util.Size)lhs;
            var rhsSize = (Android.Util.Size)rhs;
            // We cast here to ensure the multiplications won't overflow
            return Long.Signum((long)lhsSize.Width * lhsSize.Height - (long)rhsSize.Width * rhsSize.Height);
        }


    }
}