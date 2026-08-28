---
name: drawnui-opentk
description: DrawnUI on the OpenTK desktop head (Windows/Linux, plus what macOS would need) — DrawnUiWindow apps, CanvasHost overlays over raw OpenGL, update/rendering modes, desktop input wiring, output-dir assets, window icon and DWM chrome niceties, Linux/WSL2 fixes, trimmed single-file publish, and using an OpenTK head as an instant visual test harness for shared DrawnUI code. Trigger on "drawnui opentk", "DrawnUiWindow", "CanvasHost", "drawnui desktop", "GL overlay drawnui".
version: 1.1.0
tags: [drawnui, opentk, desktop, opengl, dotnet]
---

# DrawnUI OpenTK (desktop head)

Package `DrawnUi.OpenTk` (+ addon `DrawnUi.OpenTk.Game` for games: `DrawnGame`, `KeyboardManager`, `AspectLayer`). Framework rules in `drawnui` skill, C# composition in `drawnui-fluent`, GL-state-restore contract detail in the `drawnui` skill's "OpenTK / Mixed GL+DrawnUI" section.

## App csproj shape

```xml
<OutputType>WinExe</OutputType>            <!-- no console window on Windows -->
<TargetFramework>net10.0</TargetFramework>
<RuntimeIdentifiers>win-x64;linux-x64;linux-arm64</RuntimeIdentifiers>
<ApplicationIcon>icon.ico</ApplicationIcon> <!-- Explorer/taskbar -->
<ItemGroup>
  <EmbeddedResource Include="icon.ico" />   <!-- title-bar icon, loaded at runtime -->
  <Content Include="fonts\**"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
  <Content Include="Images\**"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
</ItemGroup>
```

- Linux publish needs `SkiaSharp.NativeAssets.Linux`.
- Self-contained single-file Release: `SelfContained` + `PublishTrimmed` (`TrimMode=full`) + `PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract`. Full trim cuts SkiaSharp/OpenTK reflection — root them: `<TrimmerRootAssembly Include="OpenTK.Graphics" />` + `OpenTK.Windowing.Desktop`, `SkiaSharp`, `HarfBuzzSharp`.
- Cross-link assets from a sibling project: `<Content Include="..\Other\fonts\X.ttf" Link="fonts\X.ttf" CopyToOutputDirectory="PreserveNewest" />`.

## Startup

`Super.UseDrawnUi().ConfigureFonts(fonts => fonts.AddFont("fonts/X.ttf", "Alias", FontWeight.Regular)).Build();` ONCE before creating any window/canvas. Fonts/assets resolve relative to the OUTPUT dir (no MAUI asset pipeline); `Super.Screen.Density = 1`. Namespaces: `DrawnUi.OpenTk`, `DrawnUi.Draw`, `DrawnUi.Views` — alias `using Color = DrawnUi.Color;` to dodge OpenTK's `Color`.

## Entry pattern A — `DrawnUiWindow` (fully-drawn app/game)

```csharp
var canvas = new Canvas { RenderingMode = RenderingModeType.Accelerated,
    UpdateMode = UpdateModeType.Constant, /* Fill, BackgroundColor, Content */ };
var ns = new NativeWindowSettings {
    ClientSize = new Vector2i(800, 600), Title = "My App",
    API = ContextAPI.OpenGL, Profile = ContextProfile.Core,
    APIVersion = OperatingSystem.IsLinux() ? new Version(3,3) : new Version(4,6),
    WindowState = WindowState.Normal, Icon = LoadWindowIcon(),
};
using var window = new DrawnUiWindow(new GameWindowSettings(), ns, canvas);
window.Run();
```

`DrawnUiWindow` owns the Skia GPU surface, VSync, mouse/keyboard routing, centering, chrome, fullscreen (F11 toggle / ESC exit built in), and the no-white-flash reveal (window hidden until first `SwapBuffers`). `Super.Init()` runs automatically in its `OnLoad`; `Super.MaxFps` = primary monitor refresh rate. Overridables: `PositionWindow()`, `ConfigureWindowChrome(hwnd)` (Windows-only, called only there), `RenderScene()` (raw GL behind the canvas — base restores GL state before and `ResetContext()` after; end yours with `GL.Finish()`).

