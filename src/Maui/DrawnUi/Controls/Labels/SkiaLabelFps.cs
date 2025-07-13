using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public class SkiaLabelFps : SkiaLabel, ISkiaAnimator
{
    public SkiaLabelFps()
    {
        IsParentIndependent = true;
        WillNotUpdateParent = true;

        Tag = "FPS";
        Format = "FPS {0}";
        MonoForDigits = "8";
        MaxLines = 1;
        BackgroundColor = Colors.Black;
        TextColor = Colors.LimeGreen;
        InputTransparent = true;
        ZIndex = int.MaxValue;
        UpdateForceRefresh();
    }

    public bool IsDeactivated { get; set; }

    public bool IsPaused { get; set; }
    public bool IsHiddenInViewTree { get; set; }

    public virtual void Pause()
    {
        UnregisterAnimator(this.Uid);
    }

    public virtual void Resume()
    {
        RegisterAnimator(this);
    }

    public override void OnParentChanged(IDrawnBase newvalue, IDrawnBase oldvalue)
    {
        base.OnParentChanged(newvalue, oldvalue);

        UpdateForceRefresh();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        UpdateForceRefresh();
    }


    void UpdateForceRefresh()
    {
        if (ForceRefresh)
        {
            Start();
        }
        else
        {
            if (IsRunning)
                Stop();
        }
    }

    protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == ForceRefreshProperty.PropertyName)
        {
            UpdateForceRefresh();
        }
    }

    public static readonly BindableProperty ForceRefreshProperty = BindableProperty.Create(nameof(ForceRefresh),
    typeof(bool),
    typeof(SkiaLabelFps),
    false);
    public bool ForceRefresh
    {
        get { return (bool)GetValue(ForceRefreshProperty); }
        set { SetValue(ForceRefreshProperty, value); }
    }

    protected override void Draw(DrawingContext context)
    {
        base.Draw(context);

        if (Superview == null)
            return;

        Text = $"{Superview.FPS:00.0}";

        UpdateForceRefresh();
 
        FinalizeDrawingWithRenderObject(context);
    }


    public bool TickFrame(long frameTimeNanos)
    {
        return false;
    }

    public bool IsRunning { get; set; }
    public bool WasStarted { get; }


    public void Stop()
    {
        IsRunning = false;
        UnregisterAnimator(this.Uid);
    }

    public void Start(double delayMs = 0)
    {
        if (!IsRunning)
        {
            IsRunning = RegisterAnimator(this);
        }
    }
}
