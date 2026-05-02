# DrawnUi Shared / MAUI / Blazor Port Status

This file is the live status board for the MAUI-to-Shared-to-Blazor port.

Maintenance rule:
- Update this file every time control parity, shared extraction, or Blazor capability changes.
- Keep dates current.
- Record validated state only.

Last updated: 2026-05-02

## Overall Status

| Area | Status | Notes |
|---|---|---|
| Shared code extraction | Active | Large shared base already in `src/Shared/` and still expanding incrementally |
| Blazor infrastructure | In progress | Polyfills and renderer exist, but API coverage is partial |
| Layout parity | In progress | Linear stack rendering fixed for row/column cross-axis centering on Blazor |
| Shape rendering | Working slice | `SkiaShape` shared/Blazor path validated in sandbox |
| Visual effects | Working slice | Core effect surface compiles, gradient-backed cards render, drop shadows render, and card interactions work in Blazor sandbox |
| Text rendering | Working slice | `SkiaLabel` and a browser-safe `SkiaRichLabel` markdown slice now render in the Blazor sandbox |
| Font loading | Working infrastructure | Blazor now has TTF preload support through static web assets and `HttpClient` |
| Sandbox font assets | Working | Blazor sandbox now reuses the same TTF files and aliases as the MAUI sandbox |

## Current Validated Blazor Capabilities

| Capability | Status | Validation |
|---|---|---|
| `SkiaLayout` row/column rendering | Working | Sandbox runtime verified after `DrawLinearStack(...)` cleanup |
| Cross-axis centering in row layout | Working | Orange ellipse and purple path centered inside green row |
| `SkiaShape` basic rendering | Working | Blazor sandbox visually and numerically verified |
| Core visual effects surface | Working slice | `DropShadowEffect`, `OuterGlowEffect`, `TintEffect`, `TintWithAlphaEffect`, shared filter bases, and shader support files compile in Blazor sandbox build; runtime proof covers gradient card fills, shadows, and interaction in the cards probe |
| Cards/effects runtime probe | Working slice | Fresh Blazor sandbox runtime on 2026-04-28 renders gradient-backed cards, drop shadows, text, and tap-driven status updates with no browser console errors |
| `SkiaLabel` multiline/spans/emoji rendering | Working slice | Blazor sandbox visually verified with multiline text, styled spans, and emoji fallback/sample fonts |
| `SkiaRichLabel` markdown rendering | Working slice | VS Code browser verified heading, emphasis, lists, inline code, link styling, and multiple emojis in Blazor sandbox |
| Registered TTF font preload | Implemented | Blazor `SkiaFontManager` loads font bytes from registered URLs via `HttpClient` |
| MAUI sandbox font alias parity | Implemented | Blazor sandbox now registers `FontEmoji`, `FontText`, `FontBrand`, and `FontGame*` aliases from the existing MAUI sandbox font set |
| Weighted font alias resolution | Implemented | Blazor `SkiaFontManager.GetFont(family, weight)` supports registered weighted aliases |
| Character fallback lookup | Partial | Loaded custom fonts checked first, then `SKFontManager.MatchCharacter(...)` |

## Current Known Gaps

| Area | Gap |
|---|---|
| `SkiaLabel` | Partial | Verified runtime slice works, but full parity across all shaping/script cases is not yet complete |
| `SkiaRichLabel` | Partial | Markdown slice works in Blazor, but more complex shaped-script cases still need validation |
| Shared text stack | Partial | Core label/rich-label files are now included for the current Blazor probe, but the whole text surface is not yet fully validated |
| Visual effects runtime | Working slice | Gradient-backed cards, drop shadows, and tap-driven card interactions now render in the browser runtime for the active cards probe |
| Gradient fills | Working slice | `FillGradient`-backed card bodies now render correctly in the current Blazor cards probe |
| Shader resource loading | Partial | Browser-safe async resource loading was added, but shader effects still need runtime proof in sandbox |
| Bindable/text surface | Blazor still relies on compatibility shims instead of full MAUI-equivalent property system |
| Label runtime proof | Rich proof complete | Home page sample renders visible rich text with headings, emphasis, lists, inline code, links, and emoji in VS Code browser |

