using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using Microsoft.Maui.Storage;

namespace Sandbox
{
    /// <summary>
    /// Page to test SkiaSprite with a shipped resource default and file browsing to change source
    /// </summary>
    public class SpriteTestPage : BasePageReloadable, IDisposable
    {
        Canvas Canvas;
        SkiaSprite Sprite;
        SkiaLabel InfoLabel;
        SkiaButton BrowseButton;
        SkiaButton PlayPauseButton;

        // Default shipped resource inside this FastRepro app
        const string DefaultSpriteResource = "Anims/BlueWarrior/Warrior_Idle.png"; 

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }
            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            Canvas?.Dispose();

            BrowseButton = new SkiaButton { Text = "Browse spritesheet" };
            PlayPauseButton = new SkiaButton { Text = "Pause" };

            BrowseButton.Tapped += OnBrowseTapped;
            PlayPauseButton.Tapped += OnPlayPauseTapped;

            Sprite = new SkiaSprite
            {
                UseCache = SkiaCacheType.GPU,
                AutoPlay = true,
                Repeat = -1,
                FramesPerSecond = 15,
                Columns = 8,
                Rows = 1,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };

            // Startup default shipped resource
            Sprite.Source = DefaultSpriteResource;

            InfoLabel = new SkiaLabel
            {
                FontSize = 12,
                TextColor = Colors.LightGray,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(12, 4),
            };

            Canvas = new Canvas
            {
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.FromArgb("#0D1117"),
                Content = new SkiaLayer
                {
                    VerticalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        new SkiaStack
                        {
                            Type = LayoutType.Column,
                            VerticalOptions = LayoutOptions.Fill,
                            Children =
                            {
                                new SkiaStack
                                {
                                    UseCache = SkiaCacheType.Operations,
                                    Type = LayoutType.Column,
                                    BackgroundColor = Color.FromArgb("#161B22"),
                                    Padding = new Thickness(0,0,0,8),
                                    Children =
                                    {
                                        new SkiaLabel
                                        {
                                            Text = "SkiaSprite Test",
                                            FontSize = 20,
                                            FontWeight = FontWeights.Bold,
                                            TextColor = Colors.White,
                                            HorizontalOptions = LayoutOptions.Center,
                                            Margin = new Thickness(16,16,16,8)
                                        },
                                        InfoLabel
                                    }
                                },
                                new SkiaWrap
                                {
                                    UseCache = SkiaCacheType.Image,
                                    Spacing = 8,
                                    Padding = new Thickness(16,8),
                                    BackgroundColor = Color.FromArgb("#161B22"),
                                    Children = { BrowseButton, PlayPauseButton }
                                },
                                new SkiaLayer
                                {
                                    VerticalOptions = LayoutOptions.Fill,
                                    HorizontalOptions = LayoutOptions.Fill,
                                    Padding = new Thickness(12),
                                    Children = { Sprite }
                                }
                            }
                        },
#if DEBUG
                        new SkiaLabelFps
                        {
                            Margin = new(0,0,4,24),
                            VerticalOptions = LayoutOptions.End,
                            HorizontalOptions = LayoutOptions.End,
                            Rotation = -45,
                            BackgroundColor = Colors.DarkRed,
                            TextColor = Colors.White,
                            ZIndex = 110,
                        }
#endif
                    }
                }
            };

            this.Content = Canvas;

            UpdateInfoLabel();
        }

        void UpdateInfoLabel()
        {
            var src = Sprite?.Source ?? string.Empty;
            InfoLabel.Text = $"Source: {src} | FPS: {Sprite?.FramesPerSecond} | {Sprite?.Columns}x{Sprite?.Rows}";
        }

        async void OnBrowseTapped(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select spritesheet image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null)
                    return;

                string localPath = null;

                // If we have direct path use it, otherwise copy to cache
                if (!string.IsNullOrEmpty(result.FullPath) && File.Exists(result.FullPath))
                {
                    localPath = result.FullPath;
                }
                else
                {
                    using var stream = await result.OpenReadAsync();
                    var tempPath = Path.Combine(FileSystem.CacheDirectory, result.FileName);
                    using var fs = File.Create(tempPath);
                    await stream.CopyToAsync(fs);
                    localPath = tempPath;
                }

                if (!string.IsNullOrEmpty(localPath))
                {
                    // Use NativeFilePrefix so loader treats it as true local file path
                    Sprite.Source = SkiaImageManager.NativeFilePrefix + localPath;
                    UpdateInfoLabel();
                }
            }
            catch (Exception ex)
            {
                Super.Log(ex);
            }
        }

        void OnPlayPauseTapped(object sender, EventArgs e)
        {
            if (Sprite == null) return;

            if (Sprite.IsPlaying)
            {
                Sprite.Stop();
                PlayPauseButton.Text = "Play";
            }
            else
            {
                Sprite.Start();
                PlayPauseButton.Text = "Pause";
            }
        }
    }
}

