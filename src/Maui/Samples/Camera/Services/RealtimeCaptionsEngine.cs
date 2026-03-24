using System.Text;
using DrawnUi.Draw;
using static System.Net.Mime.MediaTypeNames;

namespace CameraTests.Services
{
    /// <summary>
    /// Manages real-time caption display using a rolling window of timed entries.
    /// Deltas build the current partial line; completed text finalizes it.
    /// Old lines expire after a configurable timeout. Renders to SkiaLabel via TextSpans
    /// with per-span black background for overlay-style captions.
    /// </summary>
    public class RealtimeCaptionsEngine : IDisposable
    {
        private const int PartialRenderIntervalMs = 66;
        private static readonly long PartialRenderIntervalTicks = TimeSpan.FromMilliseconds(PartialRenderIntervalMs).Ticks;

        public IList<string> Spans = new List<string>(128);
        private readonly float _fontSize;
        private readonly int _maxLines;
        private readonly double _expirySeconds;
        private readonly List<CaptionLine> _lines = new();
        private readonly StringBuilder _partialText = new();
        private readonly object _sync = new();
        private Timer _timer;
        private Timer _partialRenderTimer;
        private bool _partialRenderScheduled;
        private long _lastRenderTicks;
        private bool _hasVisibleCaptions;

        public event Action<IList<string>> CaptionsChanged;

        private struct CaptionLine
        {
            public string Text;
            public DateTime CreatedUtc;
        }

        /// <param name="label">Target SkiaLabel to render captions into (should have transparent background).</param>
        /// <param name="fontSize">Font size for caption text.</param>
        /// <param name="maxLines">Maximum visible caption lines (including partial).</param>
        /// <param name="expirySeconds">Seconds before a finalized line disappears.</param>
        public RealtimeCaptionsEngine(int maxLines = 3, double expirySeconds = 3.0)
        {
            _maxLines = maxLines;
            _expirySeconds = expirySeconds;
            _lastRenderTicks = DateTime.UtcNow.Ticks;

            _timer = new Timer(_ => PruneExpired(), null, 1000, 1000);
            _partialRenderTimer = new Timer(_ => FlushPartialRender(), null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Append incremental delta text to the current partial line.
        /// </summary>
        public void AppendDelta(string delta)
        {
            lock (_sync)
            {
                _partialText.Append(delta);
                SchedulePartialRenderLocked();
            }
        }

        /// <summary>
        /// Finalize the current utterance with the completed transcript.
        /// Resets partial text and starts a new caption slot.
        /// </summary>
        public void CommitLine(string text)
        {
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _lines.Add(new CaptionLine { Text = text.Trim(), CreatedUtc = DateTime.UtcNow });
                }
                _partialText.Clear();
                CancelScheduledPartialRenderLocked();
                RenderLocked();
            }
        }

        /// <summary>
        /// Clear all captions immediately.
        /// </summary>
        public void Clear()
        {
            lock (_sync)
            {
                _lines.Clear();
                _partialText.Clear();
                CancelScheduledPartialRenderLocked();
                RenderLocked();
            }
        }

        private void PruneExpired()
        {
            lock (_sync)
            {
                var cutoff = DateTime.UtcNow.AddSeconds(-_expirySeconds);
                if (_lines.RemoveAll(l => l.CreatedUtc < cutoff) > 0)
                {
                    RenderLocked();
                }
            }
        }

        private void SchedulePartialRenderLocked()
        {
            var now = DateTime.UtcNow.Ticks;
            var dueAt = _lastRenderTicks + PartialRenderIntervalTicks;
            if (now >= dueAt)
            {
                RenderLocked(now);
                return;
            }

            if (_partialRenderScheduled)
            {
                return;
            }

            var remainingTicks = dueAt - now;
            var dueMs = (int)Math.Max(1, TimeSpan.FromTicks(remainingTicks).TotalMilliseconds);
            _partialRenderScheduled = true;
            _partialRenderTimer?.Change(dueMs, Timeout.Infinite);
        }

        private void CancelScheduledPartialRenderLocked()
        {
            _partialRenderScheduled = false;
            _partialRenderTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void FlushPartialRender()
        {
            lock (_sync)
            {
                if (!_partialRenderScheduled)
                {
                    return;
                }

                _partialRenderScheduled = false;
                RenderLocked();
            }
        }

        /// <summary>
        /// Rebuild label spans from current state. Must be called under _sync lock.
        /// </summary>
        private void RenderLocked(long nowTicks = 0)
        {
            _partialRenderScheduled = false;
            _lastRenderTicks = nowTicks == 0 ? DateTime.UtcNow.Ticks : nowTicks;

            bool hasPartial = _partialText.Length > 0;
            int finalSlots = hasPartial ? Math.Max(0, _maxLines - 1) : _maxLines;
            int skip = Math.Max(0, _lines.Count - finalSlots);

            Spans.Clear();

            for (int i = skip; i < _lines.Count; i++)
            {
                Spans.Add($"{_lines[i].Text}");
            }

            if (hasPartial)
            {
                Spans.Add($"{_partialText}");
            }

            CaptionsChanged?.Invoke(Spans.ToList());
        }

 
        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
            _partialRenderTimer?.Dispose();
            _partialRenderTimer = null;
        }
    }
}
