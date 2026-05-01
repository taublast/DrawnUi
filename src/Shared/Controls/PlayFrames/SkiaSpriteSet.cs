using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DrawnUi.Controls;

/// <summary>
/// Stateful sprite switcher that PRE-CREATES one SkiaSprite per integer state via Define().
/// Base OnChangeState swaps the active child to the precreated sprite atomically.
/// Subclasses can override OnChangeState, call base, then adjust CurrentSprite (e.g., ScaleX).
/// </summary>
public class SkiaSpriteSet : ContentLayout
{
    readonly Dictionary<int, SkiaSprite> _sprites = new();
    SkiaSprite _active;

    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(int), typeof(SkiaSpriteSet), 0,
        propertyChanged: (b, o, n) => ((SkiaSpriteSet)b).ApplyState((int)o, (int)n));

    /// <summary>
    /// Current integer state. Setting this triggers OnChangeState.
    /// </summary>
    public int State
    {
        get => (int)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>
    /// Create and register a sprite for a state, preconfiguring Source, Columns, Rows, FPS and Repeat.
    /// If this state equals the current State and no active sprite exists yet, it becomes active immediately.
    /// </summary>
    public SkiaSpriteSet Define(int state, string source, int columns, int rows, double fps = 15, int repeat = -1, bool autoPlay = true)
    {
        var s = new SkiaSprite
        {
            UseCache = SkiaCacheType.Image,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            AutoPlay = autoPlay,
            Repeat = repeat,
            FramesPerSecond = fps,
            Columns = columns,
            Rows = rows,
            Source = source,
        };

        _sprites[state] = s;

        if (_active == null && state == State)
        {
            SetActive(s);
        }

        return this;
    }

    /// <summary>
    /// The currently active SkiaSprite instance.
    /// </summary>
    public SkiaSprite CurrentSprite => _active;

    void SetActive(SkiaSprite sprite)
    {
        if (_active == sprite) return;
        if (_active != null)
        {
            // Detach previous
            if (ReferenceEquals(Content, _active)) Content = null;
            _active.Stop();
        }

        _active = sprite;
        Content = _active;

        // Start if frames are ready; otherwise AutoPlay starts on load
        if (_active.TotalFrames > 0)
            _active.Start();
    }

    void ApplyState(int oldState, int newState)
    {
        OnChangeState(oldState, newState);
        Invalidate();
    }

    /// <summary>
    /// Base: swap active child to the precreated sprite for newState.
    /// Subclasses should call base first, then adjust CurrentSprite (e.g., ScaleX).
    /// </summary>
    protected virtual void OnChangeState(int oldState, int newState)
    {
        if (_sprites.TryGetValue(newState, out var sprite))
        {
            SetActive(sprite);
        }
    }
}

