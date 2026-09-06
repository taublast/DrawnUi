# DrawnUi.Native.Windows

Native Windows helpers for DrawnUI framework.

Currently consumed only by `DrawnUi.Maui.Camera` (SkiaCamera): Windows video recording with audio. Apps get it transitively through that package, never reference it directly.

## Contents

This package contains native C++ libraries that provide platform-specific functionality for DrawnUI on Windows:

- **AudioEncoderNative.dll** - Audio encoding helpers for Media Foundation
  - PCM to AAC transcoding via IMFSinkWriter
  - Bypasses .NET MAUI COM restrictions for SetInputMediaType
  - Used by DrawnUi.Maui.Camera for real-time audio recording

## Usage

Simply reference this package in your project:

```xml
<PackageReference Include="DrawnUi.Native.Windows" Version="1.0.0" />
```

The native DLL will be automatically copied to your output directory.

## Platform Requirements

- Windows 10 version 19041 or later
- x64 architecture

## License

MIT License - see LICENSE file for details

## Repository

https://github.com/taublast/DrawnUi
