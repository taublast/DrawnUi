using System.Diagnostics;
using System.Runtime.InteropServices;
using global::Windows.Win32;
using global::Windows.Win32.Foundation;
using global::Windows.Win32.Media.MediaFoundation;

namespace DrawnUi.Camera.Platforms.Windows;

/// <summary>
/// Windows AAC audio encoder using Direct MFT approach.
/// Creates AAC encoder via CoCreateInstance to bypass IMFSinkWriter's encoder search limitation.
/// Provides access to raw PCM samples for visualization (oscillograph, level meters).
/// </summary>
public class WindowsAudioEncoder : IDisposable
{
    // Microsoft AAC Encoder CLSID
    private static readonly Guid CLSID_AACMFTEncoder = new Guid("93AF0C51-2275-45d2-A35B-F2BA21CAED00");

    // COM interface GUIDs
    private static readonly Guid IID_IMFTransform = new Guid("bf94c121-5b05-4e6f-8000-ba598961414d");

    private const uint CLSCTX_INPROC_SERVER = 1;

    // MFT message constants
    private const uint MFT_MESSAGE_NOTIFY_BEGIN_STREAMING = 0x10000000;
    private const uint MFT_MESSAGE_NOTIFY_END_OF_STREAM = 0x10000001;
    private const uint MFT_MESSAGE_NOTIFY_START_OF_STREAM = 0x10000003;
    private const uint MFT_MESSAGE_COMMAND_DRAIN = 0x00000001;

    // Audio configuration
    private int _sampleRate;
    private int _channels;
    private AudioBitDepth _bitDepth;
    private int _bytesPerSample;
    private int _blockAlign;

    // Encoder state
    private IMFTransform _encoder;
    private IMFMediaType _internalAacOutputType; // For use with internal IMFTransform
    private global::Windows.Win32.Media.MediaFoundation.IMFMediaType _aacOutputType; // For use with CsWin32 SinkWriter
    private bool _isInitialized;
    private bool _isStarted;
    private readonly object _encoderLock = new();

    // Sample queue for encoded AAC output
    private readonly Queue<EncodedAudioFrame> _encodedFrames = new();
    private readonly object _queueLock = new();

    // Raw PCM access for visualization
    public event EventHandler<ReadOnlyMemory<byte>> RawPcmAvailable;

    /// <summary>
    /// Gets the AAC output media type for configuring SinkWriter passthrough.
    /// Internal because it uses CsWin32 types.
    /// </summary>
    internal global::Windows.Win32.Media.MediaFoundation.IMFMediaType AacOutputType => _aacOutputType;

    /// <summary>
    /// Gets whether the encoder is initialized and ready.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the configured sample rate.
    /// </summary>
    public int SampleRate => _sampleRate;

