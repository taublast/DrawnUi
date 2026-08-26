# Accessibility

DrawnUI renders controls into a Skia surface instead of creating a native control tree. Accessibility therefore needs a parallel virtual representation that assistive technology can read and activate.

Drawn controls do not have accessibility turned on by default on purpose to let you cotrol which parts will be exposed and how.

## Current Support

| Framework / target | Status | Implementation | Notes |
|---|---|---|---|
| Blazor | Available | Invisible ARIA overlay positioned over the canvas | Accessible today, with one important hover limitation described below |
| OpenTK Windows | Available | UIA virtual fragment providers on the native OpenTK / GLFW window | Narrator and NVDA can read and activate drawn controls |
| .NET MAUI Windows | Available | UIA virtual fragment providers on the WinUI 3 `DesktopChildSiteBridge` | Narrator and NVDA can read and activate drawn controls |
| OpenTK Linux | Incoming | AT-SPI bridge on the native OpenTK window | Planned, not shipped yet |
| .NET MAUI iOS / macCatalyst | Incoming | Virtual `UIAccessibilityElement` container | Planned, not shipped yet |
| .NET MAUI Android | Incoming | Virtual nodes via `ExploreByTouchHelper` | Planned, not shipped yet |

All targets share the same C# accessibility metadata on `SkiaControl`. Platform-specific layers consume the `SkiaAccessibilityManager` snapshot and expose it through the native accessibility API for that platform.

## Shared Model

Accessibility starts in shared code. A drawn control can expose:

- role
- label
- hint
- whether it can interact
- pressed / toggle state

That metadata is collected by `SkiaAccessibilityManager`, which maintains a snapshot of accessible nodes and their bounds in UI coordinates.

Every `SkiaControl` implements `ISkiaAccessibilityNode`, so platform layers work against the interface instead of the concrete class, the same way gestures work against `ISkiaGestureListener`.

The snapshot itself is an array of:

```csharp
public record AccessibilityNode(
    string? Label, string? Hint, string? Role,
    SKRect Rect, bool CanInteract, bool? IsPressed)
```

`Rect` is in device-independent pixels, sorted in top-to-left reading order.

### Registration lifecycle

- `OnLayoutReady()` fires once on the first valid layout and registers the control automatically when `IsAccessibilityElement` is true.
- `NotifyAccessibility()` registers on the first call and marks the snapshot dirty afterwards. Call it manually when you change accessibility props at runtime.
- Detaching a control from the tree or disposing it unregisters it together with all its registered descendants.

The manager rebuilds its snapshot at most once per `MinUpdateIntervalMs` (default 1000 ms) at the end of rendering, so it stays cheap at high frame rates, and raises `Changed` after each rebuild.

### Roles

Use the constants from `DrawnUi.Models.Aria` instead of raw strings: `RoleButton`, `RoleLink`, `RoleCheckbox`, `RoleRadio`, `RoleSwitch`, `RoleSlider`, `RoleTextbox`, `RoleTab`, `RoleMenuitem`, `RoleText`, `RoleHeading`, `RoleImg`, `RoleList`, `RoleProgressbar`, `RoleDialog`, `RoleAlert`, `RoleGroup`, `RoleNavigation` and more.

## Accessibility Props

Accessibility metadata is exposed directly on `SkiaControl`.

```csharp
control.AccessibilityRole = Aria.RoleButton;
control.AccessibilityLabel = "Save";
control.AccessibilityHint = "Saves the document";
control.AccessibilityCanInteract = true;
control.AccessibilityIsPressed = false;
```

- `AccessibilityRole` enables accessibility for the control
- `AccessibilityLabel` is the main spoken label
- `AccessibilityHint` gives extra context for assistive technology
- `AccessibilityCanInteract` marks the node as interactive
- `AccessibilityIsPressed` maps toggle state when applicable

`IsAccessibilityElement` is computed from `AccessibilityRole != null`. Setting the role back to `null` removes the control from the accessibility tree.

Can set them from code-behind or XAML where it is supported.

## Fluent Code-Behind Methods

The same metadata can be attached with fluent helpers.

```csharp
// General
.WithAccessibility(string role, string? label = null, string? hint = null, bool canInteract = false)
.WithAccessibility(string role, string? label = null, bool canInteract = false)

// Common shortcuts
.WithAccessibilityButton(string label, string? hint = null)
.WithAccessibilityButton(string label)
.WithAccessibilityButton()
.WithAccessibilityText(string text)
.WithAccessibilityText()

// Toggle state
.WithAccessibilityPressed(bool? pressed)
.WithAccessibilityToggle(string label, string? hint = null)
```

Example:

```csharp
new GameSwitch()
	.WithAccessibilityToggle(ResStrings.Sounds);
```

`WithAccessibilityToggle` keeps `AccessibilityIsPressed` in sync with toggle state, which is important for screen readers announcing switches and similar controls.

## Implementation in deep

### Blazor 

In Blazor, DrawnUI renders the canvas as usual and also renders an invisible DOM overlay for accessibility.

- The visible canvas surface is marked `aria-hidden`.
- A sibling overlay contains absolutely positioned ARIA elements that mirror the drawn controls.
- Interactive accessibility nodes can receive keyboard focus and activation.

This gives screen readers a DOM-based accessibility surface even though the real UI is drawn.

**IMPORTANT**: on Blazor accessibility overlay and canvas hover on the same control are mutually exclusive. if you add accessibility metadata to a drawn control it will stop receiving `Pointer` gestures and will not be able to react to hover, those will be catched by a corresponding accessibility DOM element. Other gestures will work as usual.

### Windows (UIA)

Both Windows targets, OpenTK and .NET MAUI, expose drawn controls through UI Automation. There is no DOM and no native control tree involved: the host window answers `WM_GETOBJECT` with a UIA fragment root, and every node of the accessibility snapshot becomes a virtual `IRawElementProviderFragment`.

What is wired:

- fragment root returned from a `WndProc` subclass on the host window
- one virtual provider per accessible control, with control type, name and runtime id
- bounding rectangles translated into screen coordinates
- tree navigation: parent, siblings, children
- `StructureChanged` raised when the snapshot is rebuilt
- `SetFocus` routed into `OnAccessibilityActivated()`, so activating a node from a screen reader triggers the drawn control
- `AutomationFocusChanged` raised when keyboard focus moves

Narrator and NVDA can read and activate drawn controls on both heads.

The COM interop types are shared between the two implementations. MAUI Windows hooks the WinUI 3 `DesktopChildSiteBridge` child window, which is the one that receives `WM_GETOBJECT` for content, instead of the top-level window, while OpenTK hooks its own native window.

## Related

- [Handling Gestures](../gestures.md)
- [Platform-Specific Styling](platform-styling.md)
- [Blazor Capabilities](../blazor/capabilities.md)