Fixed-proportion scaling on resize: `RescalingCanvas { LogicalWidth = W, LogicalHeight = H }` (Pong) or the Game addon's `AspectLayer`.

## Entry pattern B — `CanvasHost` (overlay over your own GL loop)

Your `GameWindow` subclass owns rendering; DrawnUI composites as a transparent overlay (HUD, tools, dialogs).

- Lifecycle: `host.Initialize(wakeLoop?)` in `OnLoad` (also call `Super.Init()` yourself + `VSync = VSyncMode.On`), `host.Resize(w,h)` in `OnResize` (+ `GL.Viewport`), `host.ResetGrContext(); host.Render();` in `OnRenderFrame`, `host.Dispose()` in `OnUnload`.
- Per-frame order: restore GL state Skia left dirty → draw your 3D scene → `GL.Finish()` → `ResetGrContext()` → `host.Render()` → `SwapBuffers()`.
- Overlay Canvas MUST be `RenderingMode = AcceleratedRetained` + `BackgroundColor = Colors.Transparent` — otherwise Skia clears your scene. Also required for `SkiaBackdrop`/glass to have pixels to sample.
- `CanvasHost` does NOT auto-wire input — forward `OnMouseDown/Move/Up`, `OnTextInput`, `OnKeyDown` to `host.Gestures` / `host.Input`.
- White-flash fix is manual here: `StartVisible=false` + show after first `SwapBuffers`.
- Event-driven wake: `Initialize(GLFW.PostEmptyEvent)` so `Super.OnFrame` can wake a `WaitEvents` loop.

## Update modes (power profile)

- `UpdateMode.Constant` — VSync on, renders every frame. Games/animation.
- `UpdateMode.Dynamic` — VSync off, renders only when dirty, sleeps via `GLFW.WaitEventsTimeout(1/MaxFps)`. Pair with `GameWindowSettings { UpdateFrequency = 0 }` for low-power tools/editors/launchers.

## Input

- `DrawnUiWindow` auto-routes mouse (left button) + text input to the canvas (`HandleDesktopPointerDown/Move/Up`, `HandleDesktopTextInput`); editor keys (backspace/delete/enter/arrows/home/end/Ctrl+A/Tab→4 spaces) built in. Adding game keys: override `OnKeyDown`, call `base.OnKeyDown(e)` FIRST, then `OpenTkKeyMapper.Map(e.Key)` → `KeyboardManager.KeyboardPressed(...)` (release in `OnKeyUp`).
- Custom controls layered behind a `SkiaEditor`: return `null` from `ProcessGestures` on Up when you didn't capture on Down, or you steal the editor's focus.

## Window niceties

- **Title-bar icon**: embed `icon.ico` as `EmbeddedResource`, at startup decode with `SKBitmap.Decode`, resize to 32×32, swap BGRA→RGBA (`(p[i], p[i+2]) = (p[i+2], p[i])` per pixel), wrap in `OpenTK.Windowing.Common.Input.Image` → `WindowIcon` → `NativeWindowSettings.Icon`. (`ApplicationIcon` csproj property covers only Explorer/taskbar.)
- **DWM chrome** (override `ConfigureWindowChrome(hwnd)`, helper `WindowChrome`): `SetCaptionColor(hwnd,r,g,b)` / `SetBorderColor` (Win11+, caption text auto black/white by luminance), `SetDarkMode` (Win10 20H1+), `SetRoundedCorners` (Win11+).
- System menu gets a "Fullscreen" item + Windows UIA accessibility automatically (`DrawnUiWindow`).

## Assets

Source strings (`"Images/x.gif"`, `"Lottie/x.json"`) resolve relative to the output dir. #1 gotcha: a bare `<Content Include="x.png" />` in a WinExe SDK project is NOT copied — build succeeds, runtime silently renders nothing. Always `CopyToOutputDirectory="PreserveNewest"` and verify with `ls bin/Debug/net10.0/Images`. Keep shared code head-agnostic: same logical paths, per-head asset roots (OpenTK = output dir, MAUI = `Resources/Raw`, Web = `wwwroot`).

## Linux / WSL2

