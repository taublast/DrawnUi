# DrawnUi Shared / MAUI / Blazor Port Status

This file is the live status board for the MAUI-to-Shared-to-Blazor port.

Maintenance rule:
- Update this file every time control parity, shared extraction, or Blazor capability changes.
- Keep dates current.
- Record validated state only.

Last updated: 2026-04-27

## Overall Status

| Area | Status | Notes |
|---|---|---|
| Shared code extraction | Active | Large shared base already in `src/Shared/` and still expanding incrementally |
| Blazor infrastructure | In progress | Polyfills and renderer exist, but API coverage is partial |
| Layout parity | In progress | Linear stack rendering fixed for row/column cross-axis centering on Blazor |
| Shape rendering | Working slice | `SkiaShape` shared/Blazor path validated in sandbox |
| Text rendering | Working slice | `SkiaLabel` and a browser-safe `SkiaRichLabel` markdown slice now render in the Blazor sandbox |
| Font loading | Working infrastructure | Blazor now has TTF preload support through static web assets and `HttpClient` |
| Sandbox font assets | Working | Blazor sandbox now reuses the same TTF files and aliases as the MAUI sandbox |

## Current Validated Blazor Capabilities

| Capability | Status | Validation |
|---|---|---|
| `SkiaLayout` row/column rendering | Working | Sandbox runtime verified after `DrawLinearStack(...)` cleanup |
| Cross-axis centering in row layout | Working | Orange ellipse and purple path centered inside green row |
| `SkiaShape` basic rendering | Working | Blazor sandbox visually and numerically verified |
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
| Bindable/text surface | Blazor still relies on compatibility shims instead of full MAUI-equivalent property system |
| Label runtime proof | Rich proof complete | Home page sample renders visible rich text with headings, emphasis, lists, inline code, links, and emoji in VS Code browser |

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

## Next Recommended Step

1. Validate more complex shaped-script cases in Blazor text rendering beyond the current emoji/browser fallback slice.
2. Reduce the remaining Blazor text-compat shims needed by the broader text stack.
3. Expand sandbox coverage to additional `SkiaRichLabel` markdown features like code blocks and nested formatting.