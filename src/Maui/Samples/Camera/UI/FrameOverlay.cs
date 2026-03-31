using System.Diagnostics.Metrics;
using AppoMobi.Specials;
using CameraTests.Views;
using CameraTests.Visualizers;
using DrawnUi.Camera;
using DrawnUi.Controls;
using TerraFX.Interop.Windows;

namespace CameraTests.UI;

/// <summary>
/// Video frame overlay that renders an audio EQ visualizer (and optionally other data)
/// directly into the camera preview and recording frames.
///
/// Extends <see cref="CameraOverlayLayout"/> so orientation transforms are handled
/// automatically — child content only needs to be laid out for portrait.
/// </summary>
public class FrameOverlay : CameraOverlayLayout, IAppOverlay
{
    public SkiaLabel CaptionsLabel => _captionsLabel;

    public AudioVisualizer Visualizer
    {
        get => visualizer;
        set
        {
            if (Equals(value, visualizer))
            {
                return;
            }

            visualizer = value;
            OnPropertyChanged();
        }
    }

    private SkiaLabel _labelVisualizerName;
    private SkiaLabel _captionsLabel;
    private SkiaShape _captionsPanel;
    private AudioVisualizer visualizer;
    private SkiaShape panelVisualizer;

    public FrameOverlay()
    {
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            //can place dimmer whatever here

            //then..

            // Double-buffered wrapper: caches transformed content so each frame encoder
            // thread gets a fast snapshot without stalling on layout work.
            new SkiaLayer()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                Children =
                {
                    new CameraOverlayContent().Assign(out Content)
                }
            }
        };

        CreateChildren(Content);

        // Keep the label in sync with the current visualizer name
        _labelVisualizerName.ObserveProperty(
            () => Visualizer,
            nameof(Visualizer.VisualizerName),
            me => me.Text = Visualizer?.VisualizerName ?? string.Empty);
    }

    private void CreateChildren(SkiaControl parent)
    {
        parent.Children = new List<SkiaControl>
        {
            //EQ
            new SkiaShape()
            {
                Type = ShapeType.Rectangle,
                Margin = 16,
                Padding = new Thickness(12, 10, 12, 12),
                WidthRequest = 220,
                HeightRequest = 138,
                CornerRadius = 22,
                BackgroundColor = Color.FromArgb("#A60B1220"),
                StrokeWidth = 1,
                StrokeColor = Color.FromArgb("#3311C5BF"),
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.End,
                Children =
                {
                    new SkiaLabel("AUDIO EQ")
                    {
                        FontSize = 12,
                        CharacterSpacing = 1,
                        TextColor = Color.FromArgb("#7DEAE5"),
                        UseCache = SkiaCacheType.Operations,
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                    },
                    new SkiaLabel()
                        {
                            Margin = new Thickness(0, 18, 0, 0),
                            FontSize = 11,
                            TextColor = Color.FromArgb("#A7B5C6"),
                            UseCache = SkiaCacheType.Operations,
                            HorizontalOptions = LayoutOptions.Start,
                            VerticalOptions = LayoutOptions.Start,
                        }
                        .Assign(out _labelVisualizerName),
                    new AudioVisualizer()
                        {
                            Margin = new Thickness(0, 42, 0, 0),
                            HorizontalOptions = LayoutOptions.Fill,
                            VerticalOptions = LayoutOptions.Fill,
                        }
                        .Assign(out visualizer)
                }
            }.Assign(out panelVisualizer),
            new SkiaShape()
            {
                UseCache = SkiaCacheType.Image,
                Type = ShapeType.Rectangle,
                CornerRadius = 26,
                Margin = new Thickness(20, 0, 20, 20),
                Padding = new Thickness(20, 16, 20, 18),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                BackgroundColor = Color.FromArgb("#B30A101A"),
                StrokeColor = Color.FromArgb("#3342D9F6"),
                StrokeWidth = 1,
                Children =
                {
                    new SkiaRichLabel()
                        {
                            FontFamily = "FontText",
                            FontSize = 20,
                            LineHeight = 1.1,
                            TextColor = Colors.White,
                            UseCache = SkiaCacheType.Operations,
                            Margin = new Thickness(0, 0, 0, 0),
                        }
                        .Assign(out _captionsLabel)
                }
            }.Assign(out _captionsPanel),
        };
    }

    public void AddAudioSample(AudioSample sample)
    {
        if (Visualizer != null && panelVisualizer.IsVisible && Visualizer.IsVisible)
        {
            Visualizer.AddSample(sample);
        }
    }

    public string SwitchVisualizer(int index = -1)
    {
        return Visualizer?.SwitchVisualizer(index);
    }

    public void SetAudioMonitoring(bool isAudioMonitoringEnabled)
    {
        panelVisualizer.IsVisible = isAudioMonitoringEnabled;
    }


    public void SetCaptions(IList<string> spans)
    {
        var hide = spans.Count < 1;
        if (hide)
        {
            if (!string.IsNullOrEmpty(CaptionsLabel.Text))
            {
                SetCaptionsVisible(false); //animated
            }
            else
            {
                CaptionsLabel.Text = string.Empty;
            }
        }
        else
        {
            SetCaptionsVisible(true);
            CaptionsLabel.Text = string.Join(Environment.NewLine, spans);
        }
    }

    private void SetCaptionsVisibleInternal(bool isVisible)
    {
        if (isVisible)
        {
            isVisible = !string.IsNullOrEmpty(CaptionsLabel.Text) && _canShowCaptions;
        }

        if (!isVisible)
        {
            if (!string.IsNullOrEmpty(CaptionsLabel.Text))
            {
                AnimateOut(_captionsPanel);
                return;
            }
        }

        _captionsPanel.IsVisible = isVisible;
    }

    private bool _canShowCaptions;

    public void SetCaptionsVisible(bool isVisible)
    {
        _canShowCaptions = isVisible;
        SetCaptionsVisibleInternal(isVisible);
    }


    void AnimateOut(SkiaControl control)
    {
        var animExit = new AnimatedShaderEffect()
        {
            UseBackground = PostRendererEffectUseBackgroud.Once,
            ShaderSource = MauiProgram.ShaderRemoveCaption,
            DurationMs = 400
        };

        animExit.Completed += (sender, args) =>
        {
            control.IsVisible = false;
            control.VisualEffects.Remove(animExit);
            CaptionsLabel.Text = string.Empty;
            control.DisposeObject(animExit);
        };

        if (!control.VisualEffects.Contains(animExit))
        {
            control.VisualEffects.Add(animExit);
        }

        animExit.Play();
    }
}
