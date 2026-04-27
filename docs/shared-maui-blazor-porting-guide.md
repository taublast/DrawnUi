# DrawnUi Shared / MAUI / Blazor Code Separation Guide

Live status moved to [shared-maui-blazor-port-status.md](shared-maui-blazor-port-status.md). Update that file whenever port coverage or parity changes.

## Architecture Overview

DrawnUi uses a **three-tier code separation** strategy:

```
src/
  Shared/              ← Framework-agnostic code (SkiaSharp + System.* only)
  Maui/DrawnUi/        ← MAUI-dependent code (BindableProperty, Element, Colors, etc.)
  Blazor/DrawnUi/      ← Blazor-dependent code (Razor components, WebAssembly)
```

### How it works

- **Shared** is a `.shproj` (Shared Project) with a `Shared.projitems` file listing all included `.cs` files
- Both MAUI and Blazor `.csproj` files import Shared via:
  ```xml
  <Import Project="..\..\Shared\Shared.projitems" Label="Shared" />
  ```
- Shared code compiles into whichever project imports it, inheriting that project's package references
- Partial classes bridge Shared ↔ platform: e.g. `SkiaControl.Shared.cs` (Shared) + `SkiaControl.Maui.cs` (MAUI)

### Global Usings

**Shared** (`Super.cs`): `SkiaSharp`, `System.Diagnostics`

**MAUI** (`Super.Maui.cs`): All of Shared plus `Microsoft.Maui`, `Microsoft.Maui.Controls`, `Microsoft.Maui.Graphics`, `SkiaSharp.Views.Maui`, `AppoMobi.Maui.Gestures`, `AppoMobi.Specials`

**Blazor** (`Super.Blazor.cs`): `SkiaSharp`, `AppoMobi.Specials`, `DrawnUi.*` namespaces

### MAUI-Specific Indicators (what keeps code in Maui/)

| Indicator | Package/Namespace |
|---|---|
| `BindableProperty`, `BindableObject`, `Element` | `Microsoft.Maui.Controls` |
| `Color`, `Colors`, `PointF`, `SizeF` | `Microsoft.Maui.Graphics` |
| `ContentPage`, `View`, `Handler` | `Microsoft.Maui.Controls` |
| `MainThread` | `Microsoft.Maui.ApplicationModel` |
| `LayoutOptions`, `LayoutAlignment` | `Microsoft.Maui.Controls` (Blazor re-implements these) |
| `TouchActionResult`, `TouchActionEventArgs` | `AppoMobi.Maui.Gestures` |
| XAML markup extensions, type converters | `Microsoft.Maui.Controls.Xaml` |

---

## Current State (as of 2026-04-06, after full migration)

| Location | Files | Description |
|---|---|---|
| `src/Shared/` | 241 .cs | Framework-agnostic: enums, interfaces, models, animations, animators, effects, cache, text system, gesture types, shaders, views utilities, SkiaControl/TextSpan partials |
| `src/Maui/DrawnUi/` (non-platform) | 155 .cs | Controls, layouts, scroll, shapes, images, Color-dependent effects, XAML infrastructure |
| `src/Maui/DrawnUi/` (platform) | 74 .cs | Android, iOS, Mac, Windows platform-specific code |
| `src/Blazor/DrawnUi/` | ~10 .cs | Early stage: polyfills for MAUI types (LayoutOptions, Point, Size, Thickness), Super.Blazor.cs, DrawnView.cs |

