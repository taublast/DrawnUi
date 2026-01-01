# Android GPU-Accelerated Frame Processing

This document details how video recording frame processing works on Android using the GPU-accelerated path with SurfaceTexture and OpenGL ES shaders.

## Overview

The GPU path provides **zero-copy** camera frame capture by keeping frames on the GPU throughout the entire pipeline:

```
Camera2 → SurfaceTexture → GL_TEXTURE_EXTERNAL_OES → OES Shader → Skia Surface → MediaCodec Encoder
```

**Key Benefits:**
- No CPU copies of camera frames
- No YUV→RGB conversion on CPU
- Hardware-accelerated color conversion via OES texture sampling
- Consistent 30fps recording even with complex overlays
- Lower battery consumption

## Architecture

### Components

```
┌─────────────────────────────────────────────────────────────────────┐
│                        SkiaCamera                                    │
│  - Coordinates recording start/stop                                  │
│  - Manages FrameProcessor/PreviewProcessor callbacks                 │
│  - Handles PreviewScale calculation                                  │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                   AndroidCaptureVideoEncoder                         │
│  - Creates EGL context and encoder surface                           │
│  - Processes frames via ProcessGpuCameraFrameAsync()                 │
│  - Manages Skia GRContext for overlay rendering                      │
│  - Handles MediaCodec encoding and muxing                            │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    GpuCameraFrameProvider                            │
│  - Coordinates camera frames with encoder                            │
│  - Thread synchronization (frame signaling)                          │
│  - Owns CameraSurfaceTextureRenderer                                 │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                 CameraSurfaceTextureRenderer                         │
│  - Creates OES texture and SurfaceTexture                            │
│  - Provides Surface for Camera2 output target                        │
│  - Handles UpdateTexImage() + GetTransformMatrix()                   │
│  - Renders OES texture via shader                                    │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│                      OesTextureShader                                │
│  - GLSL shader for sampling GL_TEXTURE_EXTERNAL_OES                  │
│  - Applies SurfaceTexture transform matrix                           │
│  - Handles front camera mirroring                                    │
│  - Hardware YUV→RGB conversion                                       │
└─────────────────────────────────────────────────────────────────────┘
```

### File Locations

| File | Purpose |
|------|---------|
| `Platforms/Android/AndroidCaptureVideoEncoder.cs` | Main encoder with GPU path |
| `Platforms/Android/GpuCameraFrameProvider.cs` | Frame synchronization |
| `Platforms/Android/CameraSurfaceTextureRenderer.cs` | SurfaceTexture management |
| `Platforms/Android/OesTextureShader.cs` | OES texture shader |
| `Platforms/Android/NativeCamera.Android.cs` | Camera2 session setup |

## GPU Frame Processing Pipeline

### 1. Initialization

When recording starts, the GPU path is initialized:

```csharp
// In AndroidCaptureVideoEncoder.InitializeGpuCameraPath()
_gpuFrameProvider = new GpuCameraFrameProvider();
_gpuFrameProvider.Initialize(_width, _height);  // Creates OES texture + SurfaceTexture
_useGpuCameraPath = true;
```

The initialization creates:
1. **OES Texture** - Special texture type for external image streams
2. **SurfaceTexture** - Android component that receives camera frames
3. **Surface** - Output target for Camera2 capture session
4. **OES Shader** - GLSL program for rendering the texture

### 2. Camera Session Setup

A dedicated Camera2 session is created with the GPU surface:

```csharp
// In NativeCamera.CreateGpuCameraSession()
var gpuSurface = encoder.GpuFrameProvider.GetCameraOutputSurface();
var outputConfig = new OutputConfiguration(gpuSurface);
// Camera2 now outputs directly to SurfaceTexture
```

### 3. Frame Available Callback

When a camera frame arrives:

```csharp
// SurfaceTexture.OnFrameAvailable fires on arbitrary thread
gpuEncoder.GpuFrameProvider.Renderer.OnFrameAvailable += (sender, surfaceTexture) =>
{
    // Signal frame available (sets flag, pulses waiting threads)
    // DO NOT call UpdateTexImage here - wrong thread!
};
```

