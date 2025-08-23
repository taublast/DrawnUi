// NOTE: Parts of the code below are based on
// https://www.mooict.com/wpf-c-tutorial-create-a-space-battle-shooter-game-in-visual-studio/7/

global using DrawnUi.Controls;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AppoMobi.Maui.Gestures;
using AppoMobi.Specials;
using DrawnUi.Gaming;

namespace SpaceShooter.Game;

public partial class SpaceShooter : MauiGame
{
    #region CONSTANTS

    const int MAX_ENEMIES = 24;

    const int MAX_EXPLOSIONS = 24;

    const int MAX_BULLETS = 64;

    /// <summary>
    /// Player ship movement speed
    /// </summary>
    const float PLAYER_SPEED = 300;

    /// <summary>
    /// Stars parallax
    /// </summary>
    const float STARS_SPEED = 20;

    /// <summary>
    /// Base pause between enemy spawns
    /// </summary>
    const float DEFAULT_PAUSE_ENEMY_SPAWN = 1.75f;

    /// <summary>
    /// For long running profiling
    /// </summary>
    const bool CHEAT_INVULNERABLE = false;

    /// <summary>
    /// Path to game sprites inside Resources/Raw
    /// </summary>
    public const string SpritesPath = "Space/Sprites";

    #endregion

    #region INITIALIZE

    public SpaceShooter()
    {
        InitializeComponent();

        BindingContext = this;

        Instance = this;
    }

