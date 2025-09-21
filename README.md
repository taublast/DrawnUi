 


   
## 🌱 What Was New Previously

* SkiaCamera RenderCapturedPhotoAsync always runs on rendering thread and can use GPU
* SKSL helper uses callback to pass compilation errors
* SkiaShaderEffect new event handler OnCompilationError, passing iTime as all times, passing iMouse from new props: MouseInitial, MouseCurrent
* Added SkiaCamera property IsMirrored to easily flip preview horizontally
* Added SkiaImage property DisplayRect to read scaled source area inside DrawingRect
* Fix header position for Horizontal orientation of SkiaScroll
* Some fluent extensions fixes
# DrawnUI for .NET MAUI
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/AppoMobi.Maui.DrawnUi.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)

[Docs are here!](https://taublast.github.io/DrawnUi) 👈

Replace native controls with a Skia Canvas! 🤩 On **iOS**, **MacCatalyst**, **Android**, **Windows** with hardware acceleration.

Rendering engine with a layout system, gestures and animations, powered by [SkiaSharp](https://github.com/mono/SkiaSharp).   

* To use inside a usual MAUI app, by wrapping drawn controls into `Canvas` views.
* To create a totally drawn apps with just one `Canvas` as root view.
* Drawn controls are totally virtual, no native views/handlers.
* Design in XAML or [code-behind](https://drawnui.net/articles/first-app-code.html)
* Free to use under the MIT license, a nuget package is available.

___Please star ⭐ if you like it!___

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

## Shipped With

* __Base drawn controls__
	* __SkiaControl__ Your lego brick to create anything
	* __SkiaShape__ Path, Rectangle, Circle, Ellipse, Gauge etc, can wrap other elements to be clipped inside
	* __SkiaLabel__, multiline with many options like dropshadow, gradients etc
	* __SkiaImage__ with many options and filters
	* __SkiaSvg__ with many options
	* __SkiaLayout__ (Absolute, Grid, Vertical stack, Horizontal stack, _todo Masonry_) with templates support
	* __SkiaScroll__ (Horizontal, Vertical, Both) with header, footer, zoom support and adjustable inertia, bounce, snap and much more. Can act like a collectionview with custom refresh indicator, load more etc
	* __SkiaHotspot__ to handle gestures in a lazy way
	* __SkiaBackdrop__ to apply effects to background below, like blur etc
	* __SkiaMauiElement__ to embed maui controls in your canvas

* __Custom controls derived from base ones__
	* __SkiaRichLabel__, will find an installed font for any unicode text and auto-create spans for markdown
    * __SkiaButton__ include anything inside, text, images etc
	* __SkiaRadioButton__ select something unique from options
    * __SkiaSwitch__ to be able to toggle anything
    * __SkiaProgress__ to show that your are actually doing something
	* __SkiaSlider__ incuding range selection capability
	* __SkiaLottie__ with tint customization
	* __SkiaGif__ a dedicated lightweight GIF-player with playback properties
	* __SkiaMediaImage__ a subclassed `SkiaImage` for displaying any kind of images (image/animated gif/more..)
    * __SkiaCamera__ that day we draw it on the canvas has finally come
	* __SkiaScrollLooped__ a subclassed `SkiaScroll` for neverending scrolls
	* __SkiaDecoratedGrid__ to draw shapes between rows and columns
	* __RefreshIndicator__ can use Lottie and anything as ActivityIndicator or for your scroll RefreshView
    * __SkiaDrawer__ to swipe in and out your controls
	* __SkiaCarousel__ swipe and slide controls inside a carousel
	* __SkiaWheelPicker__ your iOS-look picker wheel
	* __SkiaSpinner__ to test your luck
	* __SkiaHoverMask__ to overlay a clipping shape
	* __SkiaShell__ for navigation inside a drawn app
	* __SkiaViewSwitcher__ switch your views, pop, push and slide	
	* __SkiaTabsSelector__ create top and bottom tabs
	* __SkiaLabelFps__ for developement
    * __Other__ we hidden deep but still public
	* __Create your own!__      

* Animated Effects
	* __Ripple__
	* __Shimmer__
	* __BlinkColors__
	* __Commit yours!__

* Transforms
	* TranslationX
	* TranslationY
	* TranslationZ (none-affine)
	* ScaleX
	* ScaleY
	* Rotation
	* RotationX (none-affine)
	* RotationY (none-affine)
	* RotationZ (none-affine)
	* SkewX
	* SkewY
	* Perspective1
	* Perspective2

* Full keyboard support

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
 
## Previously

* SkiaCamera RenderCapturedPhotoAsync always runs on rendering thread and can use GPU
* SKSL helper uses callback to pass compilation errors
* SkiaShaderEffect new event handler OnCompilationError, passing iTime as all times, passing iMouse from new props: MouseInitial, MouseCurrent
* Added SkiaCamera property IsMirrored to easily flip preview horizontally
* Added SkiaImage property DisplayRect to read scaled source area inside DrawingRect
* Fix header position for Horizontal orientation of SkiaScroll
* Some fluent extensions fixes

* `SkiaMapsUi` event `LoadingChanged` and prop `IsLoading` to tracks tiles loading/complete. New prop `IsAnimated` to make zooming instant if `false`.
* `InvertPan` fixes, new prop `InvertPan` to control pan direction solving [186](https://github.com/taublast/DrawnUi/issues/186).
* `SkiaImage` new prop `RescaleSource`, with default will not rescale source when viewport size changes (ex: zooming) making rendering faster.
* `SkiaLayout` fix for ImageComposite cache expanding dirty regions.
* `SkiaImageManager` Android added retry logic for Glide loader 

* FIX DrawPlaceholder call when cache was already rendered
* FIX ImageDoubleBuffered cache not rebuilding when control wasn't updated while cache was destroyed
* FIX DrawnUiBasePage not respecting MAUI SafeInsets on iOS

* `SkiaCamera` flash/torch enhancements
* Recycled cells scrolls fixes

* SkiaCheckBox/SkiaRadioButton to consume TAP gesture only
* OnGlobalUpdate will not invalidate scale/children
* Fix tutorials compilation, Nuke caching disabled by hotfix until proper bindings are created
* 
* iOS images caching with `ImageCaching.Nuke` for urls
* Fix Android and Apple SKGLView not updating after view returned back to visible tree after being out of the tree. This is that case in MAUI when you navigate away from a page and when you return the canvas is either blank or frozen.
 
* Animation extensions for label and all controls: `TextColorToAsync` `BackgroundColorToAsync`
* Fixed `SkiaCheckbox` not triggering event
* Fluent `OnTapped` uses callback instead of effect, has second overload with gesture info
* Fixed `SkiaLottie` and other `AnimateFramesRenderer` not updating when Started
* Fixed rare crash in layout when ItemTemplate is null
* 
* `SkiaCacheType.ImageComposite` implementation for `Absolute` layout
* Cache recycling improvements
* Added translation transforms for `SkiaCacheType.ImageComposite` dirty regions usage.
* Fix `SkiaRichLabel` spans invaldation when font changes for existing label

* Recycling cache/SkiaImage surfaces to reduce allocations
* General layout calculation performance boost
* Recycled cells stack MeasureVisible performance boost
* FirstVisibleIndex/LastVisibleIndex for stacks 

* The nuget package `DrawnUi.Maui` replaces the old package Id, you can still use the old one kept for compatibility for some time.
* Docs, first appearence inside `/docs`! To teach LMs use `/aidocs` subfolder.
* Windows x64 doesn't require MSIX mode anymore, thanks to [Tommi Gustafsson](https://github.com/TommiGustafsson-HMP)
* `SkiaCamera` inside DrawnUi.Maui.Camera: iOS, MacCatalyst, Windows, Android implementations.
* `SkiaShape` now can contain many `Children` instead of one `Content`. Plus fixes for stroke and other for pixel-perfect rendering.
* `SkiaImage` rescaling quality fix after for skia v3.
* `SkiaScroll` added additional mouse wheel support for scrolling on windows, added FirstVisibleIndex, LastVisibleIndex props
* Stack and absolute layouts now correctly apply one-directional `Fill` of children, might break some legacy UIs (or might not). `Margins` and `Padding` now work properly everywhere.
* Can override virtual `OnMeasuring`, while `Measure` is not virtual anymore to assure faster screen creation and avoid re-measurements when initializing for the first time.
* Performance and safety optimizations for accelerated rendering handlers (`SkiaViewAccelerated:SKGLView`) on all platforms.
* Windows accelerated handler is now synched with display when its refresh rate is >=120.
* Frame time interpolator adjustments for DrawnUi.Maui.Game.
* Many other silent fixes and new properties/features.
* Example apps updated to align with changes.