    /// <summary>
    /// Gets the configured channel count.
    /// </summary>
    public int Channels => _channels;

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        ref Guid riid,
        out IntPtr ppv);

    /// <summary>
    /// Initializes the AAC encoder with specified audio parameters.
    /// </summary>
    /// <param name="sampleRate">Sample rate (e.g., 48000)</param>
    /// <param name="channels">Number of channels (1 or 2)</param>
    /// <param name="bitDepth">Audio bit depth</param>
    /// <returns>True if initialization succeeded</returns>
    public bool Initialize(int sampleRate, int channels, AudioBitDepth bitDepth = AudioBitDepth.Pcm16Bit)
    {
        if (_isInitialized)
            return true;

        _sampleRate = sampleRate;
        _channels = channels;
        _bitDepth = bitDepth;
        _bytesPerSample = bitDepth switch
        {
            AudioBitDepth.Pcm8Bit => 1,
            AudioBitDepth.Pcm16Bit => 2,
            AudioBitDepth.Pcm24Bit => 3,
            AudioBitDepth.Float32Bit => 4,
            _ => 2
        };
        _blockAlign = _channels * _bytesPerSample;

        try
        {
            // Step 1: Create AAC encoder via CoCreateInstance
            var clsid = CLSID_AACMFTEncoder;
            var iid = IID_IMFTransform;
            int hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_INPROC_SERVER, ref iid, out IntPtr transformPtr);

            if (hr < 0)
            {
                Debug.WriteLine($"[WindowsAudioEncoder] CoCreateInstance failed: 0x{hr:X8}");
                return false;
            }

            _encoder = (IMFTransform)Marshal.GetObjectForIUnknown(transformPtr);
            Marshal.Release(transformPtr);

            // Step 2: Find and set AAC output type matching our config
            var outputTypes = FindMatchingOutputType(_sampleRate, _channels);
            if (outputTypes == null)
            {
                Debug.WriteLine($"[WindowsAudioEncoder] No matching AAC output type for {_sampleRate}Hz, {_channels}ch");
                Dispose();
                return false;
            }

            _internalAacOutputType = outputTypes.Value.internalType;
            _aacOutputType = outputTypes.Value.csWin32Type;

            _encoder.SetOutputType(0, _internalAacOutputType, 0);

            // Step 3: Find and set PCM input type matching our config
            var pcmInputType = FindMatchingInputType(_sampleRate, _channels, _bitDepth);
            if (pcmInputType == null)
            {
                Debug.WriteLine($"[WindowsAudioEncoder] No matching PCM input type for {_sampleRate}Hz, {_channels}ch");
                Dispose();
                return false;
            }

            _encoder.SetInputType(0, pcmInputType, 0);
            Marshal.ReleaseComObject(pcmInputType);

            _isInitialized = true;
            Debug.WriteLine($"[WindowsAudioEncoder] Initialized: {_sampleRate}Hz, {_channels}ch, {_bitDepth}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsAudioEncoder] Initialize failed: {ex.Message}");
            Dispose();
            return false;
        }
    }

    /// <summary>
    /// Starts the encoder streaming session.
    /// </summary>
    public void Start()
    {
        if (!_isInitialized || _isStarted)
            return;

        lock (_encoderLock)
        {
            _encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_BEGIN_STREAMING, UIntPtr.Zero);
            _encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_START_OF_STREAM, UIntPtr.Zero);
            _isStarted = true;
        }

        Debug.WriteLine("[WindowsAudioEncoder] Started");
    }

    /// <summary>
    /// Encodes a PCM audio sample to AAC.
    /// Also fires RawPcmAvailable event for visualization.
    /// </summary>
    /// <param name="sample">Audio sample with PCM data</param>
    public void EncodeSample(AudioSample sample)
    {
        if (!_isInitialized || !_isStarted || sample.Data == null || sample.Data.Length == 0)
            return;

        // Fire event for visualization (oscillograph, level meters)
        RawPcmAvailable?.Invoke(this, sample.Data);

        lock (_encoderLock)
        {
            try
            {
                // Create input sample
                var inputSample = CreateInputSample(sample.Data, sample.TimestampNs);
                if (inputSample == null)
                    return;

                // Feed to encoder
                IntPtr samplePtr = Marshal.GetIUnknownForObject(inputSample);
                try
                {
                    _encoder.ProcessInput(0, samplePtr, 0);
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0xC00D36B5))
                {
                    // MF_E_NOTACCEPTING - encoder buffer full, drain first
                    DrainEncoderOutput();
                    _encoder.ProcessInput(0, samplePtr, 0);
                }
                finally
                {
                    Marshal.Release(samplePtr);
                    Marshal.ReleaseComObject(inputSample);
                }

                // Try to get output
                DrainEncoderOutput();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsAudioEncoder] EncodeSample error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the next encoded AAC frame, or null if none available.
    /// </summary>
    public EncodedAudioFrame? GetEncodedFrame()
    {
        lock (_queueLock)
        {
            return _encodedFrames.Count > 0 ? _encodedFrames.Dequeue() : null;
        }
    }

    /// <summary>
    /// Gets all available encoded AAC frames.
    /// </summary>
    public EncodedAudioFrame[] GetAllEncodedFrames()
    {
        lock (_queueLock)
        {
            var frames = _encodedFrames.ToArray();
            _encodedFrames.Clear();
            return frames;
        }
    }

    /// <summary>
    /// Stops encoding and drains remaining samples.
    /// </summary>
    public void Stop()
    {
        if (!_isStarted)
            return;

        lock (_encoderLock)
        {
            try
            {
                _encoder.ProcessMessage(MFT_MESSAGE_NOTIFY_END_OF_STREAM, UIntPtr.Zero);
                _encoder.ProcessMessage(MFT_MESSAGE_COMMAND_DRAIN, UIntPtr.Zero);

                // Drain remaining output
                DrainEncoderOutput();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowsAudioEncoder] Stop error: {ex.Message}");
            }

            _isStarted = false;
        }

        Debug.WriteLine("[WindowsAudioEncoder] Stopped");
    }

    /// <summary>
    /// Clears the encoded frame queue.
    /// </summary>
    public void ClearQueue()
    {
        lock (_queueLock)
        {
            _encodedFrames.Clear();
        }
    }

    private (IMFMediaType internalType, global::Windows.Win32.Media.MediaFoundation.IMFMediaType csWin32Type)? FindMatchingOutputType(int sampleRate, int channels)
    {
        for (uint i = 0; i < 50; i++)
        {
            try
            {
                _encoder.GetOutputAvailableType(0, i, out var availType);
                uint rate = 0, ch = 0;
                try { availType.GetUINT32(AudioMFGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, out rate); } catch { }
                try { availType.GetUINT32(AudioMFGuids.MF_MT_AUDIO_NUM_CHANNELS, out ch); } catch { }

                if (rate == (uint)sampleRate && ch == (uint)channels)
                {
                    // Convert to CsWin32 type via COM interop (both point to same underlying COM object)
                    IntPtr ptr = Marshal.GetIUnknownForObject(availType);
                    try
                    {
                        var csWin32Type = (global::Windows.Win32.Media.MediaFoundation.IMFMediaType)Marshal.GetObjectForIUnknown(ptr);
                        return (availType, csWin32Type);
                    }
                    finally
                    {
                        Marshal.Release(ptr);
                    }
                }
                Marshal.ReleaseComObject(availType);
            }
            catch (COMException)
            {
                break;
            }
        }
        return null;
    }

    private IMFMediaType FindMatchingInputType(int sampleRate, int channels, AudioBitDepth bitDepth)
    {
        // Determine target subtype based on bit depth
        Guid targetSubtype = bitDepth == AudioBitDepth.Float32Bit
            ? AudioMFGuids.MFAudioFormat_Float
            : AudioMFGuids.MFAudioFormat_PCM;

        for (uint i = 0; i < 50; i++)
        {
            try
            {
                _encoder.GetInputAvailableType(0, i, out var availType);
                Guid subtype = Guid.Empty;
                uint rate = 0, ch = 0;
                try { availType.GetGUID(AudioMFGuids.MF_MT_SUBTYPE, out subtype); } catch { }
                try { availType.GetUINT32(AudioMFGuids.MF_MT_AUDIO_SAMPLES_PER_SECOND, out rate); } catch { }
                try { availType.GetUINT32(AudioMFGuids.MF_MT_AUDIO_NUM_CHANNELS, out ch); } catch { }

                if (subtype == targetSubtype && rate == (uint)sampleRate && ch == (uint)channels)
                {
                    return availType;
                }
                Marshal.ReleaseComObject(availType);
            }
            catch (COMException)
            {
                break;
            }
        }
        return null;
    }

    private unsafe IMFSample CreateInputSample(byte[] pcmData, long timestampNs)
    {
        var hr = PInvoke.MFCreateMemoryBuffer((uint)pcmData.Length, out var buffer);
        if (hr.Failed)
            return null;

        try
        {
            byte* bufPtr;
            uint maxLen, curLen;
            buffer.Lock(&bufPtr, &maxLen, &curLen);
            Marshal.Copy(pcmData, 0, (IntPtr)bufPtr, pcmData.Length);
            buffer.Unlock();
            buffer.SetCurrentLength((uint)pcmData.Length);

            hr = PInvoke.MFCreateSample(out var sample);
            if (hr.Failed)
            {
                Marshal.ReleaseComObject(buffer);
                return null;
            }

            sample.AddBuffer(buffer);

            // Convert nanoseconds to 100-nanosecond units
            long timestamp100ns = timestampNs / 100;
            sample.SetSampleTime(timestamp100ns);

            // Calculate duration based on sample count
            int sampleCount = pcmData.Length / _blockAlign;
            long duration100ns = (long)(sampleCount * 10_000_000L / _sampleRate);
            sample.SetSampleDuration(duration100ns);

            Marshal.ReleaseComObject(buffer);
            return sample;
        }
        catch
        {
            Marshal.ReleaseComObject(buffer);
            return null;
        }
    }

    private void DrainEncoderOutput()
    {
        while (true)
        {
            var frame = GetEncoderOutputFrame();
            if (frame == null)
                break;

            lock (_queueLock)
            {
                _encodedFrames.Enqueue(frame.Value);
            }
        }
    }

    private EncodedAudioFrame? GetEncoderOutputFrame()
    {
        var hr = PInvoke.MFCreateSample(out var outputSample);
        if (hr.Failed)
            return null;

        hr = PInvoke.MFCreateMemoryBuffer(8192, out var outputBuffer);
        if (hr.Failed)
        {
            Marshal.ReleaseComObject(outputSample);
            return null;
        }

        outputSample.AddBuffer(outputBuffer);
        Marshal.ReleaseComObject(outputBuffer);

        IntPtr samplePtr = Marshal.GetIUnknownForObject(outputSample);

        var outputDataBuffer = new MFT_OUTPUT_DATA_BUFFER
        {
            dwStreamID = 0,
            pSample = samplePtr,
            dwStatus = 0,
            pEvents = IntPtr.Zero
        };

        int structSize = Marshal.SizeOf<MFT_OUTPUT_DATA_BUFFER>();
        IntPtr outputBufferPtr = Marshal.AllocHGlobal(structSize);
        Marshal.StructureToPtr(outputDataBuffer, outputBufferPtr, false);

        try
        {
            _encoder.ProcessOutput(0, 1, outputBufferPtr, out uint status);

            // Extract encoded data from sample
            outputSample.GetSampleTime(out long timestamp);
            outputSample.GetSampleDuration(out long duration);
            outputSample.GetBufferByIndex(0, out var resultBuffer);

            unsafe
            {
                byte* dataPtr;
                uint maxLen, curLen;
                resultBuffer.Lock(&dataPtr, &maxLen, &curLen);

                byte[] aacData = new byte[curLen];
                Marshal.Copy((IntPtr)dataPtr, aacData, 0, (int)curLen);

                resultBuffer.Unlock();
                Marshal.ReleaseComObject(resultBuffer);

                Marshal.Release(samplePtr);
                Marshal.ReleaseComObject(outputSample);

                return new EncodedAudioFrame
                {
                    Data = aacData,
                    Timestamp100ns = timestamp,
                    Duration100ns = duration
                };
            }
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0xC00D6D72))
        {
            // MF_E_TRANSFORM_NEED_MORE_INPUT - no output available
            Marshal.Release(samplePtr);
            Marshal.ReleaseComObject(outputSample);
            return null;
        }
        catch
        {
            Marshal.Release(samplePtr);
            Marshal.ReleaseComObject(outputSample);
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(outputBufferPtr);
        }
    }

    public void Dispose()
    {
        if (_isStarted)
            Stop();

        if (_encoder != null)
        {
            Marshal.ReleaseComObject(_encoder);
            _encoder = null;
        }

        if (_internalAacOutputType != null)
        {
            Marshal.ReleaseComObject(_internalAacOutputType);
            _internalAacOutputType = null;
        }

        // Note: _aacOutputType points to the same underlying COM object as _internalAacOutputType,
        // so we just null the reference without releasing
        _aacOutputType = null;

        lock (_queueLock)
        {
            _encodedFrames.Clear();
        }

        _isInitialized = false;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_OUTPUT_DATA_BUFFER
    {
        public uint dwStreamID;
        public IntPtr pSample;
        public uint dwStatus;
        public IntPtr pEvents;
    }

    [ComImport]
    [Guid("bf94c121-5b05-4e6f-8000-ba598961414d")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMFTransform
    {
        void GetStreamLimits(out uint pdwInputMinimum, out uint pdwInputMaximum, out uint pdwOutputMinimum, out uint pdwOutputMaximum);
        void GetStreamCount(out uint pcInputStreams, out uint pcOutputStreams);
        void GetStreamIDs(uint dwInputIDArraySize, [Out] uint[] pdwInputIDs, uint dwOutputIDArraySize, [Out] uint[] pdwOutputIDs);
        void GetInputStreamInfo(uint dwInputStreamID, out IntPtr pStreamInfo);
        void GetOutputStreamInfo(uint dwOutputStreamID, out IntPtr pStreamInfo);
        void GetAttributes([MarshalAs(UnmanagedType.IUnknown)] out object pAttributes);
        void GetInputStreamAttributes(uint dwInputStreamID, [MarshalAs(UnmanagedType.IUnknown)] out object pAttributes);
        void GetOutputStreamAttributes(uint dwOutputStreamID, [MarshalAs(UnmanagedType.IUnknown)] out object pAttributes);
        void DeleteInputStream(uint dwStreamID);
        void AddInputStreams(uint cStreams, [In] uint[] adwStreamIDs);
        void GetInputAvailableType(uint dwInputStreamID, uint dwTypeIndex, out IMFMediaType ppType);
        void GetOutputAvailableType(uint dwOutputStreamID, uint dwTypeIndex, out IMFMediaType ppType);
        void SetInputType(uint dwInputStreamID, IMFMediaType pType, uint dwFlags);
        void SetOutputType(uint dwOutputStreamID, IMFMediaType pType, uint dwFlags);
        void GetInputCurrentType(uint dwInputStreamID, out IMFMediaType ppType);
        void GetOutputCurrentType(uint dwOutputStreamID, out IMFMediaType ppType);
        void GetInputStatus(uint dwInputStreamID, out uint pdwFlags);
        void GetOutputStatus(out uint pdwFlags);
        void SetOutputBounds(long hnsLowerBound, long hnsUpperBound);
        void ProcessEvent(uint dwInputStreamID, IntPtr pEvent);
        void ProcessMessage(uint eMessage, UIntPtr ulParam);
        void ProcessInput(uint dwInputStreamID, IntPtr pSample, uint dwFlags);
        void ProcessOutput(uint dwFlags, uint cOutputBufferCount, IntPtr pOutputSamples, out uint pdwStatus);
    }
}

/// <summary>
/// Represents an encoded AAC audio frame ready for writing to SinkWriter.
/// </summary>
public struct EncodedAudioFrame
{
    public byte[] Data;
    public long Timestamp100ns;
    public long Duration100ns;
}

/// <summary>
/// Media Foundation GUIDs for audio encoding.
/// </summary>
internal static class AudioMFGuids
{
    public static readonly Guid MF_MT_MAJOR_TYPE = new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    public static readonly Guid MF_MT_SUBTYPE = new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

    public static readonly Guid MFMediaType_Audio = new Guid("73646961-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFAudioFormat_AAC = new Guid("00001610-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFAudioFormat_PCM = new Guid("00000001-0000-0010-8000-00aa00389b71");
    public static readonly Guid MFAudioFormat_Float = new Guid("00000003-0000-0010-8000-00aa00389b71");

    public static readonly Guid MF_MT_AUDIO_SAMPLES_PER_SECOND = new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba");
    public static readonly Guid MF_MT_AUDIO_NUM_CHANNELS = new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a");
    public static readonly Guid MF_MT_AUDIO_BITS_PER_SAMPLE = new Guid("f2deb57f-330f-481f-986a-4301d512cf9f");
    public static readonly Guid MF_MT_AUDIO_BLOCK_ALIGNMENT = new Guid("322de230-9eeb-43bd-ab7a-ff412251541d");
    public static readonly Guid MF_MT_AUDIO_AVG_BYTES_PER_SECOND = new Guid("1aab75c8-29bb-443f-95bb-584637e66c9f");
}
