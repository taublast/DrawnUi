using Android.Renderscripts;
using AppoMobi.Maui.Native.Droid.Graphics;

namespace DrawnUi.Camera
{
    public class SplinesHelper
    {
        public void Dispose()
        {
            foreach (var p in Presets)
            {
                p.Value.RendererLUT?.Destroy();
            }
            Renderer?.Destroy();
        }

        public RenderScripts Renderer { get; set; }

        public SplinesHelper()
        {

        }

        public Dictionary<string, FilterSplines> Presets { get; protected set; } = new(64);

        public void AddPreset(FilterSplines preset)
        {
            var existing = Presets.Any(x => x.Key == preset.Id);
            if (!existing)
                Presets.Add(preset.Id, preset);
        }

        public void SetPreset(string id)
        {
            var existing = Presets.FirstOrDefault(x => x.Key == id);
            if (existing.Value != null)
            {
                Current = existing.Value;
            }
            else
            {
                Current = null;
            }
        }


        private FilterSplines _Current;
        public FilterSplines Current
        {
            get { return _Current; }
            set
            {
                if (_Current != value)
                {
                    _Current = value;
                }
            }
        }


        public void Initialize(RenderScript rs)
        {
            Renderer = new RenderScripts(rs);

            //normal
            var red = new CubicSpline((0, 0), (127, 127), (255, 255));
            var green = new CubicSpline((0, 0), (127, 127), (255, 255));
            var blue = new CubicSpline((0, 0), (127, 127), (255, 255));
            var preset = CreateFilterSplines(rs, "normal", red, green, blue);
            //Rendering.AddUpdateSplinesPreset(rs, preset.Id, preset.Red, preset.Green, preset.Blue);
            AddPreset(preset);

            //color negative AUTO
            red = new CubicSpline((33, 255), (119, 127), (185, 0));
            green = new CubicSpline((28, 255), (77, 127), (135, 0));
            blue = new CubicSpline((25, 255), (60, 127), (108, 0));
            preset = CreateFilterSplines(rs, "auto", red, green, blue);
            AddPreset(preset);


            //color negative FLAT
            red = new CubicSpline((0, 255), (127, 127), (255, 0));
            green = new CubicSpline((0, 255), (127, 127), (255, 0));
            blue = new CubicSpline((0, 255), (127, 127), (255, 0));
            preset = CreateFilterSplines(rs, "flat", red, green, blue);
            //Rendering.AddUpdateSplinesPreset(rs, preset.Id, preset.Red, preset.Green, preset.Blue);
            AddPreset(preset);


            //#if DEBUG
            //            //test
            //            System.Trace.WriteLine($"NEGA FLAT test");
            //            System.Trace.WriteLine($"Blue XS: {blue.Xs[0]} {blue.Xs[1]} {blue.Xs[2]}");
            //            blue.Eval(127 / 255f);
            //            blue.Eval(12 / 255f);
            //#endif


            //Rendering.AddUpdateSplinesPreset(rs, preset.Id, preset.Red, preset.Green, preset.Blue);
            ;
            //var check = Rendering.AvailableSplinePresets;
            SetPreset("auto");
        }


        //public FilterSplines PresetPositive { get; set; }

        //public FilterSplines PresetNegativeAuto { get; set; }

        //public FilterSplines PresetNegativeFlat { get; set; }

        public FilterSplines CreateFilterSplines(RenderScript rs, string id, CubicSpline red, CubicSpline green, CubicSpline blue, bool forceBW = false)
        {
            var ret = new FilterSplines
            {
                Id = id
            };

            //test all is working
            //_splineRed.Eval(213 /255f);
            //_splineGreen.Eval(154 /255f);
            // _splineBlue.Eval(95 /255f);

            //RED
            ret.Red = new ChannelSpline()
            {
                Xs = red.Xs,
                Ys = red.Ys,
                As = red.As,
                Bs = red.Bs,
                Tag = 'R'
            };


            //Green
            ret.Green = new ChannelSpline
            {
                Xs = green.Xs,
                Ys = green.Ys,
                As = green.As,
                Bs = green.Bs,
                Tag = 'G'
            };


            //Blue
            ret.Blue = new ChannelSpline
            {
                Xs = blue.Xs,
                Ys = blue.Ys,
                As = blue.As,
                Bs = blue.Bs,
                Tag = 'B'
            };

            ret.RendererLUT = ScriptIntrinsicLUT.Create(rs, Android.Renderscripts.Element.U8_4(rs));

            for (int x = 0; x < 256; x++)
            {
                float fX = x / 255f;

                ret.RendererLUT.SetAlpha(x, 255);

                var y = (int)Math.Round(red.Eval(fX) * 255.0);
                if (y > 255)
                    y = 255;
                if (y < 0)
                    y = 0;
                ret.RendererLUT.SetRed(x, y);

                y = (int)Math.Round(green.Eval(fX) * 255.0);
                if (y > 255)
                    y = 255;
                if (y < 0)
                    y = 0;
                ret.RendererLUT.SetGreen(x, y);

                y = (int)Math.Round(blue.Eval(fX) * 255.0);
                if (y > 255)
                    y = 255;
                if (y < 0)
                    y = 0;
                ret.RendererLUT.SetBlue(x, y);

            }

            return ret;
        }

    }
}