using Android.Renderscripts;
using AppoMobi.Maui.Native.Droid.Graphics;

namespace DrawnUi.Camera
{
    public class FilterSplines
    {
        public string Id { get; set; }
        public ChannelSpline Red { get; set; }
        public ChannelSpline Green { get; set; }
        public ChannelSpline Blue { get; set; }

        public ScriptIntrinsicLUT RendererLUT { get; set; }
    }
}