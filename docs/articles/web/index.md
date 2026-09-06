# DrawnUi.Wasm (Pure WebAssembly)

`DrawnUi.Wasm` runs DrawnUI directly in the browser as a **pure .NET WebAssembly app — no Blazor**.

It uses only `[JSImport]`/`[JSExport]` interop (no `IJSRuntime`, no `ElementReference`, no Razor) and is built on the same platform-agnostic base as OpenTK (the `DRAWNUI_NET` path). DrawnUI owns an HTML `<canvas>` element and renders to it through SkiaSharp.

## Install

```bash
dotnet add package DrawnUi.Wasm
```

## When to use it

Choose `DrawnUi.Wasm` over `DrawnUi.Blazor.Wasm` when:

- you want a **standalone, fully drawn web app** (game, tool, canvas surface) without the Blazor component model
- you do not need Razor pages, routing, or mixing native HTML widgets with DrawnUI
- you want the smallest WASM payload for a single full-canvas app

Choose [`DrawnUi.Blazor.Wasm`](../blazor/index.md) instead when DrawnUI should live as a `Canvas` component inside a Blazor app alongside Razor UI.

## DrawnUi.Wasm vs Blazor

| | `DrawnUi.Wasm` | `DrawnUi.Blazor.Wasm` |
| --- | --- | --- |
| Host model | Pure WASM `Main()` via `[JSExport]` | Blazor component (`<Canvas>` in Razor) |
| Interop | `[JSImport]`/`[JSExport]` only | `IJSRuntime`, `ElementReference` |
| App shape | One full-canvas app per page | Many canvases mixed with Razor UI |
| Base | `DRAWNUI_NET` (shared with OpenTK) | Blazor `DrawnUi.Blazor.Core` |

## Rendering modes

`Canvas.RenderingMode` picks the backend:

- `Accelerated` — WebGL (GPU). Auto-falls back to raster if WebGL is unavailable.
- `Default` — raster (CPU) via `putImageData`.

Set `RenderingMode` **before** the canvas is attached (the host does this for you in `RunAsync`).

## Entry point

```csharp
[JSExport]
public static Task Main() =>
    Super.UseDrawnUi()
        .RunAsync("drawnui-canvas", () => new Canvas
        {
            Gestures = GesturesMode.Enabled,
            RenderingMode = RenderingModeType.Accelerated,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = /* your DrawnUI tree */
        });
```

`RunAsync(elementId, factory)` wires rendering, input, the `requestAnimationFrame` loop, resize, fonts and gestures automatically.

## Fonts and styles

- `ConfigureFonts(...)` and `ConfigureStyles(...)` work the same as on other targets.
- Fonts are served as **static web assets** (`wwwroot/fonts/...`) and fetched over HTTP at startup. The legacy `WasmFilesToBundle` mechanism is a no-op in the .NET WASM SDK, so do **not** rely on it — place fonts under `wwwroot` and register them with a relative path.

## Gestures

`Gestures = GesturesMode.Enabled` routes browser pointer/touch/wheel input to DrawnUI. A right click (or a long press, or the Menu key) raises `ContextMenu` on the control under it; a handler that sets `e.Handled = true` suppresses the browser's own canvas menu, otherwise it shows — see [Handling Gestures](../gestures.md#context-menu-on-the-web-right-click). Right and middle clicks never `Tapped`. If you wire `main.js` by hand, pass `onContextMenu: Input.OnContextMenu` in `setModuleExports`. `GesturesMode.Lock` additionally applies the CSS guard (`touch-action`, `overscroll-behavior`, a non-passive `touchmove` guard) that stops iOS page panning / swipe-to-close while DrawnUI owns the gesture.

## Samples

- `src/Web/DrawnUi.Wasm.Sample` — minimal "Hello DrawnUI on Web" with a button.
- `src/Web/Samples/PongWeb` — full game (GPU, fonts, gestures, OG/SEO). Live demo: <a href="https://pong.appomobi.com/" target="_blank" rel="noopener noreferrer">pong.appomobi.com</a>.

## Start here

- [Getting Started (DrawnUi.Wasm)](getting-started.md)
- [Platforms and Packages](../platforms.md)
- [Handling Gestures](../gestures.md)
- [Blazor](../blazor/index.md)
