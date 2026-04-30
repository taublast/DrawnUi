using System.Runtime.InteropServices;

namespace DrawnUi.Draw
{
    public partial class SkiaLayout
    {

        /// <summary>
        /// TODO for templated measure only visible?! and just reserve predicted scroll amount for scrolling
        /// </summary>
        /// <param name="rectForChildrenPixels"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public virtual ScaledSize MeasureWrap(SKRect rectForChildrenPixels, float scale)
        {
            var layout = new BuildWrapLayout(this);

            var measuredLayout = layout.Build(rectForChildrenPixels, scale);
            return measuredLayout;
        }

        protected override void PropagateVisibilityChanged(bool newvalue)
        {
            if (IsTemplated && RenderTree!=null)
            {
                try
                {
                    foreach (var cell in RenderTree.ToList())
                    {
                        cell.Control?.OnParentVisibilityChanged(newvalue);
                    }
                }
                catch (Exception e)
                {
                    Super.Log(e);
                }

                return;
            }

            base.PropagateVisibilityChanged(newvalue);
        }


    }
}


