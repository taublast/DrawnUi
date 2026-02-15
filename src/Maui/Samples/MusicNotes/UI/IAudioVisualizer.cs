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
        /// </summary>
        void Render(SKCanvas canvas, SKRect viewport, float scale);
        bool UseGain { get; set; }
        int Skin { get; set; }
    }
}
