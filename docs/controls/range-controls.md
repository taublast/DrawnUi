# Range Controls

DrawnUI provides a family of range-based controls that share common functionality through the `SkiaRangeBase` base class.

## Architecture

```
SkiaRangeBase (base class)
├── SkiaSlider (interactive slider with thumb)
└── SkiaLinearProgress (progress bar)
```

## SkiaRangeBase

The base class provides common functionality for all range controls:

### Properties
- `Value` - Current value within the range
- `Min` - Minimum value (default: 0)
- `Max` - Maximum value (default: 100)
- `Step` - Value stepping increment (default: 0 = no stepping)
- `TrackColor` - Background track color
- `ProgressColor` - Progress/selected portion color
- `TrackHeight` - Height of the track
- `Invert` - Reverse the direction

### Events
- `ValueChanged` - Fired when the value changes

## SkiaSlider

Interactive slider control with draggable thumb. Maintains full backward compatibility.

### Additional Properties
- `End` - Maps to `Value` for backward compatibility
- `Start` - For range sliders (when `EnableRange` is true)
- `EnableRange` - Enable dual-thumb range selection
- `SliderHeight` - Overall slider height
- `ThumbColor` - Color of the draggable thumb
- `TrackSelectedColor` - Maps to `ProgressColor` for backward compatibility

### Additional Events
- `EndChanged` - Fired when End value changes (backward compatibility)
- `StartChanged` - Fired when Start value changes

### Platform Styles
- `Cupertino` - iOS-style slider (2pt track, 28pt thumb, system blue)
- `Material` - Material Design (4dp inactive/6dp active track, 20dp thumb radius)
- `Windows` - Windows Fluent Design (moderate thickness, rounded corners)
- `Default` - Generic style

### Example Usage

```xml
<!-- Basic slider -->
<draw:SkiaSlider 
    Min="0" 
    Max="100" 
    End="50" 
    ControlStyle="Cupertino" />

<!-- Range slider -->
<draw:SkiaSlider 
    Min="0" 
    Max="100" 
    Start="20" 
    End="80" 
    EnableRange="True" />
```

## SkiaLinearProgress

Linear progress bar control for showing progress or completion status.

### Properties
All properties from `SkiaRangeBase` plus:
- Platform-specific styling through `ControlStyle`

### Platform Styles
- `Cupertino` - iOS-style progress bar (4pt height, rounded, system blue #007AFF)
- `Material` - Material Design progress bar (4pt height, slight rounding, Material purple)
- `Windows` - Windows Fluent Design progress bar (6pt height, moderate rounding, Fluent blue #0078D4)
- `Default` - Generic style

### Example Usage

```xml
<!-- Basic progress bar -->
<draw:SkiaLinearProgress 
    Min="0" 
    Max="100" 
    Value="75" 
    ControlStyle="Cupertino" />

<!-- Custom colors -->
<draw:SkiaLinearProgress 
    Min="0" 
    Max="100" 
    Value="50" 
    TrackColor="LightGray" 
    ProgressColor="Green" />
```

## Backward Compatibility

The refactoring maintains 100% backward compatibility:

- All existing `SkiaSlider` properties work exactly as before
- `End` property maps to the new `Value` property
- `TrackSelectedColor` maps to the new `ProgressColor` property
- All events (`EndChanged`, `StartChanged`) continue to work
- Existing XAML and code-behind require no changes

## Migration Guide

### For existing SkiaSlider usage:
No changes required! Your existing code will continue to work.

### For new development:
- Use `SkiaLinearProgress` for progress indicators
- Use `SkiaSlider` for interactive value selection
- Consider using the base `Value` property instead of `End` for new slider implementations
- Use `ProgressColor` instead of `TrackSelectedColor` for consistency

## Implementation Details

### SkiaRangeBase
- Provides value-to-position conversion methods
- Handles property coercion and validation
- Manages track and progress visual elements
- Implements platform styling infrastructure

### ProgressTrail
- Specialized component for progress visualization
- Similar to `SliderTrail` but optimized for progress display
- Handles width calculation based on progress value

### Platform Styling
Each control implements platform-specific `CreateXXXStyleContent()` methods based on official design guidelines:

#### iOS (Cupertino)
- **Slider**: 2pt track height, 28pt thumb diameter, system blue (#007AFF)
- **Progress**: 4pt height, fully rounded corners, system gray background
- **Colors**: iOS system blue, system gray 5 background

#### Material Design
- **Slider**: 4dp inactive track, 6dp active track, 20dp thumb radius
- **Progress**: 4dp height, slight rounding (2dp)
- **Colors**: Material primary purple, surface variant background

#### Windows (Fluent Design)
- **Slider**: Medium thickness, moderate rounding
- **Progress**: 6pt height, moderate rounding (3dp)
- **Colors**: Fluent accent blue (#0078D4), neutral background

#### Implementation Methods
- `CreateCupertinoStyleContent()` - iOS styling
- `CreateMaterialStyleContent()` - Material Design
- `CreateWindowsStyleContent()` - Windows styling
- `CreateDefaultStyleContent()` - Generic styling