## Current Blazor Compile Surface Snapshot

The current direct Blazor implementation footprint is still a foundation layer, not a broad control-for-control port. At the time of this update, the Blazor project contains platform partials or platform-owned files for:

- `Super`
- `SkiaControl`
- `SkiaLayout`
- `SkiaLayout.Scroll`
- `SkiaLayout.Grid`
- `SkiaShape`
- `SkiaImageManager`
- `Canvas`
- `DrawnView`
- `KeyboardManager`
- `DrawnExtensions`
- `ViewsAdapter`

That footprint matches the current validated slices: layout, shape rendering, text slices, image/font infrastructure, keyboard integration, and sandbox probes built on top of those primitives.

## Porting Definition Used In This Status File

For this board, a control is considered ported once it resides in `src/Shared/`.

- `Ported` means the control has been moved from MAUI-only code into the shared project.
- `Not ported yet` means the control still exists only under `src/Maui/`.
- Runtime parity in Blazor is tracked separately from structural porting.

## Controls Still Not Ported Yet

The following controls/classes are still MAUI-only and therefore remain not ported under the definition above:

| Area | Controls/classes not ported yet |
|---|---|
| Navigation | `SkiaShell` |
| Text input wrappers | `SkiaMauiEditor`, `SkiaMauiEntry` |
| Media playback wrappers | `SkiaMediaImage` |

## Features Still Not Ported Or Still Partial

| Feature area | State |
|---|---|
| Full Blazor runtime parity across all shared controls | Still partial; many controls are structurally ported via `src/Shared/` but not all have validated Blazor runtime parity yet |
| Text input/editor stack | Not ported yet; MAUI entry/editor wrappers remain MAUI-only |
| Shell navigation stack | Not ported yet |
| Media playback wrapper control | Not ported yet; `SkiaMediaImage` remains MAUI-only |
| XAML-specific converters, markup extensions, and styling helpers | MAUI-only infrastructure; no equivalent full Blazor port surface yet |
| PDF support | Explicitly excluded from the Blazor compile surface |
| Native file loading paths | Browser implementation logs native file loading as unsupported |
| Browser display refresh-rate detection | Still TODO in `Super.Blazor.cs`; frame loop uses configured fallback timing |
| Shader-backed effects runtime coverage | Still partial; compile surface exists, but runtime proof is still limited to the validated slices recorded above |
| Full bindable property parity | Still partial; Blazor uses compatibility shims instead of full MAUI-equivalent property semantics |

## Blazor Font Loading Status

Current supported pattern:

```csharp
DrawnExtensions.RegisterFont("FontText", FontWeight.Regular, "/fonts/OpenSans-Regular.ttf");
DrawnExtensions.RegisterFont("FontGame", FontWeight.Bold, "/fonts/Orbitron-Bold.ttf");

var host = await builder.UseDrawnUiAsync();
await host.RunAsync();
```

Notes:
- Fonts must be reachable as Blazor static web assets.
- Preload happens during `UseDrawnUiAsync()` before `RunAsync()`.
- Shared sync font lookup remains safe because registered Blazor fonts are loaded into cache first.

## Recently Completed

### 2026-04-27
- Fixed Blazor row/column cross-axis arrange bounds so `VerticalOptions = Center` works correctly in row layout.
- Refactored duplicated row/column draw logic into `DrawLinearStack(...)`.
- Added Blazor `SkiaFontManager` support for:
  - alias to URL registration
  - weighted alias registration
  - preload from `HttpClient`
  - cached `SKTypeface` lookup
  - fallback through `SKFontManager`
