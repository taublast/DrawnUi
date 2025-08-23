---
title: DrawnUI for .NET MAUI - SkiaSharp Rendering Engine
description: Hardware-accelerated rendering engine for .NET MAUI built on SkiaSharp. Create pixel-perfect cross-platform apps for iOS, Android, Windows, MacCatalyst.
---

<div style="position: relative; text-align: center; padding: 30px 0 70px 0; border-radius: 12px; overflow: hidden; background: #0b1220; color: white;">
  <canvas id="heroCanvas" style="position:absolute; inset:0; width:100%; height:100%; display:block; border-radius: 12px;"></canvas>
  <!-- Fallback/augment radial glows in MAUI/Microsoft palette (blue/purple/teal) -->
  <div aria-hidden="true" style="position:absolute; inset:0; pointer-events:none; border-radius: 12px; background:
    radial-gradient(1200px 600px at 10% -10%, rgba(91,33,182,0.45), transparent 60%),
    radial-gradient(900px 500px at 90% 10%, rgba(37,99,235,0.30), transparent 65%),
    radial-gradient(800px 400px at 50% 120%, rgba(0,120,212,0.20), transparent 70%);"></div>
  <div style="position: relative; z-index: 1;">
    <img src="images/draw.svg" alt="DrawnUI Logo" style="height: 80px; filter: drop-shadow(0 2px 6px rgba(0,0,0,0.35));">
    <h1 style="font-size: 3.5em; margin: 0; font-weight: 700; text-shadow: 0 2px 10px rgba(0,0,0,0.45);">
      DrawnUI for .NET MAUI
    </h1>
    <p style="font-size: 1.4em; margin: 20px auto; opacity: 0.95; max-width: 680px;">
      Build <strong>stunning cross-platform apps</strong> with hardware-accelerated SkiaSharp rendering
    </p>
    <div style="margin-top: 30px;">
      <a href="articles/getting-started.md" style="background: #2563eb; color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: 600; margin: 0 10px; box-shadow: 0 4px 15px rgba(37,99,235,0.35); transition: transform 0.2s ease, box-shadow 0.2s ease;">
        🚀 Get Started
      </a>
      <a href="https://github.com/taublast/DrawnUi" style="background: rgba(255,255,255,0.12); color: white; padding: 15px 30px; text-decoration: none; border-radius: 8px; font-weight: 600; margin: 0 10px; border: 2px solid rgba(255,255,255,0.22); backdrop-filter: blur(2px);">
        ⭐ Star on GitHub
      </a>
    </div>
  </div>
</div>

<script id="vertShader" type="x-shader/x-vertex">
precision mediump float;
attribute vec2 a_position;
varying vec2 vUv;
void main() {
    vUv = 0.5 * (a_position + 1.0);
    gl_Position = vec4(a_position, 0.0, 1.0);
}
</script>

<script id="fragShader" type="x-shader/x-fragment">
precision mediump float;

varying vec2 vUv;
uniform float u_time;
uniform float u_ratio;
uniform vec2 u_pointer_position;
uniform float u_scroll_progress;

vec2 rotate(vec2 uv, float th) {
    return mat2(cos(th), sin(th), -sin(th), cos(th)) * uv;
}

float neuro_shape(vec2 uv, float t, float p) {
    vec2 sine_acc = vec2(0.0);
    vec2 res = vec2(0.0);
    float scale = 8.0;
    for (int j = 0; j < 15; j++) {
        uv = rotate(uv, 1.0);
        sine_acc = rotate(sine_acc, 1.0);
        vec2 layer = uv * scale + float(j) + sine_acc - t;
        sine_acc += sin(layer) + 2.4 * p;
        res += (0.5 + 0.5 * cos(layer)) / scale;
        scale *= 1.2;
    }
    return res.x + res.y;
}

void main() {
    vec2 uv = 0.5 * vUv;
    uv.x *= u_ratio;

    vec2 pointer = vUv - u_pointer_position;
    pointer.x *= u_ratio;
    float p = clamp(length(pointer), 0.0, 1.0);
    p = 0.5 * pow(1.0 - p, 2.0);

    float t = 0.001 * u_time;
    float noise = neuro_shape(uv, t, p);

    noise = 1.2 * pow(noise, 3.0);
    noise += pow(noise, 10.0);
    noise = max(0.0, noise - 0.5);
    noise *= (1.0 - length(vUv - 0.5));

    // MAUI color palette
    vec3 c1 = vec3(0.145, 0.388, 0.922); // Blue #2563EB
    vec3 c2 = vec3(0.486, 0.229, 0.929); // Purple #7C3AED
    vec3 c3 = vec3(0.000, 0.471, 0.831); // Blue #0078D4

    float a = 0.5 + 0.5 * sin(3.0 * u_scroll_progress);
    float b = 0.5 + 0.5 * cos(3.0 * u_scroll_progress);
    vec3 base = mix(mix(c1, c2, a), c3, b * 0.6);

    vec3 color = normalize(base) * noise;

    gl_FragColor = vec4(color, noise);
}
</script>

