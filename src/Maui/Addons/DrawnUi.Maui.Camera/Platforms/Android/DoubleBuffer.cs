using Android.Graphics;
using Android.Renderscripts;

namespace DrawnUi.Camera
{
    public class DoubleBuffer
    {
        private Bitmap[] bitmaps;
        private Allocation[] allocations;
        private int writeIndex;
        private int readIndex;

        public Bitmap WriteBitmap => bitmaps[writeIndex];
        public Bitmap ReadBitmap => bitmaps[readIndex];
        public Allocation WriteAllocation => allocations[writeIndex];
        public Allocation ReadAllocation => allocations[readIndex];

        public DoubleBuffer(RenderScript rs, int width, int height)
        {
            bitmaps = new Bitmap[2];
            allocations = new Allocation[2];

            bitmaps[0] = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);
            bitmaps[1] = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888);

            allocations[0] = Allocation.CreateFromBitmap(rs, bitmaps[0]);
            allocations[1] = Allocation.CreateFromBitmap(rs, bitmaps[1]);

            writeIndex = 0;
            readIndex = 1;
        }

        public void Swap()
        {
            (writeIndex, readIndex) = (readIndex, writeIndex);
        }

        public void Dispose()
        {
            allocations[0].Destroy();
            allocations[0].Dispose();
            bitmaps[0].Dispose();

            allocations[1].Destroy();
            allocations[1].Dispose();
            bitmaps[1].Dispose();
        }
    }
}