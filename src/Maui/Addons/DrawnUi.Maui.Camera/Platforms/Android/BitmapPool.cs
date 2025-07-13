using Android.Renderscripts;

namespace DrawnUi.Camera
{
    public class BitmapPool
    {
        private Stack<AllocatedBitmap> pool = new();

        public AllocatedBitmap GetBitmap(RenderScript rs, int width, int height)
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }
            else
            {
                return new AllocatedBitmap(rs, width, height);
            }
        }

        public void ReturnBitmap(AllocatedBitmap bitmap)
        {
            pool.Push(bitmap);
        }
    }
}