**Critical:** `OnFrameAvailable` fires on an arbitrary Android thread, NOT the EGL context thread. We must only signal and let the encoder thread process.

### 4. Frame Processing (Encoder Thread)

```csharp
// In AndroidCaptureVideoEncoder.ProcessGpuCameraFrameAsync()

// 1. Make EGL context current
MakeCurrent();

// 2. Update SurfaceTexture (MUST be on EGL thread)
_gpuFrameProvider.TryProcessFrameNoWait(out long timestampNs);
// This calls UpdateTexImage() + GetTransformMatrix()

// 3. Reset GL state for OES rendering
ResetGlStateForOesRendering();

// 4. Render OES texture to framebuffer
GLES20.GlViewport(0, 0, _width, _height);
GLES20.GlClear(GLES20.GlColorBufferBit);
_gpuFrameProvider.RenderToFramebuffer(_width, _height, _isFrontCamera);

// 5. Apply Skia overlays (FrameProcessor)
_grContext.ResetContext();  // Skia needs GL state reset
var canvas = _skSurface.Canvas;
var frame = new DrawableFrame { Width, Height, Canvas, Time, Scale = 1f };
frameProcessor?.Invoke(frame);
canvas.Flush();
_grContext.Flush();

// 6. Submit to encoder
EGLExt.EglPresentationTimeANDROID(_eglDisplay, _eglSurface, ptsNanos);
EGL14.EglSwapBuffers(_eglDisplay, _eglSurface);

// 7. Drain encoded data
DrainEncoder(endOfStream: false, bufferingMode: IsPreRecordingMode);
```

## OES Texture Shader

### Why OES Texture?

Camera frames on Android are typically in **YUV format** (NV21, YUV_420_888, etc.). Traditional approaches require:

1. Read YUV data from camera (CPU)
2. Convert YUV→RGB on CPU (expensive!)
3. Upload RGB to GPU texture
4. Render

With **GL_TEXTURE_EXTERNAL_OES**, the GPU handles YUV→RGB conversion in hardware:

1. Camera writes directly to SurfaceTexture (zero-copy)
2. SurfaceTexture provides OES texture
3. Shader samples OES texture (hardware YUV→RGB)
4. Render

### Shader Code

**Vertex Shader (ES 3.0):**
```glsl
#version 300 es
precision highp float;

layout(location = 0) in vec2 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 uTransformMatrix;  // From SurfaceTexture.GetTransformMatrix()
uniform int uMirror;            // 1 for front camera

out vec2 vTexCoord;

void main() {
    gl_Position = vec4(aPosition, 0.0, 1.0);

    // Apply SurfaceTexture transform (handles rotation, scaling)
    vec4 transformedCoord = uTransformMatrix * vec4(aTexCoord, 0.0, 1.0);
    vTexCoord = transformedCoord.xy;

    // Mirror horizontally for front camera (selfie mode)
    if (uMirror == 1) {
        vTexCoord.x = 1.0 - vTexCoord.x;
    }
}
```

**Fragment Shader (ES 3.0):**
```glsl
#version 300 es
#extension GL_OES_EGL_image_external_essl3 : require
precision highp float;

uniform samplerExternalOES uTexture;  // OES sampler type

in vec2 vTexCoord;
out vec4 fragColor;

void main() {
    // Hardware YUV→RGB conversion happens here!
    fragColor = texture(uTexture, vTexCoord);
}
```

### Transform Matrix

The `SurfaceTexture.GetTransformMatrix()` provides a 4x4 matrix that handles:
- Camera sensor orientation (0°, 90°, 180°, 270°)
- Aspect ratio correction
- Any platform-specific transformations

This matrix is applied in the vertex shader to correctly orient the camera frame.

## Thread Synchronization

### The Problem

- `OnFrameAvailable` is called on an arbitrary Android thread
- `UpdateTexImage()` MUST be called on the thread that owns the EGL context
- `UpdateTexImage()` MUST be called before `GetTransformMatrix()`

### The Solution

