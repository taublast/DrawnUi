using CameraTests.Views;
using CameraTests.Visualizers;
using DrawnUi.Camera;

namespace CameraTests.UI;

/// <summary>
/// Video frame overlay that renders an audio EQ visualizer (and optionally other data)
/// directly into the camera preview and recording frames.
///
/// Extends <see cref="CameraOverlayLayout"/> so orientation transforms are handled
/// automatically — child content only needs to be laid out for portrait.
/// </summary>
public class CameraDataOverlay : CameraOverlayLayout, IAppOverlay
{
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
    private AudioVisualizer visualizer;
    private SkiaShape panelVisualizer;

    public CameraDataOverlay()
    {
        VerticalOptions = LayoutOptions.Fill;

        Children = new List<SkiaControl>
        {
            // Double-buffered wrapper: caches transformed content so each frame encoder
            // thread gets a fast snapshot without stalling on layout work.
            new SkiaLayer()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                Children =
                {
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
                    }.Assign(out panelVisualizer)
                }
            }
        };

        // Keep the label in sync with the current visualizer name
        _labelVisualizerName.ObserveProperty(
            () => Visualizer,
            nameof(Visualizer.VisualizerName),
            me => me.Text = Visualizer?.VisualizerName ?? string.Empty);
    }

    public void AddAudioSample(AudioSample sample)
    {
        if (Visualizer != null  && panelVisualizer.IsVisible && Visualizer.IsVisible)
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

}
