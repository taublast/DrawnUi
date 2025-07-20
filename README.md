# DrawnUI for .NET MAUI
![License](https://img.shields.io/github/license/taublast/DrawnUi.svg)
![NuGet Version](https://img.shields.io/nuget/v/AppoMobi.Maui.DrawnUi.svg)
![NuGet Downloads](https://img.shields.io/nuget/dt/AppoMobi.Maui.DrawnUi.svg)

📕 [Documentation](https://drawnui.net/) 👈

Replace native controls with a Skia Canvas! 🤩 On **iOS**, **MacCatalyst**, **Android**, **Windows** with hardware acceleration.

Rendering engine with an enhanced layout system, gestures and animations powered by [SkiaSharp](https://github.com/mono/SkiaSharp).   

* To use inside a usual MAUI app, consume drawn controls by wrapping inside `Canvas` views.
* To create a totally drawn app with just one `Canvas` as root view, use `SkiaShell` + `SkiaViewSwitcher` for navigation between screens with modals, popups, toasts etc.
* Drawn controls are totally virtual, no native views/handlers.
* Design in XAML or [code-behind](Fluent.md)
* Free to use under the MIT license, a nuget package is available.

Current development state is _ALPHA for prod_.

>This is a rendering engine for creating and rendering your own controls, pre-shipped are here for a quick start.

## 🦸 Features

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
* __Navigate__ on canvas with familiar MAUI Shell techniques 

## 🌱 What's new
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

___Please star ⭐ if you like it!___
 
## 🎁 Shipped With

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

 ## ‼️ Development Notes

* .NET 9 only, Maui.Controls 9.0.70 minimum.
* All files to be consumed (images etc) must be placed inside the MAUI app Resources/Raw folder, subfolders allowed. If you need to load from the native app folder use prefix "file://".
* Accessibility support is compatible and is on the roadmap.

## 📕 [Documentation](https://taublast.github.io/DrawnUi/)

Click here ☝️☝️☝️

---



 