### What's in Shared
- **SkiaControl partials**: `.Shared.cs`, `.Cache.cs`, `.Effects.cs`, `.Invalidation.cs`
- **TextSpan.Shared.cs**: core text span logic (glyph handling, font management, decorations, events)
- **Text types**: UsedGlyph, LineGlyph, StringReference, IDrawnTextSpan, ApplySpan, LineSpan, TextLine
- **SkiaLabel partials**: EmojiData, ObjectPools, GlyphMeasurementCache, SpanMeasurement, Line (TextMetrics/DecomposedText)
- **All enums** (~40): SkiaCacheType, LayoutType, ShapeType, DrawerDirection, GesturesMode, LoadPriority, NavigationSource, etc.
- **All interfaces** (~25): ISkiaControl, ISkiaGestureListener, ISkiaLayout, IHasBanner, IWheelPickerCell, ISkiaRadioButton, etc.
- **All models** (~40): CachedObject, DrawingContext, ScaledRect, ControlInStack, FileDescriptor, WheelCellInfo, SkiaShellNavigatedArgs, SkiaShellNavigatingArgs, etc.
- **All animators**: AnimatorBase, ScrollFlingAnimator, etc.
- **All animation infrastructure**: parameters, interfaces, extensions
- **Effects**: interfaces + bases (SkiaEffect, SkiaShaderEffect, BaseColorFilterEffect, BaseImageFilterEffect, BaseChainedEffect) + non-Color implementations (BlurEffect, SaturationEffect, ContrastEffect, ColorPresetEffect, AdjustRGBEffect, all Chain*Effect variants without Color)
- **Gesture types**: SkiaGesturesParameters, GestureEventProcessingInfo, ZoomEventArgs
- **Shaders**: SkiaShader (runtime effect wrapper)
- **Images**: LoadedImageSource
- **Helpers**: VelocityTracker, IntersectionUtils, RubberBandUtils, 3D helpers, etc.
- **Fluent layout helpers**: SkiaFrame, SkiaGrid, SkiaRow, etc.
- **Layout**: LayoutStructure
- **Scroll**: ScrollToIndexOrder, VelocityAccumulator, ScrollToPointOrder
- **Views utilities**: CanvasRestoreScope, SKAutoCanvasRestoreFixed, DisposableManager
- **Controls**: GifAnimation
- **Pdf**: PaperFormat, PdfPagePosition, Pdf utilities

### What remains in Maui (non-platform, 155 files)
These stay because they use MAUI `Color` type, inherit from MAUI `Element`/`ContentPage`, use XAML infrastructure, or are deeply coupled to the MAUI control hierarchy:
- **Controls** (~60): SkiaButton, SkiaCarousel, SkiaDrawer, SkiaShell, SkiaSlider, SkiaSwitch, etc.
- **Draw/Layout** (~15): SkiaLayout partials, StackLayoutStructure, BuildRowLayout, ContentLayout, etc.
- **Draw/Scroll** (~10): SkiaScroll partials, VirtualScroll, PlanesScroll, Plane
- **Draw/Text** (~5): SkiaLabel.cs (main), SkiaLabel.Maui.cs, TextSpan.Maui.cs, SvgSpan
- **Draw** (~10): SkiaShape partials, SkiaBackdrop, SkiaHotspot, SkiaHotspotZoom
- **Effects** (~7): TintEffect, DropShadowEffect, OuterGlowEffect, TintWithAlphaEffect, ChainTintWithAlphaEffect, ChainDropShadowsEffect, ShaderDoubleTexturesEffect
- **Features** (~15): SkiaImageManager, SkiaFontManager, SKSL, ColorExtensions, AddGestures, ImagesExtensions, KeyboardManager
- **Internals** (~15): XAML converters, markup extensions, StaticResourcesExtensions, ConditionalStyle, SkiaSetter
- **Views** (~10): Canvas, DrawnView, SkiaView, SkiaViewAccelerated, SurfaceCacheManager

### Remaining blockers for further migration
1. **MAUI `Color` type**: Used by ~30 files (effects, shapes, controls). Would require a shared Color abstraction or converting everything to SKColor internally
2. **MAUI `Element` inheritance**: TextSpan.Maui.cs, controls hierarchy. Needed for XAML binding support
3. **MAUI `FileSystem`**: SKSL.cs resource loading
4. **MAUI `FontRegistrar`**: SkiaFontManager font discovery
5. **Layout coupling**: StackLayoutStructure/BuildRowLayout reference SkiaLayout which is a deeply split partial class

---

## Tier 1: Safe to Move NOW (zero MAUI dependencies, verified)

These files have **no MAUI imports, no BindableProperty, no MAUI types**. They use only `System.*`, `SkiaSharp`, or types already in Shared.

### Pure Enums

