# DrawnUI for .NET MAUI
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/AppoMobi.Maui.DrawnUi.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)
[![PRs Welcome](https://img.shields.io/badge/PRs-Welcome-brightgreen.svg?style=flat)](https://github.com/taublast/drawnui/blob/master/CONTRIBUTING.md)

[Docs and Samples](https://drawnui.net) 👈

Rendering engine for .NET MAUI with gestures and animations and much more, powered by [SkiaSharp](https://github.com/mono/SkiaSharp).   

Supports **iOS**, **MacCatalyst**, **Android**, **Windows** with hardware acceleration.

Please note this is a .NET9 library, entry controls are not yet fully compatible with .NET10 due to MAUI changes.

* To use inside a usual MAUI app, by wrapping drawn controls into `Canvas` views.
* To create a totally drawn apps with just one `Canvas` as root view.
* Drawn controls are totally virtual, no native views/handlers.
* Design in XAML or [code-behind](https://drawnui.net/articles/first-app-code.html)
* Free to use under the MIT license, nuget package available.

We are still .NET9, for iOS 26 you might need to set inside PLIST for your iOS app:
```xml
<key>UIDesignRequiresCompatibility</key>
<true/>
```

## Features

* __Use virtual controls to draw your UI__
* __Create your own animated pixel-perfect controls__
* __Port existing native controls to drawn__
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

## 🆕 What's new for 1.7.7.3

* Optimized disposal of resources
* `SkiaScroll` clamp and panning fixes
* `SkiaGrid` remeasured on children fix, other layouts small fixes
* `Super.ThermalStateChanged` event handler on iOS to track overheating
* `SkiaCamera` recording audio, preview performane improvements and more

## 💡 Hint of the Day

**❓ Q: How to make images to Fade-In when loaded?**

**💡 A:** Subclass `SkiaImage` to define your animation:

```csharp
public class BannerImage : SkiaImage
{
    public override void OnSuccess(string source)
    {
        base.OnSuccess(source);

        this.Opacity = 0.01;
        _ = this.FadeToAsync(1, 300, Easing.SinIn);
    }
}
```

---


## Quick Start

Install the package:
```bash
dotnet add package DrawnUi.Maui
```

Initialize in `MauiProgram.cs`:
```csharp
builder.UseDrawnUi();
```

Use in XAML:
```xml
<ContentPage xmlns:draw="http://schemas.appomobi.com/drawnUi/2023/draw">
    <draw:Canvas Gestures="Enabled">
        <draw:SkiaLayout Type="Column" Spacing="16" Padding="32">
            <draw:SkiaLabel Text="Hello DrawnUI" FontSize="24" />
            <draw:SkiaButton Text="Click Me" Tapped="OnButtonClicked" />
        </draw:SkiaLayout>
    </draw:Canvas>
</ContentPage>
```

Need more performance? Set canvas `RenderingMode` to `Accelerated`.

See the [Getting Started Guide](https://drawnui.net/articles/getting-started.html) for details.

Do not miss the [Tutorials Project](https://github.com/taublast/DrawnUi.Maui/tree/main/src/Maui/Samples/Tutorials) on how to create your custom control, a recycled cells scroller and more.

---

## Sample Apps

**Demo Projects:**
- [Tutorials](https://github.com/taublast/DrawnUi/tree/main/src/Maui/Samples/Tutorials) - First steps, bindings, recycled cells
- [Sandbox Project](https://github.com/taublast/DrawnUi.Maui/tree/main/src/Maui/Samples/Sandbox) - Playground for custom controls, shaders, camera, maps etc
- [Engine Demo](https://github.com/taublast/AppoMobi.Maui.DrawnUi.Demo) - Navigation, recycled cells, camera integration
- [Shaders Carousel](https://github.com/taublast/ShadersCarousel/) - Advanced SkiaSharp v3 effects
- [Space Shooter](https://github.com/taublast/Maui.Game.SpaceShooter/) - 2D Arcade Game Etude

**Open-Source Published Apps:**
- [Filters Camera](https://github.com/taublast/ShadersCamera) - Real-time camera filters ([AppStore](https://apps.apple.com/us/app/filters-camera/id6749823005), [Google Play](https://play.google.com/store/apps/details?id=com.appomobi.drawnui.shaderscam))
- [Bricks Breaker](https://github.com/taublast/DrawnUi.Breakout) - 2D Arkanoid/Breakout arcade game ([AppStore](https://apps.apple.com/us/app/bricks-breaker/id6749823869), [Google Play](https://play.google.com/store/apps/details?id=com.appomobi.drawnui.breakout))

---

___Please star ⭐ if you like it!___

## FAQ

**Q: What is the difference between DrawnUi and other drawn frameworks?**  
A: It is not a framework but a library for .NET MAUI, to make creating drawn UI easy for everyone.

**Q: Why use DrawnUI instead of native controls?**  
A: Gives you complete control over rendering and appearance and can be much more performant for complex UIs. 

**Q: Do I need to know SkiaSharp?**  
A: No. Use the prebuilt controls and customize them. All controls are designed to be subclassed and most methods are virtual.

**Q: Can I use XAML/code-behind?**  
A: Yes, both XAML and code-behind are supported.

**Q: Can I embed native MAUI controls?**  
A: Yes, use `SkiaMauiElement` to wrap native controls like WebView inside your drawn UI.

**Q: Can I embed drawn controls into my usual MAUI app?**  
A: Yes, use `Canvas` to wrap drawn controls inside your MAUI UI.

[Full FAQ](https://drawnui.net/articles/faq.html) • [Ask Questions](https://github.com/taublast/DrawnUi/discussions)

---

[Documentation](https://drawnui.net) • [Tutorials](https://drawnui.net/articles/tutorials.html) • [Controls Reference](https://drawnui.net/articles/controls/index.html) Under development

