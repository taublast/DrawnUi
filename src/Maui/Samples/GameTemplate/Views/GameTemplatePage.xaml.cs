global using DrawnUi.Draw;
using DrawnUi.Draw;
using DrawnUi.Gaming;
using GameTemplate.Game;
using GameTemplate.Sprites;
using SkiaSharp;
using System.Collections.Concurrent;
using System.Windows.Input;
using AppoMobi.Maui.Gestures;

namespace GameTemplate.Views;

public partial class GameTemplatePage : MauiGame
{
    // Constants
    const int MAX_POOL = 64;
    const float PLAYER_SPEED = 260f; // points/sec

    // State
    public GameState State { get; private set; } = GameState.Unset;

    // Pools
    readonly Dictionary<Guid, PooledSprite> Pool = new(MAX_POOL);
    readonly List<SkiaControl> _toAdd = new(128);
    readonly ConcurrentQueue<SkiaControl> _toRemove = new();

    // Input flags
    volatile bool _left, _right, _up, _down;
    bool _isPressed;

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        Initialize();
    }

    void Initialize()
    {
        if (State != GameState.Unset)
            return;

        IgnoreChildrenInvalidations = true;
        Focus(); // enable keyboard on desktop

        // Pre-create reusable sprites
        for (int i = 0; i < MAX_POOL; i++)
        {
            var s = PooledSprite.Create();
            Pool.Add(s.Uid, s);
        }

        // Center player
        Player.TranslationX = 0;
        Player.TranslationY = -60; // relative to bottom center

        State = GameState.Ready;

        StartGame();
    }

    void StartGame()
    {
        State = GameState.Playing;
        StartLoop();
    }

    // Game loop
    public override void GameLoop(float dt)
    {
        base.GameLoop(dt);
        if (State != GameState.Playing)
            return;

        // Move player by input
        double dx = (_right ? 1 : 0) - (_left ? 1 : 0);
        double dy = (_up ? -1 : 0) - (_down ? -1 : 0);
        if (dx != 0 || dy != 0)
        {
            var len = Math.Sqrt(dx * dx + dy * dy);
            dx /= len; dy /= len;

            Player.TranslationX += dx * PLAYER_SPEED * dt;
            Player.TranslationY += dy * PLAYER_SPEED * dt;

            // clamp to visible bounds
            var halfW = Width / 2f - Player.Width / 2f;
            var halfH = Height / 2f - Player.Height / 2f;
            Player.TranslationX = Math.Clamp(Player.TranslationX, -halfW, halfW);
            Player.TranslationY = Math.Clamp(Player.TranslationY, -halfH, halfH);
        }

        // Update pooled sprites
        foreach (var view in Views)
        {
            if (view is PooledSprite s && s.IsActive)
            {
                // Deactivate if out of bounds
                if (s.TranslationY < -Height || s.TranslationY > Height || s.TranslationX < -Width || s.TranslationX > Width)
                {
                    ReturnToPool(s);
                    continue;
                }
                s.UpdatePosition(dt);
                s.UpdateState(LastFrameTimeNanos);
            }
        }

        // Apply queued adds
        if (_toAdd.Count > 0)
        {
            foreach (var add in _toAdd)
                AddSubView(add);
            _toAdd.Clear();
        }

        // Apply queued removes
        while (_toRemove.TryDequeue(out var sprite))
        {
            if (sprite is PooledSprite ps)
            {
                Pool.TryAdd(ps.Uid, ps);
            }
            RemoveSubView(sprite);
        }
    }

    void ReturnToPool(PooledSprite s)
    {
        s.IsActive = false;
        s.AnimateDisappearing().ContinueWith(_ => _toRemove.Enqueue(s));
    }

    void SpawnFromPool(double x, double y)
    {
        var sprite = Pool.Values.FirstOrDefault();
        if (sprite != null && Pool.Remove(sprite.Uid))
        {
            sprite.IsActive = true;
            sprite.ResetAnimationState();
            sprite.TranslationX = x;
            sprite.TranslationY = y;
            // Fire upward by default
            sprite.VX = 0;
            sprite.VY = -380f;
            _toAdd.Add(sprite);
        }
    }

    void Fire()
    {
        // Place at player top-center
        var px = Player.TranslationX;
        var py = Player.TranslationY - Player.Height / 2f - 8;
        SpawnFromPool(px, py);
    }

    // Gestures
    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (State != GameState.Playing)
            return base.ProcessGestures(args, apply);

        if (args.Type == TouchActionResult.Down)
        {
            _isPressed = true;
        }
        else if (args.Type == TouchActionResult.Up)
        {
            _isPressed = false;
        }
        else if (args.Type == TouchActionResult.Tapped)
        {
            Fire();
            return this;
        }
        else if (args.Type == TouchActionResult.Panning)
        {
            // Horizontal drag moves player, vertical drag can be ignored or used
            var vx = (float)(args.Event.Distance.Velocity.X / RenderingScale);
            _left = vx < 0; _right = vx > 0;
            return this;
        }

        // Stop continuous move unless panning
        if (args.Type != TouchActionResult.Panning)
        {
            _left = _right = _up = _down = false;
        }

        return this;
    }

    // Keyboard
    readonly Dictionary<MauiKey, GameKey> _keys = new()
    {
        { MauiKey.ArrowLeft, GameKey.Left },
        { MauiKey.ArrowRight, GameKey.Right },
        { MauiKey.ArrowUp, GameKey.Up },
        { MauiKey.ArrowDown, GameKey.Down },
        { MauiKey.Space, GameKey.Fire },
    };

    GameKey Map(MauiKey key) => _keys.TryGetValue(key, out var g) ? g : GameKey.Unset;

    public override void OnKeyDown(MauiKey key)
    {
        if (State != GameState.Playing) return;
        switch (Map(key))
        {
            case GameKey.Left: _left = true; _right = false; break;
            case GameKey.Right: _right = true; _left = false; break;
            case GameKey.Up: _up = true; _down = false; break;
            case GameKey.Down: _down = true; _up = false; break;
            case GameKey.Fire: Fire(); break;
        }
    }

    public override void OnKeyUp(MauiKey key)
    {
        switch (Map(key))
        {
            case GameKey.Left: _left = false; break;
            case GameKey.Right: _right = false; break;
            case GameKey.Up: _up = false; break;
            case GameKey.Down: _down = false; break;
        }
    }
}

