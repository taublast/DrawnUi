using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Controls;
using SkiaSharp;
using Windows.Foundation.Collections;
using WinRT;

namespace DrawnUi.Infrastructure
{
    /// <summary>
    /// A public wrapper for OpenGL ES functionality across different platforms.
    /// </summary>
    public static class OpenGles
    {
        #region FENCE

        // OpenGL ES 3.0+ or ANGLE

        [DllImport(WindowsModernLibraryName, EntryPoint = "glFenceSync")]
        public static extern IntPtr FenceSync(uint condition, uint flags);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glClientWaitSync")]
        public static extern uint ClientWaitSync(IntPtr sync, uint flags, ulong timeout);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteSync")]
        public static extern void DeleteSync(IntPtr sync);

        public static readonly uint GL_SYNC_GPU_COMMANDS_COMPLETE = 0x9117;
        public static readonly uint GL_SYNC_FLUSH_COMMANDS_BIT = 0x00000001;
        public static readonly uint GL_ALREADY_SIGNALED = 0x911A;
        public static readonly uint GL_CONDITION_SATISFIED = 0x911C;
        public static readonly uint GL_TIMEOUT_EXPIRED = 0x911B;
        public static readonly ulong GL_TIMEOUT_IGNORED = 0xFFFFFFFFFFFFFFFF;

        #endregion

        #region Platform Detection and Library Names

        // Platform-specific library names
        private const string WindowsModernLibraryName = "libGLESv2";

