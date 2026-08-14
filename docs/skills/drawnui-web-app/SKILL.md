---
name: drawnui-web-app
description: Scaffold a DrawnUi.Web app — pure WebAssembly (no Blazor) DrawnUI in the browser. Create a new standalone web app, or extract shared DrawnUI code from an existing app (MAUI/OpenTK) into a shared-source project and add a web head that references it. Also covers hunting intermittent WASM runtime bugs (frozen lotties, dead caches, Task.Run starvation) with probes + CPU throttling. Trigger on "drawnui web app", "drawnui.web", "drawnui.wasm", "DrawnUi.Wasm", "src/Wasm", "pure wasm drawnui", "run my drawnui app in browser", "extract drawnui shared code", "add web target to drawnui", "bug only on slow device", "lottie not playing in browser".
version: 1.1.0
tags: [drawnui, web, webassembly, wasm, shared-project, scaffolding]
---

# DrawnUi.Web App Scaffolding

Create or extend a **pure-WebAssembly DrawnUI app** (`DrawnUi.Web` package — NO Blazor, NO Razor, only `[JSImport]`/`[JSExport]`). Built on the `DRAWNUI_NET` base (SharedNet + Net shims), same base as OpenTK.

Load the **`drawnui`** skill for control/layout/caching rules and **`drawnui-fluent`** for code-behind composition. This skill covers ONLY the project plumbing.

> Repo layout note: the library project file is named `DrawnUi.Wasm.csproj` and lives under `src/Wasm/`, but its `PackageId` / NuGet package is still **`DrawnUi.Web`**. So all JS-facing names stay `DrawnUi.Web`: `PackageReference Include="DrawnUi.Web"`, `_content/DrawnUi.Web/drawnui-web.js`, `getAssemblyExports('DrawnUi.Web')`. Only the in-repo folder/csproj names changed (`src/Web` → `src/Wasm`).

Canonical reference in the DrawnUi repo: shared-source `src/Shared/Samples/Pong.Shared` (`Pong.Shared.shproj` + `.projitems`) consumed by the web head `src/Wasm/Samples/PongWeb`. Minimal starter: `src/Wasm/Samples/WasmSample` (`DrawnUi.Wasm.Sample.csproj`). Docs: `docs/articles/web/index.md` + `getting-started.md`.

---

## Decide the flow

- **New standalone web app** → do PART A only.
- **Existing DrawnUI app (MAUI/OpenTK) → also runs in browser** → do PART B (extract shared) then PART A (web head imports shared).

---

## Core rule: shared code is SHARED-SOURCE, not a class library

DrawnUI base types differ per target via `#if` and per-head NuGet package (`DrawnUi.Maui` vs `DrawnUi.Web` vs `DrawnUi.OpenTk`). A normal class library would bind to ONE package and break the others.

Use a **Shared Project** (`.shproj` + `.projitems`): the `.cs` files compile INTO each head with that head's package + constants. This is why `Pong.Shared` is `.shproj`, not a `.csproj`.

Put in shared: scenes, controls, game logic, view models, the `Canvas`/scene factory. Keep platform entry points (`Main`/`MauiProgram`) in each head.

---

## PART A — Web head (new app, or web head for shared code)

### A1. csproj

`Microsoft.NET.Sdk.BlazorWebAssembly` SDK (for static-asset/host plumbing) but NO Blazor code. `WasmBuildNative=true` is required so SkiaSharp links its WebGL js-library.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <WasmBuildNative>true</WasmBuildNative>
    <!-- DRAWNUI_NET = non-Blazor base; WEB/BROWSER = web defaults -->
    <DefineConstants>$(DefineConstants);DRAWNUI_NET;WEB;NET;NET10;BROWSER</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DrawnUi.Web" Version="*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.*" PrivateAssets="all" />
    <PackageReference Include="SkiaSharp.NativeAssets.WebAssembly" Version="*" />
    <PackageReference Include="HarfBuzzSharp.NativeAssets.WebAssembly" Version="*" />
  </ItemGroup>

  <ItemGroup>
    <WasmExtraConfig Include="emcc_flags">
      <Value>-s ERROR_ON_UNDEFINED_SYMBOLS=0 -s ALLOW_MEMORY_GROWTH=1</Value>
    </WasmExtraConfig>
  </ItemGroup>

  <!-- Only when consuming shared code (PART B). Relative path to the .projitems. -->
  <Import Project="..\..\Shared\MyApp.Shared.projitems" Label="Shared" />