    /// <summary>
    /// So it can get paused/resumed from anywhere in the app
    /// </summary>
    public static SpaceShooter Instance { get; set; }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        BindingContext = this; //insist in case parent view might set its own
    }

    protected override void OnLayoutReady()
    {
        base.OnLayoutReady();

        Task.Run(async () =>
        {
            while (Superview == null || !Superview.HasHandler)
            {
                await Task.Delay(30);
            }

            //we have some GPU cache used so we need the canvas to be fully created before we would start
            Initialize();  //game loop will be started inside

        }).ConfigureAwait(false);
    }

    public override void OnAppeared()
    {
        base.OnAppeared();

        Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
        {

            //our dialog has a transparent background that is blurring pixels under
            //so we have 2 options:
            //A - do not cache the backdrop and blur underground pixels in realtime
            //B - cache the backdrop but blur after the content below was rendered, so we implement the hack below, knowing our blurred background will be static as we show it only during pauses.

            _appeared = true;
            OnPropertyChanged(nameof(ShowDialog));
        });
    }

    void Initialize()
    {
        if (!Superview.HasHandler || _initialized)
            return;

        RndExtensions.RandomizeTime(); //amstrad cpc forever

        IgnoreChildrenInvalidations = true;

        // in case we implement key press for desktop
        Focus();

        //prebuilt reusable sprites pools
        Parallel.Invoke(
            () =>
            {
                for (int i = 0; i < MAX_ENEMIES; i++)
                {
                    AddToPoolEnemySprite();
                }
            },
            () =>
            {
                for (int i = 0; i < MAX_EXPLOSIONS; i++)
                {
                    AddToPoolExplosionSprite();
                }
            },
            () =>
            {
                for (int i = 0; i < MAX_EXPLOSIONS; i++)
                {
                    AddToPoolExplosionCrashSprite();
                }
            },
            () =>
            {
                for (int i = 0; i < MAX_BULLETS; i++)
                {
                    AddToPoolBulletSprite();
                }
            }
        );

        PlayerShieldExplosion.GoToEnd(); //hide

        _needPrerender = true;

        _initialized = true;

        PresentGame();
    }

    protected override void Draw(DrawingContext context)
    {
        base.Draw(context);

        if (_needPrerender)
        {
            //prerender or precompile something like shaders etc
            // ...

            _needPrerender = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddToPoolExplosionCrashSprite()
    {
        var explosionCrash = ExplosionCrashSprite.Create();

        explosionCrash.Finished += (s, a) =>
        {
            RemoveReusable(explosionCrash);
        };

        ExplosionsCrashPool.Add(explosionCrash.Uid, explosionCrash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddToPoolExplosionSprite()
    {
        var explosion = ExplosionSprite.Create();

        explosion.Finished += (s, a) =>
        {
            RemoveReusable(explosion);
        };

        ExplosionsPool.Add(explosion.Uid, explosion);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddToPoolBulletSprite()
    {
        var sprite = BulletSprite.Create();
        BulletsPool.Add(sprite.Uid, sprite);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void AddToPoolEnemySprite()
    {
        var enemy = EnemySprite.Create();
        EnemiesPool.Add(enemy.Uid, enemy);
    }

    public override void OnChildAdded(SkiaControl child)
    {
        if (_initialized)
            return; //do not care

        base.OnChildAdded(child);
    }

    public override void OnChildRemoved(SkiaControl child)
    {
        if (_initialized)
            return; //do not care

        base.OnChildRemoved(child);
    }

    #endregion

    #region GAME LOOP

    public override void GameLoop(float deltaSeconds)
    {
        base.GameLoop(deltaSeconds);

        if (State == GameState.Playing)
        {
            //update stars parallax
            ParallaxLayer.TileOffsetY -= STARS_SPEED * deltaSeconds;

            // get the player hit box
            var playerPosition = Player.GetPositionOnCanvasInPoints();

            _playerHitBox = new SKRect(playerPosition.X, playerPosition.Y,
                (float)(playerPosition.X + Player.Width), (float)(playerPosition.Y + Player.Height));


            // collision detection
            foreach (var x in this.Views)
            {
                if (x is EnemySprite enemySprite && enemySprite.IsActive)
                {
                    //calculate hitbox once, we read it several times later
                    enemySprite.UpdateState(LastFrameTimeNanos);

                    var enemy = enemySprite.HitBox;

                    if (enemySprite.TranslationY > this.Height)
                    {
                        CollideEnemyAndEarth(enemySprite);
                    }
                    else
                    if (_playerHitBox.IntersectsWith(enemy))
                    {
                        CollidePlayerAndEnemy(enemySprite);
                    }

                    //check collision with bullets
                    if (enemySprite.IsActive) //IsActive will be false after collision
                    {
                        foreach (var y in Views)
                        {
                            if (y is BulletSprite bulletSprite && bulletSprite.IsActive)
                            {
                                //calculate hitbox once, we read it several times later
                                bulletSprite.UpdateState(LastFrameTimeNanos);

                                var bullet = bulletSprite.HitBox;

                                if (bullet.IntersectsWith(enemy))
                                {
                                    CollideBulletAndEnemy(enemySprite, bulletSprite);
                                }
                            }
                        }
                    }

                    // if enemy is still alive it can move..
                    if (enemySprite.IsActive)
                    {
                        enemySprite.UpdatePosition(deltaSeconds);
                    }

                }
                else
                if (x is BulletSprite bulletSprite)
                {
                    if (bulletSprite.IsActive)
                    {
                        // check if bullet has reached top part of the screen
                        if (bulletSprite.TranslationY < -Height)
                        {
                            RemoveReusable(bulletSprite); //will set IsActive = false
                        }

                        // move the bullet rectangle towards top of the screen
                        if (bulletSprite.IsActive)
                        {
                            bulletSprite.UpdatePosition(deltaSeconds);
                        }
                    }
                }
            }

            // reduce time we wait between enemy creations
            _pauseEnemyCreation -= 1 * deltaSeconds;

            // our logic for calculating time between enemy spans according to difficulty and current score
            if (_pauseEnemyCreation < 0)
            {
                AddEnemy(); // run the make enemies function

                //adjust difficulty upon score
                if (Score > 300)
                {
                    _pauseEnemyCreation = DEFAULT_PAUSE_ENEMY_SPAWN - 0.75f;
                }
                else
                if (Score > 200)
                {
                    _pauseEnemyCreation = DEFAULT_PAUSE_ENEMY_SPAWN - 0.66f;
                }
                else
                if (Score > 100)
                {
                    _pauseEnemyCreation = DEFAULT_PAUSE_ENEMY_SPAWN - 0.5f;
                }
                else
                {
                    _pauseEnemyCreation = DEFAULT_PAUSE_ENEMY_SPAWN;
                }
            }

            // player movement
            if (_moveLeft)
            {
                UpdatePlayerPosition(Player.TranslationX - PLAYER_SPEED * deltaSeconds);
            }

            if (_moveRight)
            {
                UpdatePlayerPosition(Player.TranslationX + PLAYER_SPEED * deltaSeconds);
            }

            if (Health < 1)
            {
                EndGameLost();
            }

        }

        // removing sprites
        ProcessSpritesToBeRemoved();

        if (_spritesToBeAdded.Count > 0)
        {
            foreach (var add in _spritesToBeAdded)
            {
                AddSubView(add);
            }
            _spritesToBeAdded.Clear();
        }

        if (_startAnimations.Count > 0)
        {
            foreach (var animation in _startAnimations)
            {
                // remove them permanently from the canvas
                if (animation is SkiaLottie lottie)
                {
                    AddSubView(lottie);
                    lottie.Start();
                }
            }
            _startAnimations.Clear();
        }

        UpdateScore();

    }

    protected override void OnPaused()
    {
        if (State == GameState.Playing)
        {
            StopLoop();
            _lastState = this.State;
            State = GameState.Paused;
        }
    }

    protected override void OnResumed()
    {
        if (State == GameState.Paused)
        {
            State = _lastState;
            if (State == GameState.Playing)
            {
                StartLoop();
            }
        }
    }

    #endregion

    #region ACTIONS

    void Fire()
    {
        AddBullet();
    }

    void PresentGame()
    {
        var message = "defend planet earth from crazy alien ships!";
#if WINDOWS || MACCATALYST
        message += "\nuse arrows or mouse to move, click or press space to fire.";
#endif

        DialogButton = "okay".ToUpperInvariant();
        DialogMessage = message.ToUpperInvariant();

        ShowDialog = true;

        State = GameState.Ready;
    }

    void StartNewGame()
    {
        foreach (var control in Views)
        {
            if (control is EnemySprite || control is BulletSprite)
            {
                _spritesToBeRemovedLater.Enqueue(control);
            }
        }

        ProcessSpritesToBeRemoved();

        _pauseEnemyCreation = DEFAULT_PAUSE_ENEMY_SPAWN;
        Score = 0;
        Health = 100;
        ShowDialog = false;
        State = GameState.Playing;

        UpdateScore();

        StartLoop();
    }

    void EndGameLost()
    {
        // todo localize
        DialogButton = "okay".ToUpperInvariant();
        DialogMessage = "the shield was broken!\ngood luck next time captain!".ToUpperInvariant();

        EndGameInternal();

        ShowDialog = true;
    }

    void EndGameWin()
    {
        EndGameInternal();
    }

    void EndGameInternal()
    {
        State = GameState.Ended;

        StopLoop();
    }

    void AddDamage(int damage)
    {
        Health -= damage / 5f;
    }

    void CollidePlayerAndEnemy(EnemySprite enemySprite)
    {
        AddDamage(30);
        Score += 1;

        RemoveReusable(enemySprite);

        PlayerShieldExplosion.Start();
    }

    void CollideBulletAndEnemy(EnemySprite enemySprite, BulletSprite bulletSprite)
    {
        Score += 10;

        RemoveReusable(bulletSprite);
        RemoveReusable(enemySprite);

        //create explosion not at the Y-center of the enemy but "at the nose minus 20pts"
        AddExplosion(enemySprite.TranslationX + enemySprite.Width / 2f, enemySprite.TranslationY + enemySprite.Height - 20);
    }

    private void CollideEnemyAndEarth(EnemySprite enemySprite)
    {
        AddDamage(20);

        RemoveReusable(enemySprite);

        AddExplosionCrash(enemySprite.TranslationX + enemySprite.Width / 2f, enemySprite.Height / 2f);
    }

    void UpdatePlayerPosition(double x)
    {
        var leftLimit = -Width / 2f + Player.Width / 2f;
        var rightLimit = Width / 2f - Player.Width / 2f;
        var clampedX = Math.Clamp(x, leftLimit, rightLimit);

        if (clampedX != Player.TranslationX)
        {
            Player.TranslationX = clampedX;
            PlayerShield.TranslationX = clampedX;
            PlayerShieldExplosion.TranslationX = clampedX;
            HealthBar.TranslationX = clampedX;
        }
    }

    private void AddEnemy()
    {
        if (EnemiesPool.Count > 0)
        {
            var enemyIndex = RndExtensions.CreateRandom(0, EnemiesPool.Count - 1);
            var enemy = EnemiesPool.Values.ElementAt(enemyIndex);
            if (enemy != null)
            {
                if (EnemiesPool.Remove(enemy.Uid))
                {
                    enemy.IsActive = true;
                    enemy.TranslationX = RndExtensions.CreateRandom(0, (int)(Width - enemy.WidthRequest));
                    enemy.TranslationY = -50;

                    enemy.ResetAnimationState();

                    _spritesToBeAdded.Add(enemy); ;
                }
            }
        }
    }

    private void AddBullet()
    {
        var sprite = BulletsPool.Values.FirstOrDefault();
        if (sprite != null && BulletsPool.Remove(sprite.Uid))
        {
            // place the bullet on top of the player location
            sprite.TranslationX = Player.TranslationX;
            sprite.TranslationY = Player.TranslationY - sprite.HeightRequest - Player.Height;
            sprite.IsActive = true;

            _spritesToBeAdded.Add(sprite);
        }
    }

    private void AddExplosion(double x, double y)
    {
        var explosion = ExplosionsPool.Values.FirstOrDefault();
        if (explosion != null && ExplosionsPool.Remove(explosion.Uid))
        {
            explosion.IsActive = true;
            explosion.TranslationX = x - explosion.WidthRequest / 2f;
            explosion.TranslationY = y - explosion.WidthRequest / 2f;

            explosion.ResetAnimationState();

            _startAnimations.Add(explosion); ;
        }
    }

    private void AddExplosionCrash(double x, double y)
    {

        var explosion = ExplosionsCrashPool.Values.FirstOrDefault();
        if (explosion != null && ExplosionsCrashPool.Remove(explosion.Uid))
        {
            explosion.IsActive = true;
            explosion.TranslationX = x - explosion.WidthRequest / 2f;
            explosion.TranslationY = y;

            explosion.ResetAnimationState();

            _startAnimations.Add(explosion);
        }
    }

    private void RemoveReusable(IReusableSprite sprite)
    {
        sprite.IsActive = false;
        sprite.AnimateDisappearing().ContinueWith((s) =>
        {
            _spritesToBeRemovedLater.Enqueue(sprite as SkiaControl);
        }).ConfigureAwait(false);
    }

    void RemoveSprite(SkiaControl sprite)
    {
        if (sprite is SkiaLottie lottie)
        {
            lottie.Stop(); //just in case to avoid empty animators running
        }
        // remove from the canvas
        if (sprite is BulletSprite bullet)
        {
            BulletsPool.TryAdd(bullet.Uid, bullet);
        }
        else if (sprite is EnemySprite enemy)
        {
            EnemiesPool.TryAdd(enemy.Uid, enemy);
        }
        else
        if (sprite is ExplosionSprite explosion)
        {
            ExplosionsPool.TryAdd(explosion.Uid, explosion);
        }
        else
        if (sprite is ExplosionCrashSprite explosionCrash)
        {
            ExplosionsCrashPool.TryAdd(explosionCrash.Uid, explosionCrash);
        }
        RemoveSubView(sprite);
    }

    void ProcessSpritesToBeRemoved()
    {
        SkiaControl sprite;
        while (_spritesToBeRemovedLater.Count > 0)
        {
            if (_spritesToBeRemovedLater.TryDequeue(out sprite))
            {
                RemoveSprite(sprite);
            }
        }

    }

    #endregion

    #region GESTURES AND KEYS

    public ICommand CommandPressedOk
    {
        get
        {
            return new Command((context) =>
            {
                if (TouchEffect.CheckLockAndSet())
                    return;

                if (State == GameState.Ready || State == GameState.Ended)
                {
                    StartNewGame();
                }
            });
        }
    }

    /// <summary>
    /// Mappings from platform-independent keys to game action keys.
    /// Player could change these mappings if you implement this in settings.
    /// </summary>
    Dictionary<MauiKey, GameKey> ActionKeys = new()
    {
        { MauiKey.Space, GameKey.Fire },
        { MauiKey.ArrowLeft, GameKey.Left },
        { MauiKey.ArrowRight, GameKey.Right },
    };

    /// <summary>
    /// Map platform-independent key to internally assigned game key. Player could change these mappings if you implement this in settings.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    GameKey MapToGame(MauiKey key)
    {
        if (ActionKeys.TryGetValue(key, out var gameKey))
        {
            return gameKey;
        }
        return GameKey.Unset;
    }

    public override void OnKeyUp(MauiKey mauiKey)
    {
        var key = MapToGame(mauiKey);

        if (key == GameKey.Fire && (State == GameState.Ready || State == GameState.Ended))
        {
            StartNewGame();
            return;
        }

        if (State != GameState.Playing)
            return;

        if (key == GameKey.Left)
        {
            _moveLeft = false;
        }
        else
        if (key == GameKey.Right)
        {
            _moveRight = false;
        }
    }

    public override void OnKeyDown(MauiKey mauiKey)
    {
        var key = MapToGame(mauiKey);

        if (State != GameState.Playing)
            return;

        if (key == GameKey.Fire)
        {
            Fire();
        }
        else
        if (key == GameKey.Left)
        {
            _moveLeft = true;
            _moveRight = false;
        }
        else
        if (key == GameKey.Right)
        {
            _moveLeft = false;
            _moveRight = true;
        }
    }

    volatile bool _moveLeft, _moveRight;
    private PointF _lastPan;
    private PointF _lastDown;
    bool _wasPanning;
    bool _isPressed;

    const double thresholdNotPanning = 20.0;

    public override ISkiaGestureListener ProcessGestures(SkiaGesturesParameters args, GestureEventProcessingInfo apply)
    {
        if (State == GameState.Playing)
        {
            var velocityX = (float)(args.Event.Distance.Velocity.X / RenderingScale);
            //var velocityY = (float)(args.Event.Distance.Velocity.Y / RenderingScale);

            if (args.Type == TouchActionResult.Panning)
            {
                _wasPanning = true;
                _lastPan = args.Event.Location;
                if (velocityX < 0)
                {
                    _moveLeft = true;
                    _moveRight = false;
                }
                else
                if (velocityX > 0)
                {
                    _moveRight = true;
                    _moveLeft = false;
                }

                return this;
            }

            if (args.Type == TouchActionResult.Down)
            {
                _lastDown = args.Event.Location;
                _wasPanning = false;
                _isPressed = true;
            }

            if (args.Type == TouchActionResult.Tapped
                || (args.Type == TouchActionResult.Up && _isPressed && Math.Abs(args.Event.Distance.Total.X) < thresholdNotPanning * RenderingScale))
            {
                Fire();
            }

            if (args.Type == TouchActionResult.Up)
            {
                _isPressed = false;
            }

            _moveRight = false;
            _moveLeft = false;

            return this;
        }

        _moveRight = false;
        _moveLeft = false;

        return base.ProcessGestures(args, apply);
    }
    #endregion

    #region UI

    /// <summary>
    /// Score can change several times per frame
    /// so we dont want bindings to update the score toooften.
    /// Instead we update the display manually once after the frame is finalized.
    /// </summary>
    void UpdateScore()
    {
        LabelScore.Text = ScoreLocalized;
        LabelHiScore.Text = HiScoreLocalized;
    }

    private string _DialogMessage;
    public string DialogMessage
    {
        get
        {
            return _DialogMessage;
        }
        set
        {
            if (_DialogMessage != value)
            {
                _DialogMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string _DialogButton = "OK"; //can localize
    public string DialogButton
    {
        get
        {
            return _DialogButton;
        }
        set
        {
            if (_DialogButton != value)
            {
                _DialogButton = value;
                OnPropertyChanged();
            }
        }
    }

    private double _health = 100;
    public double Health
    {
        get
        {
            return _health;
        }
        set
        {
            if (!CHEAT_INVULNERABLE && _health != value)
            {
                _health = value;
                OnPropertyChanged();
            }
        }
    }

    public string ScoreLocalized
    {
        get
        {
            return $"SCORE: {Score:0}";
        }
    }

    public string HiScoreLocalized
    {
        get
        {
            return $"HI: {HiScore:0}";
        }
    }

    private int _Score;
    public int Score
    {
        get
        {
            return _Score;
        }
        set
        {
            if (_Score != value)
            {
                _Score = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ScoreLocalized));

                if (Score > HiScore)
                {
                    HiScore = Score;
                }
            }
        }
    }

    private int _HiScore = 500;
    public int HiScore
    {
        get
        {
            return _HiScore;
        }
        set
        {
            if (_HiScore != value)
            {
                _HiScore = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HiScoreLocalized));
            }
        }
    }

    private bool _ShowDialog;
    public bool ShowDialog
    {
        get
        {
            return _ShowDialog && _appeared;
        }
        set
        {
            if (_ShowDialog != value)
            {
                _ShowDialog = value;
                OnPropertyChanged();
            }
        }
    }

    bool _appeared;

    #endregion

    #region VARIABLES

    //pools of reusable objects
    //to avoid lag spikes when creating or disposing or GC-ing items we simply reuse them

    protected Dictionary<Guid, BulletSprite> BulletsPool = new(MAX_BULLETS);

    protected Dictionary<Guid, EnemySprite> EnemiesPool = new(MAX_ENEMIES);

    protected Dictionary<Guid, ExplosionSprite> ExplosionsPool = new(MAX_EXPLOSIONS);

    protected Dictionary<Guid, ExplosionCrashSprite> ExplosionsCrashPool = new(MAX_EXPLOSIONS);

    /// <summary>
    /// This could be changed from loop and from individual sprite animatiion thread
    /// </summary>
    private ConcurrentQueue<SkiaControl> _spritesToBeRemovedLater = new();

    private List<SkiaControl> _spritesToBeAdded = new(128);
    private List<SkiaControl> _startAnimations = new(MAX_EXPLOSIONS);
    private float _pauseEnemyCreation;
    private SKRect _playerHitBox = new();
    private bool _needPrerender;
    private bool _initialized;
    private GameState _lastState;

    private GameState _gameState;
    public GameState State
    {
        get
        {
            return _gameState;
        }
        set
        {
            if (_gameState != value)
            {
                _gameState = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion
}
