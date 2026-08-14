---
name: drawnui-blazor
description: DrawnUI in Blazor WebAssembly apps — startup (UseDrawnUiAsync), Canvas gestures and rendering modes, BROWSER preprocessor + shared-project pattern, single-threaded WASM divergences, fullscreen/backdrop CSS, canvas blink root cause, font payload slimming (subsetting), and publishing to GitHub Pages under a repo subpath. Trigger on "drawnui blazor", "blazor canvas", "BROWSER preprocessor", "blazor wasm drawnui", "github pages drawnui", "font subsetting wasm".
version: 2.0.0
tags: [drawnui, blazor, webassembly, dotnet]
---

# DrawnUI Blazor

DrawnUI's Blazor head (NuGet `DrawnUi.Blazor.Wasm` / `DrawnUi.Blazor.Server`): a `Canvas` Razor component drawing on WebGL or raster. For pure-WASM without Blazor see the `drawnui-web-app` skill; framework rules in `drawnui`, C# composition in `drawnui-fluent`.

## Startup

```csharp
var builder = WebAssemblyHostBuilder.CreateDefault(args);
// register fonts, images...
await builder.UseDrawnUiAsync(new DrawnUiStartupSettings { UseDesktopKeyboard = true });
```

`UseDrawnUiAsync`: builds host → `Super.Services` → inits `SkiaFontManager` + `SkiaImageManager` (async) → `Super.Init()` → attaches `KeyboardManager` when `UseDesktopKeyboard`. Host the tree in Razor: `<Canvas Content="@RootControl" RenderingMode="@RenderingModeType.Accelerated" Gestures="@GesturesMode.Enabled" />`.

## Shared-project pattern + BROWSER symbol

- Shared code compiles into each head from a shared-source project (`.shproj`/`.projitems`) — never a class library (base types differ per head package).
- File convention: `Foo.Shared.cs` (cross-platform), `Foo.Blazor.cs` (Blazor partial), `Foo.Maui.cs`. New platform behavior goes in a platform partial, never `#if` inside shared files when a partial works.
- Symbols: `BROWSER` (Blazor/WASM only — JS interop, WASM APIs), `!BROWSER` (exclude from web), `ONPLATFORM` (any real platform), plus MAUI's `ANDROID`/`IOS`/`WINDOWS`/`MACCATALYST`.

## Gestures

Pipeline: JS gesture package → `Canvas.OnTouchAction(TouchActionEventArgs)` → `ProcessGestures(SkiaGesturesParameters)` → control tree.

- `GesturesMode`: `Disabled` (no capture), `Enabled` (standard), `Lock`/`SoftLock` (panning controls). Enabling gestures auto-applies `touch-action:none; user-select:none` CSS on the canvas.
- Touch mapping: Pressed→Down, Moved/Pan*→Panning, Released/Cancelled/Exited→Up, Wheel→Wheel. Multi-touch tracked per pointer id.
- For gesture bugs on scaled/transformed controls prefer built-in transform-aware helpers (`HitIsInside()`, `HitBoxAuto`, `IsGestureForChild(...)`, rescaled `args.Event.Location`) over hand-rolled transform math.

## Rendering modes

| Mode | View | Use for |
|------|------|---------|
| `Default` | `SkiaView` (raster) | simple/static UI |
| `Accelerated` | `SkiaViewAccelerated` (WebGL) | heavy animation, games, shaders |

`HasDrawn` detection: check `paintArgs.BackendRenderTarget` dimensions, NOT `CanvasSize` (fires late).

## Single-threaded WASM divergences

| Area | MAUI | Blazor |
|------|------|--------|
| `FrameTimeInterpolator` | used for physics stability | skip — raw delta |
| `CanUseCacheDoubleBuffering` | enabled | forced `false` — no background thread exists |
| `SkiaCachedStack.AsyncPlaneAllowed` | async plane bake | forced `false` — blocking-wait on the only thread deadlocks the bake it waits for; sync SKPicture record instead. Rule: never blocking-wait on WASM main thread for work only that thread can run |
| `CanvasSize` | after layout | unreliable until ResizeObserver fires; use `paintArgs` dimensions |
| Keyboard | native events | JS global listeners |
| PDF/file system | supported | excluded / bundle-only |
| `GetDisplayRefreshRate()` | device rate | constant 60 |
| Offscreen cache bakes | dedicated worker threads | drained INLINE (`DrainOffscreenQueueInline`) — queued items would otherwise starve indefinitely under continuous frame load (observed: two lotties, first-queued control never pumped = permanent empty box). Same total main-thread cost, guaranteed execution |

Frame loop is `Task.Delay`-based off `Super.MaxFps` (not rAF).

## Fullscreen canvas backgrounds

Real browser fullscreen targets the canvas host element (`.xaml-canvas`), not the page root — and browsers apply `:not(:root):fullscreen::backdrop { background: black; }`. If a scene expects an image/gradient behind a centered surface in fullscreen, style the actual fullscreen host AND its backdrop in GLOBAL CSS (scoped `.razor.css` can miss child pseudo-elements):