</Project>
```

Match the SkiaSharp/HarfBuzz preview versions to whatever `DrawnUi.Web` itself references (check `src/Wasm/DrawnUi/DrawnUi.Wasm.csproj`) to avoid native-mismatch.

### A2. wwwroot/index.html

One `<canvas id>` + the loader module. Set `touch-action:none` on the canvas.

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>My DrawnUI Web App</title>
  <style>
    body { margin:0; padding:0; overflow:hidden; }
    #drawnui-canvas { width:100vw; height:100vh; display:block; touch-action:none; }
  </style>
</head>
<body>
  <canvas id="drawnui-canvas"></canvas>
  <script type="module" src="./main.js"></script>
</body>
</html>
```

### A3. wwwroot/main.js

Boots the runtime and calls the app's `[JSExport] Main`, wiring DrawnUI input/frame/resize. Copy the full reference from `src/Wasm/Samples/WasmSample/wwwroot/main.js` (spinner, recursive `Main` lookup, error UI). Essential glue:

```js
import { dotnet } from './_framework/dotnet.js';
import { setModuleExports } from './_content/DrawnUi.Web/drawnui-web.js';

globalThis.dotnet = dotnet;
const { getAssemblyExports, getConfig } = await dotnet
  .withApplicationArgumentsFromQuery().create();

const lib = await getAssemblyExports('DrawnUi.Web');
const Input = lib.DrawnUi.Draw.WebInput;
const Super = lib.DrawnUi.Draw.Super;
const Host  = lib.DrawnUi.Draw.BrowserHost;
setModuleExports({
  onBrowserFrame: Super.OnBrowserFrame, onCanvasResize: Host.OnCanvasResize,
  onPointerDown: Input.OnPointerDown, onPointerMove: Input.OnPointerMove,
  onPointerUp: Input.OnPointerUp, onPointerCancel: Input.OnPointerCancel,
  onWheel: Input.OnWheel, onKeyDown: Input.OnKeyDown, onKeyUp: Input.OnKeyUp,
});

const config = getConfig();
const app = await getAssemblyExports(config.mainAssemblyName);
// find [JSExport] Main in `app` and call it
```

### A4. Program.cs

```csharp
public static partial class Program
{
    [JSExport]
    public static Task Main() =>
        Super.UseDrawnUi()
            .ConfigureFonts(fonts => fonts.AddFont("fonts/MyFont.ttf", "MyFont"))
            .ConfigureStyles(styles => { /* optional Style setters */ })
            .RunAsync("drawnui-canvas", () => BuildScene());   // BuildScene lives in shared code

    static Canvas BuildScene() => new Canvas
    {
        Gestures = GesturesMode.Enabled,
        RenderingMode = RenderingModeType.Accelerated, // WebGL, auto-falls back to raster
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Content = /* your DrawnUI tree (ideally from shared project) */
    };
}
```

`RunAsync(elementId, factory)` wires rendering, input, the rAF loop, resize, fonts and gestures.

---

## PART B — Extract shared code from an existing DrawnUI app

### B1. Create the shared project folder

```
src/Shared/
  MyApp.Shared.shproj
  MyApp.Shared.projitems
  GlobalAliases.cs        (optional, e.g. type aliases)
  ... moved scene/control/.cs files ...
```

### B2. MyApp.Shared.shproj (one fresh GUID, reused below)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup Label="Globals">
    <ProjectGuid>PUT-A-NEW-GUID-HERE</ProjectGuid>
    <MinimumVisualStudioVersion>14.0</MinimumVisualStudioVersion>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props" Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.Default.props" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.Common.props" />
  <PropertyGroup />
  <Import Project="MyApp.Shared.projitems" Label="Shared" />
  <Import Project="$(MSBuildExtensionsPath32)\Microsoft\VisualStudio\v$(VisualStudioVersion)\CodeSharing\Microsoft.CodeSharing.CSharp.targets" />
</Project>
```

### B3. MyApp.Shared.projitems (SAME GUID; list EVERY shared .cs)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <PropertyGroup>
    <MSBuildAllProjects Condition="'$(MSBuildVersion)' == '' Or '$(MSBuildVersion)' &lt; '16.0'">$(MSBuildAllProjects);$(MSBuildThisFileFullPath)</MSBuildAllProjects>
    <HasSharedItems>true</HasSharedItems>
    <SharedGUID>PUT-A-NEW-GUID-HERE</SharedGUID>
  </PropertyGroup>
  <PropertyGroup Label="Configuration">
    <Import_RootNamespace>MyApp</Import_RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$(MSBuildThisFileDirectory)Scene\MainScene.cs" />
    <!-- add EVERY moved .cs file here; nothing auto-globs in a shared project -->
  </ItemGroup>
</Project>
```

