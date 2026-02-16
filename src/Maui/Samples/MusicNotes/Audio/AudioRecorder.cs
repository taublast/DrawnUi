using System.Diagnostics;
using DrawnUi.Camera;

namespace MusicNotes.Audio
{
    public partial class AudioRecorder : SkiaCamera
    {
        public AudioRecorder()
        {
            this.EnableAudioRecording = true;

            //tirn on AUDIO recorder mode
            this.EnableAudioMonitoring = true;
            this.EnableAudioRecording = true;

            //turn off VIDEO
            this.EnableVideoPreview = false;
            this.EnableVideoRecording = false;
        }

        public static readonly BindableProperty UseGainProperty = BindableProperty.Create(
            nameof(UseGain),
            typeof(bool),
            typeof(AudioRecorder),
            false);

        public bool UseGain
        {
            get => (bool)GetValue(UseGainProperty);
            set => SetValue(UseGainProperty, value);
        }

        /// <summary>
        /// Gain multiplier applied to raw PCM when UseGain is true.
        /// </summary>
        public float GainFactor { get; set; } = 3.0f;

        public event Action<AudioSample> OnAudioSample; 

        protected override AudioSample OnAudioSampleAvailable(AudioSample sample)
        {
            if (UseGain && sample.Data != null && sample.Data.Length > 1)
            {
                AmplifyPcm16(sample.Data, GainFactor);
            }

            OnAudioSample?.Invoke(sample);

            //Debug.WriteLine($"Audio timestamp {sample.Timestamp}");

            return base.OnAudioSampleAvailable(sample);
        }

        /// <summary>
        /// Amplifies PCM16 audio data in-place. Zero allocations.
        /// </summary>
        private static void AmplifyPcm16(byte[] data, float gain)
        {
            for (int i = 0; i < data.Length - 1; i += 2)
            {
                int sample = (short)(data[i] | (data[i + 1] << 8));
                sample = (int)(sample * gain);

                // Clamp to 16-bit range
                if (sample > 32767) sample = 32767;
                else if (sample < -32768) sample = -32768;

                data[i] = (byte)(sample & 0xFF);
                data[i + 1] = (byte)((sample >> 8) & 0xFF);
            }
        }

        public override void OnWillDisposeWithChildren()
        {
            base.OnWillDisposeWithChildren();


        }


    }
}
