# Getting Started — DrawnUi.Wasm

This walks through a standalone pure-WebAssembly DrawnUI app (no Blazor).

## Project file

`DrawnUi.Wasm` apps use the `Microsoft.NET.Sdk.BlazorWebAssembly` SDK (for the static-web-asset/host plumbing) but contain **no Blazor code**. Native build is required so SkiaSharp links its WebGL js-library.

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <WasmBuildNative>true</WasmBuildNative>
    <!-- DRAWNUI_NET = non-Blazor base; BROWSER enables web defaults -->
    <DefineConstants>$(DefineConstants);DRAWNUI_NET;WEB;NET;NET10;BROWSER</DefineConstants>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="DrawnUi.Wasm" Version="*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.DevServer" Version="10.0.*" PrivateAssets="all" />
    <PackageReference Include="SkiaSharp.NativeAssets.WebAssembly" Version="*" />
    <PackageReference Include="HarfBuzzSharp.NativeAssets.WebAssembly" Version="*" />
  </ItemGroup>

</Project>
```

> `WasmBuildNative=true` triggers a slower emcc relink whenever native inputs change. That is expected.

## Prerequisites

- .NET 10 SDK with the `wasm-tools` workload: `dotnet workload install wasm-tools`.
- A browser with WebGL for the GPU path (the app auto-falls back to raster otherwise).

## index.html

A static page with one `<canvas id>` and the loader module:

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>My DrawnUI Web App</title>
    <style>
        body { margin: 0; padding: 0; overflow: hidden; }
        #drawnui-canvas { width: 100vw; height: 100vh; display: block; touch-action: none; }
    </style>
</head>
<body>
    <canvas id="drawnui-canvas"></canvas>
    <script type="module" src="./main.js"></script>
</body>
</html>
```

## main.js

Boots the .NET runtime and calls the app's `[JSExport] Main`, wiring the DrawnUI input/frame/resize callbacks (this is the same loader used by the samples):

```js
import { dotnet } from './_framework/dotnet.js';
import { setModuleExports } from './_content/DrawnUi.Wasm/drawnui-web.js';

globalThis.dotnet = dotnet;
const { getAssemblyExports, getConfig } = await dotnet
    .withApplicationArgumentsFromQuery()
    .create();

const lib = await getAssemblyExports('DrawnUi.Wasm');
const Input = lib.DrawnUi.Draw.WebInput;
const Super = lib.DrawnUi.Draw.Super;
const Host  = lib.DrawnUi.Draw.BrowserHost;

setModuleExports({
    onBrowserFrame: Super.OnBrowserFrame,
    onCanvasResize: Host.OnCanvasResize,
    onPointerDown: Input.OnPointerDown,
    onPointerMove: Input.OnPointerMove,
    onPointerUp:   Input.OnPointerUp,
    onPointerCancel: Input.OnPointerCancel,
    onWheel: Input.OnWheel,
    onContextMenu: Input.OnContextMenu,
    onKeyDown: Input.OnKeyDown,
    onKeyUp:   Input.OnKeyUp,
});

const config = getConfig();
const app = await getAssemblyExports(config.mainAssemblyName);
// find the [JSExport] Main in your app assembly and call it
```

See `src/Web/DrawnUi.Wasm.Sample/wwwroot/main.js` for the complete reference loader (loader spinner, recursive `Main` lookup, error UI).

## Program.cs

```csharp
public static partial class Program
{
    [JSExport]
    public static Task Main() =>
        Super.UseDrawnUi()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("fonts/MyFont.ttf", "MyFont"); // served from wwwroot/fonts
            })
            .RunAsync("drawnui-canvas", () => new Canvas
            {
                Gestures = GesturesMode.Enabled,
                RenderingMode = RenderingModeType.Accelerated,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Content = new SkiaLayout
                {
                    Type = LayoutType.Column,
                    Padding = new Thickness(40),
                    Children =
                    {
                        new SkiaLabel { Text = "Hello DrawnUI on Web!", FontSize = 24 },
                    }
                }
            });
}
```

## Fonts (static web assets)

Place font files under `wwwroot/fonts/` and register them with a **relative** path:

```csharp
.ConfigureFonts(fonts => fonts.AddFont("fonts/MyFont.ttf", "MyFont"))
```

At startup DrawnUI fetches each registered font over HTTP and parses it with SkiaSharp. Do **not** use `WasmFilesToBundle` — it is a no-op in the .NET WASM SDK, and the font would never load.

### Built-in symbol & emoji fonts (mind the payload)

DrawnUI ships subset fonts for glyph blocks most text fonts lack. On WASM these download at startup over HTTP, so their size is part of your transfer budget — and on a host without brotli/gzip (e.g. GitHub Pages) they ship **uncompressed**:

| Call | Registers | Aliases | Size (uncompressed) |
|------|-----------|---------|---------------------|
| `fonts.AddSymbols()` | Noto Sans Math Symbols + Noto Sans Symbols 2 subsets | `FontSymbols`, `FontSymbols2` | ~285 KB (152 + 133) |
| `fonts.AddEmoji()` | Noto Color Emoji subset | `FontEmoji` | ~920 KB |

```csharp
.ConfigureFonts(fonts =>
{
    fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText");
    fonts.AddSymbols();   // ↑ → ✓ ● math/arrows/dingbats — ~285 KB
    // fonts.AddEmoji(); // color emoji — ~920 KB, add only if you render emoji
})
```

`AddSymbols()` fills arrows/math/dingbats (e.g. `↑ → ✓`) that OpenSans and similar text fonts don't carry. `AddEmoji()` is the heavy one (~0.9 MB) — include it only if you actually render emoji glyphs. To trim further, subset these to just the codepoints you use (see the font-slimming workflow).

## Run

```bash
dotnet run --project MyApp.csproj
```

The dev server serves the app at `http://localhost:<port>`.

## Hot reload (design loop)

Run with `dotnet watch --project MyApp.csproj`. `DrawnUi.Wasm` has built-in scene hot reload — fully automatic, no debugger or extra setup: editing per-frame logic (game loop, paint, gesture handlers) applies live, and the scene tree built in your `RunAsync` factory is **rebuilt automatically** on a C# Hot Reload update (`Super.HotReload` → `BrowserHost.RebuildScene()`, reusing the same WebGL context). Call `BrowserHost.RebuildScene()` to force a rebuild. Note: the factory must rebuild the whole tree. XAML hot reload does not apply (DrawnUi.Wasm is code-only).

## Related

- [DrawnUi.Wasm Overview](index.md)
- [Platforms and Packages](../platforms.md)
- [Handling Gestures](../gestures.md)
