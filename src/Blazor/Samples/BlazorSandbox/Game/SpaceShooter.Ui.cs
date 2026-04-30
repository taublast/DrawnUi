// NOTE: Parts of the code below are based on
// https://www.mooict.com/wpf-c-tutorial-create-a-space-battle-shooter-game-in-visual-studio/7/

using DrawnUi;
using DrawnUi.Draw;
using DrawnUi.Views;
using SkiaSharp;

namespace SpaceShooter.Game;

public partial class SpaceShooter : DrawnUi.Gaming.Game
{
    // Named controls referenced from SpaceShooter.cs game logic
    private SkiaImageTiles ParallaxLayer;
    private SkiaImage Player;
    private SkiaLottie PlayerShield;
    private SkiaLottie PlayerShieldExplosion;
    private HealthBar HealthBar;
    private SkiaLabel LabelScore;
    private SkiaLabel LabelHiScore;

    public SpaceShooter()
    {
        BackgroundColor = Color.FromHex("#000011");
        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Tag = "Game";

        Children = new List<SkiaControl>()
        {
            // STARS BACKGROUND
            new SkiaLayout()
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                UseCache = SkiaCacheType.Image,
                ZIndex = -1,
                Children = new List<SkiaControl>()
                {
                    new SkiaImage()
                    {
                        Aspect = TransformAspect.AspectCover,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Opacity = 0.375,
                        Source = "Space/Sprites/nebula.jpg",
                    },
                    new SkiaImageTiles()
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Opacity = 0.5,
                        Source = "Space/Sprites/stars.png",
                        TileAspect = TransformAspect.Cover,
                        TileWidth = 300,
                        TileHeight = 300,
                    },
                },
            },