<script>
window.addEventListener('load', function() {
  setTimeout(function() {
    const canvasEl = document.getElementById('heroCanvas');
    const devicePixelRatio = Math.min(window.devicePixelRatio, 2);

    const pointer = {
        x: 0,
        y: 0,
        tX: 0,
        tY: 0,
    };

    let uniforms;
    const gl = initShader();

    setupEvents();
    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);
    render();

    function initShader() {
        const vsSource = document.getElementById("vertShader").innerHTML;
        const fsSource = document.getElementById("fragShader").innerHTML;

        const gl = canvasEl.getContext("webgl") || canvasEl.getContext("experimental-webgl");

        if (!gl) {
            console.log("WebGL is not supported by your browser.");
            return null;
        }

        function createShader(gl, sourceCode, type) {
            const shader = gl.createShader(type);
            gl.shaderSource(shader, sourceCode);
            gl.compileShader(shader);

            if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
                console.error("An error occurred compiling the shaders: " + gl.getShaderInfoLog(shader));
                gl.deleteShader(shader);
                return null;
            }

            return shader;
        }

        const vertexShader = createShader(gl, vsSource, gl.VERTEX_SHADER);
        const fragmentShader = createShader(gl, fsSource, gl.FRAGMENT_SHADER);

        function createShaderProgram(gl, vertexShader, fragmentShader) {
            const program = gl.createProgram();
            gl.attachShader(program, vertexShader);
            gl.attachShader(program, fragmentShader);
            gl.linkProgram(program);

            if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
                console.error("Unable to initialize the shader program: " + gl.getProgramInfoLog(program));
                return null;
            }

            return program;
        }

        const shaderProgram = createShaderProgram(gl, vertexShader, fragmentShader);
        uniforms = getUniforms(shaderProgram);

        function getUniforms(program) {
            let uniforms = [];
            let uniformCount = gl.getProgramParameter(program, gl.ACTIVE_UNIFORMS);
            for (let i = 0; i < uniformCount; i++) {
                let uniformName = gl.getActiveUniform(program, i).name;
                uniforms[uniformName] = gl.getUniformLocation(program, uniformName);
            }
            return uniforms;
        }

        const vertices = new Float32Array([-1., -1., 1., -1., -1., 1., 1., 1.]);

        const vertexBuffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, vertexBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, vertices, gl.STATIC_DRAW);

        gl.useProgram(shaderProgram);

        const positionLocation = gl.getAttribLocation(shaderProgram, "a_position");
        gl.enableVertexAttribArray(positionLocation);

        gl.bindBuffer(gl.ARRAY_BUFFER, vertexBuffer);
        gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

        return gl;
    }

    function render() {
        const currentTime = performance.now();

        pointer.x += (pointer.tX - pointer.x) * .2;
        pointer.y += (pointer.tY - pointer.y) * .2;

        gl.uniform1f(uniforms.u_time, currentTime);
        gl.uniform2f(uniforms.u_pointer_position, pointer.x / window.innerWidth, 1 - pointer.y / window.innerHeight);
        gl.uniform1f(uniforms.u_scroll_progress, window.pageYOffset / (2 * window.innerHeight));

        gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
        requestAnimationFrame(render);
    }

    function resizeCanvas() {
        const rect = canvasEl.getBoundingClientRect();
        canvasEl.width = rect.width * devicePixelRatio;
        canvasEl.height = rect.height * devicePixelRatio;
        canvasEl.style.width = rect.width + 'px';
        canvasEl.style.height = rect.height + 'px';

        if (uniforms && uniforms.u_ratio) {
            gl.uniform1f(uniforms.u_ratio, canvasEl.width / canvasEl.height);
            gl.viewport(0, 0, canvasEl.width, canvasEl.height);
        }
    }

    function setupEvents() {
        window.addEventListener("pointermove", e => {
            updateMousePosition(e.clientX, e.clientY);
        });
        window.addEventListener("touchmove", e => {
            updateMousePosition(e.targetTouches[0].clientX, e.targetTouches[0].clientY);
        });
        window.addEventListener("click", e => {
            updateMousePosition(e.clientX, e.clientY);
        });

        function updateMousePosition(eX, eY) {
            pointer.tX = eX;
            pointer.tY = eY;
        }
    }
  }, 500);
});
</script>

## ⚡ Quick Start

<div style="background: #1a202c; color: #e2e8f0; padding: 25px; border-radius: 8px; margin: 20px 0;">

**1. Install the package:**
```bash
dotnet add package DrawnUi.Maui
```

**2. Initialize in MauiProgram.cs:**
```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>()
        .UseDrawnUi();  // ← Add this line
    
    return builder.Build();
}
```

