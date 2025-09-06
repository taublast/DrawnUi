using DrawnUi.Views;
using DrawnUi.Controls;
using DrawnUi.Draw;
using Canvas = DrawnUi.Views.Canvas;
using SkiaSharp;
using System.Diagnostics;


namespace Sandbox
{
    /// <summary>
    /// Grid-based sprite board demo: a 44x44pt tile board where a player sprite can move and an enemy is placed.
    /// Assets can be swapped later; currently both use the Warrior_Idle placeholder.
    /// </summary>
    public class SpriteBoardPage : BasePageReloadable, IDisposable
    {
        const int TileSize = 80;


        Canvas _canvas;
        SkiaLayer _root;
        GridBackground _grid;
        WarriorSprite _player;
        EnemySprite _enemy;
        SkiaLabel _info;

        int _playerCol = 1, _playerRow = 1;
        int _enemyCol = 3, _enemyRow = 1;


        // Board bounds cache (computed from grid size when needed)
        int _maxCol = 20, _maxRow = 12; // will be recomputed from _grid size on first move

        // Simple state/facing model
        enum Facing
        {
            Right,
            Left
        }

        enum Motion
        {
            Idle,
            Walk
        }

        Facing _facing = Facing.Right;
        Motion _motion = Motion.Idle;

        // Continuous movement state (while button is pressed)
        volatile bool _pressUp, _pressDown, _pressLeft, _pressRight;
        double _moveSpeed = 100; // points per second

        // Enemy AI / follow
        bool _enemyAngry = false; // becomes true after first combat
        double EnemySpeed => _moveSpeed * 0.8; // 20% slower than player

        // Debugging
        const bool DebugAI = true;
        double _aiDbgAcc = 0;


        // Frame-based game loop
        ActionOnTickAnimator _gameLoop;


        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                _gameLoop?.Stop();
                _gameLoop?.Dispose();
                _gameLoop = null;

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

            // Continuous movement while pressed; state is read in the GameLoop (no snapped tweening)
            btnUp.Pressed += (b, e) => { _pressUp = true; };
            btnUp.Released += (b, e) => { _pressUp = false; };
            btnDown.Pressed += (b, e) => { _pressDown = true; };
            btnDown.Released += (b, e) => { _pressDown = false; };
            btnLeft.Pressed += (b, e) => { _pressLeft = true; };
            btnLeft.Released += (b, e) => { _pressLeft = false; };
            btnRight.Pressed += (b, e) => { _pressRight = true; };
            btnRight.Released += (b, e) => { _pressRight = false; };

            _grid = new GridBackground
            {
                UseCache = SkiaCacheType.Operations,
                Tile = TileSize,
                BackgroundColor = Color.FromArgb("#0D1117"),
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
            };

            _player = new WarriorSprite { WidthRequest = TileSize, HeightRequest = TileSize, ZIndex = 10 };
            _player.WState = WarriorSprite.WarriorAnimState.IdleRight;

