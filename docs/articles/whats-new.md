# What's New in DrawnUi.Maui

This page highlights the latest updates, features, and improvements in DrawnUi.Maui.

## Latest Major Updates

### Package Renaming Strategy
* **New Package**: `DrawnUi.Maui` replaces the old package ID `AppoMobi.Maui.DrawnUi`
* **Backward Compatibility**: Old package kept for compatibility for some time
* **Migration**: Simply update your package reference to use the new package name

### Documentation Improvements
* **New Documentation Site**: First appearance of comprehensive docs in `/docs` folder
* **AI Training Data**: Use `/aidocs` subfolder for training language models
* **Enhanced Examples**: Updated example apps to align with latest changes

### New Controls and Features

#### SkiaCamera
* **Platform Support**: iOS, MacCatalyst, Windows, Android implementations
* **Package**: Available in `DrawnUi.Maui.Camera`
* **Integration**: Seamlessly integrates with DrawnUI canvas rendering

#### Enhanced SkiaShape
* **Multiple Children**: Now can contain many `Children` instead of one `Content`
* **Layout Types**: Can change layout type with `Layout` property
* **Pixel-Perfect Rendering**: Fixes for stroke and other rendering improvements

### Performance Optimizations

#### Layout System Improvements
* **Fill Behavior**: Stack and absolute layouts now correctly apply one-directional `Fill` of children
* **Margins & Padding**: Now work properly everywhere (might break some legacy UIs)
* **Measurement Override**: Can override virtual `OnMeasuring`, while `Measure` is not virtual anymore
* **Faster Initialization**: Assures faster screen creation and avoids re-measurements during first-time initialization

#### Rendering Performance
* **SkiaLabel Optimizations**: Important performance optimizations and fixes
* **Accelerated Handlers**: Performance and safety optimizations for `SkiaViewAccelerated:SKGLView` on all platforms
* **Windows Sync**: Windows accelerated handler now synced with display when refresh rate is >=120Hz
* **ImageComposite**: Cache now works inside another `ImageComposite`

### Gaming Enhancements
* **Frame Interpolator**: Adjustments for DrawnUi.Maui.Game
* **Display Sync**: Better synchronization with high refresh rate displays
* **Performance**: Optimizations for smooth gaming experiences

### Developer Experience

#### Fluent C# Extensions
* **Code-Behind Support**: Extensive fluent extensions for constructing UI without XAML
* **Binding Mimics**: Ability to mimic one-way and two-way bindings without MAUI bindings
* **Examples**: More examples and documentation coming

#### Control Styling
* **Default Looks**: Selectable default look for controls: SkiaButton, SkiaSwitch, SkiaCheckbox
* **Platform Styles**: Example: `<draw:SkiaButton ControlStyle="Cupertino" Text="Button" />`
* **Consistency**: Check out Sandbox project for styling examples

#### Gesture Improvements
* **Layout Events**: SkiaLayout received gesture events (`ChildTapped`, `Tapped`) for easier use without subclassing
* **Shape Events**: SkiaShape now has gesture events too
* **False-Tap Prevention**: Gestures tuned to avoid false-taps when swiping

### Breaking Changes and Migration

#### ViewsAdapter Renaming
* `GetViewForIndex` → `GetExistingViewAtIndex`
* `GetViewAtIndex` → `GetOrCreateViewForIndex`

#### Rendering Changes
* **RenderingMode**: `Canvas` property `RenderingMode` replaced `HardwareAcceleration`
* **Custom Handlers**: Retained custom handlers for all platforms

#### Layout Behavior
* **Fill Behavior**: Stack and absolute layouts now correctly apply one-directional `Fill`
* **Margins/Padding**: Proper implementation might affect legacy UIs

## Version History

### Version 1.3.x (Current - .NET 9)
* Full .NET 9 support with SkiaSharp v3
* All latest features and optimizations
* Recommended for new projects

### Version 1.2.x (Legacy - .NET 8)
* .NET 8 support with SkiaSharp v2
* No longer updated
* Use only for existing projects that cannot migrate

## Upcoming Features

### Planned Improvements
* **Accessibility Support**: Compatible and on the roadmap
* **Masonry Layout**: Todo item for SkiaLayout
* **Additional Platform Support**: Expanding beyond current platforms

### Community Contributions
* **Effects**: More visual effects and animations
* **Controls**: Community-contributed custom controls
* **Examples**: More real-world usage examples

## Migration Guide

### From Legacy Versions (1.2.x to 1.3.x)
1. **Update .NET Version**: Migrate project to .NET 9
2. **Update Package**: Change to `DrawnUi.Maui` package
3. **Update Code**: Address any breaking changes listed above
4. **Test Layouts**: Verify margin/padding behavior in your layouts
5. **Update Handlers**: Use new `RenderingMode` instead of `HardwareAcceleration`

### From Other UI Frameworks
* **XAML Compatibility**: Most XAML patterns work with minimal changes
* **Control Mapping**: See [Controls documentation](controls/index.md) for equivalent controls
* **Performance**: Review [caching strategies](development-notes.md#performance-tips) for optimal performance

## Getting Help with Updates

* **Migration Issues**: Check [FAQ](faq.md) for common migration problems
* **Breaking Changes**: See [Development Notes](development-notes.md) for detailed technical information
* **Community Support**: Ask questions in [GitHub Discussions](https://github.com/taublast/DrawnUi/discussions)
* **Bug Reports**: Report issues on [GitHub Issues](https://github.com/taublast/DrawnUi.Maui/issues)

---

**Stay Updated**: Watch the [GitHub repository](https://github.com/taublast/DrawnUi.Maui) for the latest releases and updates!
