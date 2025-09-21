# Startup Settings

DrawnUI exposes a simple configuration object, `DrawnUiStartupSettings`, that you pass to `UseDrawnUi` during app startup. It controls optional behaviors like logging, desktop window sizing, keyboard handling, and a post-initialization callback.

## Quick start

```csharp
using DrawnUi.Draw;

var builder = MauiApp.CreateBuilder();

builder
    .UseMauiApp<App>()
    .UseDrawnUi(new DrawnUiStartupSettings
    {
        // Desktop window sizing (desktop platforms)
        DesktopWindow = new()
        {
            Width = 375,
            Height = 800,
            IsFixedSize = false
        },

        // Provide an ILogger used by Super.Log
        Logger = LoggerFactory.Create(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        }).CreateLogger("DrawnUi"),

        // Optional features
        UseDesktopKeyboard = true,
        MobileIsFullscreen = false,

        // Optional post-initialization hook
        Startup = services =>
        {
            // Run custom code once DrawnUI is initialized
        }
    });
```

See also: Getting Started → Installation and Setup.

## Properties

- DesktopWindow (WindowParameters?)
  - On desktop platforms, sets the window size and optionally locks resizing.
  - Typical phone-like layout:
    - Width: 375, Height: 800, IsFixedSize: false

- MobileIsFullscreen (bool?)
  - If supported by the platform, avoids safe insets and removes some system UI (e.g., status bar) for a more immersive layout.

- UseDesktopKeyboard (bool)
  - Enables desktop keyboard handling via KeyboardManager (Windows and Mac Catalyst).

- Startup (Action<IServiceProvider>)
  - Called after DrawnUI is initialized and the MAUI App is created, useful for one-time setup that needs DI services.

  - Logger implementing `Microsoft.Extensions.Logging.ILogger` interface

### Logger

The `Super` helper provide following logging methods:
* `public static void Log(Exception e, [CallerMemberName] string caller = null)`
* `public static void Log(string message, LogLevel logLevel = LogLevel.Warning, [CallerMemberName] string caller = null)`
* `public static void Log(LogLevel level, string message, [CallerMemberName] string caller = null)`

Conventions:

- `Super.Log(Exception e)` logs as Error level
- `Super.Log(string message)` defaults to Warning level
- `Super.Log(LogLevel level, string message)` logs at the specified level
    
They can pass data to your own logger which must implement `Microsoft.Extensions.Logging.ILogger` interface.