            _enemy = new EnemySprite { WidthRequest = TileSize, HeightRequest = TileSize, ZIndex = 9 };
            _enemy.EState = EnemySprite.EnemyAnimState.Idle;

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
            UpdateWarState();

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
                                Padding = new Thickness(0, 0, 0, 8),
                                Children = { title, _info }
                            },
                            new SkiaWrap
                            {
                                UseCache = SkiaCacheType.Image,
                                Spacing = 8,
                                Padding = new Thickness(16, 8),
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
                        Margin = new(0, 0, 4, 24),
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

            // Start game loop using DrawnUi.Maui.Game pattern
            StartGameLoop();

            Content = _canvas;
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


        void UpdateEnemyAI(double dt)
        {
            if (_enemy == null || _player == null) return;

            _aiDbgAcc += dt;
            bool doLog = DebugAI && _aiDbgAcc >= 0.2;

            UpdateBoundsFromGrid();

            // If colliding, enter/maintain combat; ensure enemy faces player
            bool colliding = IsColliding();
            if (colliding)
            {
                if (!_enemyAngry) _enemyAngry = true; // first collision triggers anger

                var faceRight = _player.TranslationX >= _enemy.TranslationX;
                _enemy.EState =
                    faceRight ? EnemySprite.EnemyAnimState.AttackRight : EnemySprite.EnemyAnimState.AttackLeft;
                if (doLog)
                {
                    var a0 = GetRect(_player);
                    var b0 = GetRect(_enemy);
                    //Debug.WriteLine($"[AI] collide=true angry={_enemyAngry} a=({a0.Left:F0},{a0.Top:F0},{a0.Width:F0}x{a0.Height:F0}) b=({b0.Left:F0},{b0.Top:F0},{b0.Width:F0}x{b0.Height:F0}) state={_enemy.EState}");
                    _aiDbgAcc = 0;
                }

                UpdateWarState();
                UpdateInfo();
                return; // do not move while attacking
            }

            // If not in combat
            if (_enemyAngry)
            {
                // Follow until the distance between hit rects is <= 2 tiles
                var a = GetRect(_player);
                var b = GetRect(_enemy);

                // Distance between rectangles' edges (0 if overlapping)
                float dxGap = 0, dyGap = 0;
                if (b.Left > a.Right) dxGap = b.Left - a.Right;
                else if (a.Left > b.Right) dxGap = a.Left - b.Right;
                if (b.Top > a.Bottom) dyGap = b.Top - a.Bottom;
                else if (a.Top > b.Bottom) dyGap = a.Top - b.Bottom;
                var gapDist = Math.Sqrt(dxGap * dxGap + dyGap * dyGap);

                // Threshold: 1 tile in rendered units (use player's hitrect width as 1 tile)
                var tileRendered = a.Width > 0 ? a.Width : TileSize;
                var threshold = tileRendered * 0.5;

                if (gapDist <= threshold)
                {
                    // Move towards player center
                    var (px, py) = GetCenter(_player);
                    var (ex, ey) = GetCenter(_enemy);
                    var dx = px - ex;
                    var dy = py - ey;
                    var dist = Math.Sqrt(dx * dx + dy * dy);

                    var vx = dx / (dist == 0 ? 1 : dist);
                    var vy = dy / (dist == 0 ? 1 : dist);

                    var newX = _enemy.TranslationX + vx * EnemySpeed * dt;
                    var newY = _enemy.TranslationY + vy * EnemySpeed * dt;

                    var maxX = _maxCol * TileSize;
                    var maxY = _maxRow * TileSize;
                    newX = Math.Clamp(newX, 0, maxX);
                    newY = Math.Clamp(newY, 0, maxY);

                    _enemy.TranslationX = newX;
                    _enemy.TranslationY = newY;

                    _enemyCol = Math.Max(0, Math.Min(_maxCol, (int)Math.Round(newX / TileSize)));
                    _enemyRow = Math.Max(0, Math.Min(_maxRow, (int)Math.Round(newY / TileSize)));

                    _enemy.EState =
                        (vx >= 0) ? EnemySprite.EnemyAnimState.WalkRight : EnemySprite.EnemyAnimState.WalkLeft;

                    if (doLog)
                    {
                        //Debug.WriteLine($"[AI] follow WALK angry={_enemyAngry} gap={gapDist:F1}/{threshold:F1} dist={dist:F1} v=({vx:F2},{vy:F2}) enemy=({newX:F1},{newY:F1}) state={_enemy.EState}");
                        _aiDbgAcc = 0;
                    }
                }
                else
                {
                    // Too far: stop following and drop aggro until next fight
                    _enemyAngry = false;
                    _enemy.EState = EnemySprite.EnemyAnimState.Idle;
                    if (doLog)
                    {
                        //Debug.WriteLine($"[AI] follow STOP (aggro off) angry={_enemyAngry} gap={gapDist:F1}/{threshold:F1} enemy=({_enemy.TranslationX:F1},{_enemy.TranslationY:F1}) state={_enemy.EState}");
                        _aiDbgAcc = 0;
                    }
                }
            }
            else
            {
                // Not angry yet: idle
                _enemy.EState = EnemySprite.EnemyAnimState.Idle;
                if (doLog)
                {
                    //Debug.WriteLine($"[AI] idle calm collide=false");
                    _aiDbgAcc = 0;
                }
            }

            // Refresh HUD
            UpdateInfo();
        }

        // Game loop like Breakout: per-frame update using ActionOnTickAnimator
        void StartGameLoop()
        {
            _gameLoop?.Stop();
            _gameLoop?.Dispose();
            _gameLoop = new ActionOnTickAnimator(_root, OnGameTick);
            _gameLoop.Start();
        }

        void OnGameTick(long frameTimeNanos)
        {
            // Convert to seconds and stabilize with FrameTimeInterpolator
            float currentSeconds = frameTimeNanos / 1_000_000_000.0f;
            float dt = FrameTimeInterpolator.Instance.GetDeltaTime(currentSeconds);

            // Update player movement from input flags and enemy AI each frame
            UpdateContinuousMovement(dt);
            UpdateEnemyAI(dt);
        }


        (double cx, double cy) GetCenter(SkiaControl c)
        {
            var rect = GetRect(c);
            return (rect.MidX, rect.MidY);
        }


        void UpdateContinuousMovement(double dt)
        {
            if (_player == null) return;

            // Determine input vector from key flags
            double vx = (_pressRight ? 1 : 0) - (_pressLeft ? 1 : 0);
            double vy = (_pressDown ? 1 : 0) - (_pressUp ? 1 : 0);

            bool anyInput = (vx != 0) || (vy != 0);
            if (!anyInput)
            {
                // No input: immediately reflect Idle/War state
                UpdateWarState();
                UpdateInfo();
                return;
            }

            // Normalize for diagonal movement
            var mag = Math.Sqrt(vx * vx + vy * vy);
            if (mag > 0)
            {
                vx /= mag;
                vy /= mag;
            }

            // Facing from horizontal intent
            if (vx > 0) _facing = Facing.Right;
            else if (vx < 0) _facing = Facing.Left;

            SetState(Motion.Walk, _facing);

            UpdateBoundsFromGrid();

            var maxX = _maxCol * TileSize;
            var maxY = _maxRow * TileSize;

            var newX = _player.TranslationX + vx * _moveSpeed * dt;
            var newY = _player.TranslationY + vy * _moveSpeed * dt;

            newX = Math.Clamp(newX, 0, maxX);
            newY = Math.Clamp(newY, 0, maxY);

            _player.TranslationX = newX;
            _player.TranslationY = newY;

            _playerCol = Math.Max(0, Math.Min(_maxCol, (int)Math.Round(newX / TileSize)));
            _playerRow = Math.Max(0, Math.Min(_maxRow, (int)Math.Round(newY / TileSize)));

            UpdateWarState();
            UpdateInfo();
        }

        void SetState(Motion motion, Facing facing)
        {
            _motion = motion;
            _facing = facing;
            if (_player == null) return;

            var ws = (motion, facing) switch
            {
                (Motion.Walk, Facing.Right) => WarriorSprite.WarriorAnimState.WalkRight,
                (Motion.Walk, Facing.Left) => WarriorSprite.WarriorAnimState.WalkLeft,
                (Motion.Idle, Facing.Left) => WarriorSprite.WarriorAnimState.IdleLeft,
                _ => WarriorSprite.WarriorAnimState.IdleRight,
            };

            _player.WState = ws;
        }

        SKRect GetRect(SkiaControl c)
        {
            if (c == null) return SKRect.Empty;
            var w = (float)(c.Width > 0 ? c.Width : c.WidthRequest);
            var h = (float)(c.Height > 0 ? c.Height : c.HeightRequest);
            var x = (float)c.TranslationX;
            var y = (float)c.TranslationY;
            return new SKRect(x, y, x + w, y + h);
        }

        bool IsColliding()
        {
            // Axis-aligned bounding box intersection
            var a = GetRect(_player);
            var b = GetRect(_enemy);
            return a.IntersectsWith(b);
        }

        void UpdateWarState()
        {
            if (_player == null || _enemy == null) return;

            var overlapping = IsColliding();
            if (overlapping)
            {
                if (!_enemyAngry) _enemyAngry = true; // first collision triggers anger

                _player.WState = _facing == Facing.Left
                    ? WarriorSprite.WarriorAnimState.WarLeft
                    : WarriorSprite.WarriorAnimState.WarRight;
                var faceRight = _player.TranslationX >= _enemy.TranslationX;
                _enemy.EState =
                    faceRight ? EnemySprite.EnemyAnimState.AttackRight : EnemySprite.EnemyAnimState.AttackLeft;
            }
            else
            {
                // Outside collision, let AI control the enemy EState; only update player's state
                if (_pressUp || _pressDown || _pressLeft || _pressRight)
                    SetState(Motion.Walk, _facing);
                else
                    SetState(Motion.Idle, _facing);
            }
        }

        class EnemySprite : SkiaSpriteSet
        {
            public enum EnemyAnimState
            {
                Idle,
                WalkLeft,
                WalkRight,
                AttackLeft,
                AttackRight
            }

            public EnemyAnimState EState
            {
                get => _estate;
                set
                {
                    if (_estate == value) return;
                    _estate = value;
                    State = value switch
                    {
                        EnemyAnimState.Idle => 0,
                        EnemyAnimState.WalkLeft or EnemyAnimState.WalkRight => 1,
                        _ => 2,
                    };
                    ApplyMirror();
                }
            }

            private EnemyAnimState _estate;

            void ApplyMirror()
            {
                if (CurrentSprite != null)
                {
                    var left = (_estate == EnemyAnimState.WalkLeft || _estate == EnemyAnimState.AttackLeft);
                    CurrentSprite.ScaleX = left ? -1 : 1;
                }
            }

            public EnemySprite()
            {
                Define(0, "Anims/RedWarrior/Warrior_Idle.png", columns: 8, rows: 1, fps: 15);
                Define(1, "Anims/RedWarrior/Warrior_Run.png", columns: 6, rows: 1, fps: 15);
                Define(2, "Anims/RedWarrior/Warrior_Attack1.png", columns: 4, rows: 1, fps: 8);
                EState = EnemyAnimState.Idle;
            }

            protected override void OnChangeState(int oldState, int newState)
            {
                base.OnChangeState(oldState, newState);
                ApplyMirror();
            }
        }


        void UpdateInfo()
        {
            var collide = IsColliding();
            _info.Text = $"Player: ({_playerCol},{_playerRow})  Enemy: ({_enemyCol},{_enemyRow})" +
                         (collide ? "  |  Collision!" : string.Empty);
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
