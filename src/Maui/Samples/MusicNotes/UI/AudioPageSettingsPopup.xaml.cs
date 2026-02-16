using MusicNotes;
using MusicNotes.Audio;
using MusicNotes.Helpers;
using MusicNotes.UI;
 

namespace ShadersCamera.Views;

public partial class AudioPageSettingsPopup 
{
    private readonly AudioPage _parentPage;

    private bool isInitializing;

    AudioRecorder Recorder => _parentPage?.Recorder;

    async Task <string> GetAudioSourceText(AudioRecorder CameraControl)
    {
        if (CameraControl.AudioDeviceIndex >=0)
        {
            var arrayDevices = await CameraControl.GetAvailableAudioDevicesAsync();
            if (arrayDevices.Count > 0)
            {
                var device = arrayDevices[CameraControl.AudioDeviceIndex];
                return $"{device}";
            }
        }

        return "System Default Audio";
    }

    async Task UpdateControls()
    {
        isInitializing = true;

        //switches
        SwitchGain.IsToggled = Recorder.UseGain;

        //selectors
        LabelSource.Text = await GetAudioSourceText(Recorder);

        isInitializing = false;
    }

    public AudioPageSettingsPopup(AudioPage page)
	{
		InitializeComponent();

        _parentPage = page;

        UserSettings.FillFromHardware(Recorder);

        _ = UpdateControls();
    }

    private void UseGainSwitch_OnToggled(object sender, bool value)
    {
        if (isInitializing)
            return;

        Recorder.UseGain = value;
    }

    protected override Task OnDismissedByTappingOutsideOfPopup(CancellationToken token = new CancellationToken())
    {
        UserSettings.FillFromHardware(Recorder);
        UserSettings.Save();

        return base.OnDismissedByTappingOutsideOfPopup(token);
    }

 
    private void SelectAudioSource(object sender, ControlTappedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var inputDevices = await Recorder.GetAvailableAudioDevicesAsync();

                if (inputDevices?.Count > 0)
                {
                    // Prefix the list with "System Default" option
                    var options = new string[inputDevices.Count + 1];
                    options[0] = "System Default";
                    for (int i = 0; i < inputDevices.Count; i++)
                    {
                        options[i + 1] = inputDevices[i];
                    }

                    var result = await _parentPage.DisplayActionSheet("Select Audio Source", "Cancel", null, options);

                    if (!string.IsNullOrEmpty(result) && result != "Cancel")
                    {
                        if (result == "System Default")
                        {
                            Recorder.AudioDeviceIndex = -1;
                        }
                        else
                        {
                            // Find the index in our original list
                            // Careful: option list was shifted by 1
                            int selectedIndex = -1;
                            for (int i = 0; i < inputDevices.Count; i++)
                            {
                                if (inputDevices[i] == result)
                                {
                                    selectedIndex = i;
                                    break;
                                }
                            }

                            if (selectedIndex >= 0)
                            {
                                Recorder.AudioDeviceIndex = selectedIndex;
                            }
                        }

                        LabelSource.Text = result;
                    }
                }
                else
                {
                    _parentPage.ShowAlert("No Input Devices", "No audio input devices found.");
                }
            }
            catch (Exception ex)
            {
                _parentPage.ShowAlert("Error", $"Error getting audio devices: {ex.Message}");
            }
        });


    }
}
