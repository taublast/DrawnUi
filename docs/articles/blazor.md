# Blazor

DrawnUI also has a Blazor WebAssembly version under active development. The Blazor surface already supports the core startup flow, DrawnUI canvas hosting, font registration, and a growing set of validated controls and probes.

This page is the entry point for using DrawnUI in Blazor and for tracking which parts are ready today.

## Current Scope

What is already available in the current Blazor slice:

- `UseDrawnUiAsync()` startup hook
- Shared `DrawnUiStartupSettings`
- `Canvas` hosting for DrawnUI controls in Razor pages
- Static-web-asset font loading through `DrawnExtensions.RegisterFont(...)`
- Shared `KeyboardManager` integration through `UseDesktopKeyboard`
- Blazor sandbox probes for cards, scrolling, lottie, and keyboard input

What is still true today:

- Blazor support is in progress, not full MAUI parity
- validated slices are the safe reference point
- the live status board remains the source of truth for parity progress

See the current implementation status here:

- [Shared / MAUI / Blazor Port Status](../shared-maui-blazor-port-status.md)

## Quick Start

```csharp
using DrawnUi.Draw;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontTextBol", "/fonts/OpenSans-Semibold.ttf");

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
    UseDesktopKeyboard = true
});

await host.RunAsync();
```

## Minimal Page

```razor
@page "/"
@using DrawnUi.Draw
@using DrawnUi.Views
@using Microsoft.Maui.Controls

<Canvas WidthRequest="400"
        HeightRequest="220"
        BackgroundColor="#F4F1E8"
        RootControl="@RootControl" />

@code {
    private readonly SkiaControl RootControl = new SkiaLayout()
    {
        Margin = new Thickness(16),
        Type = LayoutType.Column,
        Children =
        {
            new SkiaLabel()
            {
                Text = "Hello from DrawnUI Blazor",
                FontFamily = "FontText",
                FontSize = 24
            }
        }
    };
}
```

## Startup

Blazor uses `UseDrawnUiAsync()` instead of the MAUI `UseDrawnUi()` extension.

Recommended startup links:

- [Startup Settings](startup-settings.md)
- [Installation and Setup](getting-started.md)

## Fonts

Fonts in Blazor should be registered with URL paths that are reachable as static web assets.

Example:

```csharp
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");
```

The registered fonts are preloaded during `UseDrawnUiAsync()`.

## Keyboard

Blazor now supports the same `KeyboardManager` event surface used by DrawnUI MAUI consumers.

Enable it during startup:

```csharp
var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
    UseDesktopKeyboard = true
});
```

Then subscribe from your page or control:

```csharp
KeyboardManager.KeyDown += OnKeyDown;
KeyboardManager.KeyUp += OnKeyUp;
```

See also:

- [Startup Settings](startup-settings.md)

## Sandbox Reference

The best working reference today is the Blazor sandbox in the repository:

- `src/Blazor/Samples/BlazorSandbox/`

Useful probe pages currently include:

- cards
- keyboard probe
- scroll probe
- lottie probe

## Related Docs

- [Startup Settings](startup-settings.md)
- [Handling Gestures](gestures.md)
- [Fluent Extensions](fluent-extensions.md)
- [Shared / MAUI / Blazor Port Status](../shared-maui-blazor-port-status.md)