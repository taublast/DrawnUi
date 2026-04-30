using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
#if !BROWSER
using DrawnUi.Features.Images;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
#endif
using Svg.Skia;

namespace DrawnUi.Draw
{
    [ContentProperty("SvgString")]
    public class SkiaSvg : SkiaControl
    {
        public static readonly BindableProperty TintColorProperty = BindableProperty.Create(nameof(TintColor),
            typeof(Color), typeof(SkiaSvg),
            Colors.Transparent,
            propertyChanged: NeedDraw);

        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        #region SHADOW

        private const string nameShadowColor = "ShadowColor";

        public static readonly BindableProperty ShadowColorProperty = BindableProperty.Create(nameShadowColor,
            typeof(Color), typeof(SkiaSvg),
            Colors.Transparent,
            propertyChanged: NeedDraw);

        public Color ShadowColor
        {
            get { return (Color)GetValue(ShadowColorProperty); }
            set { SetValue(ShadowColorProperty, value); }
        }

        private const string nameShadowX = "ShadowX";

        public static readonly BindableProperty ShadowXProperty = BindableProperty.Create(nameShadowX, typeof(double),
            typeof(SkiaSvg),
            2.0,
            propertyChanged: NeedDraw);

        public double ShadowX
        {
            get { return (double)GetValue(ShadowXProperty); }
            set { SetValue(ShadowXProperty, value); }
        }

        private const string nameShadowY = "ShadowY";

        public static readonly BindableProperty ShadowYProperty = BindableProperty.Create(nameShadowY, typeof(double),
            typeof(SkiaSvg),
            2.0,
            propertyChanged: NeedDraw);

        public double ShadowY
        {
            get { return (double)GetValue(ShadowYProperty); }
            set { SetValue(ShadowYProperty, value); }
        }

        private const string nameShadowBlur = "ShadowBlur";

        public static readonly BindableProperty ShadowBlurProperty = BindableProperty.Create(nameShadowBlur,
            typeof(double), typeof(SkiaSvg),
            5.0,
            propertyChanged: NeedDraw);

        public double ShadowBlur
        {
            get { return (double)GetValue(ShadowBlurProperty); }
            set { SetValue(ShadowBlurProperty, value); }
        }

        #endregion

        void AddShadow(SKPaint paint, double scale)
        {
            if (ShadowColor != Colors.Transparent)
            {
                paint.ImageFilter = SKImageFilter.CreateDropShadow(
                    (float)Math.Round(ShadowX * scale), (float)Math.Round(ShadowY * scale), (float)(ShadowBlur),
                    (float)(ShadowBlur),
                    ShadowColor.ToSKColor());
            }
        }

        public SkiaSvg()
        {
            UseCache = SkiaCacheType.Operations;

            _assembly = Assembly.GetCallingAssembly();
            _part1 = _assembly?.GetName().Name + ".Resources.Images.";
        }

        protected static void NeedUpdateIcon(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaSvg;
            if (control != null && !control.IsDisposed)
            {
                control.UpdateIcon();
            }
        }

        #region PROPERTIES

        public static readonly BindableProperty AspectProperty = BindableProperty.Create(
            nameof(Aspect),
            typeof(TransformAspect),
            typeof(SkiaSvg),
            TransformAspect.AspectFitFill,
            propertyChanged: NeedDraw);

        public TransformAspect Aspect
        {
            get { return (TransformAspect)GetValue(AspectProperty); }
            set { SetValue(AspectProperty, value); }
        }

        public static readonly BindableProperty FontAwesomePrimaryColorProperty = BindableProperty.Create(
            nameof(FontAwesomePrimaryColor),
            typeof(Color),
            typeof(SkiaSvg),
            Colors.Black);

        public Color FontAwesomePrimaryColor
        {
            get { return (Color)GetValue(FontAwesomePrimaryColorProperty); }
            set { SetValue(FontAwesomePrimaryColorProperty, value); }
        }

        public static readonly BindableProperty FontAwesomeSecondaryColorProperty = BindableProperty.Create(
            nameof(FontAwesomeSecondaryColor),
            typeof(Color),
            typeof(SkiaSvg),
            Colors.Gray);