```css
.game-page .xaml-canvas:fullscreen,
.game-page .xaml-canvas:fullscreen::backdrop { background-image: url('../Images/back.jpg'); }
```

CSS under `wwwroot/css/` resolves urls relative to that folder (`../Images/...`).

## Canvas blink (random one-frame blank while idle)

Root cause: heavy CSS `filter: blur()` / `backdrop-filter: blur()` elements elsewhere on the page. Each blur is its own GPU layer; large animated ones compete with the WebGL canvas layer and the browser randomly drops a canvas frame. Page-level — the same canvas doesn't blink in a plainer app.

- NOT: canvas removal/hide (MutationObserver catches nothing), resize, Blazor re-render, `preserveDrawingBuffer` (constant repaint doesn't fix it), `contain` (makes it worse).
- Diagnose by page-level elimination: `display:none` the blurred elements / `backdrop-filter:none` the header — blink stops → narrow to the biggest-radius animated blur.
- Fix (also a perf win): replace blur-glow decoration with `radial-gradient(circle, color 0%, transparent ~68%)` backgrounds + plain `transform: translate()` drift. Reserve real blur for small static elements.
- Often invisible to headless automation at DPR 1 — A/B on a real scaled display.
- Diagnostics warning: never pipe `Console` into an on-screen panel while debugging canvas rendering — the render feedback loop fakes a per-frame-render symptom.

## Full-height layout CSS (required)

Without this the canvas won't fill the viewport:

```css
html, body, #app { min-height: 100%; }
body { min-height: 100vh; }
#app > .page { min-height: 100vh; }
#app > .page > main { display: flex; flex: 1 1 auto; flex-direction: column; min-width: 0; }
#app > .page > main > .content { display: flex; flex: 1 1 auto; flex-direction: column; min-height: 0; }
#app > .page > main > .content > .xaml-element { flex: 1 1 auto; min-height: 0; }
```

## Font payload slimming (subsetting)

Fonts are the usual bulk of a WASM bundle: full color-emoji ≈ 20–24 MB, full CJK ≈ 25–28 MB; subsetting to what the app renders cuts that to hundreds of KB. `WasmFilesToBundle` is a NO-OP — fonts are static web assets under `wwwroot/fonts/`, fetched over HTTP at startup; subsetting the served file IS the win.

**Check lib-shipped subsets FIRST**: `DrawnUi.Blazor` ships ready subsets as static web assets (built by `dev/fonts/subset_fonts.py` in the repo) — `fonts.AddEmojis()` (NotoColorEmoji faces+hands, ~900 KB, alias `FontEmoji`), `fonts.AddSymbols()` (~285 KB: Noto Sans Math + Symbols 2 subsets). Note: plain arrows U+2190–21FF live in Noto Sans Math, NOT Symbols 2.

Strategy: startup subset under the real alias → per-language subsets (page reload on language switch) → stream full fonts only if arbitrary user content must render (else skip the stream AND the loading wait entirely).

Traps (learned the hard way):
- **Skin-tone modifiers U+1F3FB–1F3FF explode size ~5×** via GSUB closure (faces+hands: ~900 KB without, ~4.4 MB with). Exclude them.
- NotoColorEmoji is COLR/SVG vector — subsetting scales well, but `drop_tables=["SVG "]` does NOT shrink it (bulk is COLR layers + glyf).
- Set `ignore_missing_unicodes`/`ignore_missing_glyphs`; keep `name_IDs = ["*"]` so aliases stay stable.
- Emoji are >U+FFFF: decode surrogate pairs when scanning sources; always include U+FE0F and U+200D.
- `SkiaLabel` has NO automatic font fallback, but DOES have an opt-in single-fallback: `FontFamilyFallback` (per-codepoint, checks the fallback face when the main font misses a glyph). Without it a missing glyph is silently dropped (not tofu). Verify coverage with `TTFont(...).getBestCmap()`.
- **Missing arrows/symbols/hearts recipe (verified 2026-08, DrawnCells)**: OpenSans (and most text fonts) ship NO U+2190–21FF arrows, U+2665 ♥ etc. On WASM there are no system fonts to save you — buttons show "B" instead of "→ B". Fix = `fonts.AddSymbols()` (registers `FontSymbols` = Noto Sans Math subset + `FontSymbols2`) + a global style setter `SkiaLabel.FontFamilyFallbackProperty = "FontSymbols"` next to the FontFamily setter in `ConfigureStyles`. One place, whole app. NEVER swap the text to an ASCII lookalike instead — that is a content dodge, not a fix.
- Multi-head apps: each head has its own `wwwroot` — sync the subset file to every one.
- Measure candidate tiers empirically with fonttools before committing (glyph→byte ratio is font-specific); state the tofu tradeoff (excluded categories) to the user.

Validation: `curl` subset → 200, removed full font → 404; network tab shows only subsets at boot; console clean; one screenshot proving curated glyphs render; every head.

For the full three-stage architecture (boot subsets → in-app loading screen → deferred streaming) + AOT/brotli publish checks, see the startup-optimization section of the `drawnui-game` skill — it applies to any content-heavy Blazor DrawnUI app, not just games.

## Publishing to GitHub Pages (repo subpath)

Workflow shape: manual `workflow_dispatch` (optional `source_ref` input for pre-merge testing), pin SDK via `global.json`, **`dotnet workload install wasm-tools` BEFORE publish**, `dotnet publish` the web csproj to a temp folder, rewrite `<base href="/" />` → `<base href="/RepoName/" />` in the published index.html, `actions/upload-pages-artifact` + `actions/deploy-pages` (never commit generated site output). Full worked example: `docs/articles/blazor/publishing.html` on drawnui.net.

**wasm-tools is NOT optional on CI** (verified 2026-08, DrawnCells): without the workload the publish SUCCEEDS but skips the native relink — libSkiaSharp never links into `dotnet.native.wasm` and the deployed app dies at boot with `TypeInitialization_Type, SkiaSharp.SkiaApi` while every asset fetches 200 (managed `SkiaSharp.wasm` in the payload is not proof of native code). Diagnostic tell: deployed `dotnet.native.wasm` ~3MB (stock runtime) vs ~27MB with Skia linked — compare via `curl -sI ... | grep -i content-length` against a locally working build.

Performance: add `<RunAOTCompilation>true</RunAOTCompilation>` for game/animation apps (`WasmBuildNative` is NOT AOT; interpreter costs ~2x FPS). AOT grows `dotnet.native.wasm` ~5x raw — verify compressed transfer after deploy: `curl -sI -H "Accept-Encoding: br" .../dotnet.native.<hash>.wasm` must show `content-encoding`.

Runtime fixes Pages hosting needs:
1. **Loader sequencing**: on .NET 10, `blazor.webassembly.js` may be fingerprinted — import it dynamically and start the DrawnUI loader only after the promise resolves; never assume `window.Blazor` is ready.
2. **Asset subpaths**: root-relative fetches (`/Images/x.png`) skip the repo subpath. Resolve app asset requests against `builder.HostEnvironment.BaseAddress` in the Blazor loader layer — never leak web resolvers into shared/MAUI code.
3. **CSS urls**: `url('/Images/back.jpg')` breaks on subpath hosting and is NOT fixed by runtime registration — use deploy-relative `url('Images/back.jpg')`. Symptom: direct asset URL 200 but visual missing → check `.razor.css`/site CSS first.
4. **Stale clients after publish**: (a) content-hash app JS/CSS via `<OverrideHtmlAssetPlaceholders>true</OverrideHtmlAssetPlaceholders>` + `#[.{fingerprint}]` placeholders; (b) if framework fingerprinting is off, bust `_framework` URLs in `loadBootResource` (integrity-derived `?v=` for assemblies, nonce for `blazor.boot.json`); (c) ensure `index.html` itself is never edge-cached (CDN cache rule: bypass for `/` and `index.html`). Symptom "published but users see old build" → apply these before anything else.
5. **App-version reload** (web games): compare a stored version constant on startup, write the NEW version BEFORE `forceLoad: true` reload (else infinite loop).

Validate the deployed app, not the workflow logs: subpath opens, no `_framework` 404s, DrawnUI loader leaves splash, console clean.

## SEO for Blazor WASM sites — block the payload from crawlers (2026-08-14)

Google indexes a Blazor WASM page badly (or "Oops" fails in GSC Request Indexing) because its renderer stalls downloading the multi-MB runtime. VERIFIED 2026-08-14: robots-blocking the payload immediately fixed failing Request Indexing on both fiddle.drawnui.net and a blog post iframing a WASM sample. Fix — the page's indexable content must be STATIC HTML, and robots must never fetch the payload:

1. **Static SEO content in index.html**: real `<h1>` + prose + links in a `<footer>` OUTSIDE `#app` (Blazor never replaces it), always `display:block`, kept below the fold via `#app { min-height:100vh }` — NEVER `display:none` (discounted, and Googlebot may snapshot before boot finishes). Plus keyworded `<title>`, meta description, canonical, JSON-LD WebApplication.
2. **robots.txt blocks payload**: `Disallow: /_framework/`, `/_content/`, `decode.js`; explicitly `Allow:` favicon paths under `_content` (longest-match wins). Bots then see splash + SEO footer instantly — deterministic, no crawl budget burn. The old "never block CSS/JS" guidance targets pages whose CONTENT needs JS; here content is deliberately static.
3. **Favicon for Google Search**: needs >=48x48 (32x32 = generic globe icon in results). Ship 96x96 PNG (`<link rel="icon" sizes="96x96">`) + root `/favicon.ico` (ICO may be a PNG-embedded container).
4. **Embedded-iframe case**: a post/page embedding a WASM sample iframe fails indexing the SAME way — block the sample's `_framework` in the robots.txt of the IFRAME'S ORIGIN domain root (robots.txt works only at domain root; a project-path robots.txt is ignored; for GitHub Pages project sites the override lives in the USER-site repo, which beats a Jekyll theme's generated robots.txt).
