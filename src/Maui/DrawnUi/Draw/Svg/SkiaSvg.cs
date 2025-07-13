using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using DrawnUi.Features.Images;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
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

        //-------------------------------------------------------------
        // ShadowColor
        //-------------------------------------------------------------
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

        //-------------------------------------------------------------
        // ShadowX
        //-------------------------------------------------------------
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

        //-------------------------------------------------------------
        // ShadowY
        //-------------------------------------------------------------
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

        //-------------------------------------------------------------
        // ShadowBlur
        //-------------------------------------------------------------
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

            _assembly = Application.Current?.GetType()?.Assembly; //(); //Assembly.GetExecutingAssembly();

            if (_assembly == null)
            {
            }

            _part1 = _assembly.GetName().Name + $".Resources.Images.";
        }


        protected static void NeedUpdateIcon(BindableObject bindable, object oldvalue, object newvalue)
        {
            var control = bindable as SkiaSvg;
            {
                if (control != null && !control.IsDisposed)
                {
                    control.UpdateIcon();
                }
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

        /// <summary>
        /// When FIllGradient is set this will override its blend mode for drawing SVG with gradient
        /// </summary>
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
        /*
        public static readonly BindableProperty SvgDataProperty = BindableProperty.Create(
            nameof(SvgData),
            typeof(string),
            typeof(SkiaSvg),
            string.Empty,
            BindingMode.OneWay,
            propertyChanged: NeedUpdateIcon);

        public string SvgData
        {
            get { return (string)GetValue(SvgDataProperty); }
            set { SetValue(SvgDataProperty, value); }
        }
        */

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


        //public static SKTypeface GetTypeface(string fullFontName)
        //{
        //    SKTypeface result;

        //    var assembly = Assembly.GetExecutingAssembly();
        //    var stream = assembly.GetManifestResourceStream("ClassLibrary1.Font." + fullFontName);
        //    if (stream == null)
        //        return null;

        //    result = SKTypeface.FromStream(stream);
        //    return result;
        //}

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

        /// <summary>
        /// Protected SKSvg container
        /// </summary>
        public SKSvg Svg { get; protected set; }

        //protected SKMatrix DrawingMatrix;

        public new void Clear()
        {
            var svg = Svg;
            Svg = null;
            svg?.Dispose();

            base.Clear();
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

        //public static readonly BindableProperty IconStringProperty = BindableProperty.Create("IconString", typeof(string), typeof(SkiaSvg), null, BindingMode.OneWay, null, NeedUpdateIcon);
        //public string IconString
        //{
        //    get
        //    {
        //        return (string)GetValue(IconStringProperty);
        //    }
        //    set
        //    {
        //        SetValue(IconStringProperty, value);
        //    }
        //}


        protected override void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            base.OnPropertyChanged(propertyName);


            if (propertyName == nameof(FontAwesomePrimaryColor) ||
                propertyName == nameof(FontAwesomeSecondaryColor))
            {
                UpdateIcon();
            }


            //else
            //if (propertyName == "Height" || propertyName == "Width")
            //{
            //    AnchorX = Width / 2.0f;
            //    AnchorY = Height / 2.0f;
            //}
        }

        protected void UpdateImageFromString(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                LoadedString = "";
                return;
            }

            //replace anything
            if (FontAwesomePrimaryColor != Colors.Black)
            {
                source = source.Replace("class=\"fa-primary\"", $"fill=\"{FontAwesomePrimaryColor.ToHex()}\"");
            }

            if (FontAwesomeSecondaryColor != Colors.Gray)
            {
                source = source.Replace("class=\"fa-secondary\"", $"fill=\"{FontAwesomeSecondaryColor.ToHex()}\"");
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
                    Task.Run(async () => { await control.LoadSource(control.Source); });
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


        /// <summary>
        /// This is not replacing current animation, use SetAnimation for that.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task LoadSource(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return;

            await _semaphoreLoadFile.WaitAsync();

            try
            {
                string json;
                if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri))
                {
                    using HttpClient client = Super.Services.CreateHttpClient();
                    using var stream = await client.GetStreamAsync(uri);
                    using var reader = new StreamReader(stream);
                    json = await reader.ReadToEndAsync();
                }
                else
                {
                    using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
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

        /// <summary>
        /// Happens when loaded fine from `Source`. Will pass source as string.
        /// </summary>
        public event EventHandler<string> Success;

        /// <summary>
        /// Happens when loaded with error from `Source`. Will pass exception.
        /// </summary>
        public event EventHandler<Exception> Error;

        protected void UpdateImageFromFile()
        {
            SvgString = "";

            if (string.IsNullOrEmpty(IconFilePath))
            {
                LoadedString = "";
                return;
            }

            var fullname = _part1 + IconFilePath;
            using (Stream file = GetType().Assembly.GetManifestResourceStream(fullname))
            {
                if (file == null)
                {
                    LoadedString = "";
                    return;
                }

                var content = "";
                using (var reader = new System.IO.StreamReader(file))
                {
                    content = reader.ReadToEnd();

                    UpdateImageFromString(content);
                }
            }
        }

        /*
        protected void UpdateImageFromFile()
        {
            //    SvgString = "";

            if (string.IsNullOrEmpty(IconFilePath))
            {
                image = "";
                return;
            }

            if (IconFilePath.StartsWith("resource://", StringComparison.OrdinalIgnoreCase))
            {
                Task.Run(() =>
                {

                    var uri = new Uri(IconFilePath);
                    Assembly assembly = null;

                    var parts = uri.OriginalString.Substring(11).Split('?');
                    var resourceName = parts.First();

                    if (parts.Count() > 1)
                    {
                        var name = Uri.UnescapeDataString(uri.Query.Substring(10));
                        var assemblyName = new AssemblyName(name);
                        assembly = Assembly.Load(assemblyName);
                    }

                    if (assembly == null)
                    {
                        var callingAssemblyMethod = typeof(Assembly).GetTypeInfo().GetDeclaredMethod("GetCallingAssembly");
                        assembly = (Assembly)callingAssemblyMethod.Invoke(null, new object[0]);
                    }

                    var fullPath = $"{assembly.GetName().Name}.{resourceName}";

                    using (var stream = SkiaFontManager.GetEmbeddedResourceStream(assembly, fullPath))
                    {
                        if (stream == null)
                        {
                            Debug.WriteLine($"[SkiaSvg] Failed to load {fullPath} from {_assembly.FullName}");
                            return;
                        }

                        var content = "";

                        using (var reader = new System.IO.StreamReader(stream))
                        {
                            content = reader.ReadToEnd();

                            UpdateImageFromString(content);
                        }
                    }

                }).ConfigureAwait(false);

                return;
            }

            if (IconFilePath.ToLower().Contains("http://") || IconFilePath.ToLower().Contains("https://"))
            {
                Task.Run(ActionLoadFromUrl).ConfigureAwait(false);
            }
            else
            {
                // resource://{resourceName}?assembly={Uri.EscapeUriString(resourceAssembly.FullName)}";
                //if (IconFilePath.ToLower().Contains("resource://"))
                //{
                //    var uri = new Uri(IconFilePath);

                //    var text = uri.OriginalString;
                //    if (string.IsNullOrWhiteSpace(uri.Query))
                //    {
                //        if (_cachedMainAssembly == null)
                //            _cachedMainAssembly = Application.Current?.GetType()?.GetTypeAssemblyFullName();

                //        Uri = new Uri(_cachedMainAssembly == null ? text : $"{text}?assembly={Uri.EscapeUriString(_cachedMainAssembly)}");
                //    }
                //    else if (!uri.Query.Contains("assembly=", StringComparison.OrdinalIgnoreCase))
                //    {
                //        var assemblyName = Application.Current?.GetType()?.GetTypeAssemblyFullName();
                //        Uri = new Uri(assemblyName == null ? text : $"{text}?assembly={Uri.EscapeUriString(assemblyName)}");
                //    }
                //    else
                //    {
                //        Uri = uri;
                //    }
                //    return;
                //}

                var fullname = _part1 + IconFilePath;

                using (var stream = SkiaFontManager.GetEmbeddedResourceStream(_assembly, fullname))
                {
                    if (stream == null)
                    {
                        Debug.WriteLine($"[SkiaSvg] Failed to load {fullname} from {_assembly.FullName}");
                        return;
                    }

                    var content = "";

                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        content = reader.ReadToEnd();

                        UpdateImageFromString(content);
                    }
                }
            }

        }
        */

        private async void ActionLoadFromUrl()
        {
            // throw new NotImplementedException();

            try
            {
                var client = Super.Services.CreateHttpClient();

                SvgString = await client.GetStringAsync(IconFilePath);

                //using (var stream = await ImageService.Instance.LoadUrl(IconFilePath).AsPNGStreamAsync())
                //{
                //    // convert stream to string
                //    StreamReader reader = new StreamReader(stream);
                //    SvgString = reader.ReadToEnd();
                //}
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

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

            //MaxWidth = PixelsToDeviceUnits(scaleX * bitmap.Width);

            SKRect display = CalculateDisplayRect(dest, scaleX * pxWidth, scaleY * pxHeight,
                horizontal, vertical);

            //if (this.BlurAmount > 0)
            display.Inflate(new SKSize((float)InflateAmount, (float)InflateAmount));

            //using matrix
            //display.Offset(SkiaControl.DeviceUnitsToPixels(HorizontalOffset), SkiaControl.DeviceUnitsToPixels(VerticalOffset));

            //todo apply clipping here
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

                    //var ShadowSize = 2 * scale;
                    //var ShadowBlurAmount = 5 * scale;
                    //var ColorShadow = Colors.FromHex("#cccccc");

                    //paint.IsAntialias = true;
                    //paint.ImageFilter = SKImageFilter.CreateDropShadowOnly(0, (float)ShadowSize, 0, (float)ShadowBlurAmount,
                    //    ColorShadow.ToSKColor());
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


        //-------------------------------------------------------------
        // Zoom
        //-------------------------------------------------------------
        private const string nameZoom = "Zoom";

        public static readonly BindableProperty ZoomProperty = BindableProperty.Create(nameZoom, typeof(double),
            typeof(SkiaSvg), 1.0,
            propertyChanged: NeedDraw);

        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        /// <summary>
        /// Directly loads svg from bytes array into container
        /// </summary>
        /// <param name="byteArray"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Loads svg into container from string. For public access use SvgString property instead.
        /// </summary>
        /// <param name="loadedString"></param>
        /// <returns></returns>
        protected virtual bool CreateSvg(string loadedString)
        {
            byte[] byteArray = Encoding.ASCII.GetBytes(loadedString);
            return LoadSvgFromBytes(byteArray);
        }


        SKMatrix CreateSvgMatrix(SKRect destination, double scale)
        {
            #region Layout

            SKRect contentSize = Svg.Picture.CullRect;
            float scaledContentWidth = (float)(contentSize.Width); // * Density);
            float scaledContentHeight = (float)(contentSize.Height); // * Density);

            //multipliers to reduce
            float xRatio = destination.Width / scaledContentWidth;
            float yRatio = destination.Height / scaledContentHeight;

            var aspectX = xRatio;
            var aspectY = yRatio;
            var adjustX = 0f;
            var adjustY = 0f;

            if (Aspect == TransformAspect.Fill)
            {
                //multipliers to enlarge
                if (destination.Width > scaledContentWidth && aspectX < 1)
                {
                    aspectX = 1 + (1 - aspectX);
                }

                if (destination.Height > scaledContentHeight && aspectY < 1)
                {
                    aspectY = 1 + (1 - aspectY);
                }

                //todo can add property ResizeAnchor, actually its Center
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
            else //FIT
            {
                //keep aspect
                var aspectFit = Math.Min(xRatio / ZoomX, yRatio / ZoomY);

                var aspectFitX = (float)(aspectFit * ZoomX * Zoom);
                var aspectFitY = (float)(aspectFit * ZoomY * Zoom);

                if (yRatio == aspectFit) // was fit for by height, need to center x
                {
                    adjustX = (destination.Width - scaledContentWidth * aspectFitX) / 2.0f;
                }
                else // was fit for by width, need to center y
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

            #endregion
        }

        protected override void Paint(DrawingContext ctx)
        {
            if (Svg != null)
            {
                var scale = ctx.Scale;
                var area = ContractPixelsRect(ctx.Destination, ctx.Scale, Padding);

                //actually Skia.Svg cant render well with subpixel so...
                area = new SKRect((float)Math.Round(area.Left), (float)Math.Round(area.Top),
                    (float)Math.Round(area.Right), (float)Math.Round(area.Bottom));

                RenderingPaint ??= new SKPaint() { IsAntialias = true };
                RenderingPaint.IsDither = IsDistorted;
                RenderingPaint.BlendMode = DefaultBlendMode;

                SKMatrix matrix = CreateSvgMatrix(area, scale);

                SKPath clipPath = null;

                if (TintColor != Colors.Transparent
                    && FillGradient == null)
                {
                    base.Paint(ctx);

                    var kill1 = RenderingPaint.Shader;
                    RenderingPaint.Shader = null;
                    if (kill1 != null)
                        DisposeObject(kill1);

                    AddShadow(RenderingPaint, scale);
                    RenderingPaint.ColorFilter =
                        SKColorFilter.CreateBlendMode(TintColor.ToSKColor(), SKBlendMode.SrcIn);

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
                    adjustedMatrix =
                        adjustedMatrix.PostConcat(SKMatrix.CreateTranslation(-destination.Left, -destination.Top));

                    intermediateCanvas.DrawPicture(Svg.Picture, ref adjustedMatrix);

                    var rect = new SKRect(0, 0, destination.Width, destination.Height);
                    SetupGradient(RenderingPaint, FillGradient, rect);
                    RenderingPaint.BlendMode = GradientBlendMode;

                    intermediateCanvas.DrawRect(new SKRect(0, 0, destination.Width, destination.Height),
                        RenderingPaint);

                    var kill = RenderingPaint.Shader;
                    RenderingPaint.Shader = null;
                    if (kill != null)
                        DisposeObject(kill);
                    RenderingPaint.BlendMode = this.FillBlendMode;

                    AddShadow(RenderingPaint, scale);
                    ctx.Context.Canvas.DrawSurface(intermediateSurface, new(destination.Left, destination.Top),
                        RenderingPaint);
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

                    //drop shadow
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
