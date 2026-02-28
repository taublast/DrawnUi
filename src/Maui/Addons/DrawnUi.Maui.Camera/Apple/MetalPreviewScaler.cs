#if IOS || MACCATALYST
using CoreVideo;
using Metal;
using Foundation;

namespace DrawnUi.Camera
{
    /// <summary>
    /// Uses Metal compute shader to scale camera frames for preview.
    /// Uses semaphore to prevent GPU command queue backlog (Apple's triple-buffering pattern).
    /// </summary>
    public class MetalPreviewScaler : IDisposable
    {
        private IMTLDevice _device;
        private IMTLCommandQueue _commandQueue;
        private IMTLComputePipelineState _scalePipeline;
        private CVMetalTextureCache _textureCache;
        private IMTLTexture _outputTexture;
        private CVPixelBuffer _outputBuffer;

        private int _inputWidth;
        private int _inputHeight;
        private int _outputWidth;
        private int _outputHeight;
        private bool _isInitialized;

        // Semaphore to prevent GPU backlog - max 2 frames in flight
        private SemaphoreSlim _gpuSemaphore;
        private const int MaxFramesInFlight = 2;

        // Metal shader source for bilinear scaling
        private static readonly string ScaleShaderSource =
            "#include <metal_stdlib>\n" +
            "using namespace metal;\n" +
            "\n" +
            "kernel void scaleTexture(texture2d<half, access::sample> inputTexture [[texture(0)]],\n" +
            "                         texture2d<half, access::write> outputTexture [[texture(1)]],\n" +
            "                         uint2 gid [[thread_position_in_grid]])\n" +
            "{\n" +
            "    if (gid.x >= outputTexture.get_width() || gid.y >= outputTexture.get_height()) {\n" +
            "        return;\n" +
            "    }\n" +
            "\n" +
            "    float2 outputSize = float2(outputTexture.get_width(), outputTexture.get_height());\n" +
            "    float2 uv = (float2(gid) + 0.5) / outputSize;\n" +
            "\n" +
            "    constexpr sampler linearSampler(filter::linear, address::clamp_to_edge);\n" +
            "    half4 color = inputTexture.sample(linearSampler, uv);\n" +
            "\n" +
            "    outputTexture.write(color, gid);\n" +
            "}\n";

        public bool IsInitialized => _isInitialized;
        public int OutputWidth => _outputWidth;
        public int OutputHeight => _outputHeight;

        /// <summary>
        /// Initialize the Metal scaler with specified dimensions.
        /// </summary>
        public bool Initialize(int inputWidth, int inputHeight, int outputWidth, int outputHeight)
        {
            try
            {
                _inputWidth = inputWidth;
                _inputHeight = inputHeight;
                _outputWidth = outputWidth;
                _outputHeight = outputHeight;

                // Get Metal device
                _device = MTLDevice.SystemDefault;
                if (_device == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MetalPreviewScaler] No Metal device available");
                    return false;
                }

                // Create command queue
                _commandQueue = _device.CreateCommandQueue();
                if (_commandQueue == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MetalPreviewScaler] Failed to create command queue");
                    return false;
                }

                // Create texture cache
                _textureCache = new CVMetalTextureCache(_device);
                if (_textureCache == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MetalPreviewScaler] Failed to create texture cache");
                    return false;
                }

                // Compile shader
                NSError error;
                var compileOptions = new MTLCompileOptions();
                var library = _device.CreateLibrary(ScaleShaderSource, compileOptions, out error);
                //if (library == null || error != null)
                //{
                //    System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] Shader compile error: {error?.LocalizedDescription}");
                //    return false;
                //}

