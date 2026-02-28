namespace DrawnUi.Draw;

/// <summary>
/// Animated shader effect driven by a RangeAnimator (progress 0→1).
/// Attach to a SkiaImage (or any cached control), call Play(), remove on Completed.
///
/// Usage:
///   var fx = new AnimatedShaderEffect { ShaderSource = @"Shaders\yourshader.sksl" };
///   fx.Completed += (s, e) => { _backgroundImage.FX.Remove(fx); fx.Dispose(); };
///   _backgroundImage.FX.Add(fx);
///   fx.Play(_backgroundImage);
/// </summary>
public class AnimatedShaderEffect : SkiaShaderEffect
{
   /// <summary>
    /// Normalized center position of the effect (0.0–1.0 in each axis).
    /// </summary>
    public SKPoint Center { get; set; } = new SKPoint(0.5f, 0.5f);

    protected virtual void OnCompleted()
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler Completed;

    /// <summary>
    /// Total animation duration in milliseconds. Default 2500.
    /// </summary>
    public uint DurationMs { get; set; } = 2500;

    /// <summary>
    /// Current animation progress 0.0 → 1.0.
    /// Set automatically by Play(); can also be set manually for debugging.
    /// </summary>
    public double Progress { get; set; }


    protected RangeAnimator Animator;

    /// <summary>
    /// Starts the celebration animation on the given parent control.
    /// Safe to call multiple times — restarts from zero each time.
    /// </summary>
    public virtual void Play()
    {
        Animator?.Stop();

        if (Parent == null)
        {
            return;
        }

        Animator ??= new RangeAnimator(Parent)
        {
            OnStop = () =>
            {
                if (Animator.WasStarted)
                {
                    OnCompleted();
                }
            }
        };

        Progress = 0.0;
        AquiredBackground = false; // Reset so Once mode re-captures background on next render

        Animator.Start(
            value =>
            {
                //Debug.WriteLine($"Animator {value}");
                Progress = value;
                Update();
            },
            start:   0.0,
            end:     1.0,
            ms:      DurationMs,
            easing:  Easing.Linear);

        Update();
    }

    /// <summary>
    /// Stops the animation without firing Completed.
    /// </summary>
    public virtual void Stop()
    {
        Animator?.Stop();
    }

    protected override SKRuntimeEffectUniforms CreateUniforms(SKRect destination)
    {
        var uniforms = base.CreateUniforms(destination);
        uniforms["progress"] = (float)Progress;
        uniforms["iCenter"] = new float[] { Center.X, Center.Y };

        return uniforms;
    }

    protected override void OnDisposing()
    {
        Animator?.Stop();
        Animator = null;
        base.OnDisposing();
    }
}
