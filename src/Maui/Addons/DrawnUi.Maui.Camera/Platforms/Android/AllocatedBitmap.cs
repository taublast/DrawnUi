using Android.Graphics;
using Android.Renderscripts;

namespace DrawnUi.Camera
{
    public class AllocatedBitmap : IDisposable
    {
        public Bitmap Bitmap { get; set; }
        public Allocation Allocation { get; set; }

        public AllocatedBitmap(RenderScript rs, int width, int height)
        {
            Bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
            Allocation = Allocation.CreateFromBitmap(rs,
                Bitmap,
                Allocation.MipmapControl.MipmapNone,
                AllocationUsage.Script);
            //Allocation.CreateFromBitmap(rs, Bitmap);
        }

        public void Update()
        {
            Allocation.CopyTo(Bitmap);
        }

        public void Dispose()
        {
            if (Allocation != null)
            {
                Allocation.Destroy();
                Allocation.Dispose();
                Allocation = null;
            }

            if (Bitmap != null)
            {
                //Bitmap.Recycle();
                Bitmap.Dispose();
                Bitmap = null;
            }
        }
    }
}