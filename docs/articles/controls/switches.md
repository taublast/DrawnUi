# Switches and Toggles

DrawnUi provides toggle controls with platform-specific styling, including switches and checkboxes.

## SkiaSwitch

`SkiaSwitch` is a toggle control styled according to platform conventions, similar to an on/off switch.

### Basic Usage

```xml
<draw:SkiaSwitch
    IsToggled="false"
    WidthRequest="50"
    HeightRequest="30"
    ColorFrameOff="Gray"
    ColorFrameOn="Green"
    ColorThumbOff="White"
    ColorThumbOn="White"
    Toggled="OnSwitchToggled" />
```

### Platform-Specific Styling

Set the `ControlStyle` property to apply platform-specific styling:

- `Platform`: Automatically selects the appropriate style for the current platform
- `Cupertino`: iOS-style switch with pill-shaped track
- `Material`: Android Material Design switch
- `Windows`: Windows-style switch

```xml
<draw:SkiaSwitch
    ControlStyle="Cupertino"
    IsToggled="true" />
```

### Code-Behind Example

```csharp
private void OnSwitchToggled(object sender, bool isToggled)
{
    // Handle the toggle state change
    if (isToggled)
    {
        // Switch is ON
        DisplayAlert("Switch", "Turned ON", "OK");
    }
    else
    {
        // Switch is OFF
        DisplayAlert("Switch", "Turned OFF", "OK");
    }
}
```

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsToggled` | bool | Whether the switch is toggled on or off |
| `ColorFrameOn` | Color | The color of the track when toggled on |
| `ColorFrameOff` | Color | The color of the track when toggled off |
| `ColorThumbOn` | Color | The color of the thumb when toggled on |
| `ColorThumbOff` | Color | The color of the thumb when toggled off |
| `ControlStyle` | PrebuiltControlStyle | The platform-specific style |
| `IsAnimated` | bool | Whether state changes are animated |
| `AnimationSpeed` | uint | Animation duration in milliseconds (default: 200) |

### Events

- `Toggled`: Raised when the switch is toggled on or off
  - Event signature: `EventHandler<bool>`
  - The bool parameter indicates the new toggle state (true = on, false = off)

## SkiaCheckbox

`SkiaCheckbox` is a toggle control styled as a checkbox with platform-specific appearance.

### Basic Usage

```xml
<draw:SkiaCheckbox
    IsToggled="false"
    WidthRequest="24"
    HeightRequest="24"
    ColorFrameOff="Gray"
    ColorFrameOn="Blue"
    ColorThumbOff="Transparent"
    ColorThumbOn="White"
    Toggled="OnCheckboxToggled" />
```

### Platform-Specific Styling

Like SkiaSwitch, SkiaCheckbox supports platform-specific styling through the `ControlStyle` property.

### Properties

SkiaCheckbox shares most properties with SkiaSwitch, both inheriting from SkiaToggle.

## SkiaToggle

`SkiaToggle` is the base class for toggle controls. You can use it to create custom toggle controls with similar behavior to switches and checkboxes.

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsToggled` | bool | Whether the control is toggled on or off |
| `DefaultValue` | bool | The default toggle state |
| `ColorFrameOn/Off` | Color | The color of the frame in each state |
| `ColorThumbOn/Off` | Color | The color of the thumb in each state |
| `IsAnimated` | bool | Whether state changes are animated |

### Events

- `Toggled`: Raised when the toggle state changes
  - Event signature: `EventHandler<bool>`
  - The bool parameter indicates the new toggle state

## SkiaRadioButton

`SkiaRadioButton` is a specialized toggle control for selecting one option from a group of mutually exclusive options. It's subclassed from SkiaToggle and provides radio button functionality.

### Basic Usage

```xml
<draw:SkiaLayout Type="Column" Spacing="10">
    <draw:SkiaRadioButton
        GroupName="Options"
        Text="Option 1"
        IsToggled="true"
        WidthRequest="150"
        HeightRequest="30" />
    <draw:SkiaRadioButton
        GroupName="Options"
        Text="Option 2"
        IsToggled="false"
        WidthRequest="150"
        HeightRequest="30" />
    <draw:SkiaRadioButton
        GroupName="Options"
        Text="Option 3"
        IsToggled="false"
        WidthRequest="150"
        HeightRequest="30" />
</draw:SkiaLayout>
```

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `GroupName` | string | Name of the radio button group for mutual exclusion |
| `Text` | string | Text label for the radio button |
| `IsToggled` | bool | Whether this radio button is selected |

### Behavior

- Only one radio button in a group (same `GroupName`) can be selected at a time
- Selecting one radio button automatically deselects others in the same group
- Inherits all properties and events from SkiaToggle