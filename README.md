# DrawnUI for .NET
![NuGet DrawnUi.Net](https://img.shields.io/nuget/v/DrawnUi.Net.svg)
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/taublast/drawnui/blob/master/CONTRIBUTING.md)

DrawnUI is a rendering and UI composition engine for .NET, powered by [SkiaSharp](https://github.com/mono/SkiaSharp) with gestures, layouts, effects and animations running with hardware acceleration.

🤩 [Fiddle in browser](https://fiddle.drawnui.net) 👈

Supported hosts:

* `DrawnUi.Maui` - Android, iOS, MacCatalyst, and Windows.
* `DrawnUi.Blazor.Wasm` - browser WebAssembly rendering.
* `DrawnUi.Blazor.Server` - server-backed DrawnUI surfaces served by Blazor Server.
* `DrawnUi.Wasm` - pure browser WebAssembly, no Blazor required.
* `DrawnUi.OpenTk` - Windows and Linux desktops.
* `DrawnUi.Net` - platform-agnostic console/server rendering scenarios.
* More to come..

## Features 

* __Imagine your  UI__ - a toolbox for creating drawn controls
* __Harness the Canvas__ - engine handles everything
* __Port existing native to drawn__ - easy port, bindings support
* __Design in XAML, Razor + Canvas, or code-behind__
* __2D and 3D Transforms__
* __Visual effects__ for every control, filters and shaders
* __Animations__ targeting max FPS
* __Caching system__ for faster re-drawing
* __Optimized for performance__, rendering only visible elements, recycling templates etc
* __Gestures__ support for anything, panning, scrolling, zooming etc
* __Keyboard support__, track any key
* __Navigate__ on the canvas with shell-like techniques 

😎 [Blazor sample in browser](https://drawnui.net/sandbox/) 👈

## Addons

* Create games: `DrawnUi.DrawnUi.Game`, `DrawnUi.Blazor.Game`,`DrawnUi.OpenTk.game`.
* .NET MAUI only: `DrawnUi.MauiGraphics`
* .NET MAUI only: `DrawnUi.DrawnUi.MapsUi`
* .NET MAUI only: `DrawnUi.DrawnUi.Camera` - [Separate repo](https://github.com/taublast/DrawnUi.Maui.Camera).

---

## Resources

👉 [Docs and Samples](https://drawnui.net)   
🤖 [AI skills](https://drawnui.net/llms.txt)   
🤩 [Fiddle](https://fiddle.drawnui.net)   
⛹️ [Pong in pure WASM](https://pong.appomobi.com/)


## What's New 1.10.5.7

* On iOS MaxFps cap is now synched with display rate for better smothness.

 ### Previously 1.10.5.6

* Fixed Split=1 on a grid without ColumnDefinitions.

 ### Previously 1.10.5.5

* Target fps now allows floats instead of integers for more fluent animations, resulting in smoother scrolling etc.
* Scrolling ending jagging removed on density < 1.5 devices.
* SkiaLayout Wrap fix for auto-sized children, other layout fixes

### Previously 1.10.5.2

### General

* Breaking: migrated to SkiaSharp v4 API: `SKFilterQuality` removed (SkiaSharp), DrawnUi's own `FilterQuality` enum replaces it.
`SKPaint.FilterQuality` removed. Quality moves to SKSamplingOptions: Fix via existing `DrawnUi.Draw.SkiaSamplingOptions.GetSamplingOptions(FilterQuality)` helper.
* Added pure-WASM target (without Blazor);
* Web fonts for symbols and emojis shipped; `LoadFontAsync` via http with AOT support.
* Caching now auto-expands to include `VisualEffects` shadows/glows and SkiaShape own`Shadows`, no more need for wrappers.
* `Style` support now also for for Blazor / .NET / OpenTK targets (previously MAUI-only).
* `SharedCache` for reusing rendered caches between controls: use logical different instances of same control, same memory. 
* New `Material3` (Material You) prebuilt control style; restyled Switch, Checkbox, Slider, Progress, RadioButton, Button, Editor per platform style.
* FIX gestures for cached controls without render tree and for transformed controls.
* FIX critical: empty views taking all available size instead of 0; critical stack measurement fix.
* `SkiaGrid` autosize fixed for autosized children; ROW layout right padding fix.
* Fluent extensions improvements, compiled bindings support. 
* Range animator can be shifted externally.
* Optimized shader compilation for `SkiaShader`; auto-expanded cache region for effects.
* `ImageDoubleBuffered` cache no longer killed on change; `CachedObject.LogicalBounds` for correct gesture math on inflated caches.
* Touch effects fixed, new shockwave touch shader; shimmer effect respects speed.

### Controls
* Prebuilt control styles `Cupertino`, `Material`, `Material3`, `Windows` for `SkiaProgress`, `SkiaButton`, `SkiaSlider`, `SkiaSwitch`, `SkiaCheckbox`, `SkiaEditor`, customizations properties fixed for above controls.
* `SkiaScroll` finally gets scrollbars with customization properties.
* Recycled cells / virtualization rewrite: built-in windowed `ItemsSource` support (internal limited window pack), `ViewsAdapter` re-allocation reduction, cell pre-warming, created `SkiaCadhedStack`.
* `AutoCache` defaults to true for Scroll and Drawer: draws cache instead of content when idle.
* `MarkdownEnabled` on SkiaRichLabel — render emoji/unicode plain text without markdown parsing.
* New ascent handling on Windows for SkiaLabel, reduced allocations; crash fix disposing text spans (MAUI).
* `SkiaEditor` proper `AutoHeight` property, multiline submit: Enter submits, Shift+Enter inserts newline (all platforms incl. Windows) and many other fixes and improvements.


---
MIT | Free to use and customize

