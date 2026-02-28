#if IOS || MACCATALYST
using AVFoundation;
using Foundation;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using NetworkExtension;

namespace DrawnUi.Camera;

/// <summary>
/// iOS/macOS audio capture using AVAudioEngine.
/// Completely separate from camera session - no interference with video recording.
/// Follows the same pattern as Android's AudioCaptureAndroid.
/// </summary>
public class AudioCaptureApple : IAudioCapture
{
    private AVAudioEngine _audioEngine;
    private volatile bool _isCapturing;
    private long _captureStartTimeNs;

    public bool IsCapturing => _isCapturing;
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public AudioBitDepth BitDepth { get; private set; }

    public event EventHandler<AudioSample> SampleAvailable;

    string _lastError;
    public string LastError
    {
        get => _lastError;
        set
        {
            if (_lastError != value)
            {
                _lastError = value;
                Super.Log($"[AudioCaptureApple] {value}");
            }
        }
    }

    /// <summary>
    /// Get list of available audio input devices
    /// </summary>
    public Task<List<AudioDeviceInfo>> GetAvailableDevicesAsync()
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            var audioSession = AVAudioSession.SharedInstance();
            var availableInputs = audioSession.AvailableInputs;

            if (availableInputs != null)
            {
                for (int i = 0; i < availableInputs.Length; i++)
                {
                    var input = availableInputs[i];
                    devices.Add(new AudioDeviceInfo
                    {
                        Index = i,
                        Id = input.UID,
                        Name = input.PortName,
                        IsDefault = audioSession.CurrentRoute?.Inputs?.Any(r => r.UID == input.UID) ?? false
                    });
                    Debug.WriteLine($"[AudioCaptureApple] Available audio device [{i}]: {input.PortName} (UID: {input.UID}, Type: {input.PortType})");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCaptureApple] Error getting audio devices: {ex}");
        }
        return Task.FromResult(devices);
    }

    public async Task<bool> StartAsync(int sampleRate = 44100, int channels = 1,
                                        AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit, int deviceIndex = -1)
    {
        if (_isCapturing)
            return true;

        try
        {
            // Request microphone permission
            var status = AVCaptureDevice.GetAuthorizationStatus(AVAuthorizationMediaType.Audio);
            if (status == AVAuthorizationStatus.NotDetermined)
            {
                var granted = await AVCaptureDevice.RequestAccessForMediaTypeAsync(AVAuthorizationMediaType.Audio);
                if (!granted)
                {
                    LastError = "Microphone permission denied";
                    Cleanup();
                    return false;
                }
            }
            else if (status != AVAuthorizationStatus.Authorized)
            {
                LastError = $"Microphone permission status: {status}";
                Cleanup();
                return false;
            }

            // Configure audio session for recording
            var audioSession = AVAudioSession.SharedInstance();
            NSError sessionError;
            audioSession.SetCategory(AVAudioSessionCategory.PlayAndRecord,
                AVAudioSessionCategoryOptions.DefaultToSpeaker | AVAudioSessionCategoryOptions.AllowBluetooth,
                out sessionError);
            if (sessionError != null)
            {
                LastError = $"Audio session category error: {sessionError}";
                Cleanup();
                return false;
            }

            if (deviceIndex < 0)
            {
                deviceIndex = 0;
            }

            // Select specific audio input device if requested
            var availableInputs = audioSession.AvailableInputs;
            if (availableInputs != null && deviceIndex < availableInputs.Length)
            {
                var selectedInput = availableInputs[deviceIndex];
                if (audioSession.SetPreferredInput(selectedInput, out sessionError))
                {
                    Debug.WriteLine($"[AudioCaptureApple] Selected audio device [{deviceIndex}]: {selectedInput.PortName}");
                }
                else
                {
                    LastError = "Failed to select audio device: {sessionError?.LocalizedDescription}";
                    Cleanup();
                    return false;
                }
            }
            else
            {
                Debug.WriteLine($"[AudioCaptureApple] Invalid device index {deviceIndex}, using default");
            }


            audioSession.SetActive(true, out sessionError);
            if (sessionError != null)
            {
                LastError = $"Audio session activation error: {sessionError}";
                Cleanup();
                return false;
            }

            // Store requested parameters
            SampleRate = sampleRate;
            Channels = channels;
            BitDepth = bitDepth;

            // Create audio engine
            _audioEngine = new AVAudioEngine();
            var inputNode = _audioEngine.InputNode;

            // Enable voice processing for AGC, echo cancellation, and noise suppression (iOS 13+)
            try
            {
                NSError vpError;
                if (inputNode.SetVoiceProcessingEnabled(true, out vpError))
                {
                    Debug.WriteLine("[AudioCaptureApple] Voice processing enabled (AGC, echo cancellation, noise suppression)");
                }
                else
                {
                    Debug.WriteLine($"[AudioCaptureApple] Voice processing not available: {vpError?.LocalizedDescription}");
                }
            }
            catch (Exception vpEx)
            {
                Debug.WriteLine($"[AudioCaptureApple] Voice processing setup failed: {vpEx.Message}");
            }

            // Get the native format of the input (may change after enabling voice processing)
            var inputFormat = inputNode.GetBusOutputFormat(0);

            Debug.WriteLine($"[AudioCaptureApple] Input format: {inputFormat.SampleRate}Hz, {inputFormat.ChannelCount}ch");

            // Install tap on input node - use native format for best performance
            // Buffer size 4096 at 44.1kHz = ~93ms chunks, good balance of latency vs overhead
            uint bufferSize = 4096;

            inputNode.InstallTapOnBus(
                bus: 0,
                bufferSize: bufferSize,
                format: inputFormat,
                tapBlock: (buffer, when) => OnAudioBufferReceived(buffer, when)
            );

            // Start engine
            NSError engineError;
            if (!_audioEngine.StartAndReturnError(out engineError))
            {
                LastError = $"Engine start failed: {engineError?.LocalizedDescription}";
                Cleanup();
                return false;
            }

            _captureStartTimeNs = GetCurrentTimeNs();
            _isCapturing = true;

            Debug.WriteLine($"[AudioCaptureApple] Started successfully. Native format: {inputFormat.SampleRate}Hz, {inputFormat.ChannelCount}ch");
            return true;
        }
        catch (Exception ex)
        {
            LastError = $"Start failed:[{sampleRate}, {channels}, {bitDepth}, {deviceIndex}]  {ex}";
            Cleanup();
            return false;
        }
    }

    private void OnAudioBufferReceived(AVAudioPcmBuffer buffer, AVAudioTime when)
    {
        if (!_isCapturing || buffer == null || buffer.FrameLength == 0) return;

        try
        {
            var frameCount = (int)buffer.FrameLength;
            var channelCount = (int)buffer.Format.ChannelCount;
            var nativeSampleRate = buffer.Format.SampleRate;

            // CRITICAL: Always use system uptime for timestamps to match video PTS time base
            // The video CMSampleBuffer.PresentationTimeStamp uses the same monotonic clock
            // DO NOT use when.SampleTime - it's a running sample counter that doesn't reset
            // between recordings and will cause massive timestamp misalignment
            long timestampNs = GetCurrentTimeNs();

            // Convert float samples to 16-bit PCM
            // AVAudioEngine always provides float data
            byte[] pcmData = ConvertFloatToPcm16(buffer, frameCount, channelCount);
            if (pcmData == null || pcmData.Length == 0) return;

            var sample = new AudioSample
            {
                Data = pcmData,
                TimestampNs = timestampNs,
                SampleRate = (int)nativeSampleRate,
                Channels = channelCount > 1 ? 2 : 1, // Mono or stereo
                BitDepth = AudioBitDepth.Pcm16Bit
            };

            // Fire event - let subscriber handle on their thread if needed
            SampleAvailable?.Invoke(this, sample);
        }
        catch (Exception ex)
        {
            // Don't let exceptions escape the callback - it would crash the audio engine
            Debug.WriteLine($"[AudioCaptureApple] Buffer processing error: {ex.Message}");
        }
    }

    private unsafe byte[] ConvertFloatToPcm16(AVAudioPcmBuffer buffer, int frameCount, int channelCount)
    {
        try
        {
            // For mono output, just use first channel
            // For stereo, interleave channels
            int outputChannels = channelCount > 1 ? 2 : 1;
            byte[] result = new byte[frameCount * outputChannels * 2]; // 2 bytes per 16-bit sample

            // Get pointer to float data
            var floatChannelData = buffer.FloatChannelData;
            if (floatChannelData == IntPtr.Zero) return null;

            // floatChannelData is float** (array of channel pointers)
            float** channelPtrs = (float**)floatChannelData;

            int byteIndex = 0;
            for (int frame = 0; frame < frameCount; frame++)
            {
                for (int ch = 0; ch < outputChannels; ch++)
                {
                    int sourceChannel = ch < channelCount ? ch : 0;
                    float sample = channelPtrs[sourceChannel][frame];

                    // Clamp and convert to 16-bit
                    sample = Math.Clamp(sample, -1.0f, 1.0f);
                    short pcmSample = (short)(sample * 32767.0f);

                    // Little-endian
                    result[byteIndex++] = (byte)(pcmSample & 0xFF);
                    result[byteIndex++] = (byte)((pcmSample >> 8) & 0xFF);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCaptureApple] Float to PCM conversion error: {ex.Message}");
            return null;
        }
    }

    private static long GetCurrentTimeNs()
    {
        return (long)(NSProcessInfo.ProcessInfo.SystemUptime * 1_000_000_000);
    }

    public Task StopAsync()
    {
        Cleanup();
        return Task.CompletedTask;
    }

    private void Cleanup()
    {
        _isCapturing = false;

        try
        {
            if (_audioEngine != null)
            {
                var inputNode = _audioEngine.InputNode;
                inputNode?.RemoveTapOnBus(0);
                _audioEngine.Stop();
                _audioEngine.Dispose();
                _audioEngine = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioCaptureApple] Cleanup error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }
}
#endif
