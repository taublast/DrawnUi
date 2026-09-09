# DrawnUI for .NET
![NuGet DrawnUi.Net](https://img.shields.io/nuget/v/DrawnUi.Net.svg)
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/taublast/drawnui/blob/master/CONTRIBUTING.md)

👉 [Official Site](https://drawnui.net)   

DrawnUI is a rendering and UI composition engine for .NET, powered by [SkiaSharp](https://github.com/mono/SkiaSharp) with gestures, layouts, effects and animations running with hardware acceleration.

🤩 [Fiddle in browser](https://fiddle.drawnui.net) 👈

Supported hosts:

* `DrawnUi.Maui` - Android, iOS, MacCatalyst, and Windows.
* `DrawnUi.Blazor.Wasm` - browser WebAssembly rendering.
* `DrawnUi.Blazor.Server` - server-backed DrawnUI surfaces served by Blazor Server.
* `DrawnUi.Wasm` - pure browser WebAssembly, no Blazor required.
* `DrawnUi.OpenTk` - Windows and Linux desktops.
* `DrawnUi.Net` - platform-agnostic console/server rendering scenarios.

## React?

DrawnUI for React just appeared as a standalone DrawnUI engine in TypeScript, running on [CanvasKit](https://skia.org/docs/user/modules/canvaskit/) (Skia compiled to WebAssembly) in the browser. It tends to use same API as the .NET version. 
Under active development, more info [on our site](https://drawnui.net/articles/react).

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

## What's New 1.10.6.9

  * Makes drag-to-reorder work on a templated SkiaLayout inside a SkiaScroll (handle `ObservableCollection.Move`).
  * Layout system: content was arranged into up to 1px less room than it was measured for when an inset landed on a fraction of a pixel (2pt padding at scale 1.25 = 2.5px per side): measuring reserved it rounded, drawing subtracted the raw value. Buttons clipped the last glyph of some captions, at some positions only. Drawing now reserves the smaller of the two, so content can only gain room, never lose it, and whole-pixel insets are untouched.
  * Fix `SkiaLabel` to no longer emits a phantom empty line on truncation (issue #338)
  * `SkiaLabel`: the glyph width cache was keyed on typeface and text only, so a width measured at one font size was served at another. It now also keys on font size, skew, scale and character spacing.
  * `SkiaPicker`: the selection sheet is presented on the window that owns the picker instead of the app's first window, so it no longer opens behind you when a second window is open.
  * Updated docs and skills at [https://drawnui.net](https://drawnui.net)

 ### Previously

  * Blazor: auto-sized `Canvas` (no `HeightRequest`) measured its content with unit constraints where pixels were expected, so labels wrapped an extra line and the host was reported too tall; the first change that dirtied the content re-measured, the canvas element resized and its drawing buffer blanked for a frame (the "blink on first tap"). Canvas size floats are no longer splatted onto the html canvas as culture-formatted `width`/`height` attributes, and a canvas resize now requests a repaint under `UpdateModeType.Dynamic`.
  * Blazor: can handle browser context menu instead of supression:  [Mouse buttons and the browser context menu](docs/articles/gestures.md#mouse-buttons-and-the-browser-context-menu).
  * Use latest `AppoMobi.Gestures` nuget version for Blazor improvements
  * SkiaLabel: built-in drop shadow size now cached properly
  * SkiaShell blur backdrop and modal animation fixed
  * HotFix for autosized templated layout not growing/shrinking when NOT inside a scroll
  * `SkiaEditor` implemented `IsSpellCheckEnabled` property
  * Fix `SkiaSvg` source to accept Unicode strings.
  * `SkiaEditor` implemented `IsSpellCheckEnabled` property
  * Layout engine consistency sweep: a control's internal layout is now always computed for the box it is actually arranged in. `Arrange` re-measures on a Fill axis when the final box differs from the measured-for constraint (MAUI `ArrangeOverride(finalSize)` parity), so centered rows inside grids/stacks, wrapping text and nested layouts no longer render for a stale width.
  * Grid: children are measured once more at their final cell after spans, minimums, star decompression and the last-track stretch; a Fill child in an `Auto` track can no longer inflate the track past the grid (wrapping labels, scrolls); no infinite stretch when the grid sits inside a scroll.
  * Column/Row: second measure pass keeps the main-axis constraint; `Split>1` on non-templated columns fixed (all columns drew at x=0); templated `Split` slot advance fixed for Center/End cells; templated draw rect no longer double-aligns Center/End cells or draws Fill-Y cells `float.MaxValue` tall inside a scroll; main-axis `Center` clamps to the child's own size; a Fill child on an unbounded axis is auto-sized instead of blowing the stack; auto-sized stacks holding only cross-axis Fill children no longer collapse to 0; templated main-axis Fill cells are auto-sized (MAUI StackLayout semantics).
  * Wrap: a Fill-X child shares its row again (flex-fill), Center/End children stay in flow.
  * Fill layouts measured on an unbounded axis report their content size instead of infinity; `MaximumWidthRequest`/`MaximumHeightRequest` are honored at arrange as well as measure.
  * Styled controls (`SkiaSlider`, `SkiaProgress`, `SkiaSwitch`, `SkiaCheckbox`, `SkiaRadioButton`, `SkiaButton`, `RefreshIndicator`) no longer override user-set properties when their look is built lazily: `HorizontalOptions`, `UseCache`, colors, `SliderHeight`, `OnGestures`... set by you always win (`SetStyleDefault` for control authors).
  * Windows: accelerated `Canvas` under an animated XAML scale transform (popup zoom-in, `ScaleTo`) rendered a 2x zoomed crop for the animated frames. DrawnUi now owns the swap-chain panel (`DrawnSwapChainPanel`, forked from SkiaSharp's `AngleSwapChainPanel`): the GL surface is sized by the real DPI and only recreated on a real DPI change, never on transient composition-scale changes.
  * Fixed Windows builds failing with `MSB3030: Could not copy ... DrawnUi.Maui\Platforms\Windows\Natives\libEGL.dll` on 1.10.5.15: Windows ANGLE natives are now delivered as build items instead of a raw `Copy` into the output directory, so they reach the MSIX payload as well as unpackaged output, for both a project reference and a nuget reference.
 
* 
---
MIT | Free to use and customize

---

DrawnUI is built and maintained by Nick Kovalsky, who is available for commercial work: full mobile and desktop app development, custom controls, performance and rendering work, Xamarin → MAUI migrations, and support contracts. Get in touch via [LinkedIn](https://www.linkedin.com/in/nick-kovalsky-92a770174/) or taublast(at)gmail.com.

