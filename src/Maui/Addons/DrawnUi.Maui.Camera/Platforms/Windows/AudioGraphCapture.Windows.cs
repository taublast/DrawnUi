using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.MediaProperties;

namespace DrawnUi.Camera.Platforms.Windows
{
    /// <summary>
    /// Frame-by-frame audio capture using AudioGraph API for real-time video processing.
    /// This provides true frame-by-frame control over audio capture, better suited for 
    /// syncing with video frame processing than MediaFrameReader.
    /// </summary>
    public class AudioGraphCapture : IAudioCapture
    {
        private bool _isCapturing;
        private AudioGraph? _audioGraph;
        private AudioDeviceInputNode? _deviceInputNode;
        private AudioFrameOutputNode? _frameOutputNode;
        private readonly object _lock = new();

        // Timestamp tracking (sample-count based for precision)
        private long _totalSamplesCaptured;
        private DateTime _captureStartTime;

        public bool IsCapturing => _isCapturing;
        public int SampleRate { get; private set; }
        public int Channels { get; private set; }

        private AudioBitDepth _bitDepth;
        private bool _disposed;

        public event EventHandler<AudioSample>? SampleAvailable;

        /// <summary>
        /// Start audio capture with optional device selection
        /// </summary>
        public async Task<bool> StartAsync(int sampleRate = 44100, int channels = 1, AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit, int deviceIndex = -1)
        {
            if (_isCapturing)
            {
                Debug.WriteLine("[AudioGraphCapture] Already capturing");
                return true;
            }

            SampleRate = sampleRate;
            Channels = channels;
            _bitDepth = bitDepth;
            _totalSamplesCaptured = 0;
            _captureStartTime = DateTime.UtcNow;

            Debug.WriteLine($"[AudioGraphCapture] Initializing - SampleRate: {sampleRate}Hz, Channels: {channels}, BitDepth: {bitDepth}");

            try
            {
                // Create AudioGraph settings with lowest latency
                // IMPORTANT: Do NOT set EncodingProperties - let AudioGraph use system defaults
                // Requesting unsupported formats (like 44100Hz/mono) causes FormatNotSupported error
                var settings = new AudioGraphSettings(global::Windows.Media.Render.AudioRenderCategory.Media)
                {
                    QuantumSizeSelectionMode = QuantumSizeSelectionMode.LowestLatency
                    // AudioGraph will use system default format (typically 48000Hz stereo)
                };

                var result = await AudioGraph.CreateAsync(settings);

                if (result.Status != AudioGraphCreationStatus.Success)
                {
                    Debug.WriteLine($"[AudioGraphCapture] Failed to create AudioGraph: {result.Status}");
                    return false;
                }

                _audioGraph = result.Graph;

                // Update with actual AudioGraph settings (may differ from requested)
                // AudioGraph controls the actual format - we must use what it provides
                SampleRate = (int)_audioGraph.EncodingProperties.SampleRate;
                Channels = (int)_audioGraph.EncodingProperties.ChannelCount;

                Debug.WriteLine($"[AudioGraphCapture] AudioGraph created successfully:");
                Debug.WriteLine($"[AudioGraphCapture]   Requested: {sampleRate}Hz/{channels}ch - Actual: {SampleRate}Hz/{Channels}ch");
                Debug.WriteLine($"[AudioGraphCapture]   SamplesPerQuantum: {_audioGraph.SamplesPerQuantum}");
                Debug.WriteLine($"[AudioGraphCapture]   LatencyInSamples: {_audioGraph.LatencyInSamples}");

                // Find audio input device
                var devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                
                Debug.WriteLine($"[AudioGraphCapture] Available audio devices ({devices.Count}):");
                for (int i = 0; i < devices.Count; i++)
                {
                    var dev = devices[i];
                    string defaultMarker = dev.IsDefault ? " [DEFAULT]" : "";
                    Debug.WriteLine($"[AudioGraphCapture]   [{i}] {dev.Name}{defaultMarker}");
                }
                
                DeviceInformation? selectedDevice;

                if (deviceIndex >= 0 && deviceIndex < devices.Count)
                {
                    selectedDevice = devices[deviceIndex];
                    Debug.WriteLine($"[AudioGraphCapture] ✓ SELECTED device [{deviceIndex}]: {selectedDevice.Name}");
                }
                else
                {
                    selectedDevice = devices.FirstOrDefault(d => d.IsDefault) ?? devices.FirstOrDefault();
                    int actualIndex = -1;
                    for (int i = 0; i < devices.Count; i++)
                    {
                        if (devices[i].Id == selectedDevice?.Id)
                        {
                            actualIndex = i;
                            break;
                        }
                    }
                    Debug.WriteLine($"[AudioGraphCapture] ✓ SELECTED default device [{actualIndex}]: {selectedDevice?.Name ?? "None"}");
                }

                if (selectedDevice == null)
                {
                    Debug.WriteLine("[AudioGraphCapture] ❌ ERROR: No audio capture device found");
                    return false;
                }

                // Create device input node
                var inputResult = await _audioGraph.CreateDeviceInputNodeAsync(
                    global::Windows.Media.Capture.MediaCategory.Media,
                    _audioGraph.EncodingProperties,
                    selectedDevice);

                if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
                {
                    Debug.WriteLine($"[AudioGraphCapture] Failed to create input node: {inputResult.Status}");
                    return false;
                }

                _deviceInputNode = inputResult.DeviceInputNode;

                // Create frame output node - this gives us frame-by-frame access
                _frameOutputNode = _audioGraph.CreateFrameOutputNode(_audioGraph.EncodingProperties);

                // Connect input to output
                _deviceInputNode.AddOutgoingConnection(_frameOutputNode);

                // Subscribe to quantum started event - this fires for each audio frame
                _audioGraph.QuantumStarted += OnAudioGraphQuantumStarted;

                // Start the audio graph
                _audioGraph.Start();
                _isCapturing = true;

                Debug.WriteLine("[AudioGraphCapture] Capture started successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioGraphCapture] StartAsync error: {ex.Message}");
                return false;
            }
        }

