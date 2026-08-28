---
name: drawnui
description: Working with DrawnUI UI, controls, layouts (SkiaStack, SkiaRow, SkiaLayer, SkiaWrap, SkiaGrid), rendering, caching, gestures, overlays, SkiaLayout, SkiaShape, SkiaButton, Canvas, virtualization, or XAML/C# composition in DrawnUi-based apps.
triggers:
  - drawnui
version: 1.10.0
tags: [drawnui, blazor, webassembly, dotnet]
---

# DrawnUI

## Maintenance Rule

- Update this skill whenever a new verified DrawnUI rule, pattern, pitfall, correction, or better workflow is discovered and it is missing here.
- If the user corrects a DrawnUI assumption, behavior, or recommended approach, correct this skill too after verifying the new rule well enough to avoid storing a bad pattern.
- Prefer keeping reusable cross-project DrawnUI guidance here; put repo-only facts in repo memory when they are too specific to generalize.

Use for DrawnUI-specific implementation details, not generic MAUI assumptions.

Primary docs source: `docs` inside the DrawnUi repo. Locate a local checkout if present; otherwise use the published docs at `https://drawnui.net` (same content, rendered) or the repo `https://github.com/DrawnUi/DrawnUi.Net`. For SOURCE CODE without a local checkout, read it from GitHub (browse or raw fetch) — NuGet packages ship compiled assemblies with SourceLink PDBs and XML API docs (`lib/<tfm>/*.xml` in the package cache is useful for offline API signatures), not `.cs` files.

Start here before guessing patterns:
- `docs/index.md`
- `docs/articles/getting-started.md`
- `docs/articles/layouts.md`
- `docs/articles/gestures.md`
- `docs/articles/interactive-button.md`
- `docs/articles/sample-apps.md`
- `docs/articles/web/index.md` (pure WebAssembly target)
- `docs/api/index.md`

For Blazor-specific work load the **`drawnui-blazor`** skill. For pure-WebAssembly (`DrawnUi.Web`, no Blazor) see the section below.

## Core Rules