### B4. Move code + add to projitems

- Move scenes/controls/logic out of the existing head into `src/Shared/`.
- Add each moved file as a `<Compile Include=.../>` in `.projitems`.
- Expose a scene factory the heads call (e.g. `public static Canvas BuildScene()` or a `SkiaControl` subclass) instead of building the tree inside `Main`/`MauiProgram`.

### B5. Import the shared project into EVERY head

Existing head (e.g. MAUI `.csproj`) and the new web head:

```xml
<Import Project="..\..\Shared\MyApp.Shared.projitems" Label="Shared" />
```

(Relative path from each head to the `.projitems`.) The existing head keeps its `DrawnUi.Maui`/`DrawnUi.OpenTk` package; the web head adds `DrawnUi.Web`. Same source, compiled per target.

### B6. Guard platform-specific code in shared files

```csharp
#if BROWSER            // DrawnUi.Web (and Blazor) — pure-managed web
#elif DRAWNUI_NET      // Net / OpenTK desktop base
#else                  // MAUI heads
#endif
```

Audit moved code for MAUI-only APIs (`MainThread`, native handlers, `FileSystem.OpenAppPackageFileAsync`) and gate or abstract them.

---

## Web-specific gotchas (carry from the `drawnui` skill)

- **Fonts**: `WasmFilesToBundle` is a NO-OP in the .NET WASM SDK — fonts must be STATIC WEB ASSETS under `wwwroot/fonts/`, registered with a relative path; `DrawnUi.Web` fetches them over HTTP at startup (`SkiaFontManager.InitializeWebAsync`). Do not use `WasmFilesToBundle`.
- **Styles**: `ConfigureStyles(...)` works; explicit per-control property setters WIN over styles (a hardcoded `FontFamily="X"` overrides the style's font).
- **Gestures**: `GesturesMode.Lock` auto-applies the iOS swipe-away CSS/JS guard (lib-level). `Enabled` = route input, no page guard.
- **RenderingMode** must be final BEFORE the canvas attaches — `RunAsync` handles it; don't flip it afterward (disposes the view, kills the loop).
- GPU traps + headless CDP validation: read `src/Wasm/DrawnUi/CLAUDE.md`.

---

## Hot reload (design loop)

Run with `dotnet watch` (DevServer injects the browser-refresh script). `DrawnUi.Web` has BUILT-IN scene hot reload (lib-level, fully automatic, no debugger required, no user code):

- **C# Hot Reload (deltas, keeps state)** — method-body edits that run after the edit apply live: game loop, `WhenPaint`/`Draw`, gesture/tap handlers, per-frame logic.
- **Static scene tree rebuild** — the tree built in your `RunAsync` factory IS rebuilt automatically: `[MetadataUpdateHandler]` (`DrawnUi.HotReloadService`, debounced ~1s) → `Super.HotReload` event → `BrowserHost` re-runs the factory and re-attaches, reusing the same `WebSkiaView`/GL context. So edit your `Content = ...` tree and it refreshes in place. No debugger gate — fires under bare `dotnet watch`; inert in published apps (runtime only applies deltas during dev).
- Force a manual rebuild any time: `BrowserHost.RebuildScene()`.
- Mirrors the MAUI `BasePageReloadable` / `Super.HotReload` pattern. Implementation: `src/Wasm/DrawnUi/HotReload.cs`, `Super.Web.cs` (event), `BrowserHost.cs` (`RebuildScene`/`AttachScene`).

Caveats:
- The factory passed to `RunAsync` must rebuild the WHOLE tree (don't capture and mutate one cached control) — `RebuildScene` calls it fresh each time.
- `WasmBuildNative=true` does NOT block managed hot reload (native relink only on native/csproj changes).
- **XAML hot reload: N/A** — DrawnUi.Web is code-only.

---

## Build / run / validate

```bash
dotnet build  MyApp.Web.csproj -c Debug   # first build relinks native (slow), expected
dotnet run    --project MyApp.Web.csproj  # DevServer serves http://localhost:<port>
dotnet watch  --project MyApp.Web.csproj  # design loop with hot reload
```

Validate fonts/assets are served (e.g. `curl -I http://localhost:<port>/fonts/MyFont.ttf` → 200). For runtime/render validation without Playwright MCP, drive headless Chrome over CDP per `src/Wasm/DrawnUi/CLAUDE.md` (confirm a rAF tick counter; WebGL screenshots are unreliable).

---

## Pitfalls checklist

- Forgot `WasmBuildNative=true` → GPU path silently raster / link errors.
- Used a class library for shared code → wrong DrawnUI base per target. Use `.shproj`/`.projitems`.
- Forgot to add a moved file to `.projitems` → "type not found" only on the web head.
- Font under project `fonts/` via `WasmFilesToBundle` instead of `wwwroot/fonts/` → font never loads.
- `<base href>` mismatch → relative font/asset fetch 404; check `document.baseURI`.
- Flipping `RenderingMode` after attach → blank canvas.
- SkiaSharp/HarfBuzz native versions diverge from `DrawnUi.Web` → native load failure.

---

## Runtime bug hunting (WASM — pure DrawnUi.Web or Blazor)

For intermittent bugs that only appear on slow devices / under CPU throttle — frozen lotties, dead caches, stalled pipelines. Static analysis alone fails; get runtime evidence early: one probe trace from a real frozen instance beats hours of reading.

### Known single-threaded WASM facts (check FIRST)

- **There is no background thread.** `Task.Run` work runs on the MAIN thread between `requestAnimationFrame` callbacks.
- **`Task.Run` work can starve INDEFINITELY** under continuous rAF load + CPU throttle. Any must-run pipeline stage needs inline or frame-loop execution on browser: `if (OperatingSystem.IsBrowser()) DoInline(); else Task.Run(...)`.
- **Never gate with a one-shot "is running" bool** reset only inside the queued task — a lost task latches the gate forever and all later work is silently dropped.
- `ImageDoubleBuffered` shows ONLY its cache: if the build pipeline dies, the control is an empty box at correct size while its animator ticks. `Operations`/`None` paint inline — immune. "Changing UseCache fixes it" = pipeline bug, not control bug.
- Pure-WASM head (`BrowserHost`) redraws EVERY rAF regardless of invalidation — "canvas went idle" theories are dead there.
- Detached-but-not-disposed controls keep animators registered on the Canvas forever. Overlays/dialogs must be `Dispose()`d after `RemoveSubView`; `ClearChildren()` only detaches — use `DisposeChildren()` for rebuilt lists.

### Probe pattern (temporary, in lib)

Central helper in a `SkiaControl` partial, type-filtered + budgeted; `Console.WriteLine` lands in the browser console:

```csharp
// TEMP PROBE — remove after investigation
internal static int _probeBudget = 100000;
internal void ProbeLog(string msg)
{
    if (_probeBudget > 0 && GetType().Name == "SkiaLottie")
    { _probeBudget--; Console.WriteLine($"[PROBE {Uid.ToString().Substring(0,4)}] {msg}"); }
}
```

Place probes at every stage boundary of the suspected pipeline — the MISSING line in a frozen trace is the answer. Log anomalies always, healthy steady state throttled. High-value probes: `Update()` swallow branches (`NeedUpdate=true` + frozen `RenderCount` = permanently muted control), `Render()` entry, animator `Start` with `canPlay/wasLayout/running/superview`, per-tick heartbeat every 60th.

### Browser automation (Playwright + CDP)

```js
const session = await page.context().newCDPSession(page);
await session.send('Network.enable');
await session.send('Network.setCacheDisabled', { cacheDisabled: true }); // MANDATORY after server rebuild
await session.send('Emulation.setCPUThrottlingRate', { rate: 2 });
```

- DrawnUi canvas = no DOM: click by coordinates from one screenshot read.
- Animation detection without reading images: two tightly-clipped screenshots 700 ms apart, buffer equality (`!a.equals(b)` = animated). Exclude FPS overlays from the clip.
- Debug-interpreter timing may never reproduce production timing — hand the probe build to a real repro environment early instead of burning attempts.

### Dev server gotchas

- After a lib edit: `dotnet build`, then KILL + restart the dev server. A restarted server with changed fingerprints serves stale SRI-mismatched manifests to a cached page → silent boot failure ("Failed to fetch ... .wasm") — always disable browser cache via CDP + hard reload.
- Verify the browser runs YOUR build: the served `dotnet.native.<hash>.js` hash must match the local build output.
- Release publish drops `#if DEBUG` dev screens; temporary csproj `<DefineConstants>$(DefineConstants);DEBUG;</DefineConstants>` re-enables (REMOVE after).
- Cleanup before finishing: all probes out (grep `ProbeLog|_probe`), temp defines out, everything rebuilds clean.

## SEO: crawlers must not download the WASM payload

For any public DrawnUi.Web/WASM site: static `<h1>`+prose SEO footer outside the app mount (below fold via `#app{min-height:100vh}`, never display:none), robots.txt `Disallow` on the runtime/payload dirs (with favicon Allow exceptions), favicon >=48x48 for Google. Full recipe: see `drawnui-blazor` skill "SEO for Blazor WASM sites".
