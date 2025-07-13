using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrawnUi.Draw;

namespace DrawnUi.Gaming
{
    /// <summary>
    /// Base class for implementing a game. StartLoop, StopLoop, override GameLoop(..) etc.
    /// </summary>
    public class MauiGame : SkiaLayout, IMauiGame
    {
        /// <summary>
        /// Can disable frame time interpolator
        /// </summary>
        public static bool FrameInterpolatorDisabled { get; set; }

        private ActionOnTickAnimator _appLoop;
        protected long LastFrameTimeNanos;

        public MauiGame()
        {
            Super.Log("****************** MAUIGAME CREATED **********************");

            KeyboardManager.KeyDown += OnKeyboardDownEvent;
            KeyboardManager.KeyUp += OnKeyboardUpEvent;
        }

        public override void OnDisposing()
        {
            KeyboardManager.KeyUp -= OnKeyboardUpEvent;
            KeyboardManager.KeyDown -= OnKeyboardDownEvent;

            base.OnDisposing();
        }

        protected virtual void OnResumed()
        {
        }

        protected virtual void OnPaused()
        {
        }

        /// <summary>
        /// Override this for your game. `deltaSeconds` is time elapsed between the previous frame and this one 
        /// </summary>
        /// <param name="deltaSeconds"></param>
        public virtual void GameLoop(float deltaSeconds)
        {
        }

        /// <summary>
        /// Stops game loop
        /// </summary>
        public virtual void StopLoop()
        {
            _appLoop?.Stop();
        }

        /// <summary>
        /// Starts game loop
        /// </summary>
        /// <param name="delayMs"></param>
        public void StartLoop(int delayMs = 0)
        {
            if (_appLoop == null)
            {
                _appLoop = new(this, GameTick);
            }

            Trace.WriteLine("****************** GAMELOOP STARTED **********************");
            _appLoop.Start(delayMs);
        }

        protected FrameTimeInterpolator FrameTimeInterpolator = new();

        /// <summary>
        /// Internal, use override GameLoop for your game.
        /// </summary>
        /// <param name="frameTimeNanos"></param>
        protected virtual void GameTick(long frameTimeNanos)
        {
            // Incoming frameTime is in nanoseconds
            // Calculate delta time in seconds for later use
            float deltaSeconds = (frameTimeNanos - LastFrameTimeNanos) / 1_000_000_000.0f;

            // Use stable time
            if (!MauiGame.FrameInterpolatorDisabled)
                deltaSeconds = FrameTimeInterpolator.GetDeltaTime(deltaSeconds);

            LastFrameTimeNanos = frameTimeNanos;

            GameLoop(deltaSeconds);
        }

        private bool _IsPaused;

        public bool IsPaused
        {
            get { return _IsPaused; }
            set
            {
                if (_IsPaused != value)
                {
                    _IsPaused = value;
                    OnPropertyChanged();
                }
            }
        }

        public void Pause()
        {
            IsPaused = true;
            OnPaused();
        }

        public void Resume()
        {
            LastFrameTimeNanos = SkiaControl.GetNanoseconds();
            IsPaused = false;
            OnResumed();
        }

        #region KEYS

        /// <summary>
        /// Override this to process game keys
        /// </summary>
        /// <param name="key"></param>
        public virtual void OnKeyDown(MauiKey key)
        {
        }

        /// <summary>
        /// Override this to process game keys
        /// </summary>
        /// <param name="key"></param>
        public virtual void OnKeyUp(MauiKey key)
        {
        }

        /// <summary>
        /// Do not use directly. It's public to be able to send keys to game manually if needed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="key"></param>
        public void OnKeyboardDownEvent(object sender, MauiKey key)
        {
            OnKeyDown(key);
        }

        /// <summary>
        /// Do not use directly. It's public to be able to send keys to game manually if needed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="key"></param>
        public void OnKeyboardUpEvent(object sender, MauiKey key)
        {
            OnKeyUp(key);
        }

        #endregion
    }
}
