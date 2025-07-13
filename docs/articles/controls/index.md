# Controls Overview

DrawnUi positions itsself as an angine providing a toolset to create and use custom drawn controls. Out-of-the box it provides you with base controls that can be used a lego-bricks to composite custom controls, and proposes some useful pre-made custom controls.

The main spirit is to have all controlls subclassable and customizable at the maximum possible extent.

DrawnUi provides a comprehensive set of UI controls rendered with SkiaSharp for optimal performance. All controls support platform-specific styling and extensive customization options.

## Control Categories

DrawnUi controls can be organized into several categories:

### Aliases
There are controls that are aliases for other controls, te make porting existing native apps easier, to replace one name in code with another:
- SkiaFrame is an alias for SkiaShape of Rectangle type, MAUI Frame
- SkiaStack is for SkiaLayout type Column with default horizontal Fill, MAUI VerticalStackLayout
- SkiaRow is for SkiaLayout type Row, MAUI HorizontalStackLayout
- SkiaLayer is for SkiaLayout type Absolute with default horizontal Fill, MAUI Grid with 1 col/row used for layering
- SkiaGrid is for SkiaLayout type Grid with default horizontal Fill, MAUI Grid
- SkiaWrap is for SkiaLayout type Wrap with default horizontal Fill, a bit similar to MAUI FlexLayout

### Layout Controls
- [SkiaLayout](layouts.md#skialayout): Base layout container. supported types: Absolute, Grid, Column, Row, Wrap. 
- SkiaScroll: Scrolling container with virtualization support
- SnappingLayout: Base class for snap points
- SkiaDrawer: Swipe-in/out panel, subclassed SnappingLayout,
- SkiaCarousel: Swipeable carousel, subclassed SnappingLayout
- [ContentLayout](layouts.md#contentlayout): Optimized for single child, SkiaShape derives from this one
- SkiaShape: Base class for all shapes, can wrap other elements to be clipped inside

### Text Controls
- [SkiaLabel](text.md#skialabel): High-performance text rendering, supports spans
- [SkiaRichLabel](text.md#skiamarkdownlabel): Markdown-capable text, unicode friendly, autofinds font its text (auto-creates spans). Use for text with complex formatting, smileys, and different languages

### Graphics Controls
- [SkiaImage](images.md#skiaimage): Image rendering with many options and filters
- [SkiaSvg](shapes.md#skiasvg): SVG vector graphics rendering with styling and effects
- [SkiaGif](images.md#skiagif): Animated GIF support - dedicated lightweight GIF-player with playback properties
- [SkiaMediaImage](images.md#skiamediaimage): Media image, subclassed SkiaImage for displaying any kind of images (image/animated gif/more..)
- [SkiaLottie](animations.md#skialottie): Lottie animation with tint customization, subclassed SkiaImage

### Button Controls
It's important to notice that every control can behaive like a button with gestures attached, but here is a pre-made button control, providing different platforms looks via `ControlStyle` property:
- [SkiaButton](buttons.md): Standard button with platform-specific styling

### Toggle Controls
- [SkiaSwitch](switches.md#skiaswitch): Platform-styled toggle switch to be able to toggle anything
- [SkiaCheckbox](switches.md#skiacheckbox): Platform-styled checkbox
- [SkiaToggle](switches.md#skiatoggle): Base toggle class for custom toggles
- [SkiaRadioButton](switches.md#skiaradiobutton): Radio button to select something unique from options, subclassed SkiaToggle

### Navigation Controls
- [SkiaShell](shell.md): Navigation framework for navigation inside a drawn app, subclassed SkiaLayout
- [SkiaViewSwitcher](shell.md#skiaviewswitcher): View switcher to switch your views, pop, push and slide, subclassed SkiaLayout

### Input Controls
- [SkiaSlider](input.md#skiaslider): Slider including range selection capability
- [SkiaProgress](input.md#skiaprogress): Progress indicator to show that you are actually doing something
- [SkiaWheelPicker](input.md#skiawheelpicker): iOS-look picker wheel
- [SkiaSpinner](input.md#skiaspinner): Spinner to test your luck

### Specialized Controls
- [SkiaScrollLooped](scroll.md#skiascrolllooped): Subclassed SkiaScroll for neverending scrolls
- [SkiaDecoratedGrid](layouts.md#skiadecoratedgrid): Grid able to draw shapes between rows and columns
- [RefreshIndicator](scroll.md#refreshindicator): Can use Lottie and anything as ActivityIndicator or for your scroll RefreshView
- [SkiaHoverMask](shapes.md#skiahovermask): Overlay a clipping shape
- [SkiaTabsSelector](shell.md#skiatabsselector): Create top and bottom tabs
- [SkiaCamera](native-integration.md#skiacamera): Camera control that draws on the canvas
- [SkiaHotspot](layouts.md#skiahotspot): Handle gestures in a lazy way
- [SkiaBackdrop](layouts.md#skiabackdrop): Apply effects to background below, like blur etc
- [SkiaMauiElement](native-integration.md#skiamauielement): Embed MAUI controls in your canvas

### Development Controls
- [SkiaLabelFps](text.md#skialabelfps): FPS display for development
