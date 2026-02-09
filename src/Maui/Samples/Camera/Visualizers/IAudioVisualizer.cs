using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrawnUi.Camera;

namespace CameraTests
{
    /// <summary>
    /// Interface for audio visualizers
    /// </summary>
    public interface IAudioVisualizer: IDisposable
    {
        void AddSample(AudioSample sample);
        void Render(SKCanvas canvas, float width, float height, float scale, string recognizedText = null);
        bool UseGain { get; set; }
        int Skin { get; set; }
    }
}
