# Frequently Asked Questions

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

## Thechnical Questions

**Q: How do I create custom controls with DrawnUI?**  
A: Subclass `SkiaControl` for custom, `SkiaLayout` for container etc, . Override the `Paint` method to draw with SkiaSharp on the canvas provided inside drawing context.

**Q: Can I embed native MAUI controls inside DrawnUI?**  
A: Yes! Use `SkiaMauiElement` to embed native MAUI controls like WebView inside your DrawnUI canvas. This allows you to combine the best of both worlds.

**Q: Can I use MAUI's default `Resources/Images` folder?**  
A: Sorry, no, drawn resources lives inside `Resources/Raw` and subfolders.

**Q: How do I change SkiaSvg source not from file/url?**  
A: set `SvgString` property to svg text string.

**Q: How do I change SkiaImage source not from file/url?**  
A: set directly: `mySkiaImage.SetImageInternal(skiaImage)`.

**Q: How do I prevent touch events from passing through overlapping controls?**  
A: Use the `BlockGesturesBelow="True"` property on the top control. Note that `InputTransparent` makes the control itself avoid gestures, not controls below.

**Q: How do I internally rebuild the ItemsSource?**  
A: Directly call `layout.ApplyItemsSource()`.

## Troubleshooting
 
**Q: Why is scrolling/rendering slow?**  

**Solutions:**
1. Always use cache for layers of controls:
   * Do NOT cache scrolls/heavily animated controls and above
   * `UseCache = SkiaCacheType.Operations` for labels and svg
   * `UseCache = SkiaCacheType.Image` for complex layouts, buttons etc
   * `UseCache = SkiaCacheType.ImageComposite` for complex layouts where a region changes while others remain static, like a stack with different user-handled controls.
   * `UseCache = SkiaCacheType.ImageDoubleBuffered` for equally sized recycled cells. Will show old cache while preparing new one in background.
   * `UseCache = SkiaCacheType.GPU` for small static overlays like headers, navbars.
2. Check that you do not have some logging running for every rendering frame.

**Q: Why isn't my UI updating when ViewModel properties change:**  

**Solutions:**
1. Ensure ViewModel implements `INotifyPropertyChanged`
2. Check that property either has a static bindable property or calls `OnPropertyChanged()` in the setter.
3. Check that property names match exactly; use `nameof()`.
4. Ensure all your overrides, if any, of `void OnPropertyChanged([CallerMemberName] string propertyName = null)` have a `[CallerMemberName]` attribute.

---


**Can't find the answer to your question?** → 
* Please check out [samples source code](samples.md), covers many scenarios.
* [Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!