        public Color FontAwesomeSecondaryColor
        {
            get { return (Color)GetValue(FontAwesomeSecondaryColorProperty); }
            set { SetValue(FontAwesomeSecondaryColorProperty, value); }
        }

        public static readonly BindableProperty GradientBlendModeProperty = BindableProperty.Create(
            nameof(GradientBlendMode),
            typeof(SKBlendMode),
            typeof(SkiaSvg),
            SKBlendMode.SrcIn,
            propertyChanged: NeedDraw);

        public SKBlendMode GradientBlendMode
        {
            get { return (SKBlendMode)GetValue(GradientBlendModeProperty); }
            set { SetValue(GradientBlendModeProperty, value); }
        }

        public static readonly BindableProperty SvgHorizontalOptionsProperty = BindableProperty.Create(
            nameof(SvgHorizontalOptions),
            typeof(LayoutAlignment),
            typeof(SkiaSvg),
            LayoutAlignment.Center);

        public LayoutAlignment SvgHorizontalOptions
        {
            get { return (LayoutAlignment)GetValue(SvgHorizontalOptionsProperty); }
            set { SetValue(SvgHorizontalOptionsProperty, value); }
        }

        public static readonly BindableProperty SvgVerticalOptionsProperty = BindableProperty.Create(
            nameof(SvgVerticalOptions),
            typeof(LayoutAlignment),
            typeof(SkiaSvg),
            LayoutAlignment.Center);

        public LayoutAlignment SvgVerticalOptions
        {
            get { return (LayoutAlignment)GetValue(SvgVerticalOptionsProperty); }
            set { SetValue(SvgVerticalOptionsProperty, value); }
        }

        public static readonly BindableProperty SvgStringProperty = BindableProperty.Create(
            nameof(SvgString),
            typeof(string),
            typeof(SkiaSvg),
            string.Empty,
            BindingMode.OneWay,
            propertyChanged: NeedUpdateIcon);

        public string SvgString
        {
            get { return (string)GetValue(SvgStringProperty); }
            set { SetValue(SvgStringProperty, value); }
        }

        public static readonly BindableProperty ZoomXProperty = BindableProperty.Create(
            nameof(ZoomX),
            typeof(double),
            typeof(SkiaSvg),
            1.0,
            propertyChanged: NeedDraw);

        public double ZoomX
        {
            get { return (double)GetValue(ZoomXProperty); }
            set { SetValue(ZoomXProperty, value); }
        }

        public static readonly BindableProperty ZoomYProperty = BindableProperty.Create(
            nameof(ZoomY),
            typeof(double),
            typeof(SkiaSvg),
            1.0,
            propertyChanged: NeedDraw);

        public double ZoomY
        {
            get { return (double)GetValue(ZoomYProperty); }
            set { SetValue(ZoomYProperty, value); }
        }

        public static readonly BindableProperty InflateAmountProperty = BindableProperty.Create(
            nameof(InflateAmount),
            typeof(double),
            typeof(SkiaSvg),
            0.0,
            propertyChanged: NeedDraw);

        public double InflateAmount
        {
            get { return (double)GetValue(InflateAmountProperty); }
            set { SetValue(InflateAmountProperty, value); }
        }

        public static readonly BindableProperty VerticalAlignmentProperty = BindableProperty.Create(
            nameof(VerticalAlignment),
            typeof(DrawImageAlignment),
            typeof(SkiaSvg),
            DrawImageAlignment.Center,
            propertyChanged: NeedDraw);

        public DrawImageAlignment VerticalAlignment
        {
            get { return (DrawImageAlignment)GetValue(VerticalAlignmentProperty); }
            set { SetValue(VerticalAlignmentProperty, value); }
        }

        public static readonly BindableProperty HorizontalAlignmentProperty = BindableProperty.Create(
            nameof(HorizontalAlignment),
            typeof(DrawImageAlignment),
            typeof(SkiaSvg),
            DrawImageAlignment.Center,
            propertyChanged: NeedDraw);