| File (in `src/Maui/DrawnUi/`) | Namespace | Notes |
|---|---|---|
| `Controls/Drawer/DrawerDirection.cs` | `DrawnUi.Controls` | `FromBottom, FromTop, FromLeft, FromRight` |
| `Controls/Switches/PrebuiltControlStyle.cs` | `DrawnUi.Draw` | `Unset, Platform, Cupertino, Material, Windows` |
| `Features/Gestures/GesturesMode.cs` | `DrawnUi.Draw` | `Disabled, Enabled, SoftLock, Lock` |
| `Features/Images/LoadPriority.cs` | `DrawnUi.Draw` | `Low, Normal, High` |
| `Features/FileSystem/StorageType.cs` | `DrawnUi.Infrastructure` | `Cache, Internal, Public` |
| `Features/Navigation/DeviceRotation.cs` | `DrawnUi.Maui.Navigation` | `Portrait, Landscape` - **namespace needs changing** |

### Pure Interfaces

| File | Notes |
|---|---|
| `Features/Images/IHasBanner.cs` | Pure interface: `Banner`, `BannerPreloadOrdered` |
| `Controls/PickerWheel/IWheelPickerCell.cs` | Depends on `WheelCellInfo` (move together) |

### Pure Data Structures

| File | Type | Notes |
|---|---|---|
| `Draw/Text/UsedGlyph.cs` | struct | Glyph data: Id, Symbol, Position, Width |
| `Draw/Text/LineGlyph.cs` | struct | Depends on UsedGlyph only |
| `Draw/Text/StringReference.cs` | struct | String slicing: Source, StartIndex, Length |
| `Draw/Text/IDrawnTextSpan.cs` | interface | Uses DrawingContext, ScaledSize (both in Shared) |
| `Features/FileSystem/FileDescriptor.cs` | class | Uses only System.IO.FileStream |
| `Features/Navigation/IndexArgs.cs` | class | Pure EventArgs subclass |
| `Features/Navigation/RotationEventArgs.cs` | class | Depends on DeviceRotation (move together) |
| `Controls/PickerWheel/WheelCellInfo.cs` | class | Uses SkiaControl, SKRect, SKMatrix (all available) |
| `Draw/Layout/LayoutStructure.cs` | class | Uses DynamicGrid<ControlInStack> (both in Shared) |
| `Draw/Scroll/ScrollToIndexOrder.cs` | file with 3 types | `ScrollToIndexOrder` struct, `VelocityAccumulator` class, `ScrollToPointOrder` struct - all use SKPoint, Vector2, enums from Shared |

### Summary: Tier 1

**~19 files, ~25 types**. These can be moved by:
1. Copying file to corresponding path under `src/Shared/`
2. Removing from MAUI project (if not auto-excluded)
3. Adding `<Compile Include="..."/>` entry to `Shared.projitems`
4. Fixing namespace if needed (e.g., `DrawnUi.Maui.Navigation` → `DrawnUi.Navigation` or keep as-is)

---

## Tier 2: Movable After Dependency Resolution

These files are pure logic but reference types that are still in MAUI.

### Text Types Blocked by TextSpan

`TextSpan.cs` inherits `Element` (MAUI) and uses `BindableProperty`, `Color`, `Colors`. These files depend on TextSpan:

| File | Dependency |
|---|---|
| `Draw/Text/ApplySpan.cs` | `TextSpan Span` property |
| `Draw/Text/LineSpan.cs` | `TextSpan Span` property |
| `Draw/Text/TextLine.cs` | `List<LineSpan> Spans` → LineSpan → TextSpan |
| `Draw/Text/SvgSpan.cs` | Inherits TextSpan |

**To unblock:** Split TextSpan into:
- `TextSpan.Shared.cs` - core properties (FontFamily, FontSize, IsBold, IsItalic, Paint, TypeFace, glyph logic)  
- `TextSpan.Maui.cs` - BindableProperty definitions, Color properties, Element inheritance

This is a larger refactor since TextSpan currently inherits from `Element` for XAML support.

### Gesture Types Blocked by AppoMobi.Maui.Gestures