- Added Blazor startup hook `UseDrawnUiAsync()`.
- Copied the existing MAUI sandbox TTF assets into Blazor sandbox `wwwroot/fonts`.
- Registered the existing MAUI sandbox aliases in Blazor sandbox startup.
- Included the current `SkiaLabel` text slice in the Blazor build probe.
- Added Blazor compatibility fixes for `PropertyChanging` handling required by `SkiaLabel`/`TextSpan`.
- Added a visible rich text probe to the Blazor sandbox home page and verified it renders with headings, emphasis, list formatting, links, and several emoji.
- Added browser-safe fallback behavior when `SKShaper`/HarfBuzz shaping is unavailable in Blazor so text falls back to unshaped measure/draw instead of disappearing.

### 2026-04-28
- Removed the Blazor project exclusions for the shared effect/filter infrastructure.
- Added the MAUI effect classes needed for current tutorial parity work into the Blazor compile surface:
  - `DropShadowEffect`
  - `OuterGlowEffect`
  - `TintEffect`
  - `TintWithAlphaEffect`
  - `ChainDropShadowsEffect`
  - `ChainTintWithAlphaEffect`
  - `ShaderDoubleTexturesEffect`
- Restored the supporting shader/shadow files required by that effect surface:
  - `SKSL.cs`
  - `SkiaShadowsCollection.cs`
- Added browser-safe package/resource loading for:
  - `ShaderDoubleTexturesEffect`
  - `SkSl`
- Generalized several effect property-owner declarations from `SkiaImage` to `SkiaControl` so the effect surface can compile in the current Blazor control set.
- Kept the Blazor-specific cache implementation and added the `CreatedCache` hook expected by shader effects.
- Validated the full effect compile slice with:
  - `dotnet build src/Blazor/Samples/BlazorSandbox/BlazorSandbox.csproj -c Debug /v:m /clp:ErrorsOnly`
- Replaced `Home` with an interactive cards/effects workcase and archived the earlier Lottie workcase into `LottieProbe`.
- Runtime-validated the new cards probe in a fresh Blazor sandbox session:
  - page load completed with no browser console errors
  - card tap interaction updated page-visible status text
  - solid card fills rendered correctly
  - `DropShadowEffect` rendered visibly on the cards
- Fixed the shared/Blazor gradient rendering path for the cards probe:
  - `SkiaShape.Blazor` now forces shape-background fills to use the MAUI-aligned fill state after background paint setup
  - `SetupBackgroundPaint(...)` now treats fully transparent background colors like an unset color when a gradient is present, so the gradient shader is still applied
- Runtime-validated the repaired cards probe in a fresh Blazor sandbox session:
  - gradient-backed card bodies render correctly
  - drop shadows remain visible
  - tap-driven status updates still work with no browser console errors
- Refined the first interactive gradient card in the Blazor cards probe and revalidated it in browser runtime:
  - replaced the cache-corrupting tap scale path with a gesture-driven lift animation
  - added visible drag support through `ConsumeGestures`/`Panning`
  - changed the gradient to an explicit full-width horizontal fill
  - confirmed the card remains visually intact after tap and drag with no browser console errors
- Replaced the ad hoc Blazor card probe structure with a minimal translation of the MAUI `InteractiveCards` tutorial and revalidated it in a fresh browser runtime:
  - all three cards now use the MAUI tutorial container pattern again
  - card order now matches the MAUI tutorial (`Gradient`, `Gaming`, `Data`)
  - the card widths are visually consistent in the restored tutorial slice
  - browser console stayed clean on the fresh `http://localhost:5107` validation run
- Set the Blazor cards tutorial canvas to `RenderingMode="Accelerated"` for MAUI parity and reran validation on a fresh runtime:
  - `Home.razor` now explicitly uses the GL-backed canvas path instead of relying on the default software path
  - fresh sandbox instance on `http://localhost:5108` rendered the cards page successfully after the change
  - browser runtime emitted WebGL warnings only, confirming the accelerated path was active

## Next Recommended Step

1. Verify browser console/runtime behavior for shader-backed effects after the new async resource loading path.
2. Continue the cards tutorial port now that gradient-backed card surfaces render correctly in Blazor.
3. Expand runtime validation from the cards probe to additional effect combinations that rely on the same shared background/effect path.