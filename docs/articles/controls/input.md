# Input Controls

DrawnUi.Maui provides various input controls for user interaction, including sliders, progress indicators, and specialized picker controls.

## SkiaSlider

`SkiaSlider` is a versatile slider control that supports both single value selection and range selection capabilities.

### Basic Usage

```xml
<draw:SkiaSlider
    Minimum="0"
    Maximum="100"
    Value="50"
    WidthRequest="300"
    HeightRequest="40"
    TrackColor="LightGray"
    ThumbColor="Blue"
    ValueChanged="OnSliderValueChanged" />
```

### Range Selection

```xml
<draw:SkiaSlider
    Minimum="0"
    Maximum="100"
    Value="25"
    ValueTo="75"
    IsRange="true"
    WidthRequest="300"
    HeightRequest="40"
    TrackColor="LightGray"
    ThumbColor="Blue" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Minimum` | double | Minimum value of the slider |
| `Maximum` | double | Maximum value of the slider |
| `Value` | double | Current value (or start value for range) |
| `ValueTo` | double | End value for range selection |
| `IsRange` | bool | Whether the slider supports range selection |
| `TrackColor` | Color | Color of the slider track |
| `ThumbColor` | Color | Color of the slider thumb |
| `Step` | double | Step increment for value changes |

### Events

- `ValueChanged`: Raised when the slider value changes
  - Event signature: `EventHandler<double>`

## SkiaProgress

`SkiaProgress` is a progress indicator control to show that you are actually doing something, with support for determinate and indeterminate progress.

### Basic Usage

```xml
<draw:SkiaProgress
    Progress="0.5"
    WidthRequest="200"
    HeightRequest="20"
    ProgressColor="Green"
    BackgroundColor="LightGray"
    CornerRadius="10" />
```

### Indeterminate Progress

```xml
<draw:SkiaProgress
    IsIndeterminate="true"
    WidthRequest="200"
    HeightRequest="20"
    ProgressColor="Blue"
    BackgroundColor="LightGray" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Progress` | double | Progress value (0.0 to 1.0) |
| `IsIndeterminate` | bool | Whether to show indeterminate progress |
| `ProgressColor` | Color | Color of the progress bar |
| `BackgroundColor` | Color | Background color of the progress track |
| `CornerRadius` | double | Corner radius for rounded progress bar |

## SkiaWheelPicker

`SkiaWheelPicker` provides an iOS-style picker wheel for selecting items from a list.

### Basic Usage

```xml
<draw:SkiaWheelPicker
    ItemsSource="{Binding Items}"
    SelectedItem="{Binding SelectedItem}"
    WidthRequest="200"
    HeightRequest="150"
    ItemHeight="40"
    VisibleItemsCount="5" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `ItemsSource` | IEnumerable | Collection of items to display |
| `SelectedItem` | object | Currently selected item |
| `SelectedIndex` | int | Index of the selected item |
| `ItemHeight` | double | Height of each item in the picker |
| `VisibleItemsCount` | int | Number of visible items |
| `IsLooped` | bool | Whether the picker loops infinitely |

### Events

- `SelectionChanged`: Raised when the selected item changes

## SkiaSpinner

`SkiaSpinner` is a spinner control to test your luck, providing a rotating wheel with customizable segments.

### Basic Usage

```xml
<draw:SkiaSpinner
    Segments="{Binding SpinnerSegments}"
    WidthRequest="200"
    HeightRequest="200"
    SpinDuration="3000"
    SpinCompleted="OnSpinCompleted" />
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Segments` | IEnumerable | Collection of spinner segments |
| `SelectedSegment` | object | Currently selected segment |
| `SpinDuration` | int | Duration of spin animation in milliseconds |
| `IsSpinning` | bool | Whether the spinner is currently spinning |
| `SpinVelocity` | double | Initial velocity for the spin |

### Methods

- `Spin()`: Start spinning the wheel
- `Stop()`: Stop the spinning animation

### Events

- `SpinCompleted`: Raised when the spin animation completes
  - Event signature: `EventHandler<object>` where object is the selected segment
