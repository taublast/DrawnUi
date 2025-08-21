# DrawnUI for .NET MAUI

Rendering engine to draw your UI on a Skia canvas, with gestures and animations, designed to draw pixel-perfect custom controls instead of using native ones, powered by [SkiaSharp](https://github.com/mono/SkiaSharp)😍.

Supports **iOS**, **MacCatalyst**, **Android**, **Windows**.

* To use inside a usual MAUI app, consume drawn controls here and there inside `Canvas` views.
* Create a totally drawn app with just one `Canvas` as root view, `SkiaShell` is provided for navigation.
 * Drawn controls are totally virtual, these are commands for the engine on what and how to draw on a skia canvas. 
* Free to use under the MIT license, a nuget package is available.

## Installation

Install the package __AppoMobi.Maui.DrawnUi__ from NuGet.

After that initialize the library inside your `MauiProgram.cs` file:

```csharp
	builder.UseDrawnUi();
	callAnotherLine();
```

## ___This control is under construction!___