**3. Start building amazing UIs:**
```xml
<ContentPage xmlns:draw="http://schemas.taublast.com/drawnUi/2022/maui">
    <draw:Canvas>
        <draw:SkiaButton Text="Hello DrawnUI!" />
    </draw:Canvas>
</ContentPage>
```

</div>

<div style="text-align: center; margin: 30px 0;">
  <a href="articles/getting-started.md" style="background: #4299e1; color: white; padding: 12px 25px; text-decoration: none; border-radius: 6px; font-weight: 600;">
    📖 Complete Setup Guide
  </a>
</div>

---

## 🌟 What Is DrawnUI?

**DrawnUI** is a powerful rendering engine for .NET MAUI built on top of **SkiaSharp** that brings together a complete layout system, gesture recognition, smooth animations, and custom-drawn controls—all rendered on hardware-accelerated canvases.

Unlike traditional MAUI controls that rely on native platform widgets, DrawnUI renders everything directly to GPU-accelerated surfaces using SkiaSharp's 2D graphics library. This approach gives you **pixel-perfect control** over your app's appearance while maintaining **max performance** across iOS, Android, Windows, and macOS.

**Key Architecture:**
- **SkiaSharp Foundation**: Leverages Google's Skia graphics engine for consistent, high-performance 2D rendering
- **Canvas-Based Layout**: Custom layout system that positions and sizes controls on hardware-accelerated surfaces
- **Gesture Engine**: Multi-touch gesture recognition system with support for complex interactions
- **Animation Pipeline**: Smooth, performant animations using GPU acceleration and intelligent caching
- **Virtual Controls**: Lightweight control system without native platform overhead

Perfect for apps requiring **custom UI designs**, **complex animations**, **game-like interfaces**, or **pixel-perfect cross-platform consistency** that traditional MAUI controls can't achieve.

### 🏃 **Master Performance**
- **Fast App Startup** for totally drawn apps
- **Caching system** for retained rendering
- **Hardware acceleration** on all platforms
- **Virtual controls** - no native overhead

### 🎨 **Unleash Creativity**
- **Pixel-perfect** cross-platform consistency
- **Gesture system** with multi-touch support
- **2D/3D transforms** and visual effects
- **Custom shaders** and filters

### 👨‍💻 **Familiar Yet Powerful**
- **MAUI/WFP-like** properties for layout etc
- **MAUI Shell-like** navigation on canvas
- **XAML + Hot Reload** support
- **Fluent C#** syntax for code-behind UI

</div>

</div>

---

## 📔 Learn More

<div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 25px; margin: 40px 0;">

<div style="padding: 25px; border: 2px solid #4a5568; border-radius: 12px; transition: all 0.3s;">
  <h4 style="margin-bottom: 15px;">📖 Documentation</h4>
  <p style="margin-bottom: 20px; ">Complete guides and API reference</p>
  <a href="articles/getting-started.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">Getting Started →</a><br>
  <a href="articles/controls/index.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">Controls Reference →</a><br>
  <a href="articles/advanced/index.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">Advanced Topics →</a>
</div>

<div style="padding: 25px; border: 2px solid #4a5568; border-radius: 12px; transition: all 0.3s;">
  <h4 style="margin-bottom: 15px;">🧙 Tutorials</h4>
  <p style="margin-bottom: 20px; ">Step-by-step practical examples</p>
  <a href="articles/tutorials.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">View Tutorials →</a><br>
  <a href="articles/sample-apps.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">Sample Apps →</a><br>
  <a href="articles/fluent-extensions.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">Fluent Syntax →</a>
</div>

<div style="padding: 25px; border: 2px solid #4a5568; border-radius: 12px; transition: all 0.3s;">
  <h4 style="margin-bottom: 15px;">💬 Community</h4>
  <p style="margin-bottom: 20px; ">Get help and share your creations</p>
  <a href="https://github.com/taublast/DrawnUi/discussions" style="color: #4299e1; text-decoration: none; font-weight: 600;">GitHub Discussions →</a><br>
  <a href="https://github.com/taublast/DrawnUi/issues" style="color: #4299e1; text-decoration: none; font-weight: 600;">Report Issues →</a><br>
  <a href="articles/faq.md" style="color: #4299e1; text-decoration: none; font-weight: 600;">FAQ →</a>
</div>

</div>

---

<div style="text-align: center; margin-top: 40px; padding: 20px; color: #666;">
  <p>
    <img src="https://img.shields.io/github/license/taublast/DrawnUi.svg" alt="License" style="margin: 0 5px;">
    <img src="https://img.shields.io/nuget/v/DrawnUi.Maui.svg" alt="NuGet Version" style="margin: 0 5px;">
    <img src="https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg" alt="NuGet Downloads" style="margin: 0 5px;">
  </p>
  <p style="margin-top: 15px;">
    By <a href="https://taublast.github.io" style="color: #4299e1; text-decoration: none;">Nick Kovalsky (@taublast)</a> and contributors
  </p>
</div>