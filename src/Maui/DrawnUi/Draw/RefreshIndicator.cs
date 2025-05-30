﻿using DrawnUi.Controls;

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


    public override void SetDragRatio(float ratio, float ptsScrollOffset, double ptsLimit)
    {
        base.SetDragRatio(ratio, ptsScrollOffset, ptsLimit);

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

        Loader?.Stop();
    }

    public override void OnVisibilityChanged(bool newvalue)
    {
        base.OnVisibilityChanged(newvalue);

        Loader?.Stop();
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
                refresh.UpdateOrientation();
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

    protected virtual void UpdateOrientation()
    {
        if (Orientation == ScrollOrientation.Vertical)
        {
            HorizontalOptions = LayoutOptions.Fill;
            VerticalOptions = LayoutOptions.Start;
        }
        else
        if (Orientation == ScrollOrientation.Horizontal)
        {
            HorizontalOptions = LayoutOptions.Start;
            VerticalOptions = LayoutOptions.Fill;
        }

        Invalidate();
    }

    /// <summary>
    /// 0 - 1... not clamped can be over 1
    /// </summary>
    /// <param name="ratio"></param>
    public virtual void SetDragRatio(float ratio, float ptsScrollOffset, double ptsLimit)
    {

        ratio = Math.Clamp(ratio, 0, 1);

        double VisibleRatio = Math.Min(1.0, ratio / 0.98);

        double opacity = Math.Clamp(ratio, 0f, 1f);

        IsRunning = opacity >= 1;

        if (IsRunning)
        {
            opacity = 1;
        }

        Opacity = opacity;
        var visibility = Opacity != 0;

        IsVisible = visibility;

        if (Orientation == ScrollOrientation.Vertical)
        {
            if (Height > 0)
            {
                var pos = (float)( -ptsScrollOffset  -ptsLimit * (1- ratio) );

                TranslationY = pos;
            }
        }
        else if (Orientation == ScrollOrientation.Horizontal)
        {
            if (Width > 0)
            {
                TranslationX = (float)(-Width + Width * VisibleRatio);
            }
        }
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
        get
        {
            return _IsRunning;
        }
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