- `apt install libglfw3 libopenal1 libgl1 libgles2-mesa`; run with `DISPLAY=:0` on WSLg.
- EGL "Arguments are inconsistent" → bundled `libglfw.so.3` is an EGL build; symlink system GLX `libglfw3` over it.
- `GLXBadFBConfig` → request OpenGL 3.3 on Linux (Mesa D3D12 lacks 4.6).
- D3D12 "Removing Device" + segfault on WSLg → keep `WindowState = WindowState.Normal`.
- Uncapped FPS (Mesa ignores swap interval) → `DrawnUiWindow` soft-caps automatically; a custom `GameWindow` must set `UpdateFrequency` itself.

## macOS (unbuilt as of 2026-08-27 — investigated, not yet run)

Nothing in the stack blocks it; nobody has shipped it. What was verified:

- **GLFW natives exist** — `opentk.redist.glfw` 3.4.0.44 ships `runtimes/osx-arm64` and `runtimes/osx-x64`
  with `libglfw.3.dylib`. OpenTK 4 windowing is GLFW-based, hence cross-platform.
- **Skia natives exist** — use `SkiaSharp.NativeAssets.macOS`, the counterpart of the
  `SkiaSharp.NativeAssets.Linux` reference the samples already carry.
- **Windows-only code is guarded** — DWM chrome, the WndProc hook and `WindowsUiaProvider` all sit
  inside `if (OperatingSystem.IsWindows())` in `DrawnUiWindow.cs`. P/Invokes don't bind until called,
  so macOS won't trip on them. Expect a plainer window and no accessibility there.
- `RuntimeIdentifiers` in the samples lack `osx-arm64;osx-x64` — add them.

**The context settings are the trap.** The samples request GL 4.6 on anything that isn't Linux:

```csharp
APIVersion = OperatingSystem.IsLinux() ? new Version(3,3) : new Version(4,6),   // FAILS on macOS
```

macOS caps at **OpenGL 4.1**, so 4.6 fails context creation and the window never appears — the usual
reason people conclude "OpenTK doesn't run on Mac". It does. Use:

```csharp
APIVersion = OperatingSystem.IsWindows() ? new Version(4,6) : new Version(3,3),
Profile = ContextProfile.Core,
Flags = ContextFlags.ForwardCompatible,   // REQUIRED on macOS for core >= 3.2
```

`ForwardCompatible` is not optional on macOS. This lives in the caller that builds
`NativeWindowSettings`, not in the library.

Hardware acceleration is real — Apple implements GL over Metal, and Skia's GL backend needs only
GL 3.0+. GL is deprecated on macOS (since 10.14) but not removed; if it ever is, the escape hatch is
`GRContext.CreateMetal`, which changes the host, not DrawnUI drawing code.

## OpenTK head as a visual test harness

The fastest way to run shared DrawnUI code on desktop: a tiny OpenTK head over your shared-source project gives `dotnet run` → real GPU window, real mouse/keyboard, `Console.WriteLine` straight to your terminal — seconds per iteration instead of device deploys.

- Structure: shared `.projitems` with your scenes/controls + a throwaway OpenTK head project importing it (see `drawnui-web-app` skill for the shared-source pattern — identical here).
- Keep temp diagnostics in the harness head (a partial of your test page), never in shared/library code; delete before finishing.
- Pair with the `drawnui-net-harness` skill (headless, deterministic clock, pixel/structure asserts) — OpenTK head for eyeballing + interactive repro, headless harness for scripted assertions.
- `SkiaLabelFps` overlay + `Super.EnableRenderingStats` for quick perf reads.

## Samples (in-repo)

- `src/OpenTk/Samples/OpenTkPong` — fully-drawn game: `DrawnUiWindow` + `Constant`, `RescalingCanvas`, DWM chrome, key mapper, single-file publish. Shares game code with MAUI/Web heads via `Pong.Shared.projitems`.
- `src/OpenTk/Samples/OpenTkGpuHost` — event-driven low-power UI: `Dynamic` + `UpdateFrequency=0`.
- `src/OpenTk/Samples/OpenTkOverlay` — mixed host: custom `GameWindow` + `CanvasHost`, raw GL cube + transparent overlay with `SkiaBackdrop` glass + `SkiaEditor`.

Docs: `docs/articles/opentk/` (window, gestures, resources, faq, samples).
