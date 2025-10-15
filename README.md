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

* `IsLooped` property implemented for `SkiaCarousel`, for never-ending scrolls, also added `SwipeSpeed` property.
* `Gestures` property for `Canvas` new value `SoftLock` for smart working together inside native ScrollView. Note that using `Lock` value instead will totally prevent parent ScrollView to receive panning gestures.
* `SkiaLabel` subpixel rendering quality improved, note it can be can turned off with `Super.FontSubPixelRendering` static property.
* `SkiaImage` fix to avoid changing source when was created from same string with converter.
* Fix animators sometimes not starting when created to early, including `SkiaLottie` one.
* Scroll refresh indicator fixed, improvements and fixes for `SkiaCamera` and `SkiaMapsUi`, `SkiaSprite` and much more..
  
---

[Docs and Samples](https://drawnui.net) 👈

___Please star ⭐ if you like it!___
