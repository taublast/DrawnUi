# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DrawnUI for .NET MAUI is a cross-platform rendering engine that draws UI using SkiaSharp instead of native controls. It supports iOS, MacCatalyst, Android, and Windows platforms.

**Key Technology Stack:**
- .NET 9 (minimum requirement)
- MAUI Controls 9.0.70+
- SkiaSharp v3 
- Hardware-accelerated rendering via Skia canvas

## Project Structure

**Core Projects:**
- `src/Maui/DrawnUi/` - Main DrawnUI library (AppoMobi.Maui.DrawnUi package)
- `src/Shared/` - Shared project containing cross-platform code
- `src/Maui/Addons/` - Additional functionality packages (Camera, Game, MapsUi, etc.)
- `src/Maui/Samples/Sandbox/` - Main demo/testing application
- `src/Tests/` - Unit tests and benchmarks

**Key Architectural Components:**
- `SkiaControl` - Base class for all drawn controls
- `Canvas` - MAUI view wrapper that hosts drawn controls
- `SkiaShell` - Navigation system for drawn apps
- Caching system with multiple strategies (Operations, Image, GPU, etc.)
- Gesture handling system for touch interactions
- Layout system supporting Grid, Stack, Absolute positioning
- Effects and animation system

## Build Commands

**Build the main library:**
```bash
dotnet build src/Maui/DrawnUi/DrawnUi.Maui.csproj
```

**Build solution:**
```bash
dotnet build src/DrawnUi.Maui.sln
```

**Run Sandbox demo:**
```bash
dotnet build src/Maui/Samples/Sandbox/Sandbox.csproj --configuration Debug
# Then run from Visual Studio or with platform-specific commands
```

**Create NuGet packages:**
```bash
cd nugets
./makenugets.bat  # Windows
```

**Run tests:**
```bash
dotnet test src/Tests/UnitTests/UnitTests.csproj
```

**Clean build artifacts:**
```powershell
# From src/ directory
./DeleteBinObj.ps1
```

## Development Setup

**Initialize DrawnUI in MauiProgram.cs:**
```csharp
builder.UseDrawnUi(new()
{
    UseDesktopKeyboard = true,
    DesktopWindow = new()
    {
        Width = 500,
        Height = 700
    }
});
```

**Platform Requirements:**
- Minimum OS versions defined in project files
- Resources must be in `Resources/Raw` folder with subfolders allowed, Note that MAUI supports only lowercase filenames of resources and while uppercase might works for you on some plaforms they will not be read on iOS.

## Important Development Notes

**SkiaSharp Version Control:**
- Default branch targets NET 9 with SkiaSharp v3
- NET 8 legacy support is disabled no longer supported, use versions 1.2.x (no longer updated)

**Caching Strategy:**
- `Operations` - For shapes, SVG, text (SKPicture-based)
- `Image` - Simple bitmap cache, works for large sizes
- `ImageDoubleBuffered` - Best for animations, double memory usage
- `GPU` - Hardware-accelerated, for graphics memory caching
- Never cache layers containing SkiaScroll, SkiaDrawer, SkiaCarousel and similar, SkiaMauiElement and derived controls (SkiaMauiEntry etc)

**Layout Differences from Standard MAUI:**
- Default `HorizontalOptions` and `VerticalOptions` are `Start`, not `Fill`
- Grid default spacing is 1, not 8
- Column/Row layouts require explicit Fill options for parent containers

**Code-behind UI Creation, Porting to DRAWN and usage inside XAML - CRITICAL PATTERNS**

* Avoid using Grid (SkiaLayout Type Grid) where possible to replace with SkiaLayer (SkiaLayout Type Absolute) by controlling children position with their Margin property.

*  INLINE Children Creation: Create children inline in the Children collection of parent containers instead of creating a child and then adding it to container. Correct example:

   ```csharp
   Children = new List<SkiaControl>()
   {
       new SkiaLayout()
       {
           HeightRequest = 40,
           Children =
           {
               new SkiaSvg() { ... }.ObserveProperty(...),
               new SkiaLabel() { ... }.Assign(out _label)
           }
       }
       .WithGestures(...)
       .Assign(out _headerGrid)
   };
   ```