| File | Blocked by |
|---|---|
| `Features/Gestures/SkiaGesturesParameters.cs` | `TouchActionResult`, `TouchActionEventArgs` from AppoMobi.Maui.Gestures |
| `Features/Gestures/ZoomEventArgs.cs` | `PointF` from Microsoft.Maui.Graphics |

**Note:** Some Shared files already use `TouchActionResult` (e.g., `SkiaTouchResultContext.cs`, `SkiaControl.Shared.cs`). This works because Shared is a `.shproj` that compiles with the host project's references. For Blazor, these types need polyfills.

### Layout Types (partial class nesting)

| File | Issue |
|---|---|
| `Draw/Layout/SkiaLayout.Grid.Cell.cs` | Nested class in `partial class SkiaLayout` - logic is pure but structurally coupled |
| `Draw/Layout/StackLayoutStructure.cs` | Pure logic but references `SkiaLayout` type directly |
| `Draw/Layout/BuildRowLayout.cs` | References StackLayoutStructure |

**To unblock:** These can move once SkiaLayout has enough of its shared surface in `src/Shared/`.

---

## Tier 3: Splittable (Need Partial Class Extraction)

These files mix framework-agnostic logic with MAUI-specific property definitions.

### Effects (17+ files)

All effect classes in `Features/Effects/` follow the same pattern:
- Inherit from `SkiaEffect` (already in Shared)
- Define `BindableProperty` for configuration
- Core `CreateFilter()` / `Draw()` logic is framework-agnostic

**Example split for `BlurEffect.cs`:**
```
Shared:  BlurEffect.Shared.cs  → CreateFilter(), rendering logic
Maui:    BlurEffect.Maui.cs    → BindableProperty definitions
Blazor:  BlurEffect.Blazor.cs  → Blazor property system equivalent
```

**Files in this category:**
- `AdjustRGBEffect.cs`, `BlurEffect.cs`, `ContrastEffect.cs`, `SaturationEffect.cs`
- `TintEffect.cs`, `TintWithAlphaEffect.cs`, `ColorPresetEffect.cs`
- `DropShadowEffect.cs`, `OuterGlowEffect.cs`
- `BaseColorFilterEffect.cs`, `BaseImageFilterEffect.cs`, `BaseRenderEffect.cs`
- All `Chain*Effect.cs` files
- `ShaderDoubleTexturesEffect.cs`, `SkiaDoubleAttachedTexturesEffect.cs`

### Core Controls

Major controls like `SkiaShape.cs`, `SkiaLabel.cs`, `SkiaLayout.cs` already use partial classes. Their MAUI partials handle `BindableProperty` definitions. More logic could be extracted to Shared partials.

### Image Management

`SkiaImageManager.cs` has platform-specific partials (`.Android.cs`, `.Apple.cs`, `.Windows.cs`). Core image loading/caching logic could be extracted to Shared.

---

## Blazor Port Strategy

### What Blazor Already Has (polyfills)

The Blazor project re-implements these MAUI types:
- `LayoutOptions` / `LayoutAlignment` (in `Internals/Core/`)
- `Point`, `Size`, `Thickness` (in `Internals/Core/`)
- `StackOrientation`, `ViewCell`

### What Blazor Still Needs

For the Shared code to compile in Blazor context, the Blazor project must provide:

1. **Binding system replacement** - `BindableObject`, `BindableProperty`, `Element` equivalents
   - Could use a lightweight observable property system
   - Or define stub classes that mirror MAUI's API surface

2. **Color type** - MAUI's `Color` class with `.ToSKColor()` extension
   - Could use `SKColor` directly in Shared and convert at the MAUI/Blazor boundary

3. **Gesture types** - `TouchActionResult`, `TouchActionEventArgs`
   - Already referenced in Shared files
   - Blazor must provide implementations that translate browser events

4. **Graphics types** - `PointF`, `SizeF`, `RectF`
   - Can be replaced with SkiaSharp equivalents (`SKPoint`, `SKSize`, `SKRect`)
   - Or provide simple struct polyfills

5. **ICommand** - `System.Windows.Input.ICommand` (this is in .NET Standard, available everywhere)

