# DrawnUI for .NET MAUI

![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/DrawnUi.Maui.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)

[Source Code](https://github.com/taublast/DrawnUi.Maui) 👈

**A rendering engine for .NET MAUI built on top of SkiaSharp**

**Hardware-accelerated rendering engine** for **iOS**, **MacCatalyst**, **Android**, **Windows** with enhanced WPF-like layout system, gestures and animations, powered by [SkiaSharp](https://github.com/mono/SkiaSharp).

---

## 📦 Quick Install

**Install from nuget:**
```bash
dotnet add package DrawnUi.Maui
```

**Initialize in MauiProgram.cs:**
```csharp
builder.UseDrawnUi();
```

👉 More in the [Getting Started Guide](getting-started.md).

---
## Purpose

* **Easy canvas drawing** with high-level helpers 
* **Smart creation of controls** by compositing different components
* **Custom layout system** to create, layout and render your drawn controls with gestures and animations
* **Hardware-accelerated** rendering on iOS • MacCatalyst • Android • Windows
* **Free to use** under the MIT license, stable NuGet package available
* **Hybrid approach**: Use inside existing MAUI apps by wrapping drawn controls in `Canvas` views
* **Fully drawn apps**: Create totally drawn apps with just one Canvas as root view 
* **Virtual controls**: No native views/handlers created, UI-thread not required for access
* **To have the tools to create and use your own controls** to expand the limits of the predefined

## Features

### 🎨 **Rendering & Graphics**
* **Hardware-accelerated** SkiaSharp rendering with max performance
* **Pixel-perfect controls** with complete visual customization
* **2D and 3D transforms** for advanced visual effects
* **Visual effects** for every control: filters, shaders, shadows, blur
* **Caching system** for optimized re-drawing performance

### 😍 **Development Experience**
* **Design in XAML or code-behind** - choose your preferred approach
* **Fluent C# syntax** for programmatic UI creation
* **Hot Reload compatible** for rapid development iteration
* **Virtual controls** - no native views/handlers, background thread accessible

### 🚀 **Performance & Optimization**
* **Optimized rendering** - only visible elements drawn
* **Template recycling** for efficient memory usage
* **Hardware acceleration** on all supported platforms
* **Smooth animations** targeting maximum FPS

### 👆 **Interaction & Input**
* **Advanced gesture support** - panning, scrolling, zooming, custom gestures
* **Keyboard support** - track any key combination
* **Touch and mouse** input handling
* **Multi-platform input** normalization

### 🧭 **Navigation & Layout**
* **Familiar MAUI Shell** navigation techniques on canvas
* **SkiaShell + SkiaViewSwitcher** for fully drawn app navigation
* **Modals, popups, toasts** and custom overlays
* **Enhanced layout system** with advanced positioning

---



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

### Documentation & Guides
- **[Getting Started Guide](getting-started.md)** - Complete installation and setup
- **[Sample Apps](samples.md)** - Tutorials and example projects
- **[FAQ](faq.md)** - Frequently asked questions and answers
- **[Controls Documentation](controls/index.md)** - Complete controls reference
- **[Advanced Features](advanced/index.md)** - Performance and platform topics

### Community & Support
- **[GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - Community help and discussions
- **[GitHub Issues](https://github.com/taublast/DrawnUi.Maui/issues)** - Report bugs or ask questions

### Additional Resources
- **[Fluent Extensions](fluent-extensions.md)** - Code-behind UI creation patterns
- **[What's New](whats-new.md)** - Latest updates and features
- **[How DrawnUI was created](https://taublast.github.io/posts/MauiJuly/)** - article by the creator

**Can't find what you're looking for?** → **[Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!

---

