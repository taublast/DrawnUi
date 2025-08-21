using AppoMobi.Maui.Gestures;
using System.Collections.Concurrent;

namespace PreviewTests.Views.Controls;

public class MultiRippleWithTouchEffect : ShaderDoubleTexturesEffect,
    IStateEffect, ISkiaGestureProcessor
{
    public MultiRippleWithTouchEffect()
    {
        ShaderSource = "Shaders/ripples.sksl";
    }

    protected bool Initialized { get; set; }

    private PointF _mouse;

    #region IStateEffect

    /// <summary>
    /// Will be invoked before actually painting but after gestures processing and other internal calculations. By SkiaControl.OnBeforeDrawing method. Beware if you call Update() inside will never stop updating.
    /// </summary>
    public virtual void UpdateState()
    {
        if (Parent != null && !Initialized && Parent.IsLayoutReady)
        {
            Initialized = true;
        }

    }

    public override void Attach(SkiaControl parent)
    {
        base.Attach(parent);

        UpdateState();
    }

    #endregion

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);

        var activeRipples = GetActiveRipples();

        var mouseArray = new float[10 * 2];
        var progressArray = new float[10];

        for (int i = 0; i < 10; i++)
        {
            if (i < activeRipples.Count)
            {
                var ripple = activeRipples[i];
                mouseArray[i * 2] = ripple.Origin.X;
                mouseArray[i * 2 + 1] = ripple.Origin.Y;
                progressArray[i] = (float)ripple.Progress;
            }
            else
            {
                mouseArray[i * 2] = 0;
                mouseArray[i * 2 + 1] = 0;
                progressArray[i] = -1f; // inactive
            }
        }

        uniforms["origins"] = mouseArray;
        uniforms["progresses"] = progressArray;

        //was for just one ripple
        //uniforms["progress"] = (float)Progress;
        //uniforms["iMouse"] = new[] { _mouse.X, _mouse.Y, 0f, 0f };

        return uniforms;
    }

    #region RIPPLES

    public class Ripple
    {
        public Guid Uid { get; set; }
        public PointF Origin { get; set; }
        public long Time { get; set; }
        public double Progress { get; set; }
    }

    ConcurrentDictionary<Guid, Ripple> Ripples = new();

    public Ripple CreateRipple(PointF origin)
    {
        var ripple = new Ripple
        {
            Uid = Guid.NewGuid(),
            Origin = origin,
            Time = Super.GetCurrentTimeNanos()
        };
        Ripples[ripple.Uid] = ripple;
        return ripple;
    }

    public void RemoveRipple(Guid key)
    {
        Ripples.TryRemove(key, out _);
    }


    public List<Ripple> GetActiveRipples()
    {
        return Ripples.Values.OrderByDescending(x => x.Time).Take(10).ToList();
    }

    public virtual ISkiaGestureListener ProcessGestures(
        SkiaGesturesParameters args,
        GestureEventProcessingInfo apply)
    {
        _mouse = args.Event.Location;

        if (args.Type == TouchActionResult.Down && Initialized)
        {

            var ripple = CreateRipple(_mouse);

            //run new animator for every Down
            //we use this helper task so that every new rangeanimator is disposed properly at the end
            Task.Run(async () =>
            {
                await Parent.AnimateRangeAsync((v) =>
                {
                    ripple.Progress = v;
                    Update();
                }, 0, 1, 4500);

                RemoveRipple(ripple.Uid);

            }).ConfigureAwait(false);

        }

        return null;
    }

    #endregion
}