6. **Platform services** - Font loading, image loading, file system access

### Blazor Partial Class Pattern

For each Shared file using MAUI types, Blazor needs a matching partial:

```
src/Shared/Draw/Base/SkiaControl.Shared.cs       ← shared logic
src/Maui/DrawnUi/Draw/Base/SkiaControl.Maui.cs   ← MAUI BindableProperty, Colors
src/Blazor/DrawnUi/Draw/Base/SkiaControl.Blazor.cs ← Blazor property system
```

---

## Working with This Codebase: Task Reference

### Moving a File to Shared

1. Verify zero MAUI deps: search for `BindableProperty`, `Microsoft.Maui`, `Colors.`, `Color `, `Element`, `PointF`
2. Check transitive deps: if file uses type X, verify X is in Shared or has no MAUI deps
3. Copy file to mirror path under `src/Shared/` (keep same subfolder structure)
4. Add `<Compile Include="$(MSBuildThisFileDirectory)path\to\file.cs" />` to `Shared.projitems`
5. Remove original from `src/Maui/DrawnUi/` (or exclude from MAUI .csproj if needed)
6. Build both MAUI and Blazor to verify

### Splitting a File into Shared + Maui Partials

1. Make the class `partial` (if not already)
2. Create `ClassName.Shared.cs` in Shared with framework-agnostic logic
3. Keep `ClassName.Maui.cs` in Maui with BindableProperty definitions
4. Use `partial void` or `partial` methods for platform hooks
5. Add Shared file to `Shared.projitems`
6. For Blazor: create `ClassName.Blazor.cs` implementing the same partial surface

### Creating a Blazor Partial

1. Check what MAUI types the Shared code expects (BindableProperty, Color, etc.)
2. Ensure Blazor polyfills exist for those types
3. Create `.Blazor.cs` file implementing the same partial members
4. Replace MAUI-specific patterns:
   - `BindableProperty.Create(...)` → Blazor-appropriate property notification
   - `Color` → SKColor or custom Color polyfill
   - `MainThread.InvokeOnMainThreadAsync` → `InvokeAsync` (Blazor dispatcher)

### Verifying a Move

```bash
# Build MAUI project
dotnet build src/Maui/DrawnUi/DrawnUi.Maui.csproj --configuration Debug

# Build Blazor project
dotnet build src/Blazor/DrawnUi/DrawnUi.Blazor.csproj --configuration Debug
```

---

## File Reference: Current Shared.projitems Structure

The `Shared.projitems` file explicitly lists every file. Categories:

- `Draw/Base/` - SkiaControl partials, VisualLayer, VisualTreeHandler
- `Features/Effects/` - Effect interfaces and base classes
- `Features/Animations/` - Animation infrastructure
- `Features/Animators/` - All animator implementations
- `Features/Fluent/` - Fluent layout helpers
- `Internals/Enums/` - ~35 enum files
- `Internals/Extensions/` - Shared extension methods
- `Internals/Helpers/` - Utility classes (VelocityTracker, 3D helpers, etc.)
- `Internals/Interfaces/` - ~20 interface files
- `Internals/Models/` - ~35 model/struct files
- `Super.cs` - Shared partial of the Super class

---

## Key Dependency Chains to Watch

```
TextSpan (MAUI: Element + BindableProperty)
  ← ApplySpan, LineSpan
    ← TextLine
      ← SkiaLabel (MAUI)

SkiaControl (partial: Shared + MAUI)
  ← SkiaShape (MAUI)
    ← SkiaLayout (partial: has .Maui.cs)
      ← StackLayoutStructure, BuildRowLayout, etc.

TouchActionResult (AppoMobi.Maui.Gestures)
  ← SkiaGesturesParameters
    ← SkiaControl.Shared.cs (already in Shared!)
    ← Many gesture-handling controls

Color / Colors (Microsoft.Maui.Graphics)
  ← TextSpan, SkiaShape, many controls
  ← Effects (via property definitions)
```

The biggest unlock for Blazor porting is providing a `BindableObject`/`BindableProperty` polyfill and a `Color` type, since these are the most pervasive MAUI dependencies.
