#if ANDROID
using Android.Opengl;
using System;

namespace DrawnUi.Camera
{
    /// <summary>
    /// OpenGL ES shader for sampling GL_TEXTURE_EXTERNAL_OES from SurfaceTexture.
    /// Used for zero-copy GPU camera frame rendering.
    /// </summary>
    public class OesTextureShader : IDisposable
    {
        private int _program;
        private int _vertexShader;
        private int _fragmentShader;

        // Attribute locations
        private int _aPosition;
        private int _aTexCoord;

        // Uniform locations
        private int _uTransformMatrix;
        private int _uTexture;
        private int _uMirror;

        // Vertex data for full-screen quad
        private static readonly float[] QuadVertices = {
            // Position (x,y)  TexCoord (u,v)
            -1f, -1f,          0f, 0f,
             1f, -1f,          1f, 0f,
            -1f,  1f,          0f, 1f,
             1f,  1f,          1f, 1f,
        };

        private int _vbo;
        private bool _initialized;

        // Vertex shader with transform matrix support
        private const string VertexShaderSource = @"#version 300 es
precision highp float;

layout(location = 0) in vec2 aPosition;
layout(location = 1) in vec2 aTexCoord;

uniform mat4 uTransformMatrix;
uniform int uMirror;

out vec2 vTexCoord;

void main() {
    gl_Position = vec4(aPosition, 0.0, 1.0);

    // Apply SurfaceTexture transform matrix
    vec4 transformedCoord = uTransformMatrix * vec4(aTexCoord, 0.0, 1.0);
    vTexCoord = transformedCoord.xy;

    // Mirror horizontally for front camera
    if (uMirror == 1) {
        vTexCoord.x = 1.0 - vTexCoord.x;
    }
}";

        // Fragment shader for external OES texture sampling
        private const string FragmentShaderSource = @"#version 300 es
#extension GL_OES_EGL_image_external_essl3 : require
precision highp float;

uniform samplerExternalOES uTexture;

in vec2 vTexCoord;
out vec4 fragColor;

void main() {
    fragColor = texture(uTexture, vTexCoord);
}";

        // Fallback fragment shader for older devices
        private const string FragmentShaderSourceLegacy = @"#extension GL_OES_EGL_image_external : require
precision mediump float;

uniform samplerExternalOES uTexture;

varying vec2 vTexCoord;

void main() {
    gl_FragColor = texture2D(uTexture, vTexCoord);
}";

        // Fallback vertex shader for older devices
        private const string VertexShaderSourceLegacy = @"
attribute vec2 aPosition;
attribute vec2 aTexCoord;

uniform mat4 uTransformMatrix;
uniform int uMirror;

varying vec2 vTexCoord;

void main() {
    gl_Position = vec4(aPosition, 0.0, 1.0);

    vec4 transformedCoord = uTransformMatrix * vec4(aTexCoord, 0.0, 1.0);
    vTexCoord = transformedCoord.xy;

    if (uMirror == 1) {
        vTexCoord.x = 1.0 - vTexCoord.x;
    }
}";

        public bool IsInitialized => _initialized;

        /// <summary>
        /// Initialize the shader program. Must be called on GL thread with valid context.
        /// </summary>
        public bool Initialize()
        {
            if (_initialized) return true;

            try
            {
                // Try ES 3.0 shaders first
                if (!TryCompileProgram(VertexShaderSource, FragmentShaderSource))
                {
                    // Fall back to ES 2.0 shaders
                    System.Diagnostics.Debug.WriteLine("[OesTextureShader] ES 3.0 failed, trying ES 2.0 fallback");
                    if (!TryCompileProgram(VertexShaderSourceLegacy, FragmentShaderSourceLegacy))
                    {
                        System.Diagnostics.Debug.WriteLine("[OesTextureShader] Failed to compile shader program");
                        return false;
                    }
                }

                // Get attribute locations
                _aPosition = GLES20.GlGetAttribLocation(_program, "aPosition");
                _aTexCoord = GLES20.GlGetAttribLocation(_program, "aTexCoord");

                // Get uniform locations
                _uTransformMatrix = GLES20.GlGetUniformLocation(_program, "uTransformMatrix");
                _uTexture = GLES20.GlGetUniformLocation(_program, "uTexture");
                _uMirror = GLES20.GlGetUniformLocation(_program, "uMirror");

                // Create VBO for quad vertices
                int[] vbos = new int[1];
                GLES20.GlGenBuffers(1, vbos, 0);
                _vbo = vbos[0];

                GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _vbo);

                var vertexBuffer = Java.Nio.ByteBuffer.AllocateDirect(QuadVertices.Length * 4)
                    .Order(Java.Nio.ByteOrder.NativeOrder())
                    .AsFloatBuffer();
                vertexBuffer.Put(QuadVertices);
                vertexBuffer.Position(0);

                GLES20.GlBufferData(GLES20.GlArrayBuffer, QuadVertices.Length * 4, vertexBuffer, GLES20.GlStaticDraw);
                GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);