        /// <summary>
        /// Static constructor that ensures native libraries are properly loaded
        /// </summary>
        static OpenGles()
        {
            // Try to pre-load the library using our advanced loader
            string libraryName = NativeLibraryLoader.GetOpenGLESLibraryPath();

            try
            {
                bool loaded = NativeLibraryLoader.TryLoadNativeLibrary(libraryName);
                if (!loaded)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not pre-load OpenGL ES library: {libraryName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error pre-loading OpenGL ES library: {ex.Message}");
            }
        }

        #endregion

        #region Constants

        // Framebuffer Constants
        public static readonly int FramebufferBinding = 0x8CA6;
        public static readonly int RenderbufferBinding = 0x8CA7;
        public static readonly int Framebuffer = 0x8D40;
        public static readonly int Renderbuffer = 0x8D41;

        // Color Format Constants
        public static readonly int Bgra8Ext = 0x93A1;

        // Information Constants
        public static readonly int Version = 0x1F02;
        public static readonly int Extensions = 0x1F03;

        // Buffer Parameters
        public static readonly int SubpixelBits = 0x0D50;
        public static readonly int RedBits = 0x0D52;
        public static readonly int GreenBits = 0x0D53;
        public static readonly int BlueBits = 0x0D54;
        public static readonly int AlphaBits = 0x0D55;
        public static readonly int DepthBits = 0x0D56;
        public static readonly int StencilBits = 0x0D57;
        public static readonly int Samples = 0x80A9;

        // Clear Mask Constants
        public static readonly int DepthBufferBit = 0x00000100;
        public static readonly int StencilBufferBit = 0x00000400;
        public static readonly int ColorBufferBit = 0x00004000;

        // Filtering Constants
        public static readonly int Nearest = 0x2600;

        // Frame Buffer Extensions
        public static readonly int ReadFramebufferAngle = 0x8CA8;
        public static readonly int DrawFramebufferAngle = 0x8CA9;
        public static readonly int DrawFramebufferBindingAngle = 0x8CA6;
        public static readonly int ReadFramebufferBindingAngle = 0x8CAA;

        // Renderbuffer Parameters
        public static readonly int RenderbufferWidth = 0x8D42;
        public static readonly int RenderbufferHeight = 0x8D43;
        public static readonly int RenderbufferInternalFormat = 0x8D44;
        public static readonly int RenderbufferRedSize = 0x8D50;
        public static readonly int RenderbufferGreenSize = 0x8D51;
        public static readonly int RenderbufferBlueSize = 0x8D52;
        public static readonly int RenderbufferAlphaSize = 0x8D53;
        public static readonly int RenderbufferDepthSize = 0x8D54;
        public static readonly int RenderbufferStencilSize = 0x8D55;

        // Attachment Constants
        public static readonly int ColorAttachment0 = 0x8CE0;
        public static readonly int DepthAttachment = 0x8D00;
        public static readonly int StencilAttachment = 0x8D20;

        // Depth/Stencil Constants
        public static readonly int DepthComponent16 = 0x81A5;
        public static readonly int DepthStencilOes = 0x84F9;
        public static readonly int UnsignedInt_24_8_Oes = 0x84FA;
        public static readonly int Depth24Stencil8Oes = 0x88F0;

        #endregion

        #region LEGACY CONSTANTS

        public static readonly uint GL_FRAMEBUFFER_BINDING = 0x8CA6;
        public static readonly int GL_RENDERBUFFER_BINDING = 0x8CA7;

        public static readonly int GL_BGRA8_EXT = 0x93A1;
        public static readonly int GL_VERSION = 0x1F02;
        public static readonly int GL_EXTENSIONS = 0x1F03;

        // GetPName
        public static readonly int GL_SUBPIXEL_BITS = 0x0D50;
        public static readonly int GL_RED_BITS = 0x0D52;
        public static readonly int GL_GREEN_BITS = 0x0D53;
        public static readonly int GL_BLUE_BITS = 0x0D54;
        public static readonly int GL_ALPHA_BITS = 0x0D55;
        public static readonly int GL_DEPTH_BITS = 0x0D56;
        public static readonly uint GL_STENCIL_BITS = 0x0D57;
        public static readonly uint GL_SAMPLES = 0x80A9;

        // ClearBufferMask
        public static readonly int GL_DEPTH_BUFFER_BIT = 0x00000100;
        public static readonly int GL_STENCIL_BUFFER_BIT = 0x00000400;
        public static readonly int GL_COLOR_BUFFER_BIT = 0x00004000;

        public static readonly int GL_NEAREST = 0x2600;

        public static readonly int GL_READ_FRAMEBUFFER_ANGLE = 0x8CA8;
        public static readonly int GL_DRAW_FRAMEBUFFER_ANGLE = 0x8CA9;
        public static readonly int GL_DRAW_FRAMEBUFFER_BINDING_ANGLE = 0x8CA6;
        public static readonly int GL_READ_FRAMEBUFFER_BINDING_ANGLE = 0x8CAA;

        // Framebuffer Object
        public static readonly int GL_FRAMEBUFFER = 0x8D40;
        public static readonly int GL_RENDERBUFFER = 0x8D41;

        public static readonly int GL_RENDERBUFFER_WIDTH = 0x8D42;
        public static readonly int GL_RENDERBUFFER_HEIGHT = 0x8D43;
        public static readonly int GL_RENDERBUFFER_INTERNAL_FORMAT = 0x8D44;
        public static readonly int GL_RENDERBUFFER_RED_SIZE = 0x8D50;
        public static readonly int GL_RENDERBUFFER_GREEN_SIZE = 0x8D51;
        public static readonly int GL_RENDERBUFFER_BLUE_SIZE = 0x8D52;
        public static readonly int GL_RENDERBUFFER_ALPHA_SIZE = 0x8D53;
        public static readonly int GL_RENDERBUFFER_DEPTH_SIZE = 0x8D54;
        public static readonly int GL_RENDERBUFFER_STENCIL_SIZE = 0x8D55;
        public static readonly int GL_COLOR_ATTACHMENT0 = 0x8CE0;
        public static readonly int GL_DEPTH_ATTACHMENT = 0x8D00;
        public static readonly int GL_STENCIL_ATTACHMENT = 0x8D20;

        public static readonly int GL_DEPTH_COMPONENT16 = 0x81A5;
        public static readonly int GL_DEPTH_STENCIL_OES = 0x84F9;
        public static readonly int GL_UNSIGNED_INT_24_8_OES = 0x84FA;
        public static readonly int GL_DEPTH24_STENCIL8_OES = 0x88F0;

        #endregion

        #region Core Functions

        /// <summary>
        /// Gets the value of the specified integer parameter.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetIntegerv")]
        private static extern void GetIntegerValueInternal(uint pname, out int data);

        /// <summary>
        /// Gets the value of the specified integer parameter.
        /// </summary>
        public static int GetIntegerValue(int parameterName)
        {
            GetIntegerValueInternal((uint)parameterName, out int result);
            return result;
        }

        /// <summary>
        /// Direct alternative to Gles.glGetIntegerv - gets the value of the specified integer parameter.
        /// </summary>
        public static void GetIntegerv(uint pname, out int data)
        {
            GetIntegerValueInternal(pname, out data);
        }

        /// <summary>
        /// Gets a string describing the current GL configuration.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetString")]
        private static extern IntPtr GetStringInternal(uint value);

        /// <summary>
        /// Gets a string describing the current GL configuration.
        /// </summary>
        public static string GetString(int name)
        {
            IntPtr ptr = GetStringInternal((uint)name);
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : null;
        }

        /// <summary>
        /// Sets the viewport dimensions.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glViewport")]
        private static extern void ViewportInternal(int x, int y, int width, int height);

        /// <summary>
        /// Sets the viewport dimensions.
        /// </summary>
        public static void Viewport(int x, int y, int width, int height)
        {
            ViewportInternal(x, y, width, height);
        }

        /// <summary>
        /// Sets the clear color.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glClearColor")]
        private static extern void ClearColorInternal(float red, float green, float blue, float alpha);

        /// <summary>
        /// Sets the clear color.
        /// </summary>
        public static void ClearColor(float red, float green, float blue, float alpha)
        {
            ClearColorInternal(red, green, blue, alpha);
        }

        /// <summary>
        /// Clears the specified buffer.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glClear")]
        private static extern void ClearInternal(uint mask);

        /// <summary>
        /// Clears the specified buffer.
        /// </summary>
        public static void Clear(int mask)
        {
            ClearInternal((uint)mask);
        }

        #endregion

        #region Framebuffer and Renderbuffer Functions

        /// <summary>
        /// Generates renderbuffer objects.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenRenderbuffers")]
        private static extern void GenRenderbuffersInternal(int n, [In, Out] uint[] buffers);

        /// <summary>
        /// Generates renderbuffer objects.
        /// </summary>
        public static uint[] GenRenderbuffers(int count)
        {
            uint[] buffers = new uint[count];
            GenRenderbuffersInternal(count, buffers);
            return buffers;
        }

        /// <summary>
        /// Generates a single renderbuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenRenderbuffers")]
        private static extern void GenRenderbufferInternal(int n, ref uint buffer);

        /// <summary>
        /// Generates a single renderbuffer object.
        /// </summary>
        public static uint GenRenderbuffer()
        {
            uint buffer = 0;
            GenRenderbufferInternal(1, ref buffer);
            return buffer;
        }

        /// <summary>
        /// Generates framebuffer objects.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenFramebuffers")]
        private static extern void GenFramebuffersInternal(int n, [In, Out] uint[] buffers);

        /// <summary>
        /// Generates framebuffer objects.
        /// </summary>
        public static uint[] GenFramebuffers(int count)
        {
            uint[] buffers = new uint[count];
            GenFramebuffersInternal(count, buffers);
            return buffers;
        }

        /// <summary>
        /// Generates a single framebuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenFramebuffers")]
        private static extern void GenFramebufferInternal(int n, ref uint buffer);

        /// <summary>
        /// Generates a single framebuffer object.
        /// </summary>
        public static uint GenFramebuffer()
        {
            uint buffer = 0;
            GenFramebufferInternal(1, ref buffer);
            return buffer;
        }

        /// <summary>
        /// Gets renderbuffer parameter.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetRenderbufferParameteriv")]
        private static extern void GetRenderbufferParameterInternal(uint target, int pname, out int param);

        /// <summary>
        /// Gets renderbuffer parameter.
        /// </summary>
        public static int GetRenderbufferParameter(int target, int parameterName)
        {
            GetRenderbufferParameterInternal((uint)target, parameterName, out int result);
            return result;
        }

        /// <summary>
        /// Binds a renderbuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glBindRenderbuffer")]
        private static extern void BindRenderbufferInternal(uint target, uint buffer);

        /// <summary>
        /// Binds a renderbuffer object.
        /// </summary>
        public static void BindRenderbuffer(int target, uint buffer)
        {
            BindRenderbufferInternal((uint)target, buffer);
        }

        /// <summary>
        /// Binds a framebuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glBindFramebuffer")]
        private static extern void BindFramebufferInternal(uint target, uint framebuffer);

        /// <summary>
        /// Binds a framebuffer object.
        /// </summary>
        public static void BindFramebuffer(int target, uint framebuffer)
        {
            BindFramebufferInternal((uint)target, framebuffer);
        }

        /// <summary>
        /// Deletes framebuffer objects.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteFramebuffers")]
        private static extern void DeleteFramebuffersInternal(int n, [In, Out] uint[] framebuffers);

        /// <summary>
        /// Deletes framebuffer objects.
        /// </summary>
        public static void DeleteFramebuffers(uint[] framebuffers)
        {
            DeleteFramebuffersInternal(framebuffers.Length, framebuffers);
        }

        /// <summary>
        /// Deletes a single framebuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteFramebuffers")]
        private static extern void DeleteFramebufferInternal(int n, ref uint framebuffer);

        /// <summary>
        /// Deletes a single framebuffer object.
        /// </summary>
        public static void DeleteFramebuffer(uint framebuffer)
        {
            DeleteFramebufferInternal(1, ref framebuffer);
        }

        /// <summary>
        /// Deletes renderbuffer objects.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteRenderbuffers")]
        private static extern void DeleteRenderbuffersInternal(int n, [In, Out] uint[] renderbuffers);

        /// <summary>
        /// Deletes renderbuffer objects.
        /// </summary>
        public static void DeleteRenderbuffers(uint[] renderbuffers)
        {
            DeleteRenderbuffersInternal(renderbuffers.Length, renderbuffers);
        }

        /// <summary>
        /// Deletes a single renderbuffer object.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteRenderbuffers")]
        private static extern void DeleteRenderbufferInternal(int n, ref uint renderbuffer);

        /// <summary>
        /// Deletes a single renderbuffer object.
        /// </summary>
        public static void DeleteRenderbuffer(uint renderbuffer)
        {
            DeleteRenderbufferInternal(1, ref renderbuffer);
        }

        /// <summary>
        /// Attaches a renderbuffer to a framebuffer.
        /// </summary>
        [DllImport(WindowsModernLibraryName, EntryPoint = "glFramebufferRenderbuffer")]
        private static extern void FramebufferRenderbufferInternal(uint target, uint attachment,
            uint renderbuffertarget, uint renderbuffer);

        /// <summary>
        /// Attaches a renderbuffer to a framebuffer.
        /// </summary>
        public static void FramebufferRenderbuffer(int target, int attachment, int renderbufferTarget,
            uint renderbuffer)
        {
            FramebufferRenderbufferInternal((uint)target, (uint)attachment, (uint)renderbufferTarget, renderbuffer);
        }

        #endregion

        #region Texture Management

        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenTextures")]
        private static extern void GenTexturesInternal(int n, out uint textures);

        public static uint GenTexture()
        {
            GenTexturesInternal(1, out uint texture);
            return texture;
        }

        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteTextures")]
        private static extern void DeleteTexturesInternal(int n, ref uint textures);

        public static void DeleteTexture(uint texture)
        {
            DeleteTexturesInternal(1, ref texture);
        }

        [DllImport(WindowsModernLibraryName, EntryPoint = "glBindTexture")]
        public static extern void BindTexture(uint target, uint texture);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glTexImage2D")]
        public static extern void TexImage2D(uint target, int level, uint internalFormat, int width, int height,
            int border, uint format, uint type, IntPtr pixels);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glTexParameteri")]
        public static extern void TexParameteri(uint target, uint pname, int param);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glFramebufferTexture2D")]
        public static extern void FramebufferTexture2D(uint target, uint attachment, uint textarget,
            uint texture, int level);

        #endregion


        #region Drawing (Updated for ES 2.0)

        [DllImport(WindowsModernLibraryName, EntryPoint = "glEnable")]
        public static extern void Enable(uint cap);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glDisable")]
        public static extern void Disable(uint cap);

        // Vertex array support
        [DllImport(WindowsModernLibraryName, EntryPoint = "glGenBuffers")]
        private static extern void GenBuffersInternal(int n, out uint buffers);

        public static uint GenBuffer()
        {
            GenBuffersInternal(1, out uint buffer);
            return buffer;
        }

        [DllImport(WindowsModernLibraryName, EntryPoint = "glDeleteBuffers")]
        private static extern void DeleteBuffersInternal(int n, ref uint buffers);

        public static void DeleteBuffer(uint buffer)
        {
            DeleteBuffersInternal(1, ref buffer);
        }

        [DllImport(WindowsModernLibraryName, EntryPoint = "glBindBuffer")]
        public static extern void BindBuffer(uint target, uint buffer);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glBufferData")]
        public static extern void BufferData(uint target, IntPtr size, IntPtr data, uint usage);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glVertexAttribPointer")]
        public static extern void VertexAttribPointer(uint index, int size, uint type, bool normalized,
            int stride, IntPtr pointer);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glEnableVertexAttribArray")]
        public static extern void EnableVertexAttribArray(uint index);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glDrawArrays")]
        public static extern void DrawArrays(uint mode, int first, int count);

        // Shader support
        [DllImport(WindowsModernLibraryName, EntryPoint = "glCreateShader")]
        public static extern uint CreateShader(uint type);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glShaderSource")]
        public static extern void ShaderSource(uint shader, int count, string[] source, int[] length);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glCompileShader")]
        public static extern void CompileShader(uint shader);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetShaderiv")]
        public static extern void GetShaderiv(uint shader, uint pname, out int param);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glCreateProgram")]
        public static extern uint CreateProgram();

        [DllImport(WindowsModernLibraryName, EntryPoint = "glAttachShader")]
        public static extern void AttachShader(uint program, uint shader);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glLinkProgram")]
        public static extern void LinkProgram(uint program);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetProgramiv")]
        public static extern void GetProgramiv(uint program, uint pname, out int param);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glUseProgram")]
        public static extern void UseProgram(uint program);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetAttribLocation")]
        public static extern int GetAttribLocation(uint program, string name);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glGetUniformLocation")]
        public static extern int GetUniformLocation(uint program, string name);

        [DllImport(WindowsModernLibraryName, EntryPoint = "glUniform1i")]
        public static extern void Uniform1i(int location, int value);

        // Constants
        public static readonly uint GL_TEXTURE_MIN_FILTER = 0x2801;
        public static readonly uint GL_TEXTURE_MAG_FILTER = 0x2800;
        public static readonly uint GL_TEXTURE_2D = 0x0DE1;
        public static readonly uint GL_QUADS = 0x0007; // Not used anymore, but kept for reference
        public static readonly uint GL_RGBA = 0x1908;
        public static readonly uint GL_UNSIGNED_BYTE = 0x1401;
        public static readonly uint GL_DEPTH_TEST = 0x0B71;
        public static readonly uint GL_ARRAY_BUFFER = 0x8892;
        public static readonly uint GL_STATIC_DRAW = 0x88E4;
        public static readonly uint GL_FLOAT = 0x1406;
        public static readonly uint GL_TRIANGLES = 0x0004;
        public static readonly uint GL_VERTEX_SHADER = 0x8B31;
        public static readonly uint GL_FRAGMENT_SHADER = 0x8B30;
        public static readonly uint GL_COMPILE_STATUS = 0x8B81;
        public static readonly uint GL_LINK_STATUS = 0x8B82;

        #endregion
    }

