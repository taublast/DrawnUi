# DrawnUI for React

DrawnUI for React is the DrawnUI engine in TypeScript, running on [CanvasKit](https://skia.org/docs/user/modules/canvaskit/) (Skia compiled to WebAssembly) in the browser. React composes the control tree through a custom `react-reconciler` renderer. React never touches the canvas: it creates, updates and removes engine controls, and the engine measures, arranges and paints them.

It is early and under active development. The package is published under the `preview` tag.

- Source: [github.com/DrawnUi/DrawnUi.React](https://github.com/DrawnUi/DrawnUi.React)
- Live demo: [helloreact.drawnui.net](https://helloreact.drawnui.net)
- Agent skill: [drawnui-react/SKILL.md](https://helloreact.drawnui.net/skills/drawnui-react/SKILL.md)

## Install

```bash
npm i drawnui-react@preview react react-dom
```

`drawnui-react` gives you the React tags plus the engine types, `drawnui-react/core` the engine alone. CanvasKit's `.wasm` is referenced with a `?url` import, so a bundler that understands it (Vite) is required.

## Usage

```tsx
await Super.UseDrawnUi()
  .ConfigureFonts((fonts) => fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText"))
  .BuildAsync();

<Canvas BackgroundColor={Colors.DarkSlateBlue} RenderingMode="Accelerated" Gestures="Enabled">
  <SkiaStack Spacing={8} Padding={new Thickness(16)} VerticalOptions="Center">
    <SkiaLabel Text="Hello World" FontSize={32} TextColor={Colors.White} HorizontalOptions="Center" />
    <SkiaButton Text="Tap me" ApplyEffect="Ripple" HorizontalOptions="Center" Tapped={() => setCount((c) => c + 1)} />
  </SkiaStack>
</Canvas>
```

## What it shares with DrawnUi.Net

The goal is the same API surface as the .NET version: same control names, same PascalCase property names, same measure/arrange/paint contract, so documentation transfers.

Ported so far: `SkiaLayout` in Absolute, Column and Row (plus the `SkiaStack` / `SkiaRow` / `SkiaLayer` aliases) with templated recycling cells in Column mode, `SkiaScroll`, `SkiaLabel` and `SkiaRichLabel`, `SkiaShape`, `SkiaImage`, `SkiaSvg`, `SkiaButton`, `SkiaSwitch`, `SkiaCheckbox`, `SkiaRadioButton`, `SkiaSlider`, `SkiaProgress`, `SkiaCarousel`, `SkiaDrawer`, `SkiaEditor`, `SkiaLottie`, `SkiaGif`, `SkiaSprite`, `SkiaBackdrop`, shader effects, gradients, transforms, animators, the tap and pan gesture pipeline, and the accessibility overlay model used by DrawnUi.Blazor.

Caching follows the .NET model: `UseCache` takes the same values. `Operations` records an `SkPicture` and replays it, `Image` snapshots an offscreen surface, `ImageDoubleBuffered` keeps the last cache while a new one is produced, and `ImageComposite` keeps its offscreen surface between records and repaints only the children that changed plus the siblings they overlap.

## Still in progress

Work continues control by control against the .NET sources. On the list today:

- A dedicated GPU cache path. `GPU` currently resolves to `Image` and `ImageCompositeGPU` to `ImageComposite`.
- Long press, hover and multi-touch pinch. They are declared for parity and not produced yet.
- Styles and `ConfigureStyles`.
- Background cache recording. Everything runs on the browser main thread, so recording is synchronous, as in DrawnUi.Blazor.

[SKIPPED.md](https://github.com/DrawnUi/DrawnUi.React/blob/main/SKIPPED.md) in the repository tracks the port area by area: what has landed, with what semantics, and what is still to come.