        public DrawImageAlignment HorizontalAlignment
        {
            get { return (DrawImageAlignment)GetValue(HorizontalAlignmentProperty); }
            set { SetValue(HorizontalAlignmentProperty, value); }
        }

        public static readonly BindableProperty HorizontalOffsetProperty = BindableProperty.Create(
            nameof(HorizontalOffset),
            typeof(double),
            typeof(SkiaSvg),
            0.0,
            propertyChanged: NeedDraw);

        public double HorizontalOffset
        {
            get { return (double)GetValue(HorizontalOffsetProperty); }
            set { SetValue(HorizontalOffsetProperty, value); }
        }

        public static readonly BindableProperty VerticalOffsetProperty = BindableProperty.Create(
            nameof(VerticalOffset),
            typeof(double),
            typeof(SkiaSvg),
            0.0,
            propertyChanged: NeedDraw);

        public double VerticalOffset
        {
            get { return (double)GetValue(VerticalOffsetProperty); }
            set { SetValue(VerticalOffsetProperty, value); }
        }

        private const string nameHasContent = "HasContent";

        public static readonly BindableProperty HasContentProperty = BindableProperty.Create(
            nameHasContent,
            typeof(bool),
            typeof(SkiaSvg),
            false,
            BindingMode.OneWayToSource);

        public bool HasContent
        {
            get { return (bool)GetValue(HasContentProperty); }
            set { SetValue(HasContentProperty, value); }
        }

        public static readonly BindableProperty IconFilePathProperty = BindableProperty.Create(
            nameof(IconFilePath),
            typeof(string),
            typeof(SkiaSvg),
            default(string), propertyChanged: NeedUpdateIcon);

        private string _part1;

        public string IconFilePath
        {
            get => (string)GetValue(IconFilePathProperty);
            set => SetValue(IconFilePathProperty, value);
        }

        #endregion

        private string _loadedString;
        private readonly Assembly _assembly;

        protected string LoadedString
        {
            get { return _loadedString; }
            set
            {
                if (_loadedString != value)
                {
                    _loadedString = value;
                    HasContent = !string.IsNullOrEmpty(value);
                }
            }
        }

        protected SKPaint RenderingPaint { get; set; }

        public override void OnDisposing()
        {
            LoadedString = null;

            Svg?.Dispose();
            Svg = null;

            RenderingPaint?.Dispose();
            RenderingPaint = null;

            base.OnDisposing();
        }

        public SKSvg Svg { get; protected set; }

        public new void Clear()
        {
            var svg = Svg;
            Svg = null;
            svg?.Dispose();
        }

        private static string ToSvgHex(Color color)
        {
            var skColor = color.ToSKColor();
            return $"#{skColor.Red:X2}{skColor.Green:X2}{skColor.Blue:X2}";
        }

        public void UpdateIcon()
        {
            Clear();

            if (!string.IsNullOrEmpty(SvgString))
                UpdateImageFromString(SvgString);

            if (!string.IsNullOrEmpty(LoadedString))
            {
                CreateSvg(LoadedString);
            }

            Update();
        }

        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);

