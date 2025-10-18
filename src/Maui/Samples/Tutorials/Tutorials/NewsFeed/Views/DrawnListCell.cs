using System.Diagnostics;

namespace DrawnUI.Tutorials.NewsFeed
{
    public class DrawnListCell : SkiaDynamicDrawnCell
    {
        private CancellationTokenSource _animationTokenSource;

        protected bool Animate = true;

        public bool WasVisible { get; set; }

        // Static properties for delay management
        private static int _lastDelay = 0;
        private static long _lastRenderTime = 0;
        protected int TimeWindowMs = 100; // Reset delay if more than this since last animation
        protected int DelayIncrementMs = 50; // Increment next delay 
        protected int TimeAnimateMs = 200;
        protected float InitialScale = 0.75f;

        public DrawnListCell()
        {
            HorizontalOptions = LayoutOptions.Fill;
            UseCache = SkiaCacheType.ImageDoubleBuffered;
        }

        void SetupAnimation()
        {
            if (Animate)
            {
                _animationTokenSource?.Cancel();
                DisposeObject(_animationTokenSource);
                _animationTokenSource = new CancellationTokenSource();

                Opacity = 0;
                Scale = InitialScale; // Initial small scale
            }
        }

        public override void OnDisposing()
        {
            _animationTokenSource?.Cancel();
            _animationTokenSource?.Dispose();
            base.OnDisposing();
        }

        public override void Render(DrawingContext context)
        {
            if (!WasVisible)
            {
                WasVisible = true;
                if (Animate && BindingContext != null)
                {
                    SetupAnimation();

                    // Calculate dynamic delay based on time window
                    long currentTime = Stopwatch.GetTimestamp();
                    long elapsedMs = (long)(Stopwatch.GetElapsedTime(_lastRenderTime, currentTime).TotalMilliseconds);

                    if (elapsedMs > TimeWindowMs)
                    {
                        _lastDelay = 0; // Reset delay if outside time window
                    }
                    else
                    {
                        _lastDelay += DelayIncrementMs; // Increment delay within window
                    }

                    _lastRenderTime = currentTime;

                    int delayMs = _lastDelay;

                    // Animate opacity and scale together
                    _ = AnimateRangeAsync(d =>
                    {
                        if (_animationTokenSource.IsCancellationRequested) return; // Exit if cancelled

                        if (d == 1)
                        {
                            Opacity = 1;
                            Scale = 1;
                        }
                        else
                        {
                            Opacity = d;
                            Scale = InitialScale + (1 - InitialScale) * d; // Scale from InitialScale to 1
                        }
                    }, 0, 1, TimeAnimateMs, Easing.SinInOut, cancel: _animationTokenSource.Token, applyEndValueOnStop: true, delayMs);
                }
            }

            base.Render(context);
        }

        protected override void SetContent(object ctx)
        {
            base.SetContent(ctx);
            WasVisible = false;
        }
    }
}
