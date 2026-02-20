using DrawnUi.Camera;

namespace MusicNotes.UI
{
    /// <summary>
    /// Interface for audio visualizers
    /// </summary>
    public interface IAudioVisualizer: IDisposable
    {
        void AddSample(AudioSample sample);
        /// <summary>
        /// Renders into a viewport (in pixels) on the provided canvas.
        /// Visualizers should respect viewport offset/size and not assume (0,0).
        /// Returns true if the visualizer still has animation in progress (e.g. decaying
        /// bars or falling peak dots) and needs another redraw even without new audio data.
        /// </summary>
        bool Render(SKCanvas canvas, SKRect viewport, float scale);

        /// <summary>
        /// Will reset internal state of the visualizer, like if was stated from scratch
        /// </summary>
        void Reset();

        bool UseGain { get; set; }
        int Skin { get; set; }
    }
}
