using DrawnUi.Draw;

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
        private readonly SkiaLabel _label;
        private readonly float _fontSize;
        private readonly int _maxLines;
        private readonly double _expirySeconds;
        private readonly List<CaptionLine> _lines = new();
        private string _partialText = "";
        private readonly object _sync = new();
        private Timer _timer;

        private struct CaptionLine
        {
            public string Text;
            public DateTime CreatedUtc;
        }

        /// <param name="label">Target SkiaLabel to render captions into (should have transparent background).</param>
        /// <param name="fontSize">Font size for caption text.</param>
        /// <param name="maxLines">Maximum visible caption lines (including partial).</param>
        /// <param name="expirySeconds">Seconds before a finalized line disappears.</param>
        public RealtimeCaptionsEngine(SkiaLabel label, float fontSize = 16f, int maxLines = 3, double expirySeconds = 3.0)
        {
            _label = label;
            _fontSize = fontSize;
            _maxLines = maxLines;
            _expirySeconds = expirySeconds;
            _timer = new Timer(_ => PruneExpired(), null, 1000, 1000);
        }

        /// <summary>
        /// Append incremental delta text to the current partial line.
        /// </summary>
        public void AppendDelta(string delta)
        {
            lock (_sync)
            {
                _partialText += delta;
                RenderLocked();
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
                _partialText = "";
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
                _partialText = "";
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

        /// <summary>
        /// Rebuild label spans from current state. Must be called under _sync lock.
        /// </summary>
        private void RenderLocked()
        {
            bool hasPartial = !string.IsNullOrEmpty(_partialText);
            int finalSlots = hasPartial ? Math.Max(0, _maxLines - 1) : _maxLines;
            int skip = Math.Max(0, _lines.Count - finalSlots);

            _label.Spans.Clear();

            bool first = true;
            for (int i = skip; i < _lines.Count; i++)
            {
                if (!first)
                    _label.Spans.Add(new TextSpan { Text = "\n", FontSize = 6 });
                _label.Spans.Add(new TextSpan
                {
                    Text = $" {_lines[i].Text} ",
                    TextColor = Colors.White,
                    BackgroundColor = Color.FromArgb("#CC000000"),
                    FontSize = _fontSize
                });
                first = false;
            }

            if (hasPartial)
            {
                if (!first)
                    _label.Spans.Add(new TextSpan { Text = "\n", FontSize = 6 });
                _label.Spans.Add(new TextSpan
                {
                    Text = $" {_partialText} ",
                    TextColor = Color.FromArgb("#CCFFFFFF"),
                    BackgroundColor = Color.FromArgb("#99000000"),
                    FontSize = _fontSize
                });
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