    /// <summary>
    /// Helper class for cross-platform P/Invoke with improved DLL loading
    /// </summary>
    public static class NativeLibraryLoader
    {
        /// <summary>
        /// Attempts to load a native library from various possible locations
        /// </summary>
        /// <param name="libraryName">Base name of the library</param>
        /// <returns>True if the library was successfully loaded</returns>
        public static bool TryLoadNativeLibrary(string libraryName)
        {
            try
            {
                // Try standard load first
                NativeLibrary.Load(libraryName);
                return true;
            }
            catch
            {
                // If that fails, we'll search in possible locations
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = Path.GetDirectoryName(assemblyLocation);

                if (string.IsNullOrEmpty(assemblyDirectory))
                    return false;

                // Check architecture
                string architecture = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.X86 => "x86",
                    Architecture.Arm => "arm",
                    Architecture.Arm64 => "arm64",
                    _ => string.Empty
                };

                // Define possible paths where the library might be found
                var searchPaths = new[]
                {
                    // Current directory
                    Path.Combine(assemblyDirectory, GetPlatformLibraryFileName(libraryName)),
                    
                    // Architecture-specific runtime folder (.NET standard pattern)
                    !string.IsNullOrEmpty(architecture)
                        ? Path.Combine(assemblyDirectory, "runtimes", $"win-{architecture}", "native", GetPlatformLibraryFileName(libraryName))
                        : null,
                    
                    // lib folder
                    Path.Combine(assemblyDirectory, "lib", GetPlatformLibraryFileName(libraryName)),
                    
                    // native folder
                    Path.Combine(assemblyDirectory, "native", GetPlatformLibraryFileName(libraryName)),
                    
                    // Just next to the executable
                    Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty, GetPlatformLibraryFileName(libraryName))
                };