            // STARS PARALLAX
            new SkiaImageTiles()
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Source = "Space/Sprites/stars.png",
                Tag = "ParallaxLayer",
                TileAspect = TransformAspect.Cover,
                TileCacheType = SkiaCacheType.Image,
                TileWidth = 600,
                TileHeight = 600,
            }.Assign(out ParallaxLayer),

            // SCORE
            new SkiaLabel()
            {
                Margin = new Thickness(16),
                BackgroundColor = Colors.Transparent,
                FillBlendMode = SKBlendMode.Color,
                FontFamily = "FontGameExtraBold",
                FontSize = 24,
                HorizontalOptions = LayoutOptions.Start,
                MaxLines = 1,
                StrokeColor = Colors.Black,
                StrokeWidth = 2,
                TextColor = Colors.White,
                UseCache = SkiaCacheType.Image,
                ZIndex = 110,
                FillGradient = new SkiaGradient()
                {
                    Type = GradientType.Linear,
                    StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
                    Colors = new List<Color>
                    {
                        Colors.White,
                        Color.FromHex("#FFFF00"),
                        Colors.Orange,
                        Colors.Red,
                        Colors.DarkRed,
                    },
                },
            }.Assign(out LabelScore),

            // HI SCORE
            new SkiaLabel()
            {
                Margin = new Thickness(16),
                BackgroundColor = Colors.Transparent,
                FillBlendMode = SKBlendMode.Color,
                FontFamily = "FontGameExtraBold",
                FontSize = 24,
                HorizontalOptions = LayoutOptions.End,
                MaxLines = 1,
                Opacity = 0.9,
                StrokeColor = Colors.Black,
                StrokeWidth = 2,
                TextColor = Colors.White,
                UseCache = SkiaCacheType.Image,
                ZIndex = 110,
                FillGradient = new SkiaGradient()
                {
                    Type = GradientType.Linear,
                    StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
                    Colors = new List<Color>
                    {
                        Colors.White,
                        Color.FromHex("#FFFF00"),
                        Colors.Orange,
                        Colors.Red,
                        Colors.DarkRed,
                    },
                },
            }.Assign(out LabelHiScore),

            // PLAYER
            new SkiaImage()
            {
                Aspect = TransformAspect.AspectFitFill,
                HeightRequest = 50,
                HorizontalOptions = LayoutOptions.Center,
                Source = "Space/Sprites/player.png",
                TranslationY = -40,
                UseCache = SkiaCacheType.Image,
                VerticalOptions = LayoutOptions.End,
                WidthRequest = 60,
                ZIndex = 5,
            }.Assign(out Player),

            // SHIELD EXPLOSION
            new SkiaLottie()
            {
                AutoPlay = false,
                DefaultFrame = -1,
                HorizontalOptions = LayoutOptions.Center,
                LockRatio = 1,
                Opacity = 0.75,
                Source = "Space/Lottie/crash.json",
                SpeedRatio = 0.6,
                Tag = "ShieldExplosion",
                TranslationX = 0,
                TranslationY = -44,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                VerticalOptions = LayoutOptions.End,
                WidthRequest = 110,
                ZIndex = 4,
            }.Assign(out PlayerShieldExplosion),

            // PLAYER SHIELD
            new SkiaLottie()
            {
                AutoPlay = true,
                HorizontalOptions = LayoutOptions.Center,
                LockRatio = 1,
                Opacity = 0.66,
                Repeat = -1,
                Source = "Space/Lottie/shield.json",
                SpeedRatio = 0.5,
                Tag = "PlayerShield",
                TranslationY = 6,
                UseCache = SkiaCacheType.Operations,
                VerticalOptions = LayoutOptions.End,
                WidthRequest = 130,
                ZIndex = 4,
            }.Assign(out PlayerShield),

            // HEALTH BAR
            new HealthBar()
            {
                HeightRequest = 4,
                HorizontalOptions = LayoutOptions.Center,
                Tag = "Health",
                TranslationY = -28,
                UseCache = SkiaCacheType.ImageDoubleBuffered,
                VerticalOptions = LayoutOptions.End,
                WidthRequest = 40,
                ZIndex = 6,
            }
            .ObserveProperty(this, nameof(Health), me => { me.Value = Health; })
            .Assign(out HealthBar),

            // MOTHER EARTH
            new SkiaImage()
            {
                Aspect = TransformAspect.AspectCover,
                HeightRequest = 45,
                HorizontalOptions = LayoutOptions.Fill,
                Opacity = 0.95,
                Source = "Space/Sprites/earth.png",
                UseCache = SkiaCacheType.GPU,
                VerticalOffset = 26,
                VerticalOptions = LayoutOptions.End,
                ZIndex = 1,
            },

            // DIALOG
            new SkiaLayout()
            {
                Margin = new Thickness(50),
                HeightRequest = -1,
                HorizontalOptions = LayoutOptions.Center,
                MinimumHeightRequest = 50,
                MinimumWidthRequest = 300,
                VerticalOptions = LayoutOptions.Center,
                ZIndex = 200,
                Children = new List<SkiaControl>()
                {
                    // background layer
                    new SkiaLayout()
                    {
                        Padding = new Thickness(16),
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        ZIndex = -1,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                CornerRadius = 14,
                                HorizontalOptions = LayoutOptions.Fill,
                                VerticalOptions = LayoutOptions.Fill,
                                BackgroundColor = Color.FromHex("#22000000"),
                            },
                        },
                    },

                    // front layer cached
                    new SkiaLayout()
                    {
                        Padding = new Thickness(17),
                        HorizontalOptions = LayoutOptions.Fill,
                        UseCache = SkiaCacheType.GPU,
                        Children = new List<SkiaControl>()
                        {
                            new SkiaShape()
                            {
                                Margin = new Thickness(0),
                                BackgroundColor = Colors.Black,
                                ClipBackgroundColor = true,
                                CornerRadius = 17,
                                HorizontalOptions = LayoutOptions.Fill,
                                StrokeColor = Colors.Black,
                                StrokeWidth = 1,
                                VerticalOptions = LayoutOptions.Start,
                                StrokeGradient = new SkiaGradient()
                                {
                                    Type = GradientType.Linear,
                                    StartXRatio = 0, StartYRatio = 0, EndXRatio = 1, EndYRatio = 1,
                                    Colors = new List<Color>
                                    {
                                        Color.FromHex("#6666ff66"),
                                        Color.FromHex("#66339933"),
                                    },
                                },
                                Shadows = new List<SkiaShadow>()
                                {
                                    new SkiaShadow()
                                    {
                                        Blur = 8,
                                        Opacity = 0.15,
                                        X = 0, Y = 0,
                                        Color = Color.FromHex("#00ff00"),
                                    },
                                },
                                Children = new List<SkiaControl>()
                                {
                                    new SkiaLayout()
                                    {
                                        HorizontalOptions = LayoutOptions.Fill,
                                        VerticalOptions = LayoutOptions.Start,
                                        Children = new List<SkiaControl>()
                                        {
                                            new SkiaImage()
                                            {
                                                Aspect = TransformAspect.AspectCover,
                                                HorizontalOptions = LayoutOptions.Fill,
                                                VerticalOptions = LayoutOptions.Fill,
                                                Opacity = 0.25,
                                                Source = "Space/Sprites/nebula.jpg",
                                                ZIndex = -1,
                                            },

                                            // dialog content column
                                            new SkiaLayout()
                                            {
                                                Type = LayoutType.Column,
                                                Padding = new Thickness(16, 20, 16, 24),
                                                HorizontalOptions = LayoutOptions.Fill,
                                                Spacing = 24,
                                                VerticalOptions = LayoutOptions.Start,
                                                Children = new List<SkiaControl>()
                                                {
                                                    // dialog message
                                                    new SkiaLabel()
                                                    {
                                                        CharacterSpacing = 2.5,
                                                        FontFamily = "FontGameExtraBold",
                                                        FontSize = 20,
                                                        HorizontalOptions = LayoutOptions.Center,
                                                        HorizontalTextAlignment = DrawTextAlignment.Center,
                                                        LineSpacing = 1.2,
                                                        StrokeColor = Color.FromHex("#44FF0000"),
                                                        StrokeWidth = 1.5,
                                                        TextColor = Color.FromHex("#33FF44"),
                                                        VerticalOptions = LayoutOptions.Start,
                                                        FillGradient = new SkiaGradient()
                                                        {
                                                            Type = GradientType.Linear,
                                                            StartXRatio = 0, StartYRatio = 0, EndXRatio = 0, EndYRatio = 1,
                                                            Colors = new List<Color>
                                                            {
                                                                Colors.White,
                                                                Color.FromHex("#FFFF00"),
                                                                Colors.Orange,
                                                                Colors.Red,
                                                                Colors.DarkRed,
                                                            },
                                                        },
                                                    }.ObserveProperty(this, nameof(DialogMessage), me => { me.Text = DialogMessage; }),

                                                    // OK button
                                                    new SkiaShape()
                                                    {
                                                        BackgroundColor = Color.FromHex("#33000000"),
                                                        CornerRadius = 8,
                                                        HeightRequest = 34,
                                                        HorizontalOptions = LayoutOptions.Center,
                                                        StrokeColor = Color.FromHex("#cc33FF44"),
                                                        StrokeWidth = 0.55,
                                                        Tag = "DialogBtn",
                                                        WidthRequest = 90,
                                                        Children = new List<SkiaControl>()
                                                        {
                                                            new SkiaLabel()
                                                            {
                                                                CharacterSpacing = 1.75,
                                                                FontFamily = "FontGameMedium",
                                                                FontSize = 16,
                                                                HorizontalOptions = LayoutOptions.Center,
                                                                HorizontalTextAlignment = DrawTextAlignment.Center,
                                                                StrokeColor = Color.FromHex("#2233FF44"),
                                                                StrokeWidth = 1.5,
                                                                TextColor = Color.FromHex("#cc33FF44"),
                                                                VerticalOptions = LayoutOptions.Center,
                                                            }.ObserveProperty(this, nameof(DialogButton), me => { me.Text = DialogButton; }),
                                                        },
                                                    }.OnTapped(me => { CommandPressedOk.Execute(null); }),
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            }.ObserveProperty(this, nameof(ShowDialog), me => { me.IsVisible = ShowDialog; }),
        };

        BindingContext = this;

        Instance = this;
    }
}
