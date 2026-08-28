using DrawnUi.Controls;


namespace DrawnUi.Draw;

public class LottieRefreshIndicator : RefreshIndicator
{
    protected SkiaLottie Loader;

    //protected override void CreateDefaultContent()
    //{
    //    if (this.Views.Count == 0)
    //    {
    //        SetDefaultMinimumContentSize(40, 40);

    //        Loader = new()
    //        {
    //            AutoPlay = false,
    //            Repeat = -1,
    //            ColorTint = AppColors.BrandPrimary,
    //            HorizontalOptions = LayoutOptions.Fill,
    //            VerticalOptions = LayoutOptions.Fill,
    //            LockRatio = 1,
    //            Source = "Lottie/iosloader.json"
    //        };

    //        AddSubView(Loader);

    //        if (IsRunning)
    //            Loader.Start();
    //    }
    //}

    public override void SetAnimationState(bool running)
    {
        //base.SetAnimationState(running);
    }


    public override void SetDragRatio(float ratio, float ptsScrollOffset, double ptsLimit, double ptsTrigger)
    {
        base.SetDragRatio(ratio, ptsScrollOffset, ptsLimit, ptsTrigger);

        if (FindLoader() && !IsRunning)
        {
            var frame = Loader.GetFrameAt(ratio);
            //Debug.WriteLine($"[Loader] set frame {frame}");
            Loader.Seek(frame);
        }
    }

    protected override void OnIsRunningChanged(bool value)
    {
        base.OnIsRunningChanged(value);

        if (FindLoader())
        {
            if (!value)
            {
                Debug.WriteLine($"[Loader] STOP");
                Loader.Stop();
            }
            else
            {
                Debug.WriteLine($"[Loader] PLAY");
                Loader.Start();
            }
        }
    }

    public override void OnParentVisibilityChanged(bool newvalue)
    {
        base.OnParentVisibilityChanged(newvalue);

        if (!newvalue)
            Loader?.Stop();
        else if (IsRunning && FindLoader())
            Loader.Start();
    }

    public override void OnVisibilityChanged(bool newvalue)
    {
        base.OnVisibilityChanged(newvalue);

        if (!newvalue)
            Loader?.Stop();
        else if (IsRunning && FindLoader())
            Loader.Start();
    }

    bool FindLoader()
    {
        if (Loader == null)
        {
            Loader = this.FindView<SkiaLottie>("Loader");
        }

        return Loader != null;
    }
}

public class RefreshIndicator : SkiaLayout, IRefreshIndicator
{
    public RefreshIndicator()
    {
        InputTransparent = true;
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Start;
    }

    public static readonly BindableProperty OrientationProperty = BindableProperty.Create(nameof(Orientation),
        typeof(ScrollOrientation), typeof(RefreshIndicator),
        ScrollOrientation.Vertical,
        propertyChanged: (bindable, old, changed) =>
        {
            if (bindable is RefreshIndicator refresh && changed is ScrollOrientation orientation)
            {
                if (orientation == ScrollOrientation.Both)
                {
                    throw new NotImplementedException();
                }

                refresh.UpdateOrientation(old is ScrollOrientation previous ? previous : ScrollOrientation.Vertical);
            }
        });

    /// <summary>
    /// <summary>Gets or sets the scrolling direction of the ScrollView. This is a bindable property.</summary>
    /// </summary>
    public ScrollOrientation Orientation
    {
        get { return (ScrollOrientation)GetValue(OrientationProperty); }
        set { SetValue(OrientationProperty, value); }
    }

    protected virtual void UpdateOrientation(ScrollOrientation previous)
    {
        // only swap alignments still at the previous orientation's defaults, keep user-set ones
        var (oldH, oldV) = previous == ScrollOrientation.Horizontal
            ? (LayoutOptions.Start, LayoutOptions.Fill)
            : (LayoutOptions.Fill, LayoutOptions.Start);
        var (newH, newV) = Orientation == ScrollOrientation.Horizontal
            ? (LayoutOptions.Start, LayoutOptions.Fill)
            : (LayoutOptions.Fill, LayoutOptions.Start);

        if (HorizontalOptions.Alignment == oldH.Alignment)
            HorizontalOptions = newH;
        if (VerticalOptions.Alignment == oldV.Alignment)
            VerticalOptions = newV;

        Invalidate();
    }

    /// <summary>
    /// 0 - 1... not clamped can be over 1
    /// </summary>
    /// <param name="ratio"></param>
    public virtual void SetDragRatio(float ratio, float ptsScrollOffset, double ptsLimit, double ptsTrigger)
    {
        //ratio = (float)Math.Pow(Math.Clamp(ratio, 0, 1), 4.0);

        ratio = (float)Math.Clamp(ratio, 0, 1);

        double opacity = ratio;

        if (Orientation == ScrollOrientation.Vertical)
        {
            if (ratio <= 0)
            {
                // getPosition divides by the ratio: at 0 (HideRefreshIndicator after a refresh) it produced
                // NaN/-Infinity for TranslationY and Opacity, which poisoned the transform so the indicator
                // never showed again on the next pull. Park it just above the viewport instead.
                TranslationY = -(float)Height;
                opacity = 0;
            }
            else if (Height > 0)
            {
                var diff = Height - ptsLimit;

                float getPosition(float k)
                {
                    return (float)(-Height / k - ptsScrollOffset / k +
                                  (ptsLimit + diff + (ptsLimit - Height) / 2) * k);
                }

                var pos = getPosition(ratio);
                var max = getPosition(1.0f);

                TranslationY = pos;
                opacity = Math.Clamp((max / pos) * ratio, 0, 1);
            }
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            if (Width > 0)
            {
                var diff = Width - ptsLimit;

                float getPosition(float k)
                {
                    return (float)(-Width / k - ptsScrollOffset / k +
                                   (ptsLimit + diff + (ptsLimit - Width) / 2) * k);
                }

                var pos = getPosition(ratio);
                var max = getPosition(1.0f);

                TranslationY = pos;
                opacity = Math.Clamp((max / pos) * ratio, 0, 1);
            }
        }

        IsRunning = opacity >= 1;
        if (IsRunning)
        {
            opacity = 1;
        }

        Opacity = opacity;
        var visibility = Opacity != 0;

        SetAnimationState(visibility);
    }

    public virtual void SetAnimationState(bool running)
    {
        IsVisible = running;
    }

    public float VisibleRatio { get; set; }

    protected virtual void OnIsRunningChanged(bool value)
    {
    }

    private bool _IsRunning;

    /// <summary>
    /// ReadOnly
    /// </summary>
    public bool IsRunning
    {
        get { return _IsRunning; }
        set
        {
            if (_IsRunning != value)
            {
                _IsRunning = value;
                OnPropertyChanged();
                OnIsRunningChanged(value);
            }
        }
    }
}
