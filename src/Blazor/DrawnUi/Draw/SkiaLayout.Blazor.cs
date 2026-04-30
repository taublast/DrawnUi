using System.Collections;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout : SkiaControl, ISkiaLayout
    {
        #region HOTRELOAD

        public void Clear()
        {
            ClearChildren();
        }

        public void ReportHotreloadChildRemoved(SkiaControl control)
        {
            
        }

        public void ReportHotreloadChildAdded(SkiaControl child)
        {
 
        }

        #endregion



    }
}
