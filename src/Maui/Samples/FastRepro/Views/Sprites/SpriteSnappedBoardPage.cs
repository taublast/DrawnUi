using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using SkiaSharp;

namespace Sandbox
{
    /// <summary>
    /// Grid-based sprite board demo: a 44x44pt tile board where a player sprite can move and an enemy is placed.
    /// Assets can be swapped later; currently both use the Warrior_Idle placeholder.
    /// </summary>
    public class SpriteSnappedBoardPage : BasePageReloadable, IDisposable
    {
        const int TileSize = 80; // points/pixels (device independent as per DrawnUi scale)
        const string DefaultSpriteResource = "Anims/BlueWarrior/Warrior_Idle.png";

        Canvas _canvas;
        SkiaLayer _root;
        GridBackground _grid;
        WarriorSprite _player;
        SkiaSprite _enemy;
        SkiaLabel _info;

        int _playerCol = 1, _playerRow = 1;
        int _enemyCol = 3, _enemyRow = 3;


        // Board bounds cache (computed from grid size when needed)
        int _maxCol = 20, _maxRow = 12; // will be recomputed from _grid size on first move

        // Simple state/facing model
        enum Facing { Right, Left }
        enum Motion { Idle, Walk }
        Facing _facing = Facing.Right;
        Motion _motion = Motion.Idle;
        bool _isMoving = false;
        CancellationTokenSource _moveCts;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Content = null;
                _canvas?.Dispose();
            }
            base.Dispose(isDisposing);
        }

        public override void Build()
        {
            _canvas?.Dispose();

            var title = new SkiaLabel
            {
                Text = "Sprite Board (44x44)",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(16, 16, 16, 8)
            };

            _info = new SkiaLabel
            {
                FontSize = 12,
                TextColor = Colors.LightGray,
                HorizontalOptions = LayoutOptions.Fill,
                Margin = new Thickness(12, 0, 12, 8)
            };

            var btnUp = new SkiaButton { Text = "↑" };
            var btnDown = new SkiaButton { Text = "↓" };
            var btnLeft = new SkiaButton { Text = "←" };
            var btnRight = new SkiaButton { Text = "→" };

            btnUp.Tapped += async (s, e) => await MovePlayerAsync(0, -1);
            btnDown.Tapped += async (s, e) => await MovePlayerAsync(0, 1);
            btnLeft.Tapped += async (s, e) => await MovePlayerAsync(-1, 0);
            btnRight.Tapped += async (s, e) => await MovePlayerAsync(1, 0);

            _grid = new GridBackground
            {
                UseCache = SkiaCacheType.Operations,
                Tile = TileSize,
                BackgroundColor = Color.FromArgb("#0D1117"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };

            _player = new WarriorSprite
            {
                WidthRequest = TileSize,
                HeightRequest = TileSize,
                ZIndex = 10
            };
            _player.WState = WarriorSprite.WarriorAnimState.IdleRight;

            _enemy = new SkiaSprite
            {
                UseCache = SkiaCacheType.Image,
                WidthRequest = TileSize,
                HeightRequest = TileSize,
                AutoPlay = true,
                Repeat = -1,
                FramesPerSecond = 15,
                Columns = 8,
                Rows = 1,
                ZIndex = 9
            };
            _enemy.Source = "Anims/RedWarrior/Warrior_Idle.png";

            var boardLayer = new SkiaLayer
            {
                // Absolute layout by default; we position sprites via TranslationX/Y
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = { _grid, _enemy, _player }
            };

            // Initial placement
            PlaceOnGrid(_player, _playerCol, _playerRow);
            PlaceOnGrid(_enemy, _enemyCol, _enemyRow);
            SetState(Motion.Idle, Facing.Right);
            UpdateInfo();

            _root = new SkiaLayer
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
                                Children = { title, _info }
                            },
                            new SkiaWrap
                            {
                                UseCache = SkiaCacheType.Image,
                                Spacing = 8,
                                Padding = new Thickness(16,8),
                                BackgroundColor = Color.FromArgb("#161B22"),
                                Children = { btnUp, btnDown, btnLeft, btnRight }
                            },
                            new SkiaLayer
                            {
                                VerticalOptions = LayoutOptions.Fill,
                                HorizontalOptions = LayoutOptions.Fill,
                                Padding = new Thickness(12),
                                Children = { boardLayer }
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
            };

            _canvas = new Canvas
            {
                RenderingMode = RenderingModeType.Accelerated,
                Gestures = GesturesMode.Enabled,
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Color.FromArgb("#0D1117"),
                Content = _root
            };

            Content = _canvas;
        }

        async Task MovePlayerAsync(int dx, int dy)
        {
            if (_player == null || _grid == null) return;
            if (_isMoving) return; // ignore while in motion

            UpdateBoundsFromGrid();

            // Determine target tile with clamping
            var (targetCol, targetRow) = ClampTarget(_playerCol + dx, _playerRow + dy);
            if (targetCol == _playerCol && targetRow == _playerRow)
            {
                // Still update facing when pressing left/right without moving
                if (dx > 0) _facing = Facing.Right;
                else if (dx < 0) _facing = Facing.Left;
                SetState(Motion.Idle, _facing);
                return;
            }

            // Choose facing based on horizontal intent, keep previous if vertical only
            if (dx > 0) _facing = Facing.Right;
            else if (dx < 0) _facing = Facing.Left;

            // Enter walking state
            SetState(Motion.Walk, _facing);

            // Compute pixel target
            var targetX = targetCol * TileSize;
            var targetY = targetRow * TileSize;

            _isMoving = true;
            _moveCts?.Cancel(); _moveCts?.Dispose();
            _moveCts = new CancellationTokenSource();

            try
            {
                var easing = Easing.CubicOut;
                var ms = 2000f; // speed per tile
                await _player.TranslateToAsync(targetX, targetY, ms, easing, _moveCts);

                _playerCol = targetCol;
                _playerRow = targetRow;
            }
            catch (TaskCanceledException) { }
            finally
            {
                _isMoving = false;
                // Back to idle in the same facing
                SetState(Motion.Idle, _facing);
                UpdateInfo();
            }
        }

        void PlaceOnGrid(SkiaControl control, int col, int row)
        {
            control.HorizontalOptions = LayoutOptions.Start;
            control.VerticalOptions = LayoutOptions.Start;
            control.TranslationX = col * TileSize;
            control.TranslationY = row * TileSize;
        }


        void UpdateBoundsFromGrid()
        {
            if (_grid == null) return;
            var r = _grid.RenderedAtDestination;
            if (r.Width <= 0 || r.Height <= 0) return;
            var cols = (int)Math.Floor(r.Width / TileSize);
            var rows = (int)Math.Floor(r.Height / TileSize);
            _maxCol = Math.Max(0, cols - 1);
            _maxRow = Math.Max(0, rows - 1);
        }

        (int Col, int Row) ClampTarget(int col, int row)
        {
            var c = Math.Max(0, Math.Min(_maxCol, col));
            var r = Math.Max(0, Math.Min(_maxRow, row));
            return (c, r);
        }

        void SetState(Motion motion, Facing facing)
        {
            _motion = motion; _facing = facing;
            if (_player == null) return;

            var ws = (motion, facing) switch
            {
                (Motion.Walk, Facing.Right) => WarriorSprite.WarriorAnimState.WalkRight,
                (Motion.Walk, Facing.Left)  => WarriorSprite.WarriorAnimState.WalkLeft,
                (Motion.Idle, Facing.Left)  => WarriorSprite.WarriorAnimState.IdleLeft,
                _                           => WarriorSprite.WarriorAnimState.IdleRight,
            };

            _player.WState = ws;
        }

        void UpdateInfo()
        {
            var collide = (_playerCol == _enemyCol) && (_playerRow == _enemyRow);
            _info.Text = $"Player: ({_playerCol},{_playerRow})  Enemy: ({_enemyCol},{_enemyRow})" +
                         (collide ? "  |  Collision!" : string.Empty);
        }

        /// <summary>
        /// Warrior-specific mapping from integer states to sprite sheet + layout and mirroring.
        /// Subclassing SkiaSpriteSet ensures Source swaps are atomic per state, while geometry
        /// (Columns/Rows) and mirroring are set here, in one place.
        /// </summary>
        class WarriorSprite : SkiaSpriteSet
        {
            public enum WarriorAnimState { IdleRight, IdleLeft, WalkRight, WalkLeft }

            public WarriorAnimState WState
            {
                get => _wstate;
                set
                {
                    if (_wstate == value) return;
                    _wstate = value;
                    // Map to base two-state model: 0 = idle, 1 = walk
                    State = (value == WarriorAnimState.IdleLeft || value == WarriorAnimState.IdleRight) ? 0 : 1;
                }
            }
            private WarriorAnimState _wstate;

            public WarriorSprite()
            {
                // Precreate exactly two states with their own sprites and geometry
                Define(0, "Anims/BlueWarrior/Warrior_Idle.png", columns: 8, rows: 1, fps: 15);
                Define(1, "Anims/BlueWarrior/Warrior_Run.png",  columns: 6, rows: 1, fps: 15);
                // Startup default
                WState = WarriorAnimState.IdleRight;
            }

            protected override void OnChangeState(int oldState, int newState)
            {
                // Swap active sprite first
                base.OnChangeState(oldState, newState);

                // Apply mirroring based on facing
                if (CurrentSprite != null)
                {
                    CurrentSprite.ScaleX = (WState == WarriorAnimState.IdleLeft || WState == WarriorAnimState.WalkLeft) ? -1 : 1;
                }
            }
        }

        /// <summary>
        /// Simple background that draws grid lines every Tile points.
        /// </summary>
        class GridBackground : SkiaControl
        {
            public int Tile { get; set; } = TileSize;
            public SKColor LineColor { get; set; } = new SKColor(60, 70, 80, 255);
            public float LineWidth { get; set; } = 1f;
            public float LineAlpha { get; set; } = 0.5f;

            protected override void Paint(DrawingContext ctx)
            {
                base.Paint(ctx);
                var canvas = ctx.Context.Canvas;
                var r = ctx.Destination;

                using var p = new SKPaint
                {

                    Color = LineColor.WithAlpha((byte)(255 * LineAlpha)),
                    IsAntialias = false,
                    StrokeWidth = LineWidth,
                    Style = SKPaintStyle.Stroke
                };

                // Vertical lines
                for (float x = r.Left; x <= r.Right; x += Tile)
                {
                    canvas.DrawLine(x, r.Top, x, r.Bottom, p);
                }
                // Horizontal lines
                for (float y = r.Top; y <= r.Bottom; y += Tile)
                {
                    canvas.DrawLine(r.Left, y, r.Right, y, p);
                }
            }
        }
    }
}

