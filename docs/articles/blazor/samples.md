# Blazor Samples

These samples show the three current DrawnUI hosting patterns for Blazor.

Use them as the runnable reference set when you want to compare browser-local rendering, server-rendered DrawnUI, and mixed-runtime hosting in one place.

## BlazorSandbox

Path: `src/Blazor/Samples/BlazorSandbox/`

`BlazorSandbox` is the main browser-side sample for `DrawnUi.Blazor.Wasm`.

- Runs DrawnUI in the browser
- Uses the `Canvas` component for local rendering and interaction
- Best fit for animation-heavy, gesture-heavy, and canvas-first UI
- Useful when you want DrawnUI to feel local and responsive without server round-trips

This is the best sample to start with if you want the strongest current Blazor runtime for general DrawnUI surfaces.

Hosted sample:

- <a href="https://drawnui.net/sandbox/" target="_blank" rel="noopener noreferrer">Open Blazor WebAssembly sample</a>

## BlazorSandboxServer

Path: `src/Blazor/Samples/BlazorSandboxServer/`

`BlazorSandboxServer` shows the server-rendered DrawnUI path.

- Renders DrawnUI on the server and delivers frames through the `Canvas` component
- Good for event-driven widgets, inspectors, and lower-frequency UI updates
- Useful when DrawnUI is one part of a larger Blazor Server app and browser-local canvas behavior is not required

This is the sample to study when you want server-owned DrawnUI surfaces and understand the current tradeoffs around hover, pointer-move, wheel, and high-FPS interaction.

Hosted sample:

- <a href="https://sample-blazor1.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Blazor Server sample</a>

## BlazorSandboxHybrid

Path: `src/Blazor/Samples/BlazorSandboxHybrid/`

`BlazorSandboxHybrid` demonstrates the mixed Blazor Web App model, where one DrawnUI surface can stay browser-local and another can stay server-owned.

- Uses a mixed runtime split instead of forcing the whole app into one model
- Useful when one panel needs local responsiveness while another panel is acceptable as server-rendered frames
- Best fit for gradual adoption and side-by-side runtime comparison

This is the sample to open when you need both runtime models in the same application and want a concrete reference for how to split them.

Hosted sample:

- <a href="https://sample-blazor2.appomobi.com/" target="_blank" rel="noopener noreferrer">Open Blazor hybrid sample</a>

## Which Sample To Start With

Choose the sample that matches your runtime decision:

- Start with `BlazorSandbox` for the strongest current DrawnUI runtime on the web
- Start with `BlazorSandboxServer` when DrawnUI is part of an existing Blazor Server flow
- Start with `BlazorSandboxHybrid` when you need both runtime models in one app

## Related

- [Blazor Overview](index.md)
- [Blazor WebAssembly](wasm.md)
- [Blazor Server](server.md)
- [Blazor Hybrid Web App](hybrid.md)
- [Blazor Capabilities](capabilities.md)