                var scaleFunction = library.CreateFunction("scaleTexture");
                if (scaleFunction == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MetalPreviewScaler] Failed to create scale function");
                    return false;
                }

                _scalePipeline = _device.CreateComputePipelineState(scaleFunction, out error);
                //if (_scalePipeline == null || error != null)
                //{
                //    System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] Pipeline state error: {error?.LocalizedDescription}");
                //    return false;
                //}

                // Create output texture
                var descriptor = MTLTextureDescriptor.CreateTexture2DDescriptor(
                    MTLPixelFormat.BGRA8Unorm,
                    (nuint)outputWidth,
                    (nuint)outputHeight,
                    false);
                descriptor.Usage = MTLTextureUsage.ShaderRead | MTLTextureUsage.ShaderWrite;
                _outputTexture = _device.CreateTexture(descriptor);

                // Create output CVPixelBuffer for reading back
                _outputBuffer = new CVPixelBuffer(outputWidth, outputHeight, CVPixelFormatType.CV32BGRA, null);

                // Create semaphore to limit frames in flight (prevents GPU backlog)
                _gpuSemaphore = new SemaphoreSlim(MaxFramesInFlight, MaxFramesInFlight);

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] Initialized: {inputWidth}x{inputHeight} -> {outputWidth}x{outputHeight}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] Initialize error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Scale the input CVPixelBuffer and copy result to output byte array.
        /// Returns true if successful. Skips frame if GPU is backed up (prevents lag spikes).
        /// </summary>
        public bool Scale(CVPixelBuffer inputBuffer, byte[] outputData, out int bytesPerRow)
        {
            bytesPerRow = 0;

            if (!_isInitialized || inputBuffer == null || outputData == null)
                return false;

            // Check if GPU is backed up - if so, skip this frame to prevent lag spike
            // This is the key to smooth performance: drop frames rather than queue them
            if (!_gpuSemaphore.Wait(0))  // Non-blocking check
            {
                // GPU is busy with max frames in flight - skip this frame
                return false;
            }

            try
            {
                // Create Metal texture from input CVPixelBuffer (zero-copy, reuses buffer memory)
                var inputTexture = _textureCache.TextureFromImage(
                    inputBuffer,
                    MTLPixelFormat.BGRA8Unorm,
                    inputBuffer.Width,
                    inputBuffer.Height,
                    0,
                    out CVReturn cvError);

                if (cvError != CVReturn.Success || inputTexture == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] TextureFromImage failed: {cvError}");
                    _gpuSemaphore.Release();
                    return false;
                }

                // Create command buffer
                var commandBuffer = _commandQueue.CommandBuffer();
                var computeEncoder = commandBuffer.ComputeCommandEncoder;

                // Set pipeline and textures
                computeEncoder.SetComputePipelineState(_scalePipeline);
                computeEncoder.SetTexture(inputTexture.Texture, 0);
                computeEncoder.SetTexture(_outputTexture, 1);

                // Calculate thread groups
                var threadGroupSize = new MTLSize(16, 16, 1);
                var threadGroups = new MTLSize(
                    (_outputWidth + 15) / 16,
                    (_outputHeight + 15) / 16,
                    1);

                computeEncoder.DispatchThreadgroups(threadGroups, threadGroupSize);
                computeEncoder.EndEncoding();

                // Execute and wait
                commandBuffer.Commit();
                commandBuffer.WaitUntilCompleted();

                // Copy output texture to byte array
                bytesPerRow = _outputWidth * 4;
                var region = new MTLRegion
                {
                    Origin = new MTLOrigin(0, 0, 0),
                    Size = new MTLSize(_outputWidth, _outputHeight, 1)
                };

                // Get bytes from output texture
                unsafe
                {
                    fixed (byte* ptr = outputData)
                    {
                        _outputTexture.GetBytes((IntPtr)ptr, (nuint)bytesPerRow, region, 0);
                    }
                }

                // Dispose the CVMetalTexture (not the underlying buffer)
                inputTexture.Dispose();

                // Release semaphore - allow next frame
                _gpuSemaphore.Release();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] Scale error: {ex.Message}");
                _gpuSemaphore.Release();
                return false;
            }
        }

        /// <summary>
        /// Scale from an existing Metal texture (for use with ArtOfFoto pattern).
        /// </summary>
        public bool ScaleFromTexture(IMTLTexture inputTexture, byte[] outputData, out int bytesPerRow)
        {
            bytesPerRow = 0;

            if (!_isInitialized || inputTexture == null || outputData == null)
                return false;

            // Check if GPU is backed up
            if (!_gpuSemaphore.Wait(0))
            {
                return false;
            }

            try
            {
                // Create command buffer
                var commandBuffer = _commandQueue.CommandBuffer();
                var computeEncoder = commandBuffer.ComputeCommandEncoder;

                // Set pipeline and textures
                computeEncoder.SetComputePipelineState(_scalePipeline);
                computeEncoder.SetTexture(inputTexture, 0);
                computeEncoder.SetTexture(_outputTexture, 1);

                // Calculate thread groups
                var threadGroupSize = new MTLSize(16, 16, 1);
                var threadGroups = new MTLSize(
                    (_outputWidth + 15) / 16,
                    (_outputHeight + 15) / 16,
                    1);

                computeEncoder.DispatchThreadgroups(threadGroups, threadGroupSize);
                computeEncoder.EndEncoding();

                // Execute and wait
                commandBuffer.Commit();
                commandBuffer.WaitUntilCompleted();

                // Copy output texture to byte array
                bytesPerRow = _outputWidth * 4;
                var region = new MTLRegion
                {
                    Origin = new MTLOrigin(0, 0, 0),
                    Size = new MTLSize(_outputWidth, _outputHeight, 1)
                };

                unsafe
                {
                    fixed (byte* ptr = outputData)
                    {
                        _outputTexture.GetBytes((IntPtr)ptr, (nuint)bytesPerRow, region, 0);
                    }
                }

                _gpuSemaphore.Release();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MetalPreviewScaler] ScaleFromTexture error: {ex.Message}");
                _gpuSemaphore.Release();
                return false;
            }
        }

        public void Dispose()
        {
            _outputBuffer?.Dispose();
            _outputBuffer = null;

            _outputTexture?.Dispose();
            _outputTexture = null;

            _textureCache?.Dispose();
            _textureCache = null;

            // Note: Don't dispose _device, _commandQueue, _scalePipeline - they're managed by the system
            _scalePipeline = null;
            _commandQueue = null;
            _device = null;

            _isInitialized = false;
            System.Diagnostics.Debug.WriteLine("[MetalPreviewScaler] Disposed");
        }
    }
}
#endif