* Adding Children Dynamically: **ALWAYS** use `AddSubView()` method instead of `Children.Add()` for SkiaLayout and other drawn containers:
   - ❌ NEVER: `layout.Children.Add(control)`
   - ✅ INSTEAD: `layout.AddSubView(control)`

   This ensures proper parent-child relationships and rendering pipeline setup.

* **Recycled/Reusable Cells Pattern (CRITICAL)**: For recycled cells (like in SkiaCarousel, SkiaScroll with templates), follow these strict rules:

   **❌ NEVER add/remove children dynamically at runtime** - This breaks recycling!

   **✅ ALWAYS pre-create ALL UI elements during cell construction:**

   ```csharp
   public class MyRecycledCell : SkiaDynamicDrawnCell
   {
       private SkiaLayout _pricesContainer;
       private List<SkiaLayout> _priceSlots; // Pre-created slots
       private const int MaxPriceSlots = 5;

       public MyRecycledCell()
       {
           CreateContent();
       }

       private void CreateContent()
       {
           _priceSlots = new List<SkiaLayout>();
           _pricesContainer = new SkiaLayout { Type = LayoutType.Row };

           // Pre-create maximum number of slots needed
           for (int i = 0; i < MaxPriceSlots; i++)
           {
               var slot = CreatePriceSlot();
               _priceSlots.Add(slot);
               _pricesContainer.AddSubView(slot); // Add during construction only!
           }
       }

       protected override void SetContent(object ctx)
       {
           if (ctx is MyData data)
           {
               // Only UPDATE properties, never add/remove children
               for (int i = 0; i < MaxPriceSlots; i++)
               {
                   var slot = _priceSlots[i];
                   if (i < data.Prices.Count)
                   {
                       slot.IsVisible = true;
                       // Update labels, colors, etc.
                       (slot.Children[0] as SkiaLabel).Text = data.Prices[i].Title;
                   }
                   else
                   {
                       slot.IsVisible = false; // Hide unused slots
                   }
               }
           }
       }
   }
   ```

   **Key principles for recycled cells:**
   - Create complete UI structure with maximum capacity in constructor
   - At runtime: only change properties (IsVisible, Text, Color, etc.)
   - Never call `AddSubView()`, `Children.Add()`, `ClearChildren()`, or `RemoveSubView()` after construction
   - Hide unused elements with `IsVisible = false` instead of removing them
   - Store references to pre-created elements for efficient updates

* SkiaControl Base for Content:
   - Content property MUST be `SkiaControl`, NOT `View`.
   
* Do not use MainThread when not explicitely asked too, DrawnUI doesn't need it.
   
* NO MAUI Bindings - Use DrawnUI Fluent Extensions ONLY:
   - ❌ NEVER: `SetBinding(Property, new Binding(...))`
   - ✅ INSTEAD: `.ObserveProperty(source, nameof(Prop), me => { me.Value = Prop; })`
   - ✅ INSTEAD: `.ObserveProperties(source, [nameof(P1), nameof(P2)], me => { ... })`
   - And other approrpiate available in Fluent extensions.

* Fluent Chaining: Chain all methods directly on control creation:

   ```csharp
   new SkiaLabel() { ... }
       .ObserveProperty(...)
       .Assign(out _field)
       .WithGestures(...)
   ```

* Assign Pattern**: Use `.Assign(out _field)` to capture references for later use

* Layout Types remainder: Use `SkiaLayout` with:
   - `Type = LayoutType.Column` - Vertical stack
   - `Type = LayoutType.Row` - Horizontal stack
   - `Type = LayoutType.Grid` - Grid layout
   - `Type = LayoutType.Wrap` - Flex/wrap layout
   - `Type = LayoutType.Absolute` - Absolute positioning

