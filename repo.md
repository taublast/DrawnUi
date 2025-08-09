---
description: Repository Information Overview
alwaysApply: true
---

# DrawnUi.Maui Information

## Summary
DrawnUi.Maui is a cross-platform rendering engine for .NET MAUI that replaces native controls with a Skia Canvas. It provides hardware-accelerated rendering on iOS, MacCatalyst, Android, and Windows platforms using SkiaSharp. The library allows developers to create pixel-perfect UI controls with animations, gestures, and visual effects.

## Structure
- **src/**: Main source code
  - **Maui/**: Core MAUI implementation
    - **DrawnUi/**: Main library project
    - **Addons/**: Additional components (Camera, Game, MapsUi, etc.)
    - **MetaPackage/**: NuGet meta-package
    - **Samples/**: Example applications
  - **Shared/**: Shared code used across projects
  - **Tests/**: Unit tests and benchmarks
- **docs/**: Documentation and API reference
- **nugets/**: NuGet packaging scripts

## Language & Runtime
**Language**: C# (.NET)
**Version**: .NET 9.0
**Build System**: MSBuild
**Package Manager**: NuGet
**Platforms**: iOS 15.0+, MacCatalyst 15.2+, Android 21.0+, Windows 10.0.19041.0+

## Dependencies
**Main Dependencies**:
- SkiaSharp.Views.Maui.Controls (3.119.0)
- SkiaSharp.Skottie (3.119.0)
- Svg.Skia (3.0.3)
- AppoMobi.Maui.Navigation (1.9.3-pre)
- AppoMobi.Maui.Gestures (1.9.7)
- AppoMobi.Specials (9.0.3)
- EasyCaching.InMemory (1.9.2)
- CommonMark.NET (0.15.1)

**Platform-Specific Dependencies**:
- **Windows/MacCatalyst**: Microsoft.Extensions.Http.Polly (9.0.6)
- **Android**: HarfBuzzSharp (8.3.1.1), AppoMobi.Maui.Native (1.0.1.0-pre)

## Build & Installation
```bash
# Build the main library
dotnet build src\Maui\DrawnUi\DrawnUi.Maui.csproj

# Create NuGet packages
dotnet pack src\Maui\DrawnUi\DrawnUi.Maui.csproj
dotnet pack src\Maui\MetaPackage\AppoMobi.Maui.DrawnUi\AppoMobi.Maui.DrawnUi.csproj
dotnet pack src\Maui\Addons\DrawnUi.Maui.Camera\DrawnUi.Maui.Camera.csproj
```

## Testing
**Framework**: xUnit
**Test Location**: src/Tests/UnitTests
**Configuration**: UnitTests.csproj
**Run Command**:
```bash
dotnet test src\Tests\UnitTests\UnitTests.csproj
```

**Benchmarks**:
- Uses BenchmarkDotNet (0.13.12)
- Located in src/Tests/Benchmarks
- Run with: `dotnet run -c Release -p src\Tests\Benchmarks\SomeBenchmarks.csproj`

## Main Components
- **Core Library**: DrawnUi.Maui - The main rendering engine
- **Meta Package**: AppoMobi.Maui.DrawnUi - Convenience package that includes all components
- **Addons**:
  - DrawnUi.Maui.Camera - Camera integration
  - DrawnUi.Maui.Game - Game development support
  - DrawnUi.Maui.MapsUi - Maps integration
  - DrawnUi.MauiGraphics - Graphics utilities

## Key Features
- Hardware-accelerated drawing with SkiaSharp
- Custom control creation with animations and effects
- Gesture support (panning, scrolling, zooming)
- Keyboard input handling
- Navigation system similar to MAUI Shell
- Caching system for performance optimization
- Visual effects and filters for all controls
- 2D and 3D transformations