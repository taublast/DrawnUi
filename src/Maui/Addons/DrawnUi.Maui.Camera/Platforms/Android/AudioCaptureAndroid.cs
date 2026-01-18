using Android.Content;
using Android.Media;
using System.Text;
using Debug = System.Diagnostics.Debug;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Encoding = Android.Media.Encoding;

namespace DrawnUi.Camera;

public class AudioCaptureAndroid : IAudioCapture
{
    private AudioRecord _audioRecord;
    private Thread _audioThread;
    private volatile bool _isCapturing;
    private int _bufferSize;
    private AudioTimestamp _audioTimestamp;
    private Android.Media.AudioDeviceInfo[] _availableDevices;

    public bool IsCapturing => _isCapturing;
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public AudioBitDepth BitDepth { get; private set; }

    public event EventHandler<AudioSample> SampleAvailable;

    /// <summary>
    /// Get list of available audio input devices
    /// </summary>
    public Task<List<DrawnUi.Camera.AudioDeviceInfo>> GetAvailableDevicesAsync()
    {
        var devices = new List<DrawnUi.Camera.AudioDeviceInfo>();
        try
        {
            var audioManager = (AudioManager)Android.App.Application.Context.GetSystemService(Context.AudioService);
            if (audioManager != null && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                _availableDevices = audioManager.GetDevices(GetDevicesTargets.Inputs);
                if (_availableDevices != null)
                {
                    for (int i = 0; i < _availableDevices.Length; i++)
                    {
                        var device = _availableDevices[i];
                        var productName = device.ProductName?.ToString() ?? "";
                        var deviceType = device.Type.ToString();
                        devices.Add(new DrawnUi.Camera.AudioDeviceInfo
                        {
                            Index = i,
                            Id = device.Id.ToString(),
                            Name = !string.IsNullOrEmpty(productName)
                                ? $"{productName} ({deviceType})"
                                : deviceType,
                            IsDefault = device.Type == AudioDeviceType.BuiltinMic
                        });
                        Debug.WriteLine($"[AudioCaptureAndroid] Available audio device [{i}]: {productName} (ID: {device.Id}, Type: {deviceType})");
                    }
                }
            }
            else
            {
                // Fallback for older APIs - just report default microphone
                devices.Add(new DrawnUi.Camera.AudioDeviceInfo
                {
                    Index = 0,
                    Id = "default",
                    Name = "Default Microphone",
                    IsDefault = true
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCaptureAndroid] Error getting audio devices: {ex.Message}");
        }
        return Task.FromResult(devices);
    }

    public async Task<bool> StartAsync(int sampleRate = 44100, int channels = 1, AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit, int deviceIndex = -1)
    {
        if (_isCapturing) return true;

        try
        {
            SampleRate = sampleRate;
            Channels = channels;
            BitDepth = bitDepth;

            var channelConfig = channels == 1 ? ChannelIn.Mono : ChannelIn.Stereo;
            var audioFormat = bitDepth == AudioBitDepth.Pcm8Bit ? Encoding.Pcm8bit :
                              bitDepth == AudioBitDepth.Float32Bit ? Encoding.PcmFloat :
                              Encoding.Pcm16bit; // Default and 24-bit fallback (Android doesn't support 24-bit natively usually)

            // Android doesn't natively support 24-bit packed PCM on all devices/versions basically.
            // Usually 16-bit is safe. 
            if (bitDepth == AudioBitDepth.Pcm24Bit)
            {
                 // Fallback to 16bit or Float? Plan says "Android does not natively support 24-bit; use 32-bit float and convert if needed."
                 // For now let's stick to 16-bit if requested 24-bit or use Float if supported.
                 // Ideally checking AudioRecord.GetMinBufferSize would tell us validation.
                 audioFormat = Encoding.PcmFloat; // Try float for high quality
                 BitDepth = AudioBitDepth.Float32Bit; // Adjust property
            }

            _bufferSize = AudioRecord.GetMinBufferSize(sampleRate, channelConfig, audioFormat);
            
            if (_bufferSize == (int)TrackStatus.Error || _bufferSize == (int)TrackStatus.ErrorBadValue)
            {
                 Debug.WriteLine($"[AudioCaptureAndroid] Invalid audio parameters. Rate: {sampleRate}, Ch: {channels}, Fmt: {audioFormat}");
                 return false;
            }

            // Increase buffer size for safety
            _bufferSize *= 2; 

            // Initialize AudioRecord
            // Requires RECORD_AUDIO permission
            _audioRecord = new AudioRecord(AudioSource.Mic, sampleRate, channelConfig, audioFormat, _bufferSize);

            if (_audioRecord.State != State.Initialized)
            {
                Debug.WriteLine("[AudioCaptureAndroid] AudioRecord failed to initialize.");
                return false;
            }

            // Select specific audio input device if requested (API 23+)
            if (deviceIndex >= 0 && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
            {
                // Get available devices if not already cached
                if (_availableDevices == null)
                {
                    var audioManager = (AudioManager)Android.App.Application.Context.GetSystemService(Context.AudioService);
                    _availableDevices = audioManager?.GetDevices(GetDevicesTargets.Inputs);
                }

                if (_availableDevices != null && deviceIndex < _availableDevices.Length)
                {
                    var selectedDevice = _availableDevices[deviceIndex];
                    if (_audioRecord.SetPreferredDevice(selectedDevice))
                    {
                        Debug.WriteLine($"[AudioCaptureAndroid] Selected audio device [{deviceIndex}]: {selectedDevice.ProductName?.ToString() ?? "Unknown"} (ID: {selectedDevice.Id})");
                    }
                    else
                    {
                        Debug.WriteLine($"[AudioCaptureAndroid] Failed to select audio device {deviceIndex}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[AudioCaptureAndroid] Invalid device index {deviceIndex}, using default");
                }
            }

            _audioTimestamp = new AudioTimestamp();
            _isCapturing = true; // Set flag before starting thread
            _audioRecord.StartRecording();

            _audioThread = new Thread(AudioCaptureLoop) 
            { 
                IsBackground = true,
                Name = "AudioCaptureThread" 
            };
            _audioThread.Start();

            Debug.WriteLine($"[AudioCaptureAndroid] Started. Rate: {sampleRate}, Ch: {channels}, BitDepth: {BitDepth}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCaptureAndroid] Start failed: {ex}");
            StopCaptureInternal();
            return false;
        }
    }

    public Task StopAsync()
    {
        StopCaptureInternal();
        return Task.CompletedTask;
    }

    private void StopCaptureInternal()
    {
        _isCapturing = false;
        try
        {
            if (_audioRecord != null)
            {
                if (_audioRecord.RecordingState == RecordState.Recording)
                {
                    _audioRecord.Stop();
                }
                _audioRecord.Release();
                _audioRecord = null;
            }
        }
        catch (Exception ex)
        {
             Debug.WriteLine($"[AudioCaptureAndroid] Stop error: {ex.Message}");
        }
    }

    private void AudioCaptureLoop()
    {
        // Allocation for buffer
        // Note: For PcmFloat, we need float[], for Pcm16bit short[], etc. 
        // byte[] works for Read(byte[], ...) but for Float we should use Read(float[], ...) for better performance/correctness if supported.
        // However AudioSample expects byte[] currently.
        
        byte[] buffer = new byte[_bufferSize]; 

        while (_isCapturing && _audioRecord != null)
        {
            try
            {
                int val = _audioRecord.Read(buffer, 0, _bufferSize);
                
                if (val > 0)
                {
                    long timeNs = 0;
                    
                    // Try to get precise timestamp
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.N &&
                        _audioRecord.GetTimestamp(_audioTimestamp,   AudioTimebase.Monotonic) ==  0)//AudioApi.Success)
                    {
                        timeNs = _audioTimestamp.NanoTime;
                    }
                    else
                    {
                        timeNs = Android.OS.SystemClock.ElapsedRealtimeNanos();
                    }

                    // Copy data to avoid buffer overwrites if passed by reference (AudioSample structs hold ref usually?)
                    // AudioSample defined Data as byte[]. So we need a copy.
                    byte[] dataParams = new byte[val];
                    Array.Copy(buffer, dataParams, val);

                    var sample = new AudioSample
                    {
                        Data = dataParams,
                        TimestampNs = timeNs,
                        SampleRate = SampleRate,
                        Channels = Channels,
                        BitDepth = BitDepth
                    };

                    SampleAvailable?.Invoke(this, sample);
                }
                else
                {
                    // Error or no data
                    if (val < 0)
                    {
                        Debug.WriteLine($"[AudioCaptureAndroid] Audio read error code: {val}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioCaptureAndroid] Loop error: {ex}");
                break;
            }
        }
    }

    public void Dispose()
    {
        StopCaptureInternal();
    }
}
