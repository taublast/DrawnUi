# Frequently Asked Questions

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

---


**Can't find the answer to your question?** → 
* Please check out [samples source code](samples.md), covers many scenarios.
* [Ask in GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)** - The community is here to help!