        // GUID for IMemoryBufferByteAccess COM interface
        private static readonly Guid IMemoryBufferByteAccessGuid = new Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D");

        /// <summary>
        /// Called for each audio quantum (frame) captured
        /// </summary>
        private void OnAudioGraphQuantumStarted(AudioGraph sender, object args)
        {
            if (!_isCapturing || _frameOutputNode == null)
                return;

            try
            {
                // Get the audio frame from the output node
                // Windows.Media.AudioFrame
                using var frame = _frameOutputNode.GetFrame();
                if (frame == null) return;

                using var buffer = frame.LockBuffer(AudioBufferAccessMode.Read);
                using var reference = buffer.CreateReference();

                // Get raw audio data using WinRT projection interop (proper way for WinRT types)
                unsafe
                {
                    // Use WinRT.MarshalInspectable - proper WinRT projection API (from AudioTest working code)
                    var referencePtr = WinRT.MarshalInspectable<global::Windows.Foundation.IMemoryBufferReference>.FromManaged(reference);
                    try
                    {
                        var iid = typeof(IMemoryBufferByteAccess).GUID;
                        Marshal.QueryInterface(referencePtr, ref iid, out var bufferByteAccessPtr);
                        try
                        {
                            var byteAccess = Marshal.GetObjectForIUnknown(bufferByteAccessPtr) as IMemoryBufferByteAccess;
                            if (byteAccess == null)
                            {
                                Debug.WriteLine($"[AudioGraphCapture] GetObjectForIUnknown returned null");
                                return;
                            }

                            byteAccess.GetBuffer(out byte* dataPtr, out uint capacity);

                            if (dataPtr == null || capacity == 0)
                                return;

                            // Convert bytes to float array (32-bit float = 4 bytes per sample)
                            int floatCount = (int)(capacity / 4);
                            float[] floatSamples = new float[floatCount];

                            fixed (float* destPtr = floatSamples)
                            {
                                Buffer.MemoryCopy(dataPtr, destPtr, capacity, capacity);
                            }

                            // Calculate sample count per channel
                            int sampleCount = floatCount / Channels;

                            // Convert float to PCM16 for encoder
                            byte[] pcmData = ConvertFloatToPcm16(floatSamples);

                            // Calculate timestamp from sample count (more precise than DateTime)
                            long timestampNs = (long)((double)_totalSamplesCaptured / SampleRate * 1_000_000_000);
                            _totalSamplesCaptured += sampleCount;

                            var sample = new AudioSample
                            {
                                Data = pcmData,
                                TimestampNs = timestampNs,
                                SampleRate = SampleRate,
                                Channels = Channels,
                                BitDepth = AudioBitDepth.Pcm16Bit
                            };

                            // Fire event (do not throw from audio callback - would crash AudioGraph)
                            try
                            {
                                SampleAvailable?.Invoke(this, sample);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[AudioGraphCapture] SampleAvailable event handler error: {ex.Message}");
                            }
                        }
                        finally
                        {
                            Marshal.Release(bufferByteAccessPtr);
                        }
                    }
                    finally
                    {
                        Marshal.Release(referencePtr);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never throw from audio callback - would crash AudioGraph
                Debug.WriteLine($"[AudioGraphCapture] Error in OnAudioGraphQuantumStarted: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts float32 samples (-1.0 to 1.0) to PCM16 byte array
        /// </summary>
        private byte[] ConvertFloatToPcm16(float[] floatSamples)
        {
            byte[] pcmData = new byte[floatSamples.Length * 2];
            for (int i = 0; i < floatSamples.Length; i++)
            {
                // Clamp to valid range and convert to 16-bit PCM
                float sample = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
                short pcmSample = (short)(sample * 32767);
                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }
            return pcmData;
        }

        public async Task StopAsync()
        {
            if (!_isCapturing)
                return;

            _isCapturing = false;

            if (_audioGraph != null)
            {
                _audioGraph.QuantumStarted -= OnAudioGraphQuantumStarted;
                _audioGraph.Stop();
            }

            Debug.WriteLine("[AudioGraphCapture] Capture stopped");
            await Task.CompletedTask;
        }

        public async Task<List<AudioDeviceInfo>> GetAvailableDevicesAsync()
        {
            var devices = new List<AudioDeviceInfo>();
            try
            {
                var audioDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                for (int i = 0; i < audioDevices.Count; i++)
                {
                    var device = audioDevices[i];
                    devices.Add(new AudioDeviceInfo
                    {
                        Index = i,
                        Id = device.Id,
                        Name = device.Name,
                        IsDefault = device.IsDefault
                    });
                    Debug.WriteLine($"[AudioGraphCapture] Available audio device [{i}]: {device.Name} (ID: {device.Id})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioGraphCapture] Error getting audio devices: {ex.Message}");
            }
            return devices;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                StopAsync().Wait(1000);

                _frameOutputNode?.Dispose();
                _deviceInputNode?.Dispose();
                _audioGraph?.Dispose();

                _frameOutputNode = null;
                _deviceInputNode = null;
                _audioGraph = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioGraphCapture] Dispose error: {ex.Message}");
            }
        }
    }


}
