using AppoMobi.Specials;
using DrawnUi.Draw;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;

namespace DrawnUi.Rive;

/// <summary>
/// todo subclass AnimatedFramesRenderer
/// </summary>
public partial class SkiaRive : SkiaControl
{
    public override void OnWillDisposeWithChildren()
    {
        Stop();

        base.OnWillDisposeWithChildren();
    }


    // Source actions originating from other threads must be funneled through this queue.
    private readonly ConcurrentQueue<Action> sceneActionsQueue = new();
    private string _animationName;

    // This is the render-thread copy of the animation parameters. They are set via
    // _sceneActionsQueue. _scene is then blah blah blah
    private string _artboardName;
    private bool _isPlaying;

    private DateTime? _lastPaintTime;

    private readonly SemaphoreSlim _semaphoreLoadFile = new(1, 1);
    private string _stateMachineName;

    private bool canAdvance;

    protected bool PlayWhenAvailable;

    private bool wasLayout;

    protected bool WasStarted = false;

    private byte[] Animation { get; set; }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged();
                if (value)
                    InternalStart();
                else
                    InternalStop();
            }
        }
    }

    private bool CanPlay
    {
        get
        {
#if WINDOWS
            if (_scene == null || !_scene.IsLoaded)
                return false;
#endif

            return wasLayout;
        }
    }

    public async Task<bool> LoadSource(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        await _semaphoreLoadFile.WaitAsync();

        try
        {
            byte[] data = null;
            if (Uri.TryCreate(fileName, UriKind.Absolute, out var uri) && uri.Scheme != "file")
            {
                var client = new WebClient();
                data = await client.DownloadDataTaskAsync(uri);
            }
            else
            {
                if (fileName.SafeContainsInLower(SkiaImageManager.NativeFilePrefix))
                {
                    var fullFilename = fileName.Replace(SkiaImageManager.NativeFilePrefix, "",
                        StringComparison.InvariantCultureIgnoreCase);
                    using var fileStream = new FileStream(fullFilename, FileMode.Open);
                    data = new byte[fileStream.Length];
                    fileStream.Read(data, 0, data.Length);
                    fileStream.Dispose(); // Don't keep the file open.
                }
                else
                {
                    using var fileStream = await FileSystem.OpenAppPackageFileAsync(fileName);
                    data = new byte[fileStream.Length];
                    fileStream.Read(data, 0, data.Length);
                    fileStream.Dispose(); // Don't keep the file open.
                }
            }

            //todo bind all platforms!!!

#if WINDOWS
            SetAnimation(data, false);
#endif

            return true;
        }
        catch (Exception e)
        {
            Trace.WriteLine($"[Rive] failed to load animation {fileName} using OpenAppPackageFileAsync");
            Trace.WriteLine(e);
            return false;
        }
        finally
        {
            _semaphoreLoadFile.Release();
        }
    }

    public void SetAnimation(byte[] animation, bool disposePrevious)
    {
        if (animation == null)
        {
            Trace.WriteLine("[Rive] Cannot set null animation");
            return;
        }

        if (IsPlaying) IsPlaying = false;

        if (Animation != null && disposePrevious) Free(Animation);

        Animation = animation;

#if WINDOWS
        UpdateScene(SceneUpdates.File, Animation); //we will not advance until Start() is called
#endif
        _lastPaintTime = null;

        //Debug.WriteLine($"[SkiaRive] Loaded animation: Version:{Animation.Version} Duration:{Animation.Duration} Fps:{Animation.Fps} InPoint:{Animation.InPoint} OutPoint:{Animation.OutPoint}");

        //InitializeAnimator();

        if (AutoPlay) Start();
    }

    private void Free(byte[] animation)
    { }

    public override void OnDisposing()
    {
        if (Animation != null) Stop();

        if (Animation != null)
        {
            Free(Animation);
            Animation = null;
        }

        base.OnDisposing();
    }


    protected virtual void OnFinished()
    {
        Finished?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnStarted()
    {
        Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     All repeats completed
    /// </summary>
    public event EventHandler Finished;

    public event EventHandler Started;

    protected override void OnLayoutChanged()
    {
        base.OnLayoutChanged();

        wasLayout = true;

        if (PlayWhenAvailable)
        {
            PlayWhenAvailable = false;
            InternalStart();
        }
    }

    public void Start()
    {
        IsPlaying = true;
    }

    public void Stop()
    {
        IsPlaying = false;
    }

    protected void InternalStart()
    {
        if (CanPlay)
        {
            canAdvance = true;
            Update();
        }
        else
        {
            PlayWhenAvailable = true;
        }
    }

    protected void InternalStop()
    {
        canAdvance = false;
        Debug.WriteLine("[SkiaRive] Stop!");
    }

    public void Seek() //todo
    {
        if (Animation != null)
        {
            //todo add method to animator!!!

            //SkottieAnimation.SeekFrame(value);
        }
    }


    private enum SceneUpdates
    {
        File = 3,
        Artboard = 2,
        AnimationOrStateMachine = 1
    }


    #region PROPS

    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(string),
        typeof(SkiaRive),
        string.Empty,
        propertyChanged: ApplySourceProperty);

    private static void ApplySourceProperty(BindableObject bindable, object oldvalue, object newvalue)
    {
        if (bindable is SkiaRive control)
            Task.Run(async () =>
            {
                var result = await control.LoadSource(control.Source);
                if (result) control.Update();
            });
    }

    public string Source
    {
        get => (string)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }


    public static readonly BindableProperty AutoPlayProperty = BindableProperty.Create(nameof(AutoPlay),
        typeof(bool),
        typeof(SkiaRive),
        true);

    public bool AutoPlay
    {
        get => (bool)GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    #endregion
}