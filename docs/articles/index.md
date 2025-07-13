# DrawnUI for .NET MAUI

**A lightweight library for .NET MAUI built on top of SkiaSharp, to layout and draw your UI on a Skia canvas.**

---

## 🎯 Purpose

* Provides infrastructure to create and render drawn controls with gestures and animations, comes with some pre-built controls.
* Profits from hardware-accelerated rendering on iOS • MacCatalyst • Android • Windows
* Free to use under the MIT license, a nuget package is available.
* To consume inside a usual MAUI app - wrap drawn controls inside `Canvas` views.
* To create a totally drawn apps with just one `Canvas` as root view - `SkiaShell`, `SkiaViewSwitcher` are provided for navigation with modals, popups, toasts etc.

**Drawn controls are virtual: no native views/handlers created, UI-thread is not required to accessed and modify them.**

## ⚡ Features

* __Draws using SkiaSharp with hardware acceleration__
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
* __Navigate__ on canvas with familiar MAUI Shell techniques 
* __Shipped with pre-built controls__ consume, customize and get inspired to create your own!

## 🎮 Live Examples

- **[Engine Demo](https://github.com/taublast/AppoMobi.Maui.DrawnUi.Demo)** - A totally drawn app demo with recycled cells, camera etc
- **[Space Shooter Game](https://github.com/taublast/AppoMobi.Maui.DrawnUi.SpaceShooter)** - Arcade game etude built with DrawnUI
- **[Shaders Carousel](https://github.com/taublast/ShadersCarousel/)** - A totally drawn app with making use of SkiaSharp v3 shaders
- **[Sandbox project](https://github.com/taublast/DrawnUi.Maui/tree/main/src/Maui/Samples/Sandbox)** - Experiment with pre-built drawn controls and more


## 🤔 Onboarding

**Q: What is the difference between DrawnUi and other drawn frameworks?**  
A: Not really comparable since DrawnUI is just a library for **.NET MAUI**, to let you draw UI instead of using native views.

**Q: Why choose drawn over native UI?**  
A: Rather a freedom choice to draw what you want and how you see it.  
It also can bemore performant to draw a complex UI on just one canvas instead of composing it with many native views.

**Q: Do I need to know how to draw on a canvas??**  
A: No, you can start by using prebuilt drawn controls and customize them. All controls are initially designed to be subclassed, customized, and almost every method is virtual. 

**Q: Can I still use XAML?**  
A: Yes you can use both XAML and code-behind to create your UI.  

**Q: Can I avoid using XAML at all costs?**  
A: Yes feel free to use code-behind to create your UI, up to using background thread to access and modify drawn controls properties.

**Q: How do I create custom controls with DrawnUI?**  
A: Inherit from `SkiaControl` for basic controls or `SkiaLayout` for containers etc. Override the `Paint` method to draw with SkiaSharp.

**Q: Can I embed native MAUI controls inside DrawnUI?**  
A: Yes! Use `SkiaMauiElement` to embed native MAUI controls like WebView inside your DrawnUI canvas. This allows you to combine the best of both worlds.

**Q: Possible to create a game with DrawnUI?**  
A: Well, since you draw, why not just draw a game instead of a business app. DrawnUI comes with gaming helpers and custom accelerated platform views to assure a smooth display-synched rendering.

## 📚 Knowledge Base

- **[FAQ](faq.md)** - Frequently asked questions and answers
- **[GitHub Issues](https://github.com/taublast/DrawnUi.Maui/issues)** - Report bugs or ask questions
- **[Background Article](https://taublast.github.io/posts/MauiJuly/)** - Why DrawnUi was created

**Can't find the answer to your question?** → **[Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!

---

**Ready to get started?** → **[Install and Setup Guide](getting-started.md)**