                _initialized = true;
                System.Diagnostics.Debug.WriteLine("[OesTextureShader] Initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OesTextureShader] Initialize error: {ex.Message}");
                return false;
            }
        }

        private bool TryCompileProgram(string vertexSource, string fragmentSource)
        {
            // Compile vertex shader
            _vertexShader = GLES20.GlCreateShader(GLES20.GlVertexShader);
            GLES20.GlShaderSource(_vertexShader, vertexSource);
            GLES20.GlCompileShader(_vertexShader);

            int[] compiled = new int[1];
            GLES20.GlGetShaderiv(_vertexShader, GLES20.GlCompileStatus, compiled, 0);
            if (compiled[0] == 0)
            {
                string log = GLES20.GlGetShaderInfoLog(_vertexShader);
                System.Diagnostics.Debug.WriteLine($"[OesTextureShader] Vertex shader compile error: {log}");
                GLES20.GlDeleteShader(_vertexShader);
                _vertexShader = 0;
                return false;
            }

            // Compile fragment shader
            _fragmentShader = GLES20.GlCreateShader(GLES20.GlFragmentShader);
            GLES20.GlShaderSource(_fragmentShader, fragmentSource);
            GLES20.GlCompileShader(_fragmentShader);

            GLES20.GlGetShaderiv(_fragmentShader, GLES20.GlCompileStatus, compiled, 0);
            if (compiled[0] == 0)
            {
                string log = GLES20.GlGetShaderInfoLog(_fragmentShader);
                System.Diagnostics.Debug.WriteLine($"[OesTextureShader] Fragment shader compile error: {log}");
                GLES20.GlDeleteShader(_vertexShader);
                GLES20.GlDeleteShader(_fragmentShader);
                _vertexShader = 0;
                _fragmentShader = 0;
                return false;
            }

            // Link program
            _program = GLES20.GlCreateProgram();
            GLES20.GlAttachShader(_program, _vertexShader);
            GLES20.GlAttachShader(_program, _fragmentShader);
            GLES20.GlLinkProgram(_program);

            int[] linked = new int[1];
            GLES20.GlGetProgramiv(_program, GLES20.GlLinkStatus, linked, 0);
            if (linked[0] == 0)
            {
                string log = GLES20.GlGetProgramInfoLog(_program);
                System.Diagnostics.Debug.WriteLine($"[OesTextureShader] Program link error: {log}");
                GLES20.GlDeleteProgram(_program);
                GLES20.GlDeleteShader(_vertexShader);
                GLES20.GlDeleteShader(_fragmentShader);
                _program = 0;
                _vertexShader = 0;
                _fragmentShader = 0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Draw the OES texture to the current framebuffer.
        /// </summary>
        /// <param name="textureId">OES texture ID from SurfaceTexture</param>
        /// <param name="transformMatrix">4x4 transform matrix from SurfaceTexture.GetTransformMatrix()</param>
        /// <param name="mirror">True to mirror horizontally (for front camera)</param>
        public void Draw(int textureId, float[] transformMatrix, bool mirror)
        {
            if (!_initialized) return;

            GLES20.GlUseProgram(_program);

            // Bind texture
            GLES20.GlActiveTexture(GLES20.GlTexture0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, textureId);
            GLES20.GlUniform1i(_uTexture, 0);

            // Set transform matrix
            GLES20.GlUniformMatrix4fv(_uTransformMatrix, 1, false, transformMatrix, 0);

            // Set mirror flag
            GLES20.GlUniform1i(_uMirror, mirror ? 1 : 0);

            // Bind VBO and set vertex attributes
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, _vbo);

            // Position attribute: 2 floats, stride = 4 floats (16 bytes), offset = 0
            GLES20.GlEnableVertexAttribArray(_aPosition);
            GLES20.GlVertexAttribPointer(_aPosition, 2, GLES20.GlFloat, false, 16, 0);

            // TexCoord attribute: 2 floats, stride = 4 floats (16 bytes), offset = 2 floats (8 bytes)
            GLES20.GlEnableVertexAttribArray(_aTexCoord);
            GLES20.GlVertexAttribPointer(_aTexCoord, 2, GLES20.GlFloat, false, 16, 8);

            // Draw quad as triangle strip
            GLES20.GlDrawArrays(GLES20.GlTriangleStrip, 0, 4);

            // Cleanup
            GLES20.GlDisableVertexAttribArray(_aPosition);
            GLES20.GlDisableVertexAttribArray(_aTexCoord);
            GLES20.GlBindBuffer(GLES20.GlArrayBuffer, 0);
            GLES20.GlBindTexture(GLES11Ext.GlTextureExternalOes, 0);
            GLES20.GlUseProgram(0);
        }

        public void Dispose()
        {
            if (_vbo != 0)
            {
                int[] vbos = { _vbo };
                GLES20.GlDeleteBuffers(1, vbos, 0);
                _vbo = 0;
            }

            if (_program != 0)
            {
                GLES20.GlDeleteProgram(_program);
                _program = 0;
            }

            if (_vertexShader != 0)
            {
                GLES20.GlDeleteShader(_vertexShader);
                _vertexShader = 0;
            }

            if (_fragmentShader != 0)
            {
                GLES20.GlDeleteShader(_fragmentShader);
                _fragmentShader = 0;
            }

            _initialized = false;
        }
    }
}
#endif
