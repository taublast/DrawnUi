namespace PreviewTests.Views.Controls;

public class CacheConsumer : SkiaShape
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(SkiaControl),
        typeof(CacheConsumer),
        null,
        propertyChanged: WhenSourceChanged,
        defaultBindingMode: BindingMode.OneTime);

    private SkiaImage _imageHolder;

    public SkiaControl Source
    {
        get { return (SkiaControl)GetValue(SourceProperty); }
        set { SetValue(SourceProperty, value); }
    }

    private static void WhenSourceChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is CacheConsumer control)
        {
            control.AttachSource();
        }
    }


    private void OnSourceCacheChanged(object sender, CachedObject cache)
    {
        //_imageHolder.VerticalOffset = -500;
        _imageHolder.SetImageInternal(cache.Image);
    }

    public override void OnDisposing()
    {
        base.OnDisposing();

        if (Source != null)
        {
            Source.CreatedCache -= OnSourceCacheChanged;
            Source = null;
        }

        _imageHolder = null; //will be disposed by engine as being inside Views
    }

    protected void AttachSource()
    {
        if (Source != null)
        {
            Source.CreatedCache += OnSourceCacheChanged;

            _imageHolder = new SkiaImage()
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Aspect = TransformAspect.AspectCover,
            };

            this.Views.Clear();
            Views.Add(_imageHolder);
        }
    }


}
