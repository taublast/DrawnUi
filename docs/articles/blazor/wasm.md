# Blazor WebAssembly

Use `DrawnUi.Blazor.Wasm` when DrawnUI should render locally in the browser and process interaction locally.

## When To Choose It

Choose the WASM/browser runtime when you need:

- local responsiveness
- high-frequency redraw
- animation-heavy surfaces
- gesture-heavy or pointer-heavy interaction
- pages that are mostly or entirely DrawnUI-driven

If the DrawnUI surface should feel like a local canvas, this is the correct runtime.

## Install

```bash
dotnet add package DrawnUi.Blazor.Wasm
```

## Startup

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
DrawnExtensions.RegisterFont("FontTextTitle", "/fonts/OpenSans-Semibold.ttf");

var host = await builder.UseDrawnUiAsync(new DrawnUiStartupSettings
{
    UseDesktopKeyboard = true
});

await host.RunAsync();
```

Blazor uses `UseDrawnUiAsync()` instead of the MAUI `UseDrawnUi()` extension.

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

## Fonts

Fonts should be registered with URL paths that are reachable as static web assets.

```csharp
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");
```

The registered fonts are preloaded during `UseDrawnUiAsync()`.

## Trimming

Blazor WebAssembly apps using DrawnUI may also need to root `SkiaSharp` for trimming:

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="SkiaSharp" RootMode="EntireAssembly" />
</ItemGroup>
```

## Keyboard

Enable browser keyboard support during startup:

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

## Best Reference

The best working browser/WASM reference today is:

- `src/Blazor/Samples/BlazorSandbox/`

Useful probes currently include cards, keyboard, scrolling, and other runtime validation pages.

## Live Sample

Use the hosted sample to demonstrate the current browser-side DrawnUI runtime:

- <a href="https://drawnui.net/sandbox/" target="_blank" rel="noopener noreferrer">Open Blazor WebAssembly sample</a>

## Publishing

Before shipping to production, read [Publishing (AOT & Compression)](publishing.md). The default Release publish runs your code on the interpreter and most servers will not serve the precompressed assets — both will make your app feel broken on real-world hardware.

## Related Docs

- [Blazor](index.md)
- [Blazor FAQ](faq.md)
- [Publishing (AOT & Compression)](publishing.md)
- [Startup Settings](../startup-settings.md)
- [Handling Gestures](../gestures.md)