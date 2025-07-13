using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrawnUi.Views
{
    public class SKAutoCanvasRestoreFixed : IDisposable
    {
        private SKCanvas canvas;
        private readonly int saveCount;

        public SKAutoCanvasRestoreFixed(SKCanvas canvas)
            : this(canvas, true)
        {
        }

        public SKAutoCanvasRestoreFixed(SKCanvas canvas, bool doSave)
        {
            this.canvas = canvas;
            this.saveCount = 0;

            if (canvas != null)
            {
                saveCount = canvas.SaveCount;
                if (doSave)
                {
                    canvas.Save();
                }
            }
        }

        public void Dispose()
        {
            if (canvas != null && canvas.Handle != IntPtr.Zero)
            {
                Restore();
            }
        }

        /// <summary>
        /// Perform the restore now, instead of waiting for the Dispose.
        /// Will only do this once.
        /// </summary>
        public void Restore()
        {
            if (canvas != null)
            {
                canvas.RestoreToCount(saveCount);
                canvas = null;
            }
        }
    }
}
