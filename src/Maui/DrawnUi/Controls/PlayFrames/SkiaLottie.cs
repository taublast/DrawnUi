using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using System.Text;
using DrawnUi.Features.Images;
using Microsoft.Maui.Storage;
using Newtonsoft.Json.Linq;
using Animation = SkiaSharp.Skottie.Animation;

namespace DrawnUi.Controls;

public class SkiaLottie : AnimatedFramesRenderer
{
    /// <summary>
    ///     To avoid reloading same files multiple times..
    /// </summary>
    public static ConcurrentDictionary<string, string> CachedAnimations = new();

    public override void Stop()
    {
        base.Stop();

        SeekToDefaultFrame();
    }

    protected override void OnFinished()
    {
        base.OnFinished();

        SeekToDefaultFrame();
    }

    public virtual void SeekToDefaultFrame()
    {
        if (Animator != null)
        {
            lock (_lockSource)
            {
                if (IsOn)
                    Seek(DefaultFrameWhenOn);
                else
                    Seek(DefaultFrame);

                Monitor.PulseAll(_lockSource);
            }
        }
        else
        {
            needSeek = true;
        }
    }

    private bool needSeek;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        if (needSeek)
        {
            needSeek = false;
            if (!IsPlaying)
            {
                SeekToDefaultFrame();
            }
        }
    }


    protected override void OnAnimatorSeeking(double frame)
    {
        if (Animation != null)
        {
            if (frame < 0)
            {
                frame = Animation.OutPoint;
            }
            Animation.SeekFrame(frame);
        }

        base.OnAnimatorSeeking(frame);
    }

    protected override void ApplyDefaultFrame()
    {
        if (!IsPlaying)
        {
            if (Animator != null && !IsOn)
                Seek((int)DefaultFrame);
        }
    }

    private static void ApplyDefaultFrameWhenOnProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaLottie control && !control.IsPlaying)
        {
            if (control.Animator != null && control.IsOn)
                control.Seek((int)newvalue);
        }
    }

    private static void ApplyIsOnProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaLottie control)
        {
            if (control.Animator != null)
            {
                var value = (bool)newvalue;
                if (control.IsPlaying || !value)
                    control.Stop();
                else
                {
                    if (control.ApplyIsOnWhenNotPlaying)
                        control.SeekToDefaultFrame();
                }
            }
        }
    }

    private readonly object _lockSource = new();
    private Animation _animation;

    private readonly SemaphoreSlim _semaphoreLoadFile = new(1, 1);

    public Animation Animation
    {
        get => _animation;

        set
        {
            if (_animation != value)
            {
                _animation = value;
                OnPropertyChanged();
            }
        }
    }

    public static readonly BindableProperty ApplyIsOnWhenNotPlayingProperty = BindableProperty.Create(nameof(ApplyIsOnWhenNotPlaying),
    typeof(bool),
    typeof(SkiaLottie),
    true);

    /// <summary>
    /// Gets or sets whether the IsOn state should be applied when the animation is not playing.
    /// When true (default), changing IsOn will immediately seek to the appropriate frame.
    /// When false, IsOn state is only applied during animation playback.
    /// </summary>
    public bool ApplyIsOnWhenNotPlaying
    {
        get { return (bool)GetValue(ApplyIsOnWhenNotPlayingProperty); }
        set { SetValue(ApplyIsOnWhenNotPlayingProperty, value); }
    }

    public static readonly BindableProperty IsOnProperty = BindableProperty.Create(nameof(IsOn),
    typeof(bool),
    typeof(SkiaLottie),
    false, propertyChanged: ApplyIsOnProperty);

    /// <summary>
    /// Gets or sets the toggle state for animations with distinct on/off states.
    /// When true, displays DefaultFrameWhenOn frame when stopped.
    /// When false, displays DefaultFrame when stopped.
    /// Perfect for animated toggles, checkboxes, or switch controls.
    /// </summary>
    public bool IsOn
    {
        get { return (bool)GetValue(IsOnProperty); }
        set { SetValue(IsOnProperty, value); }
    }

    public static readonly BindableProperty DefaultFrameWhenOnProperty = BindableProperty.Create(nameof(DefaultFrameWhenOn),
        typeof(int),
        typeof(SkiaLottie),
        0, propertyChanged: ApplyDefaultFrameWhenOnProperty
    );
    /// <summary>
    /// For the case IsOn = True. What frame should we display at start or when stopped. 0 (START) is default, can specify other number. if value is less than 0 then will seek to the last available frame (END).
    /// </summary>
    public int DefaultFrameWhenOn
    {
        get => (int)GetValue(DefaultFrameWhenOnProperty);
        set => SetValue(DefaultFrameWhenOnProperty, value);
    }

    public static readonly BindableProperty ColorTintProperty = BindableProperty.Create(
        nameof(ColorTint),
        typeof(Color),
        typeof(SkiaLottie),
        TransparentColor, propertyChanged: ApplySourceProperty);

    /// <summary>
    /// Gets or sets a global color tint applied to the entire Lottie animation.
    /// This provides a quick way to theme animations without modifying the source file.
    /// For multiple color replacements, use the Colors collection instead.
    /// </summary>
    public Color ColorTint
    {
        get => (Color)GetValue(ColorTintProperty);
        set => SetValue(ColorTintProperty, value);
    }


    #region COLORS


    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors),
        typeof(IList<Color>),
        typeof(SkiaLottie),
        defaultValueCreator: (bindable) =>
        {
            var newCollection = new ObservableCollection<Color>();
            newCollection.CollectionChanged +=((SkiaLottie)bindable).OnSkiaPropertyColorCollectionChanged;
            return newCollection;
        },
        validateValue: (bo, v) => v is IList<Color>,
        propertyChanged: ColorsPropertyChanged,
        coerceValue: CoerceColors);


    public IList<Color> Colors
    {
        get => (IList<Color>)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    private static object CoerceColors(BindableObject bindable, object value)
    {
        if (!(value is ReadOnlyCollection<Color> readonlyCollection))
        {
            return value;
        }

        return new ReadOnlyCollection<Color>(
            readonlyCollection.Select(s => s)
                .ToList());
    }



    private static void ColorsPropertyChanged(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaLottie control)
        {
            if (oldvalue is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnSkiaPropertyColorCollectionChanged;
            }
            if (newvalue is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += control.OnSkiaPropertyColorCollectionChanged;
            }

            control.ReloadSource();
        }

    }

    private void OnSkiaPropertyColorCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        this.ReloadSource();
    }

    #endregion


    public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source),
        typeof(string),
        typeof(SkiaLottie),
        string.Empty,
        propertyChanged: ApplySourceProperty);


    /// <summary>
    /// Gets or sets the path or URL to the Lottie JSON file.
    /// Supports web URLs, native file system paths (file://), and bundled resources from Resources/Raw folder.
    /// Example: "Lottie/animation.json" loads from Resources/Raw/Lottie/animation.json
    /// </summary>
    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    protected override void RenderFrame(DrawingContext ctx)
    {
        if (IsDisposing || IsDisposed)
            return;

        if (Animation is Animation lottie)
            lock (_lockSource)
            {
                lottie.Render(ctx.Context.Canvas, DrawingRect);
                Monitor.PulseAll(_lockSource);
            }
    }

    public string LoadLocalJson(string fileName)
    {
        string json = null;
        try
        {
            if (fileName.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
            {
                var fullFilename = fileName.Replace(SkiaImageManager.NativeFilePrefix, "");
                using var stream = new FileStream(fullFilename, FileMode.Open);
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }
            else
            {
                using var stream = FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            CachedAnimations.TryAdd(fileName, json);

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return json;
    }

    /// <summary>
    /// This is not replacing current animation, only pre-loading! Use SetAnimation after that if needed.
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public async Task<Animation> LoadSource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        await _semaphoreLoadFile.WaitAsync();

        try
        {
            string json = null;
            if (CachedAnimations.TryGetValue(fileName, out json))
            {
                Debug.WriteLine($"Loaded {fileName} from cache");
            }
            else
            {
                if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri) && uri.Scheme != "file")
                {
                    using HttpClient client = Super.Services.CreateHttpClient();
                    var data = await client.GetByteArrayAsync(uri);
                    //var client = new WebClient();
                    //var data = await client.DownloadDataTaskAsync(uri);
                    json = Encoding.UTF8.GetString(data);
                }
                else
                {
                    if (fileName.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
                    {
                        var fullFilename = fileName.Replace(SkiaImageManager.NativeFilePrefix, "");
                        using var stream = new FileStream(fullFilename, FileMode.Open);
                        using var reader = new StreamReader(stream);
                        json = await reader.ReadToEndAsync();
                    }
                    else
                    {
                        using var stream = await FileSystem.OpenAppPackageFileAsync(fileName);
                        using var reader = new StreamReader(stream);
                        json = await reader.ReadToEndAsync();
                    }

                    CachedAnimations.TryAdd(fileName, json);
                }
            }

            if (Colors.Any())
            {
                json = ApplyTint(json, Colors);
            }
            else
            if (ColorTint.Alpha > 0)
            {
                json = ApplyTint(json, new List<Color>()
                {
                    ColorTint
                });
            }

            if (ProcessJson != null) json = ProcessJson(json);

            return LoadAnimationFromJson(OnJsonLoaded(json));
        }
        catch (Exception e)
        {
            Super.Log($"[SkiaLottie] LoadSource failed to load animation {fileName}");
            return null;
        }
        finally
        {
            _semaphoreLoadFile.Release();
        }
    }

    public Animation CreateAnimation(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            if (Colors.Count>0)
            {
                json = ApplyTint(json, Colors);
            }
            else
            if (ColorTint.Alpha > 0)
            {
                json = ApplyTint(json, new List<Color>()
                {
                    ColorTint
                });
            }

            if (ProcessJson != null) json = ProcessJson(json);

            return LoadAnimationFromJson(OnJsonLoaded(json));
        }
        catch (Exception e)
        {
            Super.Log($"[SkiaLottie] LoadSource failed to create animation");
            return null;
        }
 
    }

    public static Stream StreamFromResourceUrl(string url, Assembly assembly = null)
    {
        var uri = new Uri(url);

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
            assembly = Assembly.GetCallingAssembly();
        }

        var fullPath = $"{assembly.GetName().Name}.{resourceName}";

        Console.WriteLine($"[StreamFromResourceUrl] loading {fullPath}..");

        return assembly.GetManifestResourceStream(fullPath);
    }

    /// <summary>
    ///     Called by LoadAnimationFromResources after file was loaded so we can modify json if needed before it it consumed.
    ///     Return json to be used.
    ///     This is not called by LoadAnimationFromJson.
    /// </summary>
    protected virtual string OnJsonLoaded(string json)
    {
        return json;
    }

    /// <summary>
    ///     This is not replacing current animation, use SetAnimation for that.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public Animation LoadAnimationFromJson(string json)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            var data = SKData.CreateCopy(bytes);
            if (Animation.TryCreate(data, out var animation))
                return animation;
            throw new Exception("SkiaSharp.Skottie.Animation.TryParse failed.");
        }
        catch (Exception e)
        {
            Trace.WriteLine("[SkiaLottie] failed to load animation from json");
            Trace.WriteLine(e);
        }

        return null;
    }

    public void SetAnimation(Animation animation, bool disposePrevious)
    {
        lock (_lockSource)
        {
            if (animation == null || animation == Animation || IsDisposed) return;

            var wasPlaying = IsPlaying;
            Animation kill = null;

            if (wasPlaying)
            {
                kill = Animation;
                Stop();
            }

            Animation = animation;

            //Debug.WriteLine($"[SkiaLottie] Loaded animation: Version:{Animation.Version} Duration:{Animation.Duration} Fps:{Animation.Fps} InPoint:{Animation.InPoint} OutPoint:{Animation.OutPoint}");

            InitializeAnimator(); //autoplay applied inside

            if (IsOn)
            {
                OnAnimatorSeeking(DefaultFrameWhenOn);
            }
            else
            {
                OnAnimatorSeeking(DefaultFrame);
            }

            if (wasPlaying && !IsPlaying) Start();

            if (kill != null && disposePrevious)
                Tasks.StartDelayed(TimeSpan.FromSeconds(2), () => { kill.Dispose(); });

            Invalidate();

            Monitor.PulseAll(_lockSource);
        }
    }


    private void Free(Animation skottieAnimation)
    {
        skottieAnimation.Dispose();
    }

    public override void OnDisposing()
    {
        lock (_lockSource)
        {
            base.OnDisposing();

            if (Animation != null)
            {
                Free(Animation);
                Animation = null;
            }

            Monitor.PulseAll(_lockSource);
        }
    }

    protected override void OnAnimatorUpdated(double value)
    {
        base.OnAnimatorUpdated(value);

        if (Animation != null)
        {
            Animation.SeekFrame(value);
            Update();
        }
    }

    protected override void ApplySpeed()
    {
        if (Animation == null)
            return;

        var speed = 1.0;
        if (SpeedRatio < 1)
            speed = Animation.Duration.TotalMilliseconds * (1 + SpeedRatio);
        else
            speed = Animation.Duration.TotalMilliseconds / SpeedRatio;
        Animator.Speed = speed;
    }

    protected override void OnAnimatorInitializing()
    {
        if (Animation != null)
        {
            ApplySpeed();

            Animator.mValue = Animation.InPoint;
            Animator.mMinValue = Animation.InPoint;
            Animator.mMaxValue = Animation.OutPoint;
            Animator.Distance = Animator.mMaxValue - Animator.mMinValue;
        }
    }

    protected override bool CheckCanStartAnimator()
    {
        return Animation != null;
    }

    protected override void OnAnimatorStarting()
    {
        Animator.SetValue(Animation.InPoint);
    }

    public virtual void GoToStart()
    {
        if (Animation != null)
        {
            Animation.Seek(0.0);
            Update();
        }
    }

    public virtual void GoToEnd()
    {
        if (Animation != null)
        {
            Animation.Seek(100.0);
            Update();
        }
    }

    private object lockSource = new();

    public virtual void ReloadSource()
    {
        if (string.IsNullOrEmpty(Source))
        {
            return;
        }

        lock (lockSource)
        {
            string json = null;
            Animation animation = null;

            if (CachedAnimations.TryGetValue(Source, out json))
            {
                animation = CreateAnimation(json);
            }

            if (animation != null)
            {
                SetAnimation(animation, true);
                return;
            }

            var type = GetSourceType(Source);

            switch (type)
            {
                case SourceType.Url:
                    Tasks.StartDelayedAsync(TimeSpan.FromMilliseconds(1), async () =>
                    {
                        var a = await LoadSource(Source);
                        if (a != null)
                        {
                            SetAnimation(a, true);
                        }
                    });
                    break;
                default:

                    json = LoadLocalJson(Source);
                    animation = CreateAnimation(json);
                    if (animation != null)
                    {
                        SetAnimation(animation, true);
                        return;
                    }
                    break;
            }
        }
   
 
    }


    private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaLottie control)
            control.ReloadSource();
    }


    #region PROCESSING

    public class ColorEqualityComparer : IEqualityComparer<Color>
    {
        private const float Tolerance = 0.0001f;

        public bool Equals(Color x, Color y)
        {
            return Math.Abs(x.Red - y.Red) < Tolerance &&
                   Math.Abs(x.Green - y.Green) < Tolerance &&
                   Math.Abs(x.Blue - y.Blue) < Tolerance &&
                   Math.Abs(x.Alpha - y.Alpha) < Tolerance;
        }

        public int GetHashCode(Color obj)
        {
            int hash = 17;
            hash = hash * 23 + obj.Red.GetHashCode();
            hash = hash * 23 + obj.Green.GetHashCode();
            hash = hash * 23 + obj.Blue.GetHashCode();
            hash = hash * 23 + obj.Alpha.GetHashCode();
            return hash;
        }
    }

    public Func<string, string> ProcessJson;

    public static string ApplyTint(string json, IList<Color> newTints)
    {
        var lottieJson = JObject.Parse(json);
        List<(JToken Token, Color Color)> colorList = new();

        FindColorProperties(lottieJson, colorList);

        // Get the unique colors using a custom equality comparer
        var uniqueColors = colorList.Select(c => c.Color)
            .Distinct(new ColorEqualityComparer())
            .ToList();

        // Create a mapping from original colors to new tints
        Dictionary<Color, Color> colorMapping = new Dictionary<Color, Color>(new ColorEqualityComparer());
        for (int i = 0; i < uniqueColors.Count; i++)
        {
            var originalColor = uniqueColors[i];
            Color newTint;
            if (i < newTints.Count)
            {
                newTint = newTints[i];
            }
            else
            {
                // If there are more unique colors than tints, use the first tint for remaining colors
                newTint = newTints[0];
            }
            colorMapping[originalColor] = newTint;
        }

        // Replace colors in the JSON
        foreach (var (token, color) in colorList)
        {
            // Get the mapped new color
            Color newColor = colorMapping[color];
            // Preserve the original alpha
            newColor = newColor.WithAlpha(color.Alpha);

            if (token.Type == JTokenType.String)
            {
                // Handle "sc" or "fc" properties
                token.Replace(new JValue(ColorToHexString(newColor)));
            }
            else if (token.Type == JTokenType.Array)
            {
                // Handle "k" properties with color arrays
                token.Replace(JToken.Parse(ColorToArrayString(newColor)));
            }
        }

        return lottieJson.ToString();
    }



    public static void FindColorProperties(JToken token, List<(JToken Token, Color Color)> colorList)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                if (property.Name == "k")
                {
                    if (IsColorArray(property.Value))
                    {
                        var color = StringToXamarinColor(property.Value.ToString());
                        colorList.Add((property.Value, color));
                    }
                }
                else if (property.Name == "sc" || property.Name == "fc")
                {
                    var color = StringToXamarinColorFromHex(property.Value.ToString());
                    colorList.Add((property.Value, color));
                }

                // Recursively process the property's value
                FindColorProperties(property.Value, colorList);
            }
        }
        else if (token is JArray arr)
        {
            foreach (var item in arr)
            {
                // Recursively process each item in the array
                FindColorProperties(item, colorList);
            }
        }
    }



    public static string ColorToRGBHexString(Color color)
    {
        var r = (int)(color.Red * 255);
        var g = (int)(color.Green * 255);
        var b = (int)(color.Blue * 255);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static string ColorToHexString(Color color)
    {
        // Format the color as a hex string with alpha
        var a = (int)(color.Alpha * 255);
        var r = (int)(color.Red * 255);
        var g = (int)(color.Green * 255);
        var b = (int)(color.Blue * 255);
        return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
    }

    public static string ColorToArrayString(Color color)
    {
        // Format the color as an array string
        var r = color.Red.ToString(CultureInfo.InvariantCulture);
        var g = color.Green.ToString(CultureInfo.InvariantCulture);
        var b = color.Blue.ToString(CultureInfo.InvariantCulture);
        var a = color.Alpha.ToString(CultureInfo.InvariantCulture);
        return $"[\n  {r},\n  {g},\n  {b},\n  {a}\n]";
    }

    public static Color StringToXamarinColorFromHex(string hexColor)
    {
        // Remove the '#' if present
        if (hexColor.StartsWith("#"))
            hexColor = hexColor.Substring(1);

        // Parse RGB values
        if (hexColor.Length == 6)
        {
            var r = int.Parse(hexColor.Substring(0, 2), NumberStyles.HexNumber) / 255.0;
            var g = int.Parse(hexColor.Substring(2, 2), NumberStyles.HexNumber) / 255.0;
            var b = int.Parse(hexColor.Substring(4, 2), NumberStyles.HexNumber) / 255.0;
            return new Color((float)r, (float)g, (float)b);
        }
        else if (hexColor.Length == 8)
        {
            var a = int.Parse(hexColor.Substring(0, 2), NumberStyles.HexNumber) / 255.0;
            var r = int.Parse(hexColor.Substring(2, 2), NumberStyles.HexNumber) / 255.0;
            var g = int.Parse(hexColor.Substring(4, 2), NumberStyles.HexNumber) / 255.0;
            var b = int.Parse(hexColor.Substring(6, 2), NumberStyles.HexNumber) / 255.0;
            return new Color((float)r, (float)g, (float)b, (float)a);
        }
        else
        {
            throw new FormatException("Invalid color format");
        }
    }


    public static Color StringToXamarinColor(string colorString)
    {
        // Remove whitespace and square brackets from the string
        colorString = colorString.Replace(" ", "").Replace("[", "").Replace("]", "");

        // Split the string into individual color components
        var components = colorString.Split(',');

        // Parse the color components as floats
        var r = float.Parse(components[0], CultureInfo.InvariantCulture);
        var g = float.Parse(components[1], CultureInfo.InvariantCulture);
        var b = float.Parse(components[2], CultureInfo.InvariantCulture);
        var a = float.Parse(components[3], CultureInfo.InvariantCulture);

        // Create and return a Xamarin.Forms Color object
        return new Color(r, g, b, a);
    }

    public static string ColorToString(Color color)
    {
        // Extract the color components as floats
        var r = color.Red;
        var g = color.Green;
        var b = color.Blue;
        var a = color.Alpha;

        // Format the string color representation
        var colorString =
            $"[\n  {r.ToString(CultureInfo.InvariantCulture)},\n  {g.ToString(CultureInfo.InvariantCulture)},\n  {b.ToString(CultureInfo.InvariantCulture)},\n  {a.ToString(CultureInfo.InvariantCulture)}\n]";

        return colorString;
    }


    public static float CalculateSaturation(Color color)
    {
        var max = Math.Max(Math.Max(color.Red, color.Green), color.Blue);
        var min = Math.Min(Math.Min(color.Red, color.Green), color.Blue);
        var delta = max - min;
        var sum = max + min;

        if (Math.Abs(delta) < 0.001) return 0;

        var saturation = sum <= 1 ? delta / sum : delta / (2 - sum);
        return (float)saturation;
    }

    private static bool IsColorArray(JToken token)
    {
        if (token is JArray arr && arr.Count == 4)
        {
            foreach (var item in arr)
                if (item.Type != JTokenType.Float && item.Type != JTokenType.Integer)
                    return false;
            return true;
        }

        return false;
    }

    private static bool IsStrokeWidthProperty(JObject parent, JProperty property)
    {
        if (parent.TryGetValue("ty", out var type) && type.Value<string>() == "st") return true;
        return false;
    }

    #endregion
}