- **PROHIBITED: writing any DrawnUI UI composition without a caching plan.** Before emitting code, decide for EVERY subtree whether it is cached or uncached and WHY, from its invalidation source: static chrome (toolbars, panels, side bars) → `UseCache = Image` at the top of the subtree; scroll content → `Image` on the content (blitted at offset — never cache the scroll's parent, that thrashes per scroll frame); document/artboard layers with independently-changing children → `ImageComposite` (re-records changed children only); constantly-repainting overlays (adorners, marquee, custom per-frame paint) → NO cache; stable overlays sitting over live surfaces that repaint every frame → cache them so they blit. Shipping a whole app shell with zero `UseCache` (only leaf visuals cached) is the failure mode this rule exists to prevent — it makes a drawn app feel laggy and reads as "drawn UI is slow" when it is not.
- ALWAYS add XML documentation comments to every class and public/protected API member you create in DrawnUI library code (classes, properties, fields, methods, ctors). Human-readable, states purpose + when to use; no undocumented public surface.
- Prefer DrawnUI docs first, then existing repo/sample patterns, before inventing new structure.
- Keep `Canvas` at `RenderingMode="Accelerated"` when hosting DrawnUI camera or GPU-rendered content.
- Do not nest `UseCache="GPU"` inside already GPU-cached or GPU-rendered content unless runtime evidence proves it safe.
- Prefer top-level GPU cache only. Do not assume inner controls should default to no cache; decide from redraw source, visual stability, and subtree cost.
- Preserve stable layout sizing. Avoid `Auto` sizing around performance-sensitive DrawnUI surfaces unless existing code already depends on it.
- For camera overlays, place controls inside DrawnUI tree when interaction/visual sync matters.
- For simple app chrome over DrawnUI, MAUI overlay is acceptable only if user did not ask for DrawnUI-native controls.

## C# Code-Behind Composition and Event Wiring

See **`drawnui-fluent`** skill for all C# code-behind patterns: inline control construction, `.Assign(out _field)`, `.OnTapped()`, `.OnTextChanged()`, `.ObserveProperty()`, `.ObserveProperties()`, `.Adapt()`, `.WhenPaint()`, `.ObserveSelf()`, gradients, colors, shadows, and per-control code patterns.

## Layouts — prefer semantic aliases

Prefer alias controls over raw `SkiaLayout Type="..."` — same class, preset `Type` + sane defaults. Aliases default `HorizontalOptions=Fill`; base `SkiaLayout` does NOT fill and defaults to `Type=Absolute`, so raw `SkiaLayout Type=Column` aligns differently than `SkiaStack`.

| Alias | Equals | Use |
|---|---|---|
| `SkiaStack` | `SkiaLayout Type=Column` + Fill | vertical stack (MAUI VerticalStackLayout) |
| `SkiaRow` | `SkiaLayout Type=Row` | horizontal stack |
| `SkiaLayer` | `SkiaLayout Type=Absolute` + Fill | overlay/superposition; single-cell-grid substitute |
| `SkiaWrap` | `SkiaLayout Type=Wrap` + Fill | responsive wrap |
| `SkiaGrid` | `SkiaLayout Type=Grid` + Fill | MAUI Grid alternative |
| `SkiaFrame` | `SkiaShape Type=Rectangle` | NOT a layout — shape alias |

- Grid: `ColumnDefinitions`/`RowDefinitions` (`*`, `2*`, `Auto`, absolute), attached `SkiaLayout.Column/Row/ColumnSpan/RowSpan` (0-based), `ColumnSpacing`/`RowSpacing`. `SkiaDecoratedGrid` = grid drawing separator lines (`HorizontalLine`/`VerticalLine` gradients).
- `Split` = explicit column count for Column/Wrap/Grid with ItemsSource; `UseDynamicColumns` lets a short last row expand; grid `Invert` flips fill order to columns-first.
- Perf: for a known-size icon + label pair prefer an Absolute layer + left `Margin` on the label over a 2-column grid.
- **Shader effect + image that overflows its box = misplaced output** (root-caused & fixed in DrawnUi 2026-08-20, two stacked bugs). Triggers only when a `SkiaShaderEffect` sits on a `SkiaImage` whose drawn content is bigger than its `DrawingRect` — classically `AspectCover` driven by HEIGHT (square/landscape source in a taller-than-wide box; portrait sources are width-driven and never show it). (1) `SkiaImage.CachedImage` returned `ScaledSource.Image`, rasterized at DISPLAY size, and effects map that texture 1:1 onto the box → content shifted right by half the horizontal overdraw. (2) `SkiaShaderEffect.CreateSnapshot` snapshotted `ctx.Destination` (CANVAS coords) out of `ctx.Surface`, which is a CACHE surface while baking into a cached parent → shifted by the control's canvas offset. Diagnosing this class: put a marker stripe at the source centre and measure it in the rendered pixels across box aspect ratios AND with/without the effect — theory alone kept "proving" the aspect math correct, because the math WAS correct.
- **Custom deco painting: pick the stage by what must sit under/over it** (device-verified 2026-08-20, nameplate crescent): `WhenPaint` runs BEFORE the shape paints its own background — anything drawn there gets tinted/covered by a translucent bg; `WhenPainted`/PostAnimators draw above everything, but under a CACHED parent the callback's `ctx.Destination` can span the whole canvas, not the control (rendered a full-height stripe). The reliable pattern for "after background, before content, correctly sized": a tiny `SkiaControl` subclass overriding `Paint(DrawingContext)`, mounted as the parent's first child with Fill×Fill — children paint after the shape bg, each with its own correct `ctx.Destination`, and the shape's rounded clip still applies. Example: red crescent = `outerRRect.Op(shiftedRRect, SKPathOp.Difference)`.
- **Inside recycled cells, toggle state markers via `Opacity`, never `IsVisible`** (device-verified 2026-08-19): flipping a child's IsVisible invalidates the CELL's measure, and a lone remeasure of one recycled `MeasureFirst` cell mid-display runs against wrong constraints — the cell visibly resizes/corrupts the grid. Opacity 0/1 repaints without touching layout and reads identically. Applies to selection checks, badges, play glyphs — anything bound per-item in `SetContent`/bind.
- **`LockRatio` makes SQUARES only — it cannot express an aspect tile** (device-verified 2026-08-19, black-gaps regression): `CreateMeasureRequest` rewrites BOTH constraints to the same value `min(w,h) * |ratio|` (negative) or `max(w,h) * ratio` (positive). `LockRatio = -1.33` in a grid column therefore asks for a 1.33×column SQUARE that overflows its column — it does NOT give width=column/height=column×1.33. For an aspect tile in a `Split` grid: keep width `Fill` and set an explicit `HeightRequest` derived from the layout's own arranged width (`grid.DrawingRect.Width / RenderingScale` at `LayoutIsReady`, observed by cells) — never `LockRatio`, never a screen metric.
- **NEVER size a dynamic layout from `DeviceDisplay.MainDisplayInfo`** (or any screen metric). It is the SCREEN, not the container: on desktop windows, split views, resizable or padded hosts it is off by multiples (a 500pt window on a 2048px screen gave cells ~4× too tall), and being read once at construction it never follows resize/rotation. For a grid of cells use `SkiaGrid` with `*` column definitions; for proportional sizing inside a container use `HorizontalFillRatio` / `VerticalFillRatio`; for content-driven heights use `MeasureItemsStrategy = MeasureVisible` instead of faking a uniform height for `MeasureFirst`.
- **Grid: implicit column is `Auto`, and a Fill child in an Auto track is measured at the final track size** (fixed 2026-08-28, lib ≥ 1.10.5.18): `DefaultColumnDefinition`/`DefaultRowDefinition` are `Auto` (MAUI's implicit is `*`). Since the 2026-06 refactor a Fill child in an Auto track is measured UNCONSTRAINED (content-sized track, MAUI desired-size semantics), then `RemeasureFillChildrenInAutoCells` re-measures it at the resolved track — but `MeasureGrid` stretched the LAST column/row to fill a Fill grid only AFTER that, without re-measuring. Result: child measured at content width, arranged at full width; its internal layout (a `SkiaRow(Center)`) centered inside the content width → flush left (ArtOfFoto exposure page: stack measured 356px, arranged 451px, picker row Left=0). Fix: `GridStructure.RemeasureFillChildrenInExpandedTracks` called after the stretch. Diagnostic tell: `control.MeasuredSize.Pixels.Width < control.DrawingRect.Width` on a Fill child of a grid with no `ColumnDefinitions`. Headless caveat: `SkiaLayout` Grid in the UnitTests project does not measure its children from a bare `Measure()`/`Arrange()` (children stayed 0x0) — validate grid layout bugs at runtime or via `HeadlessCanvasHost`, not the plain measure test.
- **Measure-vs-arrange consistency sweep (2026-08-28, lib ≥ 1.10.5.18)** — one systemic rule + the paths that violated it. Rule: a control's internal layout must be computed for the box it is ARRANGED in. `SkiaControl.Arrange` now re-measures on a Fill axis when the destination differs from the measured-for constraint (MAUI `ArrangeOverride(finalSize)` parity; idempotent, steady state free). Path fixes: Grid measures every child once more at its FINAL cell (`RemeasureChildrenAtFinalCells`, after spans/minimums/star decompression/last-track stretch), clamps an unconstrained Fill-in-Auto measure to the finite grid (wrapping label / scroll no longer inflate the track), and never stretches the last track to ∞ inside a scroll; Fill layouts on an unbounded axis measure to CONTENT instead of ∞→-1/0 (all Column/Row/Wrap/List sites); `MaximumWidth/HeightRequest` capped at arrange too; templated stack draw rect = arranged `Destination` size (Center/End cells were aligned twice, Fill-Y cells in a scroll drew `float.MaxValue` tall); non-templated Column `Split>1` advanced x only for Rows (all columns drew at x=0); templated/Fast Split advanced the slot by MEASURED width (End/Center cells collapsed the grid); main-axis `Center` in a Column/Row now clamps to the child's desired size (was centered in the whole remaining rect); Fill child on an infinite main axis = auto-sized (was ∞ slot → stack invisible); auto-sized stack with only cross-axis-Fill children adopts the constraint instead of 0; Wrap Fill-X child shares the row again (1.9.7.4 flex-fill); Wrap Center/End child stays in flow. Also (owner-approved same day): TEMPLATED main-axis Fill cells are auto-sized like MAUI (one site, `MeasureAndArrangeCell`: Column+NeedFillY → ∞ height constraint, Row+NeedFillX → ∞ width; 3 Fill/Fill cells in a 1000px Column = 30/30/30, not one 1000px cell), and `MeasureContentCore` no longer peeks the previous frame's `DrawingRect` (measure must not depend on the last arrange; the Arrange guard supplies the real box). Deliberately NOT changed: main-axis `End` still pushes to the stack end (1.9.7.4 semantic, owner's call — skip); `LockRatio>0` with an infinite axis still collapses auto-sized content to its content size (icon labels rely on it — a "fix" made 120px squares; leave). Tests: `src/Tests/UnitTests/LayoutSweepTests.cs`, `StackWrapSweepTests.cs`, `SkiaLayoutSecondPassTests.cs`, `StyleDefaultsTests.cs`. Headless grid tests need `Super.Screen.Density = 1` in the fixture ctor (else children measure at scale 0 → 0x0).
- **Column/Row second measure pass keeps the main-axis constraint** (fixed 2026-08-28, lib ≥ 1.10.5.18): a non-templated `SkiaLayout` Column/Row measured with an INFINITE main axis (content of a `SkiaScroll`) does a second pass (`ProcessSecondPass`) re-measuring perpendicular-Fill children with the final stack width/height. It used to pass `cell.Area.Height` — the whole stack height, clamped from ∞ — as the HEIGHT constraint of a `HorizontalOptions=Fill` child, so anything inside sizing from the height constraint (`LockRatio` = `max(w,h)`, Fill descendants) ballooned to the full stack: ArtOfFoto main-screen cards went from 94px to 504px tall (icon `LockRatio=1` → 479px). Now the main-axis constraint is re-derived from the layout rect edge (`rect.Bottom - Area.Top`, ∞ stays ∞); only the perpendicular one changes. Diagnostic tell: cells measure right on the first pass, then a second `OnMeasuring` arrives with `h == parent stack height`; `Environment.StackTrace` in an `OnMeasuring` override shows `ProcessSecondPass`. (Console trap on Windows: `float.PositiveInfinity` prints as `8` under the default codepage — "h=8" in a log is ∞, not eight.) Regression test: `src/Tests/UnitTests/SkiaLayoutSecondPassTests.cs`.
- `Spacing` = between children (single value); `Padding` internal; `Margin` external. Layouts support `BackgroundColor` directly but NOT `CornerRadius` (verified: no such member on SkiaLayout/aliases — wrap in `SkiaShape`/`SkiaFrame` for a rounded panel).
- iOS safe insets require a MAUI root wrapper (e.g. `Grid`) around the `Canvas`; opt out via startup `MobileIsFullscreen = true`.

## Positioning children inside containers — 4 ways, prefer in order

WPF-style system: there is no free X/Y — the layout computes position and stamps the arranged `DrawingRect`. Pick the FIRST way that fits:

1. **Layout-owned (default)**: Column/Row/Grid/Wrap parents position children — set nothing. Tune with container `Spacing`/`Padding` and per-child alignment + size requests.
2. **Margin + alignment — the WPF-style workhorse**, works in ANY container incl. Absolute: `Margin = new(50,0,0,0)` places after a logical 50pt column; `VerticalOptions = End` + `Margin = new(0,0,0,100)` = exactly 100pt above the container bottom. Arranged by layout → `DrawingRect`/`HitBoxAuto` stay truthful (hit-testing, adorners, position math all correct). Often replaces a whole `SkiaGrid`.
3. **`Left`/`Top` — cached controls only** (`UseCache != None`): offsets the cached output directly, no matrix transform, faster than Translation, background-thread friendly. Mind cache defaults — many controls are cached out of the box (labels, shapes, svg, lottie…), see the defaults table in § Caching Guidance.
4. **`TranslationX/Y` — LAST resort**: matrix transform with save/restore around every draw, the most expensive option. Only when nothing else fits (live gesture drag, transform animations). Trap: a translated CONTAINER shifts rendering but NOT its descendants' `HitBoxAuto`/`DrawingRect` — hit-testing/adorner math under a moved container goes stale unless you compensate with the ancestors' own-transform offsets.

## Control Patterns

- `SkiaLayout`: base of ALL layout types (and of `SkiaShape`); author with the aliases above.
- `SkiaShape`: `Type` Rectangle/Circle/Ellipse/Path/Polygon/Line/Arc; IS a SkiaLayout — holds multiple `Children` and CLIPS them to its outline (`LayoutChildren` sets their arrangement); `ClipBackgroundColor=true` = hollow shape (stroke/shadow only). Custom circular UI, shutter buttons, frames, chips, clipped containers.
- `SkiaButton`: standard text/button interactions. Custom visuals contract: child tagged `Tag="BtnText"` / `Tag="BtnShape"` auto-binds text/style. Platform look set in code via `UsingControlStyle` — SkiaButton has NO bindable `ControlStyle` (unlike `SkiaSwitch`/`SkiaCheckbox`/`SkiaSlider`, which DO: Cupertino/Material/Windows/Platform).
- **Style builders must never clobber user-set properties** (fixed 2026-08-28, lib ≥ 1.10.5.18): `Create*StyleContent()` on styled controls (`SkiaSlider`, `SkiaProgress`; Cupertino/Material/Windows/default) runs LAZILY at first measure via `CreateDefaultContent` — i.e. AFTER the user's object initializer/XAML. They used to assign `HorizontalOptions = Fill`, `MinimumWidthRequest`, `UseCache` unconditionally → a `SkiaSlider { HorizontalOptions = Center, WidthRequest = 150 }` rendered flush left (ArtOfFoto light table). Now `SkiaControl.SetStyleDefault(BindableProperty, value)` applies style defaults only when `!IsSet(property)`. Rule for any new styled control: ctor assignments are fine (user code runs after), but anything inside a lazy content builder goes through `SetStyleDefault`. Pass values with the property's exact CLR type (`64d` for a double property — a boxed `int` throws in `SetValue`). Tell: user-set alignment/cache silently "ignored" on a styled control only; plain layouts honor it. Test: `src/Tests/UnitTests/StyleDefaultsTests.cs`.
- `SkiaSvg`: use for icon-based controls. `TintColor` vs `FillColor`/`StrokeColor`; `SvgString` sets SVG from inline text.
- `SkiaImage`: default `Aspect = AspectCover` — CROPS; set `AspectFit` explicitly when the whole image must show. Never combine Operations/GPU cache with GPU-surface shader effects on it.
  - Built-in per-image adjustments, no effect object needed: `Brightness`, `Contrast`, `Saturation`, `Gamma`, `Lighten`, `Darken`, `Blur`, `ColorTint` + `EffectBlendMode`, `UseGradient`/`StartColor`/`EndColor`, `ZoomX`/`ZoomY`, `HorizontalOffset`/`VerticalOffset`.
  - `SpriteWidth`/`SpriteHeight`/`SpriteIndex` crop ONE static frame out of a sheet. For animated sheets use `SkiaSprite` (`Columns`/`Rows`/`FramesPerSecond`).
- `SkiaLabel`: lightweight text; `Spans` of `TextSpan` (per-span `Tapped`, `AutoFont` for emoji), `AutoSize=TextToView`, `FontWeight` 100–900.
- `SkiaRichLabel`: markdown + automatic font fallback for emoji/CJK (ex-SkiaMarkdownLabel); `LinkTapped`/`CommandLinkTapped`.

### Wider control catalog (details: `docs/articles/controls/*.md`)

`SkiaHotspot` (invisible tap area) · `ContentLayout` (single-`Content` host) · `SkiaSwitch` / `SkiaCheckbox` / `SkiaRadioButton` (`GroupName`) / `SkiaToggle` (base for custom toggles) · `SkiaSlider` (value = `End`; `EnableRange` → `Start..End`; `Step`) · `SkiaProgress` (`Progress` 0–1, `IsIndeterminate`) · `SkiaSpinner` (fortune wheel: `Segments`, `Spin()`, `SpinCompleted`) · `SkiaWheelPicker` (iOS wheel; `SelectedIndex` bindable) · `SkiaCarousel` (`SelectedIndex`, `SidesOffset` peek, `ScrollProgress`, `InTransition`) · `SkiaDrawer` (edge panel: `Direction`, `HeaderSize`, `IsOpen`, `CommandOpen/Close/Toggle`) · `SkiaShell` (drawn navigation: `GoToAsync` routes, modals, popups, toasts; tagged `ShellLayout`/`RootLayout`/`NavigationLayout`) · `SkiaViewSwitcher` (`PushAsync`/`PopAsync`, `TransitionType`) · `SkiaTabsSelector` · `SkiaScrollLooped` (`IsBanner`, `CycleSpace`) · `RefreshIndicator` (pull-to-refresh, any content incl. Lottie) · `SkiaScrollBar` (`Dock=Start` for inverted lists) · `SkiaGif` · `SkiaMediaImage` (auto image/gif) · `SkiaSprite` (sheet: `Columns/Rows/FramesPerSecond`, `FrameSequence`, named sequences) · `SkiaSpriteSet` (stateful actor, `HitBoxAuto` trimmed hit rect) · `SkiaImageManager` (`PreloadImages()`, `ReuseBitmaps`, `CacheLongevitySecs`) · `SkiaLabelFps` · `SkiaHoverMask` · `SkiaMauiElement` (embed native MAUI view — MAUI head only; Windows = snapshot; **no cached ancestors**, see its section below) · `SkiaCamera` (see `skiacamera` skill).
- On Blazor, local `SkiaSvg.Source` assets are not instant by default on first render. For public-facing or above-the-fold local SVGs, register them at startup with `DrawnExtensions.RegisterSvg(...)` and warm them through `UseDrawnUiAsync(...)` so the first page paint can hit the shared SVG text cache instead of async fetching.
- `SkiaLabel`: use for lightweight overlay text.
- `SkiaImage.PreviewBase64`: instant inline preview (e.g. tiny blurred jpeg from backend) shown while `Source` downloads. Contract: RAW base64 only — `SetFromBase64` feeds it straight to `Convert.FromBase64String`, a `data:image/...;base64,` data-URI prefix throws FormatException. Must be set BEFORE `Source` in code-behind. Clear both on recycled cells to avoid stale previews.

## SkiaShell overlays and safe insets

Nothing in the lib's overlay/popup/modal path subtracts system insets — `ShellLayout` spans the FULL edge-to-edge window, so anything pinned to an edge lands under the Android navigation bar / iOS home indicator. Inset source of truth: `Super.Screen.BottomInset` / `TopInset` (`Internals/Helpers/Screen.cs`, populated per platform, `Super.InsetsChanged` fires on change); `SkiaShell.BottomInsets`/`TopInsets` mirror it but only after `OnLayoutInvalidated` has run at least once — prefer `Super.Screen.*` in code that can run earlier (e.g. `ShowToast` builds its control inside `Task.Run`). App-side XAML binds `AddMarginBottom="{Binding BottomInsets}"`.

Toast (`SkiaShell.ShowToast`) was fixed 2026-07-20 (device-verified) by padding the toast root — `Padding = new Thickness(0, 0, 0, Super.Screen.BottomInset)` — NOT a margin: padding keeps the background full-bleed to the screen edge (correct edge-to-edge look) and keeps the slide-in/out animation, which translates by `sender.Height`, correct. Same treatment is needed for any custom bottom-pinned overlay (`VerticalOptions=End` inside `ShellLayout`).

Known unrelated bug nearby, still unfixed: `SkiaShell.OnLayoutInvalidated` sets `TopInsets = Super.Screen.BottomInset` (with a `//WTF is this???` comment) — should be `TopInset`.

## SkiaMauiElement / SkiaMauiEntry / SkiaMauiEditor — NO CACHED ANCESTORS

Hosted native MAUI views (`SkiaMauiElement` and its `SkiaMauiEntry`/`SkiaMauiEditor` subclasses) are real platform views laid over the canvas. Their on-screen rect + visibility come from the per-frame RENDERED NODE TREE: `DrawnView` (`DrawnView.cs`, "notify registered tree final nodes") walks `skiaControl.FindRenderedNode(subscriber)` and sets `VisualTransform.IsVisible = node != null` → `SkiaMauiElement.ApplyTransform` → `LayoutNativeView`.

**`SkiaControl.Render()` sets `VisualLayer = null` every frame and rebuilds it.** A control drawing from CACHE does NOT re-render its children → its fresh node has EMPTY `Children` → the hosted element's node is unreachable from the root → `IsVisible=false` → Android view forced `ViewStates.Invisible` (size 0x0) → **absent from the native view hierarchy, cannot take focus, keyboard never opens**. Silent: no exception, the drawn placeholder/frame still paints, so it looks like "focus is broken".

Rules until the lib stops depending on the node tree:
- EVERY ancestor of a `SkiaMauiElement` must be uncached. Two default-on traps: `SkiaShape` sets `UseCache=Operations` in its ctor (a plain frame around an entry kills it → `UseCache="None"`), and `SkiaScroll.AutoCache` sets `Operations` on itself (→ `AutoCache="False"`).
- Diagnosing on Android, in order: `adb shell uiautomator dump` → is there an `EditText` node at all? (missing = native view Invisible, NOT a focus bug); `adb shell dumpsys input_method | grep -E "mInputShown|mServedView="` (`mServedView=DecorView` = nothing focused); only then look at focus code.
- Verified 2026-07-20 on device (demo `ScreenVarious`): entry unblocked by scroll `AutoCache=False`, editor still dead until its `SkiaShape` wrapper got `UseCache="None"`. `AnimateSnapshot` was NOT involved (`snapshot=False` throughout).

Two lib bugs fixed the same day (both in `src/Maui/DrawnUi/Controls/EditText/`):
- `SkiaMauiEntry` created its native `Entry` in `OnLayoutReady()`, which never fires → `Content` stayed null → no native control ever existed. Now created in `OnMeasuring`, mirroring `SkiaMauiEditor` which always did it there. **Create hosted native content in `OnMeasuring`, never in `OnLayoutReady`.**
- Both classes still declared `public new bool OnFocusChanged(bool)` after `ISkiaGestureListener` renamed it to `SetFrameworkFocus(bool)`. It compiled (base `SkiaControl.SetFrameworkFocus` is `public virtual` with a matching signature) but the interface slot silently bound to the base returning `CanBeFocused` (default **false**) → canvas always rejected focus. Fixed to `override SetFrameworkFocus` + `CanBeFocused = true` + claim focus on `TouchActionResult.Down` (these controls consume every gesture, so the native view may never see the touch). When renaming an interface method, grep for the OLD name across the repo — `new`-shadowed leftovers are dead code with NO build error.

## SkiaEditor emoji/unicode + caret over shaped runs

- `SkiaEditor.UseUnicode` (default true) renders emoji/CJK via `SkiaRichLabel` with `MarkdownEnabled=false` (rich font-run fallback, NO markdown formatting). `UseMarkdown` = rich + markdown. Both false = plain single-font `SkiaLabel` (fastest, no emoji — plain label picks ONE font per label from the FIRST glyph, can't mix text+emoji).
- Backspace/delete on stub heads (Blazor/Net, no native control) must be grapheme-aware: `SkiaEditor.CodeUnitsBeforeCaret/AfterCaret` (via `StringInfo` text elements). Naive `count=1` code-unit delete splits a surrogate-pair emoji → orphaned surrogate corrupts the string/markdown parser. MAUI unaffected (native EditText is grapheme-aware).
- Caret-over-shaped-text (2026-07 root fix): the caret maps `CursorPosition`→X by reading the line-span's POSITIONED glyph array `LineSpan.Glyphs` (`LineGlyph[]`). `SkiaLabel.MeasureLineGlyphs` used to return `Glyphs=null` for SHAPED runs (emoji/complex scripts) — text still rendered (shaper draws at paint time) but the caret had no positions → cursor stuck at 0 after typing/pasting emoji. Fix: when `NeedsGlyphPositions` (editors set it — normal labels DON'T, so their hot path is a single added bool check then the same width-only early-return, NO slowdown), `MeasureLineGlyphs` builds positioned `LineGlyph[]` from the `SKShaper.Result` via `BuildGlyphsFromShaping`.
- **`SKShaper.Result.Clusters` are UTF-8 BYTE offsets, NOT UTF-16 char offsets** (SkiaSharp feeds HarfBuzz UTF-8). The caret works in UTF-16 code units, so `BuildGlyphsFromShaping` must convert cluster byte offsets → char indices (`Utf8ByteOffsetToCharIndex`, `Rune.Utf8SequenceLength`). Not doing this made an all-emoji string report glyph0 spanning both emojis (`clusters=0,4` → `[s0+4]` instead of `[s0+2],[s2+2]`) → line had 5 slots for 4 code units → caret landed at HALF the typed emojis. ASCII-prefix + trailing-emoji masks it (byte==char for ASCII, last glyph clamps to text.Length). Device-verified on Android via FastRepro + adb (`adb shell input tap`, logcat `DOTNET` tag, gated `SkiaLabel.DebugCaret` logging removed after).
- `SkiaEditor.GetLineGlyphs` flattens ALL font-run spans into ONE-ENTRY-PER-CODE-UNIT with line-absolute X (span glyph Position is span-relative; render adds cumulative span offsetX). `CursorPosition` is a code-unit offset, so a multi-unit glyph (surrogate emoji) must occupy as many caret slots as code units. Single-span ASCII takes a zero-alloc fast path.
- Debugging note: this was found by console-logging `LineSpan.Glyphs` count at the caret read (`spans=2 glyphs=0` while text rendered) — the positioned array being null despite visible text is the tell that the glyphs live only in the shaper/`TextSpan.Glyphs` (`UsedGlyph`, NO positions), not `LineSpan.Glyphs`.

## SkiaShape stroke contracts the content clip (descender trap)

A stroked `SkiaShape` clips its CHILDREN to a stroke-inset path (`CalculateClipSizeForStroke` → shape inset + `GetSmallUnderStroke` + Ceiling/Floor). Small in px, but for TEXT it cuts glyph descenders (g/j/p) that overflow the font's metric line box — the visible cut is as big as the font's overshoot, so it looks large and is font/DPR-specific (only reproduces on the head whose font overshoots). Borderless shapes clip to the full rect (`return destination`) so the overflow shows; bordered ones cut it → symptom presents as "only stroked styles cut, and by a lot". Fix for a text-bearing shape: `protected override SKRect CalculateClipSizeForStroke(SKRect d, float s) => d;` (clip to full rect; border still draws via the shape path, Padding keeps text off the frame). Done on `SkiaEditor` 2026-07 for its Cupertino/Windows (bordered) styles. Base method is now `virtual`.

## SkiaEditor (totally drawn text input)

- `SkiaEditor : SkiaShape` (namespace `DrawnUi.Draw`), source `src/Shared/EditText/SkiaEditor.cs` + per-platform partials (`src/Maui/DrawnUi/Platforms/*/SkiaEditor.*.cs`, Blazor, Net). Default `UseCache = Operations` set in constructor.
- Key properties: `Text`, `PlaceholderText`, `PlaceholderColor`, `PlaceholderHorizontalAlignment`, `FontSize`, `FontFamily`, `TextColor`, `CursorColor`, `SelectionColor`, `MaxLines` (1 = single-line), `AutoHeight`, `IsPassword`, `ReturnType` (MAUI enum, e.g. `ReturnType.Send`), `KeyboardType`, plus inherited SkiaShape visuals (`CornerRadius`, `BackgroundColor`, `Padding`).
- Chat-style growing input: `MaxLines = 3, AutoHeight = true` — starts 1 line tall, grows with actual rendered line count (word-wrap counted via `Label.LinesCount`, not just `\n`), caps at MaxLines then scrolls, shrinks back on delete. Ignored when `HeightRequest` set. Height self-heals next frame after label remeasure (`UpdateViewportHeight` runs each `Paint` with 0.5pt change guard). Verified headless (DrawnUi.Net `HeadlessCanvasHost`, 2026-06).
- Enter-to-send: multiline + `ReturnType = ReturnType.Send` → Enter submits (`TextSubmitted`/`CommandOnSubmit`, focus kept), Shift+Enter inserts line break, Alt+Enter soft break (Blazor/Net/OpenTk). Gate: `ShouldSubmitOnEnter` (shared). Default `ReturnType.Done` keeps Enter=newline — gate is strictly `Send`. `ReturnType` available on ALL targets incl. Blazor (shim enum `src/Blazor/DrawnUi/Compat/ReturnType.Blazor.cs`). Per-platform: Windows `PreviewKeyDown` intercept; Blazor/Net `StubPressEnter(splitLine, shift)`; Android `Control_EditorAction` + multiline-Send uses `SetRawInputType` trick (Send IME key with multiline editing, NOT `SetSingleLine(true)`); Apple `ShouldChangeText` (hardware Shift+Enter NOT distinguishable there — Send always submits on "\n"). Full chat combo verified headless 2026-06: `MaxLines=3, AutoHeight=true, ReturnType=Send`.
- Commands: `CommandOnSubmit` (return key), `CommandOnTextChanged`, `CommandOnFocusChanged`. Events: `TextChanged`, `TextSubmitted`, `FocusChanged`, `CursorMoved`.
- Fluent: `.OnTextChanged(text => ...)` works on SkiaEditor (verified: `src/OpenTk/Samples/OpenTkOverlay/OverlayPanel.cs`). For submit prefer `CommandOnSubmit = new Command(...)` in the initializer.
- Reference usages in the DrawnUi repo: `src/Maui/Samples/FastRepro/MainPageEditors.cs` (single-line, password, centered, placeholder variants + keyboard adaptation), `src/OpenTk/Samples/OpenTkOverlay/OverlayPanel.cs` (dark-theme editor with cursor/selection colors).
- Programmatic Text set: must push to the native input control immediately (`SyncNativeText` partial, called from `OnControlTextChanged`, fixed 2026-06). Before fix, editor→native sync happened only in `SetFocusNative`: clearing a chat input programmatically left old text in the hidden native control (WinUI TextBox / Android EditText / UITextView) and the next native text event resurrected it. Net/Blazor have no native text store — immune.
- Programmatic focus: `editor.IsFocused = true` / `SetFocus(true)` must mirror the tap path — fixed 2026-06 so the property change also syncs `Canvas.FocusedChild` (`SyncSuperviewFocus`, deferred via `OnLayoutChanged` when set before attach) and Windows retries native `Focus(Programmatic)` on TextBox `Loaded`. Historic symptom of the broken half-path: cursor blinks but keyboard dead (Net/OpenTK route keys via `FocusedChild`; Windows native focus failed silently pre-load).
- Soft-keyboard pattern (from FastRepro, page is `BasePageReloadable` exposing `KeyboardSize`): bottom spacer `new SkiaControl{HeightRequest=0}.Observe(this, (me,prop)=>{ if(prop==nameof(KeyboardSize)) me.HeightRequest=KeyboardSize; })` + on the scroll `.Observe(this, ...)` set `AdaptToKeyboardFor = Canvas.FocusedChild as SkiaControl; AdaptToKeyboardSize = KeyboardSize;`.
- `AdaptToKeyboardFor/AdaptToKeyboardSize` is ONLY for a focused editor INSIDE that scroll (150ms-delayed calc scrolls a normal scroll to reveal it). NEVER wire it on an inverted chat scroll whose editor lives outside in a send bar with a keyboard spacer: on Android the delayed calc can run before the spacer relayout and applies `ViewportOffsetY -= ~keyboardHeight`, shoving the chat into history — newest message covered, "content stays put, scroll shrank and cut it". The inverted scroll (Rotation=180) keeps its newest-side anchor on viewport resize BY ITSELF — verified 2026-07: keyboard toggle = zero plane re-records, zero cell re-measures, stable offset (DrawnChatList OpenTk `KeyboardTest` probe).
- Tap on an ALREADY-focused editor must re-run the native focus path — fixed 2026-07 in the shared Down handler (`SetFocusInternal(true)` when `IsFocused` is already true; the BindableProperty callback won't refire). Android `ShowSoftInput` uses explicit flags `(ShowFlags)0`, not `Implicit`: implicit requests are ignorable and reliably fail to re-show the keyboard after a BACK dismiss (the hidden EditText never loses native focus — `ClearFocus()` on the only focusable view re-focuses it, so there is no focus transition for the implicit show to ride on).
- Canvas focus rules (all 3 heads + shared `DrawnView.ReportFocus`, final 2026-07): controls CLAIM focus themselves (editor self-focuses on its Down); the canvas decides only on the COMPLETED Tapped. Down/Panning/Up never change `FocusedChild` — clearing focus on Down closed the keyboard mid-gesture, the spacer relayout moved the send button from under the pointer and its Tapped never fired (chat text silently not sent). On Tapped: move focus to the consumer, or clear on a tap over nothing (= outside-tap keyboard dismiss); skip both if focus was claimed during this gesture (`_focusedChildAtDown` captured on Down). `ReportFocus` asks the new target to ACCEPT first (`SetFrameworkFocus(true)`): a non-focusable consumer (send button, shape) leaves the current focus untouched — keyboard stays open across button taps, Telegram-style. Re-entrancy-guarded (`_reportingFocus`) because accepting editors sync back via `SyncSuperviewFocus`.
- Chat "tap messages to dismiss keyboard" is APP-level by design (cells consume Tapped but don't accept focus, so the framework keeps the editor focused): wire `ChatStack.ChildTapped += ... Editor.SetFrameworkFocus(false)`.

## Gestures & Interaction

Two layers must BOTH be on, or handlers never fire: (1) canvas host input mode, (2) control-level handlers.

- Host: MAUI `Canvas.Gestures` = `Enabled` (normal interactive default), `SoftLock` (canvas inside a native ScrollView, cooperates), `Lock` (capture whole input stream — games/fullscreen). Blazor WASM: `GesturesMode` param, opt-in. Blazor Server: no param, control handlers still work. DrawnUi.Net headless: inject gestures yourself.
- Code-behind fluent event patterns (`.OnTapped()`, `.OnLongPressing()`, `.WithGestures(...)`): see **`drawnui-fluent`** skill.
- XAML tap handlers use `Tapped="HandlerName"` — when porting to code, replace with `.OnTapped()`.
- Raw handling: `ConsumeGestures` — handler `(object sender, SkiaGesturesInfo e)`; check `e.Args.Type` (`TouchActionResult`: Tapped/Panning/Up/Down/LongPressing/Cancelled), set `e.Consumed = true` to stop propagation. Keep the handler SYNCHRONOUS; offload async/animation via `Task.Run` inside.
- Gesture data: `e.Args.Event.Distance.Delta` (divide by `control.RenderingScale` for pts), `.Distance.Total` (swipe), `.Location`, `.StartingLocation`, `NumberOfTouches`.
- MVVM XAML attached props (any control): `draw:AddGestures.CommandTapped`, `.CommandTappedParameter`, `.AnimationTapped="Scale|Ripple|Fade"`.
- Propagation: `LockChildrenGestures` (`LockTouch`: `Disabled`(default, pass all)/`Enabled`(consume all)/`PassNone`/`PassTap`/`PassTapAndLongPress`); `BlockGesturesBelow=true` stops touch reaching lower z-layers; `InputTransparent` makes the control ITSELF ignore input (not the things below it).
- Custom controls: override `ProcessGestures(args, apply)` — return `this` = consumed, `null` = pass; never consume Up unless required. Expand hitbox via `CreateHitRect()` override (inflate × `RenderingScale`). Layout gesture events: `ChildTapped`, `Tapped`.
- Canvas accumulates input async and processes it in order at the START of each frame.
- Keep interaction on outer composed control when building custom buttons from nested shapes.
- Touch-not-firing checklist: host `Gestures` mode enabled? handler on the interactive outer control? overlay above with `BlockGesturesBelow`? wrong consumer returned from `ProcessGestures`? `SkiaScroll IgnoreWrongDirection=true` when children need the cross-axis pan.
- Tap-POSITION consumers (ripple origin, press-feedback point, anything needing WHERE inside a control the tap landed) must read `apply.MappedLocation`, NOT raw `args.Event.Location`. The gesture dispatch folds parent transforms + the `SkiaCachedStack` plane blit delta (`RenderTree.Offset`) into `MappedLocation`, but NOT into `apply.ChildOffset`. Raw location works only when there's no plane/transform (then `MappedLocation == Event.Location`). Symptom of the bug: on a cell served from a cached plane (small-cell contact list, chat), the FIRST tap after scrolling a screen away plays the ripple at a scroll-stale Y; the second tap is correct (the first tap's ripple forced a live frame that re-recorded the plane at the current offset, collapsing the delta). Canonical inside-control math = `IsGestureInside`: `MappedLocation + TranslateInputCoords(ChildOffset, accountForCache:true)` vs `DrawingRect`. Fixed 2026-07-21 in `GetOffsetInsideControlInPoints` call sites (`SkiaControl.SendTapped`, `SkiaHotspot`, `SkiaButton`).

## Startup & Init

- MAUI: `builder.UseDrawnUi(new DrawnUiStartupSettings { ... })`. Settings: `DesktopWindow` (`WindowParameters` Width/Height/IsFixedSize — phone-like desktop window), `UseDesktopKeyboard` (KeyboardManager on Windows/MacCatalyst), `MobileIsFullscreen` (drop safe insets/status bar), `Logger` (ILogger consumed by `Super.Log`), `Startup` (`Action<IServiceProvider>` post-init hook).
- Blazor: `await Super.UseDrawnUi(builder).WithBaseUrl(builder.HostEnvironment.BaseAddress).WithOptions(settings).ConfigureFonts(...).PreloadAssets(...).BuildAndRunAsync()`.
- .NET/OpenTK: `Super.UseDrawnUi().ConfigureFonts(...).Build()` ONCE before creating any canvas/window (`BuildAsync()` for async font loading).
- Fonts: `fonts.AddFont("fonts/OpenSans-Regular.ttf", "FontText", FontWeight.Regular)`; emoji via `NotoColorEmoji-Regular.ttf`. Preload: `assets.AddImage("bot.png", "images/bot.png")`.
- MAUI drawn assets live in `Resources/Raw/**` (NOT `Resources/Images`); filenames LOWERCASE — uppercase silently fails to load on iOS.
- `Canvas.RenderingMode` replaced the legacy `HardwareAcceleration` property. Current line = .NET 9 + SkiaSharp v3 (v1.3+); package renamed `AppoMobi.Maui.DrawnUi` → `DrawnUi.Maui`.
- Packages by host: `DrawnUi.Maui` / `DrawnUi.Blazor.Wasm` / `DrawnUi.Blazor.Server` / `DrawnUi.Web` (pure WASM — PackageId verified in csproj; docs pages saying "DrawnUi.Wasm" are stale) / `DrawnUi.OpenTk`(+`.Game`) / `DrawnUi.Net` (headless render, server-side image/PDF, offscreen layout validation).
- Debug/profiling toggles: `Super.EnableRenderingStats`, `canvasView.FPS`/`FrameTime`, `control.DebugShowBounds`, `Super.ShowInvalidatedAreas`, `Super.MaxFps = 30` (iOS battery/heat cap).
- `Super.DisplayException(this, e)` renders an exception on the canvas — wrap `InitializeComponent()`/build code in it during development.

## Invalidation & Custom Controls

- `Update()` = redraw + invalidate own cache; `Repaint()` = redraw WITHOUT destroying cache (position/transform changes); `InvalidateMeasure()` = size/layout recalc; if a parent refuses to refresh, `Parent?.Invalidate()`.
- NEVER dispose SKBitmap/SKPicture/render objects mid-frame — `control.DisposeObject(x)` batches disposal safely at frame end.
- Custom drawing: override `Paint(DrawingContext ctx)` — `ctx.Context.Canvas`, `ctx.Destination`, `ctx.Scale`. Custom layout: override `MeasureAbsolute`/`ArrangeChildren`.
- `Measure` is NOT virtual — override `OnMeasuring` in custom controls.
- Custom composed control pattern: extend a layout, override `CreateDefaultContent()` with `if (Views.Count == 0) AddSubView(CreateView());` guard; aggregate multi-property reactions into one `MapProperties()` called from `propertyChanged` callbacks.
- UI-not-updating checklist: VM implements INPC? property actually raises change (bindable property or `OnPropertyChanged()`)? name matches `nameof()`? custom `OnPropertyChanged` override kept `[CallerMemberName]`?
- Fade-in image on load: subclass `SkiaImage`, override `OnSuccess`, set `Opacity=0.01` then `FadeToAsync(1)`.
- `layout.ApplyItemsSource()` forces ItemsSource rebuild. ViewsAdapter renames: `GetViewForIndex`→`GetExistingViewAtIndex`, `GetViewAtIndex`→`GetOrCreateViewForIndex`.

### SkiaSharp v3/v4 API (DrawnUI pins 4.148+; v2-era snippets won't compile)

- `SKPaint` no longer carries filter quality: `SKPaint.FilterQuality` REMOVED. Pass `SKSamplingOptions` per draw call: `canvas.DrawImage(img, rect, sampling, paint)`. DrawnUI helpers (`DrawnUi.Draw`): `FilterQuality` enum (None/Low/Medium/High/Ultra) + `SkiaSamplingOptions.GetSamplingOptions(q)` and presets `NearestNoMip` (1:1 blits), `LinearNoMip`, `LinearLinear` (smooth up/down-scale), `MitchellCubic` (best upscale), `CatmullRomCubic` (sharper, may ring). `CachedObject.Draw` overloads take `in SKSamplingOptions`.
- `SKPaint` no longer carries text state: `Typeface`, `TextSize`, `TextAlign`, `MeasureText`, `GetTextPath` etc. moved to `SKFont`. Draw text as `canvas.DrawText(text, x, y, font, paint)` — font geometry from `SKFont`, color/shader/stroke from `SKPaint`. Text AA via `SKFontEdging` on the font (`SubpixelAntialias`/`Antialias` — DrawnUI switches on `Super.FontSubPixelRendering`), not paint flags.
- SKSL scalar uniforms must be `float`, not `float[1]` (see drawnui-fluent shader section).

## Caching Guidance

### A control with an IPostRendererEffect NEVER blits from its cache

`SkiaShaderEffect`, `SkiaBackdrop` and anything else implementing `IPostRendererEffect` bypass the cached blit entirely. In `SkiaControl.Shared.cs` the draw path is literally `if (EffectPostRenderers.Count == 0) { cache.Draw(...) }` followed by `foreach (var postRenderer in EffectPostRenderers) postRenderer.Render(context);` — so when the list is non-empty the cache is NOT drawn and the effect re-executes **every frame**: build uniforms, `Compiled.ToShader(...)`, `DrawRect`, then `Canvas.Flush()`. A `Canvas.Flush()` per effect per frame is a GPU pipeline stall, so the cost scales with the NUMBER of shader-carrying controls on screen and is nearly independent of how simple the SKSL is.

Consequences, all verified from source:
- **Never remove an image cache from a control that carries a shader effect.** `GetPrimaryTextureImage` (mode `Always`, the default) returns `Parent?.CachedImage` when present, and otherwise falls back to `CreateSnapshot` = `Canvas.Flush()` + `Surface.Snapshot()` EVERY frame. The cache on such a control is not a redundant "nested cache" — it is the effect's input texture. Removing it makes the control dramatically more expensive, not cheaper.
- **N live shader-carrying controls on one screen is an N-stall-per-frame design.** A filter-picker strip of live shader thumbnails over a camera preview measured 60fps → 36fps (+11ms/frame) for ~8 visible thumbs on a high-end Windows GPU. Hiding the strip restored 60fps.
- **If the shaded content changes far slower than the frame rate, do not use a live effect at all.** Bake the shaded result into an `SKImage` once per content change and show it in a plain cached `SkiaImage` with no `VisualEffects`. Thumbnails refreshed every few seconds must never re-run their shader 60 times a second.
- `UseBackground = PostRendererEffectUseBackgroud.Once` freezes the input snapshot (removing the per-frame `Surface.Snapshot`) but the `Render` still runs per frame — it is a mitigation, not a fix.
- Diagnosing: FPS drops proportional to how many shader-carrying controls are VISIBLE, and does not improve when you switch to a simpler shader. That signature means per-frame effect dispatch, not shader cost.

### Group before you cache (layer grouping)

Caching is a decision about SUBTREES, not about individual controls. Scattered siblings sitting over a surface that repaints every frame (camera preview, GL scene, video, animated canvas) are traversed and drawn ONE BY ONE on every frame even when each of them is individually cached — the parent still walks them, applies transforms and blits N times. Wrap such a set of controls in ONE container (`SkiaLayer` for overlay/absolute, `SkiaStack`/`SkiaRow`/`SkiaGrid` where the arrangement demands it) and cache THAT container: the live surface then pays a single blit per frame.

Applies to: camera/GL overlays (shutter row, top bar, badges, HUD), static app chrome over any animated content, side panels, toolbars.

Pick the cache type by the GROUP's size and its own change rate:
- small / medium group, redraws relatively often on state change → `GPU` (GPU-resident bitmap, blit with no re-upload)
- large / near-fullscreen group → `Operations` (recorded draw commands, avoids holding a huge bitmap; also the safe choice when memory matters more than blit cost)
- large group, visually static, memory budget fine → `Image`
- group whose children change INDEPENDENTLY of each other (document/artboard/HUD with separate live readouts) → `ImageComposite` (re-records only the dirty child)
- any child inside the group that repaints EVERY frame does not belong in the group — pull it out as a sibling with no cache, so it never dirties the whole layer

Constraints when grouping: keep the GPU cache at the TOP grouped container, never nested inside another GPU-cached/GPU-rendered subtree; never put `Operations` above a `GPU`-cached child (GRContext change can crash); once the group is cached, descendants usually stay uncached by omission unless they have their own separate reuse case.

- Omitted `UseCache` means the CONTROL'S OWN default, which is NOT always `None` — see the defaults table below. Layouts, `SkiaImage` and `SkiaButton` do default to `None`, so for those treat caching as opt-in, added only where reuse value is clear.

| Default | Controls |
|---|---|
| `Operations` | `SkiaLabel`, `SkiaShape`, `SkiaSvg`, `SkiaEditor`, `SkiaScrollBar`, `SkiaSpriteSet` (needs Left+Top), `SkiaWheelPickerCell` |
| `OperationsFull` | `SkiaHoverMask` |
| `ImageDoubleBuffered` | `SkiaLottie`, `SkiaGif`, `SkiaSlider`, `SkiaProgress`* |
| `Image` | `SkiaWheelShape`, `SkiaRadioButton`* |
| `None` | `SkiaSprite`, `SkiaCachedStack` (owns its own plane cache), layouts, `SkiaImage`, `SkiaButton` (its intended `OperationsFull` is disabled behind a todo) |

\* set in `CreateDefaultStyleContent()`, so it applies to the default look only — a custom-templated `SkiaProgress`/`SkiaRadioButton` gets no cache. All others are set in the constructor.

Conditional: `AutoCache` on `SkiaScroll`/`SkiaDrawer` sets THEIR OWN `UseCache = Operations` once Content exists — deliberate exception to "never cache a scroll".
- No cache, `UseCache="None"`: use when subtree is cached and you don't need to cache more. Or if the control is will be updating with new content with frequency close to canvas fps. - GPU-backed cache should preferably live at top cached container, not nested deeper in subtree. Nested GPU cache is possible only when you explicitly know that case is safe and beneficial.
- `UseCache="GPU"`: use when layer is worth being stored as a GPU-backed bitmap. Best for small overlays with high frequency of redrawing, prefer small top overlays.
- `UseCache="Image"`: use when layer is worth being stored as a CPU-bitmap and redrawn from it later instead of recalculations/logical paintings.
- `UseCache="Operations"` will be stored as drawing commands. Must never be used over any child below having `GPU` tye of cache - might crash if GRContext changes. Useful for small vector-based controls or large animated containers to avoid storing large bitmaps.
- Decide caching from both subtree cost and parent redraw frequency. Over live surfaces that repaint every frame, even small overlay controls can benefit from top-level cache if their own visuals are mostly stable between state changes.
- Simple capture buttons over live camera preview can justify top-level cache because camera invalidation would otherwise force subtree traversal and redraw every frame.
- Frequently mutating does not automatically mean no cache. First ask what causes redraws: parent surface every frame, or subtree visuals themselves every frame. Cache helps in first case if subtree is stable; cache often loses in second case.
- If parent cache already captures the whole reusable subtree, descendants commonly stay non-cached by omission unless they have their own separate reuse case.
- If visual bug or native crash appears, inspect cache placement first.

### Cache machinery (only when debugging the cache system itself)

- `UseCache` is what you set, `UsingCacheType` is what runs (`Draw/Base/SkiaControl.Cache.cs:555-604`), resolved in order: (1) `!AllowCaching || !Super.CacheEnabled` → `None`; (2) if `CanUseCacheDoubleBuffering && Super.Multithreaded`: `None`→`OperationsFull`, `ImageDoubleBuffered`/`GPU`→`Image`, `ImageComposite`/`ImageCompositeGPU`→`Operations`; (3) `ImageDoubleBuffered && !CanUseCacheDoubleBuffering`→`Image`; (4) `GPU`/`ImageCompositeGPU` without `Super.GpuCacheEnabled`→`Image`/`ImageComposite`; (5) `None` + double-buffering + Multithreaded + parent is SkiaControl → `Operations`; (6) else `UseCache`. Nothing overrides `UsingCacheType` — customize via `CanUseCacheDoubleBuffering`/`AllowCaching`. Code branches on `IsCacheComposite`/`IsCacheImage`/`IsCacheGPU`, not enum equality: a new cache type must be added to all three plus both `CreateRenderingObject` paths. `SkiaCacheType.Auto` is declared but never implemented (zero references).
- Globals (`Super.cs`): `CacheEnabled` (true, master off-switch), `GpuCacheEnabled` (true), `Multithreaded` (false, experimental — set `OffscreenRenderingAtCanvasLevel` true alongside it).
- Offscreen bakes are NOT `Task.Run`: `OffscreenRenderingService` runs `Clamp(ProcessorCount/2, 2, 4)` dedicated `AboveNormal` threads, because on the shared threadpool latency-critical bakes queue behind app startup work and the pool grows only ~1 thread/500 ms. On WASM they drain INLINE instead (see `drawnui-blazor` skill).
- `CacheValidityType { Valid, Missing, SizeMismatch, GraphicContextMismatch }` — `GraphicContextMismatch` = GRContext handle changed (app background→foreground kills every GPU-backed cache).
- `PreserveSourceFromDispose` on the OLD `CachedObject` when its surface is transferred to a new one (ImageComposite / double-buffer swap) so its Dispose skips the now-shared surface. Getting it wrong = "black/empty render right after a cache swap".
- `SurfaceCacheManager` pools CPU surfaces per (w,h): max 10 per size, 100 tracked sizes. GPU surfaces bypass the pool → `DisposableManager`, held 3 frames (may still be referenced by an in-flight frame).
- `CachedObject.Bounds` on image-backed caches is the SURFACE extent INFLATED by effects margins; the logical control rect is `RecordingArea`/`LogicalBounds`. `ExpandDirtyRegion` is now only one input — `SkiaControl.Effects.cs` takes the per-side max of it and the auto effects margin.
- Dirty children iterate allocation-free via `CollectionsMarshal.AsSpan(DirtyChildrenTracker.GetList())` — keep it that way.

### ImageDoubleBuffered semantics + blank-blink mechanism (fixed 2026-07)

- `ImageDoubleBuffered` NEVER paints sync: missing/unusable cache → `DrawPlaceholder()` (default no-op = EMPTY) + async bake on the per-control offscreen queue (`LimitedQueue(1)`, latest wins). Sync-create-when-missing is `Image` cache behavior only.
- Fallback design: invalidation sends front `RenderObject` → `RenderObjectPrevious`, which draws while rebake lands. BUT `DestroyRenderingObject()` also sets `RenderObjectPreviousNeedsUpdate = true` (poison) → next draw KILLS previous before the draw decision → nothing drawable → blank frames until bake. Poison is correct ONLY for recycled rebinds (stale pixels = wrong content).
- Historic blink triggers (both fixed in lib): (1) `InvalidateMeasureInternal` hard-destroyed on ANY remeasure REQUEST — even same resulting size (e.g. SkiaImage bitmap arrival) — now for ImageDoubleBuffered keeps front + `NeedUpdateFrontCache=true`; (2) `SkiaDynamicDrawnCell.AttachContext` destroyed on every rebind — now gated by `protected virtual bool DestroyCacheOnContextChange => true` (override to `false` in non-recycled cells like chat: stale pixels 1-2 frames beat blank).
- Actual size change: size-mismatch branch in `UseRenderingObject` refuses stale-size cache (no stretch); lib now sync re-records at the new size (`TrySyncRebuildStaleSize`, guarded by `_offscreenBakeBusy` so it never paints concurrently with a bake). Fires only on resize of an already-cached control — cold cells stay async (zero-spike scroll preserved).
- Debug tell: content shows OK, then EMPTY, then OK again = destroy-then-async-rebake window, not "cache not ready".

### Shared cache per control TYPE (CacheSharing)

- `CacheSharing = CacheSharingType.Shared` (`SkiaControl.Cache.cs`): all instances of the SAME control type on the same Canvas share ONE `CachedObject` instead of each allocating their own. Eligible cache types: `Operations`, `Image`, `GPU` (per `IsSharedCacheEligible`; the XML doc says Image/GPU only — code includes Operations too).
- Pattern: create a dedicated subclass with FIXED identical visuals baked into the constructor, e.g. `class IncomingBubbleSign : SkiaShape` (polygon points, color, size, `UseCache=Image`, `CacheSharing=Shared`). Thousands of recycled-list cells each hold their own instance, but physically one cache surface is rendered once and blitted everywhere. Example: chat bubble deco-triangle tails as `IncomingBubbleSign`/`OutcomingBubbleSign` subclasses shared by all cells.
- Constraint: instances MUST be visually identical (same size/colors/content) — the cache is keyed by type, any per-instance visual difference would show the wrong pixels.
- Disposal: individual control disposal does NOT release the shared cache; free via `SuperView.Cache.Free<T>()` or let the Canvas dispose it.
- Composes with `IsGhost` (occupies layout, not drawn) — e.g. ghost tails keep bubble alignment in follow-up messages of a same-sender group while only group-first messages draw the shared tail.

## Shader Effects (SkiaShaderEffect / SkiaBackdrop)

- Use `ShaderCode` (inline SKSL string) instead of `ShaderSource` (file path) on the `DRAWNUI_NET` / OpenTK target — `LoadFromResources` throws there. `ShaderCode` bypasses file loading entirely and compiles the string directly.
- `SkiaBackdrop` + `VisualEffects` + a `SkiaShaderEffect` subclass (e.g. `GlassBackdropEffect`) works on the OpenTK target. The backdrop snapshots `ctx.Context.Surface` which is the same GPU framebuffer used by raw GL — so GL-drawn content (cube, 3D scene) IS captured and passed to the shader as `iImage1`.
- For liquid glass over a GL scene: place `SkiaBackdrop` as the first child inside the `SkiaShape` panel. Do NOT put GPU cache (`UseCache = GPU`) on the outer shape — that caches the backdrop snapshot and prevents live updates each frame.
- Match `GlassBackdropEffect.CornerRadius` to the parent `SkiaShape.CornerRadius` (in points, not pixels — the effect multiplies by `RenderingScale` internally).
- `SkiaBackdrop.Blur = 0` when using a custom shader effect — the shader handles its own blur.

### Shader slide transitions (SkiaShaderCarousel / ShaderTransitionEffect)

- `SkiaShaderCarousel` (`DrawnUi.Controls`): `SkiaCarousel` subclass where slides never translate — an attached `ShaderTransitionEffect` (a `ShaderDoubleTexturesEffect` adding `progress` + `ratio` uniforms) blends the from/to cells' cached images. Give it a gl-transitions style `transition(vec2 uv)` via `TransitionShader` (Resources/Raw path), `TransitionShaderCode` (raw SkSL string — required on OpenTK/DRAWNUI_NET and in the fiddle), or a custom `TransitionTemplate`. Cell templates MUST use `UseCache = Image` (the effect samples cell RenderObject caches); ctor forces `RecyclingTemplate.Disabled`.
- `InterruptedTransitionMs` (default 50): a swipe during a running transition wraps the current transition up in ~that time, then plays the next. Gesture targeting is deterministic: max ONE slide per gesture, direction from velocity (threshold 100) or displacement, target computed from the gesture-origin snap — never from nearest-to-finger.
- Custom effect subclass: override `protected virtual ShaderTransitionEffect CreateTransitionEffect()` — called once from ctor; effect is attached lazily on first `Render`.
- AspectFit letterboxed slides (photo viewer case): the shader spans the whole cell, so e.g. the cube transition's reflection projects at SCREEN bottom instead of under the photo. Fix = a band-clipped effect subclass: (1) override `Render`, intersect `ctx.Destination` with the union of from/to inner `SkiaImage.DisplayRect`, call `base.Render(ctx.WithDestination(clipped))`; (2) override `SyncEngineState` and set `GetEngine().Offset` to the clipped destination origin (base sets it to TEXTURE bounds origin, so uv would not be [0,1] over the band); (3) cell textures stay full-size, so remap sampling in a custom template: `getFromColor/getToColor` sample `(uBandOffset + float2(uv.x, 1.0-uv.y) * uBandScale) * uTexResolution` with those uniforms added in `CreateUniforms` (band rect vs stored textureBounds ratios). NB `iResolution` AND `iImageResolution` are both destination-sized (`engine.CreateUniforms(destW, destH, destW, destH)`) — after clipping they are band-sized, so the full texture pixel size needs its own uniform.
- Single-texture variant of the same hack: `ClippedShaderEffect` — only intersect destination with `SkiaImage.DisplayRect`, no remap needed, because with `AutoCreateInputTexture` the snapshot is taken FROM the clipped destination, so texture == band content exactly.

## Visual Structure

- For custom shutter/camera buttons, prefer composed `SkiaShape` outer ring + inner shape/disc.
- Use `ZIndex` for overlays above camera/content.
- Keep bottom controls in dedicated overlay container when alignment inside mixed layered content becomes unstable.

## Runtime Checks

- If UI exists but looks wrong, verify: parent layout, `HorizontalOptions`, `VerticalOptions`, `Margin`, `ZIndex`, clipping, cache placement.
- If control not visible, verify it is inside DrawnUI content tree, not hidden behind another layer, and parent fills available space.
- If touch does not fire, verify gesture/tap handler is on interactive outer control and no blocking overlay sits above it.

### On-device (Android) runtime validation
- Frame smoothness ground truth: real gestures via `adb shell input swipe` + `adb shell dumpsys gfxinfo <pkg> reset` / dump — the accelerated DrawnUi canvas goes through Choreographer. CAVEAT: gfxinfo only measures frames that were SUBMITTED — a blocked render loop produces no frame at all, so multi-100ms stalls are INVISIBLE in its percentiles (observed: p99=15ms while a WasDrawn-gap probe showed 300ms+ holds). Always pair gfxinfo with an offset/frame-gap probe on `DrawnView.WasDrawn`. Do NOT judge smoothness from `SkiaScroll.Scrolled` events (sparse, not per-frame) or from repaint counts at rest.
- Offscreen double-buffer bakes run on DEDICATED worker threads (`OffscreenRenderingService` in SkiaControl.Cache.cs), NOT Task.Run: on the shared threadpool, startup bakes queued behind app tasks and the ~1-thread/500ms injection rate made cold cells materialize one by one over seconds.
- `DrawPlaceholder` (cold ImageDoubleBuffered cell awaiting first bake) is an EMPTY virtual — override it for skeleton UX. Two verified traps: it can run BEFORE the cell is bound (BindingContext null — provide a neutral fallback), and it paints in a pass WITHOUT the control's own Rotation transform (a 180°-rotated chat cell needed mirrored alignment: outgoing=Left in placeholder space to appear right on screen).
- Per-frame managed hook: `DrawnView.WasDrawn` fires for every drawn frame (subscribe via `MainScroll.Superview`). Note: frames also fire while offset is stationary (loads/measure repaints) — filter by offset delta if measuring scroll cadence.
- net10-android `Console.WriteLine` lands in logcat tag `DOTNET` (not `mono-stdout`).
- Build config per bug class: STRUCTURAL/correctness bugs -> Debug builds (minutes-faster deploy cycle, no AOT); Release only when validating performance/feel or GC/timing-sensitive behavior. Never burn Release AOT cycles on a structural repro.
- Bound every device wait: `am start` can fail silently — verify `pidof` within ~15s, and give every logcat-marker poll a deadline instead of an unbounded until-loop.
- `Explicit concurrent copying GC` lines in logcat ≈ managed (Mono bridge) full GCs — one per second during scroll means allocation pressure in the draw path; correlate with `GC.GetTotalAllocatedBytes` per frame.
- Switchable in-app auto-test driver pattern: shared partial (e.g. `ChatPage.AutoTest.cs`) with a single `public static bool AutoTestEnabled = false` flag + `partial void MaybeStartAutoTest()` called at init; driver logs `[AUTOTEST] PASS/FAIL` and the app stays fully normal when the flag is off.

### Ordered ScrollToIndex semantics (MeasureVisible)
- An `OrderedScrollToIndex` is HELD until ARRIVAL, not until issue: while pending it gates LoadMore for the whole animated flight, re-aims if content resizes mid-travel, re-issues on stall (≤2) then accepts, and is cancelled by user touch (Down). Under `MeasureVisible` it also waits until `LastMeasuredIndex >= target` and kicks `KickBackgroundMeasurement()` when measurement is idle — never resolve a jump against estimated extents.
- Background measurement invariants: only the current pass may clear `_isBackgroundMeasuring` (generation/cts identity check — a stale task's finally must not clobber it), and the measure loop must not break on its OWN staged BackgroundMeasurement changes (only on real structure mutations), else it starves to one batch per draw.

## Visual Effects (VisualEffects, shadows, glow)

- Every `SkiaControl` has a `VisualEffects` collection (`IList<SkiaEffect>`). Effects: `IImageEffect` (SKImageFilter), `IColorEffect` (SKColorFilter), `IRenderEffect`/`BaseChainedEffect` (wrap paint), `IPostRendererEffect` (after paint, shaders/backdrop), `IStateEffect`, `ISkiaGestureProcessor`.
- Built-in shadows: `DropShadowEffect` (Blur=sigma, X/Y offset in points, Color), `OuterGlowEffect` (symmetric, no offset), `ChainDropShadowsEffect` (collection of `SkiaShadow` for multi-shadow).
- Shadows/glow paint OUTSIDE `DrawingRect`. When a control is cached, the cache surface + clip are bounds-sized → shadow would be clipped. DrawnUI auto-expands cache/clip/dirty-region so this works. Do NOT require `ExpandDirtyRegion` for built-in shadow/glow — attach effect and it shows, cached or not.
- Mechanism: `SkiaEffect.GetEffectMargin(float scale)` returns per-side overflow in PIXELS (default `Thickness.Zero`). Control aggregates per-side max into `EffectsMarginPixels` (cached; recomputed on effect add/remove/replace or param/scale change via `InvalidateEffectsMargin`, which also invalidates cache). Three sites consume it through `GetRenderingExpandPixels()`: `GetCacheArea` (surface), `DrawWithClipAndTransforms` clip, and `DrawingRect`→`DirtyRegion`.
- Shadow margin = `3 * Blur` per side (+offset for DropShadow). `3σ` matches Skia's own blur filter bounds (`ceil(3*sigma)`); beyond that alpha <0.3%. SkiaSharp does not bind `computeFastBounds`, so this is computed in managed code — same result for Gaussian blur.
- Blur is treated as PIXELS (don't scale it); offsets (X/Y) are points (scale them). Mirror in custom effects.
- Legacy `SkiaShape.Shadows` (SkiaShadow list) and MAUI `Shadow` (PlatformShadow) are ALSO wired into this expansion (2026-07): base `ComputeEffectsMargin` merges PlatformShadow; `SkiaShape.ComputeEffectsMargin` override merges `Shadows` (skipped when PlatformShadow present — mirrors paint precedence). Legacy SkiaShadow sigma = `Blur*scale` (scaled, unlike effects' pixel Blur) → margin per side `3*Blur*scale ± offset*scale`. No wrappers or reserved padding for shadowed cached shapes; layout size stays shadow-free by design (CSS/Android convention). A/B harness proof: cached 40pt circle, Blur 8 / Y 12 → 0 shadow px below rect without the merge, 324 with.
- Overflow also survives ANCESTOR cache/clip boundaries via `AggregatedEffectsMarginPixels` (2026-07): own margin ∪ all children's aggregates, recursive, cached, position-agnostic (child overflow expands every side — a few extra cache px, no per-layout re-agg). `GetRenderingExpandPixels` (cache surface, Operations cull rect, OUTER clip, dirty region) reads the AGGREGATE — everywhere, unconditionally. FINAL design (2026-08, all clip-site-discriminator variants rejected): **the outer clip in `DrawWithClipAndTransforms` is an EFFECTS/cache concern and must NEVER be relied on for children containment; a viewport control contains its children ITSELF, inside its Paint — the SkiaShape pattern.** `SkiaScroll.Paint` and `SkiaCarousel.RenderViewsList` wrap child drawing in Save/`ClipSmart(ClipContentPath)`/Restore, GATED on `GetRenderingExpandPixels() != Thickness.Zero` (nothing overflows → outer clip already exact → inner clip skipped, zero cost for plain scrolls). Unified API in base `SkiaControl`: `protected SKPath ClipContentPath` (single reused path, base-disposed) + `protected virtual SKPath GetContentClip(object arguments = null)` = `ClipWith.CreateClip` if set, else virtual `CreateClip` + `Clipping` delegate — never a hardcoded rect. Scroll/carousel call `ClipSmart(canvas, GetContentClip())`; `SkiaShape` fills the same base field but deliberately keeps its direct `CreateClip(arguments,...)` build (stroke-aware args; its children clip does NOT honor ClipWith/Clipping — preserving pre-existing shape behavior). `SkiaEditor` needs nothing (IS a SkiaShape — children already path-clipped); `SkiaImageTiles` needs nothing (no effect-bearing descendants). Why: one rect can't admit shadow pixels but block content pixels; the inner clip runs INSIDE `PaintWithEffects`, so the own-effect filter captures already-contained content — shadow ON a scroll paints beyond bounds while scrolled content never does. Harness-proven all four simultaneously: scroll leak 0/0 (was 30px with blur-9 descendant shadow — the Racebox PageResult regression), tight image-cached wrapper descendant shadow 38px intact (July aggregation feature), scroll own DropShadowEffect renders on all sides, 0px content bleed into the shadow band. REJECTED discriminator variants, do not retry: strict-when-`IsClippedToBounds` (kills shadow-on-scroll), own-margin-for-everyone at clip site (user vetoed semantics), reflection override-detection (hot-path lookup, AOT-fragile), `WillClipBounds`-based strictness (true for every image-cached control → cut cached-wrapper shadows 38px→0, A/B proven). Harness-proven: strict viewport 0px leak, green `DropShadowEffect` glow on scroll renders on all sides, 0px content in the shadow band. Checking bare `WillClipBounds` for strictness is WRONG — true for every image-cached control, would revert the shadow auto-expand feature. Invalidation bubbles to all ancestors from: shadow param change (`SkiaShadow.RedrawCanvas`), Shadows collection change, `PlatformShadow` set, `OnChildAdded/OnChildRemoved`. Verified live: mutating a slider thumb's shadow Blur/Y at runtime re-expanded root margin 7→18 px and rendered below the control rect through 3 cache levels.
- **Staged structure changes need a cache invalidation** (fixed 2026-08): templated `SkiaLayout` collection changes are staged into `_pendingStructureChanges` and drained by `ApplyStructureChanges()`, which runs ONLY inside `Paint`. A layout with `UseCache != None` re-blits its cached image and never repaints on its own, so the change sat pending forever — items appended to `ItemsSource` never appeared and an autosized stack froze at its first item's height (Racebox TabMeasure: first result rendered, all later ones invisible, `stackH` stuck at one cell). `Repaint()` is NOT enough (it only asks the PARENT to redraw, cache kept); `Update()` = `InvalidateCache()` + update. Fix invalidates the cache at the three staging sites: `StageStructureChange` (all collection changes), `IntegrateMeasuredBatch` (BackgroundMeasurement batches), single-item remeasure (`SingleItemUpdate`). Semantically free: a pending structure change means the cached pixels ARE stale. Known still-open, separate root: templated + **MeasureVisible** + AUTOSIZED height + append-at-end never grows (fails with cache AND without — `ApplyStructureChanges` early-returns for `StartIndex > LastMeasuredIndexLocal` expecting background measurement to catch up, which an autosized layout never kicks off).
- When debugging a "clipped shadow" report: faint stock shadows (e.g. Material thumb 0.3-opacity black, blur 2) can LOOK like a hard clip at a tangent edge — mutate the shadow loud (red, opacity 1, bigger blur/offset) via the control tree before concluding clipping.
- `CachedObject.Bounds` semantics TRAP (bit us 2026-07): for image-backed caches Bounds = surface area INFLATED by effects margins; the logical recorded rect is `RecordingArea` (exposed as `CachedObject.LogicalBounds`). Any gesture translation (`TranslateInputCoords`), position-delta (`CalculatePositionOffset`), or composite dirty-offset math using Bounds as "recorded position" shifts by the margin the moment ANY descendant has a shadow. Symptoms: dead top/left tap strip on controls inside a cached container (composite stack with one Cupertino slider inflated the whole stack); stale color ghosts at top/left after animations (composite bg-repaint clip shifted, strip never overpainted). Repro recipe: ImageComposite stack + one shadowed child + GestureRobot tap on the top 2px strip of a switch + orange-pixel scan after toggle-OFF.
- `ExpandDirtyRegion` (Thickness, points) is the manual override for custom `Paint` bleed or effects that don't report a margin. Final expand = per-side max(auto effect margin, ExpandDirtyRegion*scale).
- **Post-effects overlay stage (where ripple draws)**: `control.PostAnimators` (`IOverlayEffect` list) render in `OnAfterDrawing` — ABOVE the control's content and children, at its true drawn position incl. transforms, over cached blits too. For overlay chrome (selection frames, debug bounds, badges): one-liner `.WhenPainted((ctx, c) => { ...draw...; return false; })` (wraps `ActionOverlayEffect`); reusable = subclass `RenderingAnimator`, override `OnRendering` (helpers `GetSelfDrawingLocation`, `DrawWithClipping`), add to `PostAnimators` + `Repaint()` — no Start needed for static overlays. Return value contract: true = request continuous repaint (animated), false = static. NEVER mutate layout properties from a paint-stage hook (`WhenPaint` runs inside base.Paint BEFORE children; mutating Width/TranslationX there aborts the paint pass — draw only). This beats tracking another control's position with an adorner child: the overlay is always in sync by construction.
- Writing a custom effect that paints beyond bounds: override `GetEffectMargin`. Effects staying inside bounds (color filters, in-place shaders) inherit `Zero`, no override.
- `CachedObject`: `Bounds` = inflated image extent, `RecordingArea` = logical DrawingRect (non-inflated). Draw offset = `Bounds.Left - RecordingArea.Left`. Validity check compares `RecordingArea.Size` (logical) — keep these two distinct or you get nonstop cache rebuild (sizes never match) or wrong blit offset (object shifted by expand).
- Article: `docs/articles/controls/effects.md`.

## OpenTK / Mixed GL+DrawnUI

Trigger: `CanvasHost`, `DrawnUiWindow`, `RenderScene()`, overlay on 3D scene, mixing GL and Skia, cube/mesh invisible while background shows, GL state corruption symptoms.

**Problem**: Skia does NOT restore GL state after compositing on a shared framebuffer/context. Left dirty: `GL_VIEWPORT` (Skia's internal coords → your geometry maps off-screen), `GL_STENCIL_TEST` enabled (all fragments fail stencil → nothing draws), `glDepthMask(false)` (`GL.Clear(DepthBufferBit)` no-ops → stale depth corrupts tests), partial color mask. Symptom: clear color visible, 3D geometry invisible; first frame fine, breaks from second frame on.

**Fix — two-way contract, BOTH sides required:**

```csharp
// BEFORE your GL draw, every frame — restore what Skia left dirty:
GL.Viewport(0, 0, clientWidth, clientHeight);
GL.Disable(EnableCap.StencilTest);
GL.DepthMask(true);
GL.ColorMask(true, true, true, true);
GL.ClearColor(r, g, b, 1f);
GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
GL.Enable(EnableCap.DepthTest);
// ... your draw calls ...
GL.Finish();

// AFTER your GL draw, before Skia renders — tell Skia its cached VAO/program/state is stale:
grContext.ResetContext();   // CanvasHost: _host.ResetGrContext()
```

- `CanvasHost` (overlay over your own `GameWindow`): restore block → draw scene → `GL.Finish()` → `_host.ResetGrContext()` → `_host.Render()` → `SwapBuffers()`. Canvas must use `RenderingMode = RenderingModeType.AcceleratedRetained` + `BackgroundColor = Colors.Transparent` so Skia doesn't clear the framebuffer before compositing.
- `DrawnUiWindow` base: `RenderDrawnUi()` already contains the restore block + `ResetContext()` — just override `RenderScene()` (enable depth test, draw, end with `GL.Finish()`).
- Same class of issue applies to any 2D-over-GL compositor (SkiaSharp raw, Flutter embedding, ImGui, NanoVG).

### OpenTK raw assets (Images / Lottie / fonts) — output-dir relative
OpenTK has NO MAUI asset pipeline. A source string like `"Images/banana.gif"` / `SkiaLottie.Source="Lottie/x.json"` resolves **relative to the OUTPUT dir** (next to the exe). The asset MUST be copied there via `Content` + `CopyToOutputDirectory`:
```xml
<Content Include="Images\**"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content>
```
**Gotcha (the #1 "asset won't load" cause):** a bare `<Content Include="Images\x.gif" />` in a `WinExe`/console SDK project is NOT copied by default — build succeeds, file shows in IDE, but runtime can't find it and it silently fails to render. Always add `CopyToOutputDirectory="PreserveNewest"`; verify with `ls bin/Debug/<tfm>/Images`. Per-head asset roots differ: OpenTK = output dir, MAUI = `Resources/Raw/**`, Web = `wwwroot/**` — keep the asset in every head you ship. `OpenTkPong.csproj` is the reference pattern. Docs: `docs/articles/opentk/resources.md`. NEVER assume an asset loads on a head without verifying it lands in that head's asset root.

## DrawnUi.Web (pure WebAssembly, no Blazor)

Package `DrawnUi.Web` — DrawnUI in the browser as a standalone pure-WASM app. NOT Blazor: only `[JSImport]`/`[JSExport]` interop, no `IJSRuntime`/`ElementReference`/Razor. Built on the `DRAWNUI_NET` base (SharedNet + Net shims, same base as OpenTK), NOT the Blazor `DrawnUi.Blazor.Core` partials. Source: `src/Web/DrawnUi.Web/`. Docs: `docs/articles/web/index.md` + `getting-started.md`.

- Entry: `Super.UseDrawnUi().ConfigureFonts(...).ConfigureStyles(...).RunAsync("canvas-id", () => new Canvas { ... })`. `RunAsync(elementId, factory)` (`BrowserHost.RunAsync`) wires rendering, input, `requestAnimationFrame` loop, resize, fonts, gestures.
- `Canvas.RenderingMode`: `Accelerated` = WebGL (GPU, auto-falls back to raster), `Default` = raster (`putImageData`). RenderingMode must be final BEFORE `AttachCanvasView` (host handles this) — changing it after attach disposes the view and kills the loop.
- Host shape: static `index.html` with one `<canvas id="drawnui-canvas">` (set `touch-action:none`) + `main.js` loader importing `./_content/DrawnUi.Web/drawnui-web.js`; csproj uses `Microsoft.NET.Sdk.BlazorWebAssembly` SDK + `WasmBuildNative=true` + `SkiaSharp.NativeAssets.WebAssembly` + `HarfBuzzSharp.NativeAssets.WebAssembly`; `DefineConstants` include `DRAWNUI_NET;WEB;BROWSER`.
- **Fonts**: `WasmFilesToBundle` is a NO-OP in the .NET WASM SDK (not consumed anywhere — verified) → VFS `SKTypeface.FromFile` never finds the file. Fonts must be served as STATIC WEB ASSETS under `wwwroot/fonts/` and registered with a relative path; `SkiaFontManager.InitializeWebAsync` fetches them over HTTP (`HttpClient` → `SKTypeface.FromData`), mirroring the Blazor preload. Lib-level, automatic via `BrowserHost.RunAsync`. Do not tell users to use `WasmFilesToBundle`.
- **Styles**: `ConfigureStyles(styles => styles.AddStyle(new Style{ TargetType=..., ApplyToDerivedTypes=..., Setters={...} }))` works (shared `DrawnUiBuilder`, both Net and Web). Explicit per-control property setters WIN over styles (`ExplicitPropertiesSet`) — a hardcoded `FontFamily="X"` overrides a style's font.
- **Gestures**: `GesturesMode.Enabled` routes pointer/touch/wheel to DrawnUI. `GesturesMode.Lock` additionally applies CSS guard at lib level (`applyGestureStyle` in `drawnui-web.js`: `touch-action:none; user-select:none` on canvas + `overscroll-behavior:none` on page + non-passive `touchmove` preventDefault) to stop iOS page panning / swipe-to-close — Web has no AppoMobi `TouchEffect`, so this CSS+JS guard replaces it.
- **Hot reload**: built-in scene rebuild, fully automatic (no debugger gate — works under bare `dotnet watch`; inert in published apps), mirrors MAUI `Super.HotReload`/`BasePageReloadable`. `[MetadataUpdateHandler]` `DrawnUi.HotReloadService` (debounced) → `Super.HotReload` → `BrowserHost.RebuildScene()` re-runs the `RunAsync` factory + re-attaches, REUSING the same `WebSkiaView`/GL context. Per-frame method-body edits apply live; the factory must rebuild the whole tree (don't return a cached instance). No subclass needed. Files: `src/Web/DrawnUi.Web/HotReload.cs`, `Super.Web.cs`, `BrowserHost.cs`. XAML hot reload n/a (code-only).
- GPU traps and headless CDP validation: see `src/Web/DrawnUi.Web/CLAUDE.md`.
- Samples: `src/Web/DrawnUi.Web.Sample` (minimal), `src/Web/Samples/PongWeb` (full game; live demo pong.appomobi.com).

## Recycled Cells — Strategy Picker

| # | Use case | Recipe |
|---|----------|--------|
| 1 | BindableLayout-style small static list (each item keeps its own view) | `MeasureAll` + `RecyclingTemplate.Disabled` |
| 2 | Many UNIFORM-height rows, LARGE cells (Instagram/Twitter cards, few per screen) | `RecyclingTemplate.Enabled` + `MeasureFirst` (hard requirement: truly uniform heights — any variance → MeasureVisible) |
| 3 | Many UNIFORM rows, SMALL height (phone book, dozens per screen) | `SkiaCachedStack` — band-plane blit instead of N per-cell draws (internally MeasureVisible + prepared views) |
| 4 | UNEVEN rows, SMALL height | `SkiaCachedStack` (its MeasureVisible handles uneven natively) |
| 5 | UNEVEN rows, MEDIUM-LARGE height (Twitter feed, chat) | `MeasureVisible` WITHOUT CachedStack — few cells visible, per-cell caches suffice |

Cross-cutting: built-in `ItemsSourceWindow` layers on top of ANY recipe once ItemsSource exceeds `SkiaLayout.WindowSourceThreshold` (300). Browser/WASM caveat: prefer #2 (plain MeasureFirst) over CachedStack — single-threaded WASM pays MeasureVisible's measure cost synchronously.

- Canonical published feed pattern (news-feed tutorial, few LARGE cells per screen): plain `SkiaStack` + `RecyclingTemplate="Enabled"` + `MeasureVisible` + `ReserveTemplates="10"` + `VirtualisationInflated="200"`, ObservableRangeCollection (Clear/AddRange refresh, AddRange LoadMore, `LoadMoreOffset="500"`), no windowing. Cell recipe: root `ImageDoubleBuffered`, background layer `Image`, content layers `Operations`, no MAUI bindings inside cells (patch in `SetContent` override of `SkiaDynamicDrawnCell`), preload via SkiaImageManager; scroll feel `FrictionScrolled="0.5"`, `ChangeVelocityScrolled="1.35"`.
- Windowed chat-style lists (small/medium cells, many per screen): `RecyclingTemplate.Disabled` + `UsePreparedViews=true` measured smoothest — view per context, revisits stay bound+measured+baked (context-indexed reservoir).
- POOL SIZING: `ItemTemplatePoolSize` must comfortably exceed resident window + in-use, or preparation/draw evict each other's cells (musical-chairs rebinding = jaggy scroll).

### UsePreparedViews (SkiaLayout, opt-in, requires MeasureVisible)

Render thread NEVER measures a templated cell. A background `CellPreparationService` worker binds+measures real views ahead of scroll, feeds the measurement memo, parks views by context. Unprepared visible cells draw a skeleton (`DrawPlaceholder` override) at the reserved slot and still enter the render tree there (gestures see a contiguous sequence).
- Skeleton is ONLY for cells with no pixels at all: a visible cell that self-invalidates (status tick, image arrives, streaming text) draws its EXISTING cache while the worker re-measures — never flash a skeleton over previously-rendered content.
- Self-growing cells (streaming text): worker remeasures, draw path adopts the new size arithmetically (`OffsetSubsequentCells`) — no render-thread measure.

### SkiaCachedStack (band-plane stack)

Drop-in for `SkiaStack` in scrolling lists with many cells per screen: records viewport ±1 viewport into an SKPicture plane, blits it while scrolling, re-records per half-viewport drift or on invalidation. `UseDoubleBuffering` records the NEXT plane off-thread while the current blits, so scroll never pays a render-thread record — but it is **FALSE by default since e9f25742 (2026-07-15)** and nothing in the samples turns it on. Two reasons: (1) it publishes STALE CELL CONTENT — `_structureGen` versions structure only, not a cell's own pixels, so a cell that changes mid-bake (image bake lands, streaming text grows) is frozen at its old state, passes the generation check and the blit REGRESSES what a live frame already showed (device: image-load flicker, "stack jumps while AI is typing"; harness `StalePlaneContentRepro` + its `BakeStall` forces the device timing window a fast dev machine hides); (2) for an `Operations`/SKPicture plane the sync record is cheap (draw ops, no raster) while the async path pays `FreezeStructure` deep-copy under `LockMeasure`, worker handoff, publish/consume generation checks and a render-thread `_bakeDone.Wait(16)` when a bake is outrun — measurably smoother single-plane on device. Turn TRUE only where a sync record genuinely can't hit frame budget (weak hardware, GPU-surface planes); on MAUI heads it also requires `UsePreparedViews` (bake must be pure cache blits — MAUI BindableObject is not thread-safe) or it silently stays single-plane. `AsyncPlaneAllowed` is hard-false on BROWSER for an unrelated reason (single-threaded WASM burns the 16ms wait every frame while the bake can't progress).
- ALL cells under a plane must be cached — one dirty cell forces a plane re-record and every uncached cell then re-executes its full paint at that record. Cache type by weight: lightweight → `Operations` or `Image`; heavy/self-updating → `ImageDoubleBuffered` (two surfaces per realized cell). Per-frame-animated cells don't belong under a plane at all.
- Never `DestroyCacheOnContextChange=true` under a plane (disposal races the render-thread blit).
- Two tuning knobs. `VirtualisationInflatedRatio` (SkiaControl, bindable, `1.0` in the CachedStack ctor) = BAND SIZE, plane covers viewport ± ratio. `PlaneRefreshRatio` (SkiaCachedStack field, `0.5`) = DRIFT before a re-record, as a fraction of viewport height. Keep `PlaneRefreshRatio` BELOW `VirtualisationInflatedRatio`: drift == band ratio exhausts coverage exactly, `PlaneCoversViewport` fails and the frame falls through to a live draw. Raise both for fewer records over a bigger band; lower for more frequent, cheaper ones.
- DIAGNOSING "is the plane actually working": `DebugString` prints `plane [coveredTop..coveredBot] valid=True` — `plane none` means the plane NEVER installed and every frame is a live per-cell draw. Second signal: on blit frames `DrawStack` doesn't run, so `drawn X-Y` stays FROZEN while the content moves. `drawn` ticking on every few-pixel scroll = not blitting.
- Split (multi-column) support: the record gates (`SnapshotFillsViewport`) tile per ROW, not per cell — cells of one row share a `Destination.Top`, so a per-cell cursor reads every column past the first as an OVERLAP and rejects EVERY record (permanent `plane none` for `Split=2`; fixed 2026-07-20). Row grouping is geometric (same top within `tol`), not `ControlInStack.Row` — the row index is not populated on every structure path (background batches, uniform adds), the tops always are. Any new gate walking the structure linearly must do the same.

### Inverted chat pattern

Scroll `Rotation=180` + `ReverseGestures`; cells `Rotation=180`; local item i == global `[WindowEnd-1-i]`; offset 0 = newest at bottom. The inverted scroll keeps its newest-side anchor on viewport resize (e.g. keyboard) BY ITSELF — do not wire `AdaptToKeyboardFor` on it (see SkiaEditor section).

## Animator restart + carousel interrupt (fixed 2026-08)

- `AnimatorBase.Start()` resets `StartFrameTimeNanos` ONLY when `!IsRunning`: re-`Initialize()` + `Start()` on a STILL-RUNNING `SkiaValueAnimator` keeps the OLD clock — next tick `deltaFromStart` ≥ new duration → animation completes instantly (teleport). Always `Stop()` before retargeting a possibly-running animator.
- `SkiaCarousel` programmatic `GoNext()/GoPrev()` during a settling snap/bounce: fixed via `InterruptSnapping()` — stops both animators, and for `IsLooped` normalizes a position in the virtual zone by whole-strip shifts into the `SelectedIndex` neighborhood (visually identical, looped rendering wraps) BEFORE mutating `SelectedIndex`; otherwise `ScrollToOffset` computes `start=CurrentSnap` in virtual coords vs a real-coords target → full-strip reverse sweep (the "4→1 animates as 1←4" bug). `FixPosition()` alone is insufficient: it only fixes when nearest anchor IS a pseudo point, not mid-flight positions nearest a real snap.
- Symptom signature: programmatic index change works when idle but teleports/reverses when clicked during the bounce tail — gesture path immune because `ResetPan()` stops animators on Down.

## Fling ending jag on low-density displays (fixed 2026-08)

- Symptom: SkiaScroll fling smooth until the end, then "jagged" die-out (px hops with growing pauses) — ONLY on rendering scale < 1.5 (desktop Windows 100–125%). Probe-verified: paint cadence uniform (16.6ms, zero gaps), animator offsets perfectly smooth SUB-pixel — the jag is presentation-side: exponential deceleration spends ~1s below 1px/frame and nearest-sampled cache blits quantize that crawl into visible 1-physical-px hops at irregular growing intervals. High-DPI screens have the same crawl but the hop quantum (1/scale pt) is sub-visual — and that slow drift IS the natural smooth die-out feel (device-tested: removing it on Android "lost the smooth feel").
- Fix in lib: `ScrollFlingAnimator` pixel-aware finish — when `|velocity| * PixelsScale <= FinishStepPixels * 60`, replaces the asymptotic tail with a 250ms velocity-continuous quadratic ease-out landing on the physical pixel grid. Skipped for duration-cut (edge-targeted) flings (`Speed < Parameters.DurationSecs`) which must land exactly. `Shift()` translates the finish trajectory too. Gated in `SkiaScroll.PrepareToFlingAfterInitialized`: enabled only when `RenderingScale < SkiaScroll.PixelAwareFlingFinishBelowScale` (static, default 1.5) — high-DPI keeps the classic exponential tail untouched.
- Diagnosis pattern that cracked it: log per painted frame (a) paint dt from `FrameTime`, (b) animator offset, (c) REAL applied px offset in `PositionViewport` — compare painted px steps vs velocity-predicted steps. Distinguishes dropped frames / time bunching / quantization in one run. Fling stop threshold is `ValueThreshold` 1.85pt/s (change-rate, 3 frames), scroll pixel-snaps only at stop (`SkiaScroll` `PositionViewport`, gated on `!IsScrolling`).

## SkiaScroll + Recycled Cells (MeasureVisible) Internals, LoadMore Both Directions

Applies to: `SkiaScroll` + `SkiaLayout` (Column/SkiaStack), `ItemTemplate` + `RecyclingTemplate.Enabled`, `MeasureItemsStrategy=MeasureVisible`. (NOTE: the tiled-planes / `VirtualisationType.Managed` virtualization was REMOVED from DrawnUi — `Managed` now aliases `Enabled`; `SkiaScroll.Planes.cs`/`PlanesScroll`/`Plane`/`PlaneOverrideStructure` no longer exist. `SkiaWheelScroll`/`SkiaWheelPicker` keep their own `VirtualScroll`+`DrawVirtual` path, unrelated to planes.) Source map (src/Shared):
- `Draw/Scroll/SkiaScroll.cs` — `Draw` (frame orchestration), LoadMore edge triggers inside `PositionViewport`, `CalculateScrollOffsetForIndex`, `ApplyContentSize`.
- `Draw/Scroll/SkiaScroll.Scrolling.cs` — `OffsetVisibleAnchorY`, `ScrollToIndex`/`OrderedScrollToIndex`, fling planning (`PrepareToFlingAfterInitialized`), `ContentOffsetBounds` (CACHED; refreshed via `InitializeViewport`).
- `Draw/Layout/SkiaLayout.ListView.cs` — structure-change pipeline (`StructureChange`, `ApplyStructureChanges`), background measurement, head-insert region.
- `Draw/Layout/SkiaLayout.ColumnRow.cs` — `DrawStack` PASS 1 (visibility, `cell.Drawn`) / PASS 2 (bind+draw, draw-time remeasure self-heal via `SingleItemUpdate` + `OffsetSubsequentCells`), per-frame bg-measure restart.
- `Draw/Layout/SkiaLayout.ViewsAdapter.cs` — recycled views: `_dataContexts` SNAPSHOT (not live ItemsSource), `_cellsInUseViews` (index→view for on-screen cells), size-key pool buckets, `ApplyInsertShift`.
- `Draw/Layout/SkiaLayout.Shared.cs` — `OnItemsSourceCollectionChanged` routing: structure-preserving (non-Reset + MeasureVisible + existing structure) vs Reset full rebuild (+`ResetScroll`+`Invalidate`).

Scroll position signal: `SkiaScroll.ViewportOffsetY/X` do NOT raise PropertyChanged (plain field properties, `OnPropertyChanged` commented out) — `.ObserveProperty` on them never fires. Subscribe to the `Scrolled` event (`EventHandler<ScaledPoint>`, offset in `e.Units`) for scroll-position-driven UI like a Telegram-style "scroll to bottom" button.

Architecture facts:
- ONE canonical geometry: `StackStructure` (`LayoutStructure` of `ControlInStack`: `ControlIndex`, `Row/Column`, `Destination`/`Area` in px, `Measured`). `_measuredItems` dict is a parallel measurement cache — its cells CAN be DIFFERENT instances from structure cells; geometry mutations must touch both.
- Collection changes are STAGED and applied during `SkiaLayout.Paint` (render thread); adapter refresh (`InitializeSoft`) is POSTED separately. These are not naturally synchronized.
- Background measurement is forward-only batches (20), integrated per batch; `DrawStack` restarts it each frame while gaps remain; cancellation is cooperative (an in-flight batch can still land after cancel).
- Structure heights may be approximate by design: draw-time remeasure heals them in index order (`SingleItemUpdate`). Invisible when scrolling down (corrections land below viewport); corrections happen on-screen when scrolling up.
- **Viewport anchor correction** (2026-06): `SkiaScroll` tracks `_viewportAnchorIndex` + `_viewportAnchorPositionPx` every frame. At the TOP of each `Draw` (before content renders): (A) `SkiaLayout.FlushSingleItemUpdatesWithAnchorCorrection(scroll)` pre-applies staged `SingleItemUpdate` changes and calls `OffsetVisibleAnchorX/Y` for any cell that was fully above the viewport — zero-frame artifact for MeasureVisible path; (B) the stored anchor position is compared with the current structure to catch non-recycled cell height changes from last frame's draw — applies `OffsetVisibleAnchorX/Y` accordingly. Double-correction is prevented by refreshing `_viewportAnchorPositionPx` after the flush before the anchor-drift check. `OffsetVisibleAnchorX` mirrors `OffsetVisibleAnchorY` for horizontal Row layouts.

Critical invariants (each violation = a real shipped bug):
1. Any items index shift (prepend/insert) MUST be atomic with the adapter, same thread+frame: `ViewsAdapter.ApplyInsertShift(source, start, count)` (= rekey `_cellsInUseViews` + `ContextIndex` + fresh snapshot) together with `ShiftMeasurementIndices`. Adapter lagging one frame ⇒ visible cells rebind to pre-insert items (+N contexts flash for 1 frame) AND their wrong-content remeasure permanently poisons structure heights (random gaps with uneven rows).
2. Never bind/measure via the adapter snapshot in the same frame as a mutation — the snapshot lags one posted action. Bind measuring templates directly to the collection-event payload items (`MeasureHeadBatchDirect` pattern).
3. Guard straggler background batches across index shifts with an epoch (`_itemsShiftEpoch` bumped in `ShiftMeasurementIndices`; `StructureChange.Epoch` checked before cache writes and at apply).
4. Prepend viewport pinning must commit BEFORE the scroll computes its frame offset: `SkiaScroll.Draw` start calls `layout.CommitPendingStructureRebase()` → translate all existing cells (structure rebuilt via `new LayoutStructure(rows)`, which renumbers Row/Col) → `UpdateProgressiveContentSize` → `OffsetVisibleAnchorY(-shift/scale)`. Same frame = same pixels, no flash.
5. `OffsetVisibleAnchorY` must also shift the incremental pan baseline (`_panningCurrentOffsetPts`) and a running fling (`Shift`), else active gestures revert the correction next tick.
6. Fling endpoints are PRE-PLANNED: beyond-edge flings get duration CUT to stop exactly at the edge existing at finger-up. Content growth mid-fling ⇒ re-plan with `CurrentVelocity` AFTER `ApplyContentSize` refreshed `ContentOffsetBounds` (replanning earlier clamps to stale bounds). Symptom otherwise: violent "snap" landing exactly at a batch boundary.
7. `ScrollToIndex` to an unmeasured/uncreated index stays deferred — retry `ExecuteScrollToIndexOrder` every `Draw`; suppress LoadMore triggers while `OrderedScrollToIndex.IsSet` (else load cascades from the parked viewport). ALSO deferred while `SkiaLayout.HasPendingStructureChanges` (staged collection changes / head insert measuring / commit pending): resolving the order against pre-change geometry can consume it as a no-op (target == current offset, e.g. ScrollToIndex(0) right after Insert(0) at top), then the pinning commit shifts the viewport with no order left — symptom: chat doesn't follow own sent message while replies (offset already shifted) do scroll.
8. LoadMore latches: 0 as "unlatched" sentinel collides with edge offset 0 (top edge IS 0) — store epsilon. Anti-ping-pong opposite-direction blocks must be time-bounded (~1.5s) or one no-op trigger freezes the opposite direction forever. Top-direction readiness gate: `_measuredItems.ContainsKey(0)` — head measured; an in-flight head insert vacates keys 0..N-1, which self-serializes backward loads.

Windowed ItemsSource pattern (infinite list, LoadMore both directions + jump-anywhere):
- App keeps window `[windowStart, windowStart+count)` over a virtual dataset; only the window is ever materialized in memory.
- Collection contract: mutate `Items` silently, then raise ONE batched notification (+`Count`/`Item[]` PropertyChanged) carrying the TRUE action with payload + exact index. Use `AppoMobi.Specials.ObservableRangeCollection<T>` **10.0.1+** (older packs lack the index-aware overloads): `AddRange` → single Add at tail; `InsertRange(0, batch)` → single Add at index 0; `RemoveRange(index, count)` → single Remove with index (consecutive block); `ReplaceRangeReset` → Reset; plain `ReplaceRange` raises Replace (scroll-preserving — NOT for window rebase). Stock `ObservableCollection`/per-item `Insert(0,x)` loops and Reset-degrading range collections (Montemagno/Toolkit style) are NOT safe — the pipeline consumes event payload + index.
- Forward load (`LoadMoreCommand`): append next batch at window end. Backward load (`LoadMoreTopCommand`): `windowStart -= n; InsertRange(0, batch)` — framework head-insert pipeline measures the block off-thread and keeps the viewport visually pinned, content above becomes scrollable.
- **Jump to a NOT-yet-loaded position** (`JumpToGlobal(target)`): clamp target to dataset bounds, then:
  - target inside current window → `scroll.ScrollToIndex(target - windowStart, false)` — stays deferred until that index is measured, retried every frame (invariant 7).
  - target outside window → `windowStart = target; ReplaceRangeReset(batch starting at target)` → Reset → full rebuild + scroll reset to top → target renders at viewport top on the next frame, instantly, with NO measurement wait and nothing between old and new window ever loaded. No `ScrollToIndex` call needed: target IS local index 0. Backward loads then fill above it, forward loads below.
- `SkiaScroll` setup: `ResetScrollPositionOnContentSizeChanged=false` (required for prepend pinning), `LoadMoreCommand` + `LoadMoreTopCommand`, generous `LoadMoreOffset`/`LoadMoreTopOffset` (~800) for runway. App-side load methods just no-op at dataset edges (windowStart==0 / window end==total) — the framework triggers fire and the no-op is the contract.

Head-insert (backward prepend) pipeline end-to-end, when `InsertRange(0, N)` raises:
1. UI thread `HandleStructurePreservingAdd`: cancels background measurement, stages an Add `StructureChange` (carrying the inserted items!), posts adapter `InitializeSoft`.
2. Render thread `ApplyAddChange` (in Paint, when `StartIndex==0 && LastMeasuredIndex>=0`): `ChildrenFactory.ApplyInsertShift` (adapter rekey + fresh snapshot, ATOMIC with the next step) → `ShiftMeasurementIndices(0,N)` (bumps `_itemsShiftEpoch`) → `StartHeadInsertMeasurement(change.Items, ...)`. Screen pixels unchanged: positions untouched, only indices moved, visible views kept their bindings.
3. Background task `MeasureHeadBatchDirect`: standalone template bound DIRECTLY to `change.Items` (never the adapter snapshot), block laid out from y=0, staged as `_pendingHeadInsert` (stamp+epoch guarded).
4. `SkiaScroll.Draw` start, next frame: `CommitPendingStructureRebase` — prepend rows, translate ALL existing cells (+`_measuredItems` instances) down by block height, rebuild `LayoutStructure`, `UpdateProgressiveContentSize`, `OffsetVisibleAnchorY(-shift/scale)` (also shifts pan baseline + running fling, flags fling replan) — all BEFORE the scroll computes this frame's offset → identical pixels, new scrollable content above.
5. `_measuredItems.ContainsKey(0)` true again → top trigger re-armed for the next backward batch.

Minimal working skeleton (MAUI code-first page; fluent patterns per `drawnui-fluent` skill):

```csharp
// cell: subclass SkiaDynamicDrawnCell, override SetContent for rebind (called on recycle)
class RowCell : SkiaDynamicDrawnCell
{
    SkiaRichLabel _label;
    public RowCell()
    {
        HorizontalOptions = LayoutOptions.Fill;            // height = content-driven, no HeightRequest
        UseCache = SkiaCacheType.Image;                    // MeasureVisible rule (see caching above)
        Children = new List<SkiaControl> { /* shape + label, .Assign(out _label) */ };
    }
    protected override void SetContent(object ctx)
    { if (ctx is MyModel m) _label.Text = m.Text; }
}

// page
Canvas = new Canvas
{
    Gestures = GesturesMode.Enabled,   // Enabled = normal; Lock only to capture the whole input stream
    RenderingMode = RenderingModeType.Accelerated,
    HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
    Content = new SkiaScroll
    {
        Orientation = ScrollOrientation.Vertical,
        ResetScrollPositionOnContentSizeChanged = false,
        LoadMoreCommand = new Command(LoadMoreForward),
        LoadMoreOffset = 800,
        LoadMoreTopCommand = new Command(LoadMoreBackward),
        LoadMoreTopOffset = 800,
        HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill,
        Content = new SkiaStack                              // SkiaLayout Type=Column
        {
            ItemTemplate = new DataTemplate(() => new RowCell()), // compiled delegate, pooled
            ItemsSource = _items,                            // ObservableRangeCollection<MyModel>
            RecyclingTemplate = RecyclingTemplate.Enabled,
            MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
            ItemTemplatePoolSize = 0,                        // 0 = auto-size pool
            Spacing = 2,
        }.Assign(out NewsStack),
    }.Assign(out MainScroll),
};
Content = new Grid { Children = { Canvas } };                // MAUI needs a Grid root wrapper

// live diagnostics overlay: observe SkiaLayout.DebugString
// new SkiaLabel{...}.ObserveProperty(NewsStack, nameof(SkiaLayout.DebugString), me => me.Text = NewsStack.DebugString)
```

Window TRIM (bounded memory cap, e.g. max 250 resident items — stage 2 of the windowed pattern, implemented in src/Shared):
- App contract: trim BEFORE loading, opposite end. Forward load trims overflow from window head (`RemoveRange(0, over)` + `windowStart += over`) then appends; backward load trims from tail (`RemoveRange(count-over, over)`) then prepends. Order matters: a trim staged AFTER a head insert would bump `_itemsShiftEpoch` and kill the in-flight head-insert measurement. `ObservableRangeCollection.RemoveRange(index, count)` raises ONE Remove event with the removed block + index.
- Head trim is TWO-PHASE, mirroring head insert in reverse and needing NO measurement (heights already in structure): phase 1 in `ApplyRemoveChange`→`ApplyHeadRemoveChange` (render thread, Paint) is pixel-neutral — `ViewsAdapter.ApplyRemoveShift` (release removed-range views + rekey survivors + fresh snapshot, atomic same-frame), drop `_measuredItems` keys, `ShiftMeasurementIndices(count, -count)`, rebuild structure from survivors at UNCHANGED positions; stage `_pendingHeadRemove{Shift=firstSurvivor.Destination.Top}`. Phase 2 next frame in `CommitPendingStructureRebase` (scroll pre-offset): translate all cells UP by shift, `UpdateProgressiveContentSize`, `OffsetVisibleAnchorY(+shift/scale)` — identical pixels, shorter content.
- Tail trim is fully synchronous in `ApplyTailRemoveChange`: release/rekey adapter, drop measured keys, bump epoch (in-flight bg batches target the removed range), clamp `LastMeasuredIndex`, rebuild structure from survivors, shrink content size. No offset compensation (nothing above viewport moved).
- Tail removal must be flagged at STAGING time (`StructureChange.TailRemoval`, set in `HandleStructurePreservingRemove` where the post-mutation count is still reliable): at apply time a same-frame subsequent prepend already changed the live count, `StartIndex == Count` checks lie.
- Adapter: `ApplyRemoveShift` ≠ `ApplyInsertShift` with negative count — plain index shifting would move views INSIDE the removed range onto surviving indices instead of releasing them. Both are PUBLIC `ViewsAdapter` API: any custom code shifting indices of a recycled-cells structure must call the matching one synchronously with the structure change, same frame, render thread (split across frames = one-frame wrong contexts + poisoned cached heights). Documented in `docs/articles/advanced/recycled-cells.md` ("Windowed ItemsSource" section).
- Guards: bg-measure restart gated on `HeadRemoveInFlight` too (staged positions would miss the pending translation); head insert arriving over an uncommitted head trim = degenerate race → drop pending + `Invalidate()`; commit epoch mismatch → `Invalidate()` (else permanent dead gap above content).
- Sizing rule: cap × rowHeight must stay well above 2 × LoadMoreOffset or trims touch rows near the viewport.

The batching collection (`ObservableRangeCollection` with `ReplaceRange`/`AddRange` raising single Reset/Add events) ships in the `AppoMobi.Specials` NuGet package; any windowed wrapper must follow that event contract (one Reset for window rebase, one ranged Add for edge loads).
- Recycled cell caching by measure strategy:
  - `MeasureVisible`: `UseCache = SkiaCacheType.Image` on the cell root. `ImageDoubleBuffered` NOT needed — measurement already runs in background, the latency the double buffer hides isn't on the hot path; plain `Image` avoids the second surface per cell.
  - `MeasureFirst` / `MeasureAll`: `GPU` cache for even-height rows with large cells; `ImageDoubleBuffered` for the other cases.

## Android 15+ Edge-to-Edge & System Bars (lib ≥ 2026-08)

Android 15+ (API 35) with targetSdk ≥ 35 enforces edge-to-edge: system bars transparent, `Window.SetStatusBarColor` / `SetNavigationBarColor` / theme bar colors / `windowOptOutEdgeToEdgeEnforcement` (targetSdk 36) are no-ops. DrawnUi handles this in the lib — no app-side config:

- **net9 (MAUI 9)**: MAUI 9 does not apply insets, so DrawnUi's `InsetsListener` pads the activity content view itself (systemBars+cutout) when `SdkInt >= 35 && targetSdk >= 35`. Skipped when `MobileIsFullscreen=true`.
- **net10 (MAUI 10)**: MAUI 10 pads by itself (Layouts default `SafeAreaEdges=Container`; ContentPage defaults `None`). `MobileIsFullscreen=true` works via handler mappings in `ConfigureHandlers.Android.cs` setting `SafeAreaEdges.None` on ContentPage/Layout/ScrollView.
- **Bar colors on 35+**: `Super.SetStatusBarColor` / `Super.SetNavigationBarColor` paint their own strip views on the DecorView (gravity Top/Bottom, height from insets) since the Window APIs are dead. Works net9+net10. Both defer via `ExecAfterInit` when called before activity exists.
- Shell apps: the top band is Shell's own `AppBarLayout` (background = Android theme `colorPrimary`, NOT `Shell.BackgroundColor` which is toolbar-only); status bar area color for Shell apps = `Super.SetStatusBarColor`.

## Android 16+ "not 16 KB aligned" compatibility dialog (MAUI apps)

Dialog "This app isn't 16 KB compatible" on Android 16/17 emulators. Diagnosis order:

1. **Ignore the "Unknown error" lines** — compressed APK entries the checker can't parse, pure noise. Find the entry saying `LOAD segment not aligned` — that's the real offender.
2. `lib_*.dll.so` entries listed = `EmbedAssembliesIntoApk=true` in a **Debug** build packing managed assemblies as unaligned wrapper .so files. Fix: scope embed to Release: `<EmbedAssembliesIntoApk Condition="'$(Configuration)'=='Release'">true</EmbedAssembliesIntoApk>`.
3. Real native lib misaligned: check ELF `p_align` of the APK's copy (16384 good, 4096 bad), then hunt WHERE the 4 KB copy comes from — known trap: referencing desktop **`SQLitePCLRaw.lib.e_sqlite3`** unconditioned on Android packs its `runtimes/linux-x64/native/libe_sqlite3.so` (4 KB) into the APK over the correct 16 KB Android aar. Fix: condition the desktop package out of android/ios TFMs.
4. **The verdict is cached per install** — after fixing, `adb uninstall` + fresh install, or the dialog keeps showing with stale text.
5. Release AABs for Play: verify with `bundletool dump config --bundle=app.aab | grep -i alignment` (want 16K). SkiaSharp 4.x is aligned; old 2.88.x is not.

## Android 12+ splash screen shows .NET logo instead of custom splash (MAUI apps)

On API 31+ the OS forces its own splash phase (solid color + centered icon) BEFORE the app window; a custom `android:windowBackground` splash drawable only shows after it, briefly or never. Themes inheriting `Maui.SplashTheme` point `windowSplashScreenBackground`/`windowSplashScreenAnimatedIcon` at MAUI-generated resources from `Resources\Splash\splash.svg` — the template's purple ".NET" logo. Below API 31 nothing changed: the windowBackground splash shows as always.

Fix in the splash theme (`Platforms\Android\Resources\values\styles.xml`):
- `windowSplashScreenBackground` = brand color resource.
- `windowSplashScreenAnimatedIcon` = brand icon drawable. Gotchas:
  1. Only plain bitmap/vector drawables work — an `<inset>` wrapper (dp or `%`) fails SILENTLY to a blank splash.
  2. The circular mask reveals only the inner ~2/3 of the icon bounds — bake transparent padding into the PNG itself: square canvas ≥ `diagonal(logo) / 0.66`, logo centered.
- Verify by cold start + immediate screencap (icon phase lasts ~1s): `am force-stop`, `am start`, capture at ~0.7s.

## Android: Canvas created hidden/offscreen stays blank forever (fixed 2026-08-24)

Symptom: DrawnUi `Canvas` controls created while hidden or offscreen (FlyoutPage drawer cells, login/overlay screens, translated containers) never draw after being revealed — content blank while MAUI Labels around them render fine. SVG loads and parses OK; `SkiaControl.Paint` is simply never called.

Cause: canvases created hidden/offscreen are correctly detected (`IsHiddenInViewTree=true`, rendering disabled). But reveals that slide/translate views (Android `DrawerLayout` offset+invalidate, translation animations) happen WITHOUT a layout pass, so `OnGlobalLayout` never fires and the visibility re-check (`NeedCheckParentVisibility`) never triggers — canvases stay disabled forever.

Fix (in lib, `DrawnView.Android.cs`): `HiddenWakePreDrawListener` — a `ViewTreeObserver.IOnPreDrawListener` that, only while the canvas is hidden-in-tree and throttled to 100ms, sets `NeedCheckParentVisibility=true`. Visible canvases pay one bool check per preDraw; hidden ones re-check parent-chain visibility at most 10x/s during window redraws.

Debug method that found it: transition-only logs can't distinguish "never re-checked" from "re-checked, still hidden" — log inside `CheckElementVisibility` itself. `uiautomator dump` confirms native views exist with correct bounds while pixels stay empty.

## Preferred Sources

1. `docs` — local DrawnUi repo checkout, or published `https://drawnui.net`, or GitHub
2. `docs/articles/*` conceptual guides
3. `docs/api/*` API surface (offline alternative: XML doc files in the NuGet package cache, `lib/<tfm>/*.xml`)
4. Existing project pattern
5. DrawnUI samples (in-repo `src/*/Samples/*`; source on GitHub if no local checkout)
6. `drawnui-help-master` agent for unfamiliar control/layout patterns (when available locally)
7. New implementation only after pattern search fails
