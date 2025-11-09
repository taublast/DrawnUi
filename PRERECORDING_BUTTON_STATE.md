# Pre-Recording Button State Indicator

## Problem

When pre-recording is active, the recording button doesn't visually indicate the state - it stays purple (the default "Record" color). This is confusing because:
- **Idle**: Purple "🎥 Record" button
- **Pre-Recording**: Purple "🎥 Record" button (no visual change!) ❌
- **File Recording**: Red "🛑 Stop" button ✅

Users couldn't tell if pre-recording was active or not.

## Solution

Added an additional `ObserveProperty` binding for `IsPreRecording` that updates the button appearance when pre-recording starts:

```csharp
.ObserveProperty(CameraControl, nameof(CameraControl.IsRecordingVideo), me => { ... })
.ObserveProperty(CameraControl, nameof(CameraControl.IsPreRecording), me => { ... })
```

## Button States

Now the button shows three distinct states:

| State | Appearance | Emoji | Color |
|-------|-----------|-------|-------|
| **Idle** | 🎥 Record | 🎥 | Purple |
| **Pre-Recording** | ⏺️ Pre-Record | ⏺️ | Orange |
| **File Recording** | 🛑 Stop (00:00) | 🛑 | Red |

## Implementation

Both observers check the same conditions:
1. If `IsRecordingVideo` is true → Red "Stop" button
2. Else if `IsPreRecording` is true → Orange "Pre-Record" button
3. Else → Purple "Record" button

This ensures consistent UI state regardless of which property changed.

## User Experience

Now when you:
1. Click "Record" → button turns orange with ⏺️ "Pre-Record" indicator
2. Pre-recording captures frames to memory buffer
3. Click again to start file recording → button turns red with 🛑 "Stop" indicator
4. Recording to file happens with buffered frames prepended
5. Click to stop → button returns to purple "🎥 Record"

Clear visual feedback for all three states! ✅