            if (propertyName == nameof(FontAwesomePrimaryColor) ||
                propertyName == nameof(FontAwesomeSecondaryColor))
            {
                UpdateIcon();
            }
        }

        protected void UpdateImageFromString(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                LoadedString = "";
                return;
            }

            if (FontAwesomePrimaryColor != Colors.Black)
            {
                source = source.Replace("class=\"fa-primary\"", $"fill=\"{ToSvgHex(FontAwesomePrimaryColor)}\"");
            }

            if (FontAwesomeSecondaryColor != Colors.Gray)
            {
                source = source.Replace("class=\"fa-secondary\"", $"fill=\"{ToSvgHex(FontAwesomeSecondaryColor)}\"");
            }

            LoadedString = source;
        }

        private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
        {
            if (bindable is SkiaSvg control)
            {
                if (newvalue == null)
                {
                    control.SvgString = null;
                    control.Update();
                }
                else
                {
                    Task.Run(async () => { await control.LoadSource(control.Source); });
                }
            }
        }

        public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
            typeof(string),
            typeof(SkiaSvg),
            string.Empty,
            propertyChanged: ApplySourceProperty);

        public string Source
        {
            get { return (string)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        private SemaphoreSlim _semaphoreLoadFile = new(1, 1);

        private async Task<Stream> OpenPackageFileStreamAsync(string fileName)
        {
#if BROWSER
            var httpClient = Super.Services?.GetService<HttpClient>();
            if (httpClient == null)
                throw new InvalidOperationException("[SkiaSvg] HttpClient service was not found.");

            return await httpClient.GetStreamAsync(fileName);
#else
            return await FileSystem.OpenAppPackageFileAsync(fileName);
#endif
        }

        public virtual async Task LoadSource(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            await _semaphoreLoadFile.WaitAsync();

            try
            {
                string json;
                if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri) && uri.Scheme != "file")
                {
#if BROWSER
                    var httpClient = Super.Services?.GetService<HttpClient>() ?? throw new InvalidOperationException("[SkiaSvg] HttpClient service was not found.");
                    using var stream = await httpClient.GetStreamAsync(uri);
#else
                    using HttpClient client = Super.Services.CreateHttpClient();
                    using var stream = await client.GetStreamAsync(uri);
#endif
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                else
                {
                    using var stream = await OpenPackageFileStreamAsync(fileName);
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }

                UpdateImageFromString(json);
                UpdateIcon();

                Success?.Invoke(this, fileName);
            }
            catch (Exception e)
            {
                Trace.WriteLine($"[SkiaSvg] LoadSource failed to load {fileName}");
                Trace.WriteLine(e);
                Error?.Invoke(this, e);
            }
            finally
            {
                _semaphoreLoadFile.Release();
            }
        }

        public event EventHandler<string> Success;
        public event EventHandler<Exception> Error;

        public static SKRect CalculateDisplayRect(SKRect dest, float bmpWidth, float bmpHeight,
            DrawImageAlignment horizontal, DrawImageAlignment vertical)
        {
            float x = 0;
            float y = 0;

            switch (horizontal)
            {
                case DrawImageAlignment.Center:
                    x = (dest.Width - bmpWidth) / 2.0f;
                    break;
                case DrawImageAlignment.Start:
                    break;
                case DrawImageAlignment.End:
                    x = dest.Width - bmpWidth;
                    break;
            }

            switch (vertical)
            {
                case DrawImageAlignment.Center:
                    y = (dest.Height - bmpHeight) / 2.0f;
                    break;
                case DrawImageAlignment.Start:
                    break;
                case DrawImageAlignment.End:
                    y = dest.Height - bmpHeight;
                    break;
            }

            x += dest.Left;
            y += dest.Top;

            return new SKRect(x, y, x + bmpWidth, y + bmpHeight);
        }

        protected void DrawPicture(SKCanvas canvas, SKPicture picture, SKRect dest,
            TransformAspect stretch,
            DrawImageAlignment horizontal = DrawImageAlignment.Center,
            DrawImageAlignment vertical = DrawImageAlignment.Center,
            SKPaint paint = null)
        {
            var pxWidth = picture.CullRect.Width;
            var pxHeight = picture.CullRect.Height;

            var scaled = RescaleAspect(pxWidth, pxHeight, dest, stretch);

            var scaleX = scaled.X * (float)ZoomX;
            var scaleY = scaled.Y * (float)ZoomY;

            SKRect display = CalculateDisplayRect(dest, scaleX * pxWidth, scaleY * pxHeight,
                horizontal, vertical);

            display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));

            if (WillClipBounds || Clipping != null)
            {
                using (SKPath path = new SKPath())
                {
                    if (Clipping != null)
                    {
                        Clipping.Invoke(path, dest);
                    }
                    else
                    {
                        path.MoveTo(dest.Left, dest.Top);
                        path.LineTo(dest.Right, dest.Top);
                        path.LineTo(dest.Right, dest.Bottom);
                        path.LineTo(dest.Left, dest.Bottom);
                        path.MoveTo(dest.Left, dest.Top);
                        path.Close();
                    }

                    canvas.DrawPath(path, paint);

                    paint.ImageFilter = null;

                    var saved = canvas.Save();

                    ClipSmart(canvas, path);

                    canvas.DrawPicture(picture, display.Left, display.Top, paint);

                    canvas.RestoreToCount(saved);
                }
            }
            else
            {
                canvas.DrawPicture(picture, display.Left, display.Top, paint);
            }
        }

        private const string nameZoom = "Zoom";

        public static readonly BindableProperty ZoomProperty = BindableProperty.Create(nameZoom, typeof(double),
            typeof(SkiaSvg), 1.0,
            propertyChanged: NeedDraw);

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        public virtual bool LoadSvgFromBytes(byte[] byteArray)
        {
            using (Stream stream = new MemoryStream(byteArray))
            {
                var svg = new SKSvg();
                try
                {
                    svg.Load(stream);
                    Svg = svg;
                    if (Svg == null)
                    {
                        throw new Exception("[SkiaSvg] Failed to load string");
                    }
                    Update();
                    return true;
                }
                catch (Exception e)
                {
                    Super.Log($"Failed to load {e}");
                }
                return false;
            }
        }

        protected virtual bool CreateSvg(string loadedString)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(loadedString);
            return LoadSvgFromBytes(byteArray);
        }

        SKMatrix CreateSvgMatrix(SKRect destination, double scale)
        {
            SKRect contentSize = Svg.Picture.CullRect;
            float scaledContentWidth = (float)(contentSize.Width);
            float scaledContentHeight = (float)(contentSize.Height);

            float xRatio = destination.Width / scaledContentWidth;
            float yRatio = destination.Height / scaledContentHeight;

            var aspectX = xRatio;
            var aspectY = yRatio;
            var adjustX = 0f;
            var adjustY = 0f;

            if (Aspect == TransformAspect.Fill)
            {
                if (destination.Width > scaledContentWidth && aspectX < 1)
                {
                    aspectX = 1 + (1 - aspectX);
                }

                if (destination.Height > scaledContentHeight && aspectY < 1)
                {
                    aspectY = 1 + (1 - aspectY);
                }

                adjustX = (destination.Width - scaledContentWidth * aspectX) / 2.0f;
                adjustY = (destination.Height - scaledContentHeight * aspectY) / 2.0f;
            }
            else if (Aspect == TransformAspect.AspectFill)
            {
                var needMoreY = destination.Height - scaledContentHeight * xRatio;
                var needMoreX = destination.Width - scaledContentWidth * yRatio;
                var needMore = Math.Max(needMoreX, needMoreY);
                if (needMore > 0)
                {
                    var moreX = needMore / scaledContentWidth;
                    var moreY = needMore / scaledContentHeight;
                    xRatio += moreX;
                    yRatio += moreY;
                }

                if (destination.Width < destination.Height)
                {
                    aspectX = xRatio;
                    aspectY = xRatio;
                }
                else
                {
                    aspectX = yRatio;
                    aspectY = yRatio;
                }

                adjustX = (destination.Width - scaledContentWidth * aspectX) / 2.0f;
                adjustY = (destination.Height - scaledContentHeight * aspectY) / 2.0f;
            }
            else
            {
                var aspectFit = Math.Min(xRatio / ZoomX, yRatio / ZoomY);

                var aspectFitX = (float)(aspectFit * ZoomX * Zoom);
                var aspectFitY = (float)(aspectFit * ZoomY * Zoom);

                if (yRatio == aspectFit)
                {
                    adjustX = (destination.Width - scaledContentWidth * aspectFitX) / 2.0f;
                }
                else
                {
                    adjustY = (destination.Height - scaledContentHeight * aspectFitY) / 2.0f;
                }

                aspectX = aspectFitX;
                aspectY = aspectFitY;
            }

            var matrix = new SKMatrix
            {
                ScaleX = aspectX,
                SkewX = 0,
                TransX = destination.Left + adjustX + (float)Math.Round(HorizontalOffset * scale),
                SkewY = 0,
                ScaleY = aspectY,
                TransY = destination.Top + adjustY + (float)Math.Round(VerticalOffset * scale),
                Persp0 = 0,
                Persp1 = 0,
                Persp2 = 1
            };

            return matrix;
        }

        protected override void Paint(DrawingContext ctx)
        {
            if (Svg != null)
            {
                var scale = ctx.Scale;
                var area = ContractPixelsRect(ctx.Destination, ctx.Scale, UsePadding);

                area = new SKRect((float)Math.Round(area.Left), (float)Math.Round(area.Top),
                    (float)Math.Round(area.Right), (float)Math.Round(area.Bottom));

                RenderingPaint ??= new SKPaint() { IsAntialias = true };
                RenderingPaint.IsDither = IsDistorted;
                RenderingPaint.BlendMode = DefaultBlendMode;

                SKMatrix matrix = CreateSvgMatrix(area, scale);

                SKPath clipPath = null;

                if (TintColor != Colors.Transparent && FillGradient == null)
                {
                    base.Paint(ctx);

                    var kill1 = RenderingPaint.Shader;
                    RenderingPaint.Shader = null;
                    if (kill1 != null)
                        DisposeObject(kill1);

                    AddShadow(RenderingPaint, scale);
                    RenderingPaint.ColorFilter = SKColorFilter.CreateBlendMode(TintColor.ToSKColor(), SKBlendMode.SrcIn);

                    ctx.Context.Canvas.DrawPicture(Svg.Picture, ref matrix, RenderingPaint);
                }
                else if (FillGradient != null)
                {
                    var kill1 = RenderingPaint.ColorFilter;
                    RenderingPaint.ColorFilter = null;
                    if (kill1 != null)
                        DisposeObject(kill1);

                    var destination = ctx.Destination;
                    var info = new SKImageInfo((int)destination.Width, (int)destination.Height,
                        SKColorType.Rgba8888,
                        SKAlphaType.Premul);

                    using var intermediateSurface = SKSurface.Create(info);
                    using var intermediateCanvas = intermediateSurface.Canvas;
                    intermediateCanvas.Clear(SKColors.Transparent);

                    var adjustedMatrix = matrix;
                    adjustedMatrix = adjustedMatrix.PostConcat(SKMatrix.CreateTranslation(-destination.Left, -destination.Top));

                    intermediateCanvas.DrawPicture(Svg.Picture, ref adjustedMatrix);

                    var rect = new SKRect(0, 0, destination.Width, destination.Height);
                    SetupGradient(RenderingPaint, FillGradient, rect);
                    RenderingPaint.BlendMode = GradientBlendMode;

                    intermediateCanvas.DrawRect(new SKRect(0, 0, destination.Width, destination.Height), RenderingPaint);

                    var kill = RenderingPaint.Shader;
                    RenderingPaint.Shader = null;
                    if (kill != null)
                        DisposeObject(kill);
                    RenderingPaint.BlendMode = this.FillBlendMode;

                    AddShadow(RenderingPaint, scale);
                    ctx.Context.Canvas.DrawSurface(intermediateSurface, new(destination.Left, destination.Top), RenderingPaint);
                }
                else
                {
                    base.Paint(ctx);

                    var kill1 = RenderingPaint.Shader;
                    var kill2 = RenderingPaint.ColorFilter;
                    RenderingPaint.Shader = null;
                    RenderingPaint.ColorFilter = null;
                    if (kill1 != null)
                        DisposeObject(kill1);
                    if (kill2 != null)
                        DisposeObject(kill2);

                    AddShadow(RenderingPaint, scale);

                    if (Clipping != null)
                    {
                        if (clipPath == null)
                        {
                            clipPath = new SKPath();
                            Clipping.Invoke(clipPath, Destination);
                        }

                        var saved = ctx.Context.Canvas.Save();
                        ClipSmart(ctx.Context.Canvas, clipPath);

                        ctx.Context.Canvas.DrawPicture(Svg.Picture, ref matrix, RenderingPaint);

                        ctx.Context.Canvas.RestoreToCount(saved);

                        DisposeObject(clipPath);
                    }
                    else
                    {
                        ctx.Context.Canvas.DrawPicture(Svg.Picture, ref matrix, RenderingPaint);
                    }
                }
            }
            else
            {
                base.Paint(ctx);
            }
        }
    }
}