7. **XAML Usage**: DrawnUI controls MUST be wrapped in `<draw:Canvas>`, and have `Gestures` property set to `Enabled` for simple scenarions and for `SoftLock` for controls that use panning. Canvas property `RenderingMode` must be `Default` for simple controls or `Accelarated` for highly animated ones. Simple animations can be rendered still with `Default`.
Try set explicit size OR Fill sides if possible instead of relying on auto-sizing, we don't want the canvas to recalculate when controls inside change something.

   ```xml
   <draw:Canvas HorizontalOptions="Fill" VerticalOptions="Start">
       <draw:SkiaLayout Type="Column">
           <draw:SkiaLabel Text="Hello" />
       </draw:SkiaLayout>
   </draw:Canvas>
   ```

* Rich Text with Spans** (use `&#10;` for newlines):
   ```xml
   <draw:SkiaLabel FontSize="15" TextColor="Black">
       <draw:TextSpan Text="Normal " />
       <draw:TextSpan Text="Bold" IsBold="True" TextColor="Red" />
       <draw:TextSpan Text="&#10;" />
       <draw:TextSpan Text="Link" Tapped="OnTapped" Underline="True" />
   </draw:SkiaLabel>
   ```

* Grid Layout in XAML (use string definitions, not collections):
   ```xml
   <draw:SkiaLayout
       Type="Grid"
       ColumnDefinitions="35,*,100"
       RowDefinitions="Auto,*,50"
       ColumnSpacing="10"
       RowSpacing="5">
       <draw:SkiaSvg Grid.Column="0" Grid.Row="0" Source="icon.svg" />
       <draw:SkiaLabel Grid.Column="1" Grid.Row="0" Text="Content" />
   </draw:SkiaLayout>
   ```
   **Note**: Use `Grid.Column` and `Grid.Row` attached properties, NOT `Column` or `Row`

* Control Mappings:
   - `StackLayout` → `SkiaLayout Type="Column"`
   - `Grid` → `SkiaLayout Type="Grid"` with `ColumnDefinitions="..."` and `RowDefinitions="..."`
   - `FlexLayout` / `SmartFlex` → `SkiaLayout Type="Wrap"`
   - `Label` → `SkiaLabel`
   - `Span` → `TextSpan`
   - `FormattedString` → Direct `TextSpan` children in `SkiaLabel`
   - `{x:Static system:Environment.NewLine}` → `&#10;` (newline character)

For more details read `docs\articles\fluent-extensions.md` file and `docs\articles\porting-maui.md` !!!

**Resource Loading:**
- Web URLs: loaded from web
- `file://` prefix: loaded from native file system  
- Otherwise: loaded from `Resources\Raw` bundle folder

## Testing and Quality

**Test Projects:**
- `src/Tests/UnitTests/` - Unit tests for core functionality
- `src/Tests/SomeBenchmarks/` - Performance benchmarks

**Key Test Areas:**
- Canvas rendering (`CanvasTests.cs`)
- Layout system (`SkiaLayoutTests.cs`) 
- Image management (`SkiaImageManagerTests.cs`)
- Rendering pipeline (`RenderingTests.cs`)

## Addon Projects

**Available Addons:**
- `DrawnUi.Maui.Camera` - Camera integration with SkiaSharp
- `DrawnUi.Maui.Game` - Game development utilities  
- `DrawnUi.Maui.MapsUi` - Map controls
- `DrawnUi.Maui.Rive` - Rive animation support (Windows only)
- `DrawnUi.MauiGraphics` - Microsoft.Maui.Graphics integration

Each addon is a separate NuGet package that can be referenced independently.

## Core strategy
When creating code always consider that we we are inside a rendering engine trying to avoid new allocations during frame processing to avoid GC and looking for max fps.
Apply this concept to new code and if you find existing code that can be adjusted to this line correct it at all times.
Act a a principal engineer: you cannot disable or remove a feature to fix an issue, the fix must never make the system worse somewhere else or potentially break something else. You are looking to build a robust and stable system, and always think how we can modify the code flow in deep to make it better, and if needed add new functionnality to solve issues or challenges.