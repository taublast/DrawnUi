using CameraTests.Visualizers;
using DrawnUi.Camera;

namespace CameraTests.UI
{
    public interface IAppOverlay
    {
        void AddAudioSample(AudioSample sample);

        /// <summary>
        /// Return the name of the visualizer switched to, or null if no visualizer was switched to
        /// </summary>
        public string SwitchVisualizer(int index = -1);

        /// <summary>
        /// The audio visualizer component, if any.
        /// </summary>
        AudioVisualizer Visualizer { get; }
    }
}