                foreach (string path in searchPaths)
                {
                    if (path == null)
                        continue;

                    if (File.Exists(path))
                    {
                        try
                        {
                            NativeLibrary.Load(path);
                            return true;
                        }
                        catch
                        {
                            // Continue to the next path
                        }
                    }
                }

                // On Windows, try additional system directories
                if (OperatingSystem.IsWindows())
                {
                    // Check System32
                    string system32Path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                        GetPlatformLibraryFileName(libraryName));

                    if (File.Exists(system32Path))
                    {
                        try
                        {
                            NativeLibrary.Load(system32Path);
                            return true;
                        }
                        catch
                        {
                            // Continue to the next attempt
                        }
                    }

                    // Check SysWOW64 on 64-bit systems
                    if (Environment.Is64BitOperatingSystem)
                    {
                        string sysWow64Path = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                            GetPlatformLibraryFileName(libraryName));

                        if (File.Exists(sysWow64Path))
                        {
                            try
                            {
                                NativeLibrary.Load(sysWow64Path);
                                return true;
                            }
                            catch
                            {
                                // Continue to the next attempt
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the platform-specific file name for a library
        /// </summary>
        private static string GetPlatformLibraryFileName(string baseName)
        {
            if (OperatingSystem.IsWindows())
            {
                // On Windows, ensure .dll extension
                return baseName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    ? baseName
                    : $"{baseName}.dll";
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsAndroid())
            {
                // On Linux/Android, use .so extension
                return baseName.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                    ? (baseName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? baseName : $"{baseName}.so")
                    : (baseName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ? $"lib{baseName}" : $"lib{baseName}.so");
            }
            else if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS())
            {
                // On macOS/iOS, use .dylib extension
                return baseName.StartsWith("lib", StringComparison.OrdinalIgnoreCase)
                    ? (baseName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ? baseName : $"{baseName}.dylib")
                    : (baseName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase) ? $"lib{baseName}" : $"lib{baseName}.dylib");
            }

            return baseName;
        }

        /// <summary>
        /// Gets the platform-specific full path to the OpenGL/ES library
        /// </summary>
        public static string GetOpenGLESLibraryPath()
        {
            if (OperatingSystem.IsMacOS())
                return "/System/Library/Frameworks/OpenGL.framework/OpenGL";
            else if (OperatingSystem.IsIOS())
                return "/System/Library/Frameworks/OpenGLES.framework/OpenGLES";
            else if (OperatingSystem.IsAndroid())
                return "libGLESv2.so";
            else if (OperatingSystem.IsWindows())
                return OperatingSystem.IsWindowsVersionAtLeast(10, 0) ? "libGLESv2.dll" : "opengl32.dll";
            else if (RuntimeInformation.OSDescription.Contains("Tizen"))
                return "libGLESv2.so";
            else
                return "libGLESv2";
        }
    }



}
