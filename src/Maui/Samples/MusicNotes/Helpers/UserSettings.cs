using MusicNotes.Audio;
using Newtonsoft.Json;

namespace MusicNotes.Helpers
{
    public class UserSettings
    {
        public UserSettings()
        {
            Lang = "en";
            Module = 0;
            Device = 0;
            Gain = false;
        }

        public string Lang { get; set; }
        public int Module { get; set; }
        public int Device { get; set; }
        public bool Gain { get; set; }
        public Dictionary<string, int> Formats { get; set; }

        private static UserSettings _loaded;

        public static void FillFromHardware(AudioRecorder hardware)
        {
            UserSettings.Current.Gain = hardware.UseGain;
            UserSettings.Current.Device = hardware.AudioDeviceIndex;
        }
        public static void ApplyToHardware(AudioRecorder hardware)
        {
            hardware.UseGain = UserSettings.Current.Gain;
            hardware.AudioDeviceIndex = UserSettings.Current.Device;
        }

        public static void Save()
        {
            var json = string.Empty;
            try
            {
                json = JsonConvert.SerializeObject(Current);
                Preferences.Default.Set("setts", json);
            }
            catch
            {
                // ignored
            }
        }

        public static UserSettings Current
        {
            get
            {
                if (_loaded == null)
                {
                    try
                    {
                        var json = Preferences.Default.Get("setts", string.Empty);
                        _loaded = JsonConvert.DeserializeObject<UserSettings>(json);
                    }
                    catch
                    {
                        // ignored
                    }

                    if (_loaded == null)
                    {
                        _loaded = new();
                    }
                }
                return _loaded;
            }
            set
            {
                _loaded = value;
                var json = string.Empty;
                try
                {
                    json = JsonConvert.SerializeObject(value);
                    Preferences.Default.Set("setts", json);
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