```csharp
public class GpuCameraFrameProvider
{
    private volatile bool _frameAvailable;
    private readonly object _frameLock = new();

    // Called on arbitrary thread
    private void OnFrameAvailable(object sender, SurfaceTexture surfaceTexture)
    {
        lock (_frameLock)
        {
            _frameAvailable = true;
            Monitor.PulseAll(_frameLock);  // Wake up encoder thread
        }
    }

    // Called on EGL thread
    public bool TryProcessFrameNoWait(out long timestampNs)
    {
        lock (_frameLock)
        {
            if (!_frameAvailable) return false;
            _frameAvailable = false;
        }

        // Now safe to call on EGL thread
        _renderer.UpdateTexImage();
        return true;
    }
}
```

### Context Reattachment

If the SurfaceTexture is created on one thread but used on another:

```csharp
// In CameraSurfaceTextureRenderer.UpdateTexImage()
if (currentThreadId != _creationThreadId && !_needsReattach)
{
    _needsReattach = true;
}

if (_needsReattach)
{
    _surfaceTexture.DetachFromGLContext();
    _surfaceTexture.AttachToGLContext(_oesTextureId);
    _creationThreadId = currentThreadId;
    _needsReattach = false;
}
```

## Pre-Recording Support

The GPU path fully supports pre-recording (buffering frames before user presses record):

```csharp
// Frame processing checks buffering mode
bool bufferingMode = IsPreRecordingMode && _preRecordingBuffer != null;

if (bufferingMode)
{
    // Buffer encoded frames to circular buffer
    DrainEncoder(endOfStream: false, bufferingMode: true);
}
else
{
    // Write directly to muxer
    DrainEncoder(endOfStream: false, bufferingMode: false);
}
```

When user presses record:
1. `StartAsync()` is called
2. Buffered frames are written to muxer
3. `IsPreRecordingMode` is set to `false`
4. Subsequent frames go directly to muxer

## Preview During Recording

During GPU recording, preview is provided by the separate ImageReader stream (not the encoder output):

```csharp
// Preview comes from ImageReader callback
// This avoids expensive GPU→CPU transfer

// In NativeCamera.IOnImageAvailableListener
Preview = outImage;  // Raw camera frame
FormsControl.UpdatePreview();

// PreviewProcessor can be applied for overlay on preview
// (different from FrameProcessor which is for recording)
```

## Fallback Strategy

The GPU path falls back to CPU path when:

1. **API < 26** - SurfaceTexture behavior unreliable
2. **GL_OES_EGL_image_external not supported** - Rare on modern devices
3. **Initialization failure** - Surface/texture creation failed

```csharp
bool useGpuPath = GpuCameraFrameProvider.IsSupported();
if (useGpuPath)
{
    try
    {
        useGpuPath = androidEncoder.InitializeGpuCameraPath(isFrontCamera);
    }
    catch
    {
        useGpuPath = false;
    }
}

if (!useGpuPath)
{
    // Fall back to legacy ImageReader + RenderScript/CPU path
    _androidPreviewHandler = async (captured) => { ... };
}
```

## Performance Characteristics

| Metric | GPU Path | CPU Path (Legacy) |
|--------|----------|-------------------|
| Frame processing | ~2-5ms | ~15-30ms |
| Memory copies | 0 | 2-3 |
| CPU usage | Low | High |
| Power consumption | Lower | Higher |
| Max stable FPS | 30+ | 20-25 |

## API Requirements

- **Minimum:** Android 8.0 (API 26)
- **OpenGL ES:** 3.0 preferred, 2.0 fallback
- **Extension:** `GL_OES_EGL_image_external` (widely supported)

## Debugging

Enable verbose logging:
```csharp
// Key log tags:
// [GpuCameraFrameProvider] - Frame synchronization
// [CameraSurfaceTextureRenderer] - Texture updates
// [OesTextureShader] - Shader compilation
// [AndroidEncoder] - Encoder state
// [SurfaceTexture] - Context reattachment
```

Common issues:
- **Black frames:** GL state not reset before OES rendering
- **Frozen frames:** UpdateTexImage not called on EGL thread
- **Rotated frames:** Transform matrix not applied in shader
- **Mirrored incorrectly:** uMirror uniform not set for front camera
