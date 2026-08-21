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

## What's New 1.10.5.13
* Fixed shader effects sampling the wrong texel when the input texture is not the control's box: an image whose `AspectCover` overflows its box (square/landscape source in a taller-than-wide tile) rendered off-centre, and a cache inflated by effects/shadow margins was offset by that margin. `CachedImage` now returns a `CachedTexture` carrying the image together with the canvas rect it was rasterized over. **Breaking** for custom effects: `GetPrimaryTextureImage` is renamed `GetPrimaryTexture`, and it, `CreateShader`, `ShouldDisposePreviousTexture` and `SyncEngineState` take/return `CachedTexture`.
* Fixed effect input snapshot taken with canvas coordinates out of a cache surface when the parent bakes into a cache.
* Fixed templated `SkiaCarousel` with `RecyclingTemplate.Disabled` coming up blank: the cell pool was one short of what a full initialization rents at once.
* Docs: the standard shader uniforms (`iResolution`, `iImageResolution`, `iTime`, `iOffset`, `iMouse`) are **mandatory** in every `.sksl`, even when unused. The engine writes all of them every frame and writing one the compiled shader does not declare throws, aborting shader creation so the effect silently renders nothing.
*  Fixed `SkiaImageManager` cache keys now slash-agnostic on all platforms.

 ### Previously

*  Hotfix for smoother rendering loop frames sync
*  Hotfix for templated layouts which were cached (BindableLayout-like), were not invalidated when applying staged structure changes
* Fixed SkiaCarousel changing selected index in the middle of animating.
* Fix children clipping in viewport when children had effects. `ClipContentPath` is now in base.
* Fix FPS metering for capped scenarions.
* Fixed Split=1 on a grid without ColumnDefinitions.
* Target fps now allows floats instead of integers for more fluent animations, resulting in smoother scrolling etc.
* Scrolling ending jagging removed on density < 1.5 devices.
* SkiaLayout Wrap fix for auto-sized children, other layout fixes
 
---
MIT | Free to use and customize

