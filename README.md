# DrawnUI for .NET MAUI
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/AppoMobi.Maui.DrawnUi.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)

[Docs and Samples](https://drawnui.net) 👈

When you want to draw your app or game a Skia Canvas instead of using native controls this library solves it. 

Supports **iOS**, **MacCatalyst**, **Android**, **Windows** with hardware acceleration.

Rendering engine with a layout system, gestures and animations, powered by [SkiaSharp](https://github.com/mono/SkiaSharp).   

* To use inside a usual MAUI app, by wrapping drawn controls into `Canvas` views.
* To create a totally drawn apps with just one `Canvas` as root view.
* Drawn controls are totally virtual, no native views/handlers.
* Design in XAML or [code-behind](https://drawnui.net/articles/first-app-code.html)
* Free to use under the MIT license, a nuget package is available.

## Features

* __Draw with helpers using SkiaSharp with hardware acceleration__
* __Create your own animated pixel-perfect controls__
* __Port existing native controls to be drawn__
* __Design in XAML or code-behind__
* __2D and 3D Transforms__
* __Visual effects__ for every control, filters and shaders
* __Animations__ targeting max FPS
* __Caching system__ for faster re-drawing
* __Optimized for performance__, rendering only visible elements, recycling templates etc
* __Gestures__ support for anything, panning, scrolling, zooming etc
* __Keyboard support__, track any key
* __Navigate__ on the canvas with MAUI Shell-like techniques 

---

## 🌱 What's new

* Gestures now filter possible palm longpressing at borders to avoid blocking taps
* Add custom `ILogger` support can add with options to record all Super.Log messages
* `SkiaImage` RescalingQuality default is now Low
* `MeasureVisible` strategy of `SkiaLayout` now supports columns via `Split`
* Fix LoadMore mechanics for `MeasureAll` strategy of `SkiaLayout`
* `SkiaScroll` IsRefreshing binding mode is now TwoWay by default
* `SkiaCamera` fix Android flash always on mode
* `SkiaCamera` implemented video recording beta mode
 
---

[Docs and Samples](https://drawnui.net) 👈

___Please star ⭐ if you like it!___
