---
name: drawnui-fluent
description: "Use when writing DrawnUI C# code-behind with fluent extensions. Covers inline control construction, .Assign(out _field), .Initialize(), .OnTapped(), .OnTextChanged(), .ObserveProperty(), .ObserveProperties(), .ObservePropertyTwoWay() (two-way binding), .Adapt(), .WhenPaint(), .ObserveSelf(), layout aliases (SkiaStack/SkiaRow/SkiaLayer), one-shot and looping animations, gradients, colors, shadows, SkiaLottie, SkiaImageTiles, and SkiaBackdrop code-behind patterns. Load before any DrawnUI C# composition task."
version: 1.3.0
tags: [drawnui, csharp, fluent, code-behind, maui, blazor]
---

# DrawnUI Fluent Extensions — C# Code-Behind

## Containers — use semantic aliases

Compose with alias containers, not raw `SkiaLayout { Type = LayoutType.X }`: `SkiaStack` (vertical, Fill), `SkiaRow` (horizontal), `SkiaLayer` (absolute overlay, Fill), `SkiaWrap` (Fill), `SkiaGrid` (Fill). Aliases preset `Type` AND `HorizontalOptions=Fill` (except SkiaRow) — raw `SkiaLayout` defaults to `Type=Absolute` with NO fill, so it aligns differently. `SkiaFrame` = `SkiaShape` rectangle, not a layout.

Constructor shorthand is the documented idiom for text controls: `new SkiaLabel("text")`, `new SkiaRichLabel("**md**")`, `new SkiaButton("Caption")`.

**Row does NOT distribute Fill children** (device-verified 2026-08-15): two `HorizontalOptions=Fill` children inside a `SkiaRow` each take full width and overflow the screen — a Row is not a star-grid. For proportional button rows use `SkiaGrid` + `.WithColumnDefinitions("2*,3*")` + `.SetGrid(col, 0)` per child. Same for `HorizontalFillRatio` inside a Row — not a substitute for grid columns.

## Composition Style — MANDATORY

**Never declare a local variable for a control and then reference it in a children list.** Always construct inline inside the collection initializer. Use `.Assign(out _field)` when a field reference is needed — it returns the control so chaining continues.

**Do not spam `AddSubView(...)` while building a known static scene or container subtree.** When the children are known at composition time, declare them together inside `Children = new List<SkiaControl> { ... }`. Reserve `AddSubView()` for dynamic/runtime operations such as pooled controls, generated rows, incremental updates, or add/remove flows that happen after the initial tree is built.

### Critical: Static vs Runtime Children Mutation

**Before layout is ready (static composition in constructor):**
```csharp
// ✅ CORRECT — initial tree building
Children = new List<SkiaControl>
{
    new SkiaLabel { Text = "A" },
    new SkiaLabel { Text = "B" },
};
```

**After layout is ready (runtime mutation, e.g. in RefreshFriendsList):**
```csharp
// ✅ CORRECT — runtime mutation
layout.ClearChildren();
layout.AddSubView(new SkiaLabel { Text = "New" });
layout.RemoveSubView(existingChild);
layout.Children.RemoveAt(0); // also valid

// ❌ WRONG — does NOT work after initial tree is built
layout.Children.Clear();        // ignored
layout.Children.Add(child);     // ignored
```

**Rule of thumb:** Use `Children = new List<...>` only during initial construction. Once `LayoutIsReady` has fired or the control is in the visual tree, use `ClearChildren()`, `AddSubView()`, `RemoveSubView()` instead.

WRONG — never do this:
```csharp
var label = new SkiaLabel { Text = "hi" };
Children = new List<SkiaControl> { label };
```

RIGHT — always inline:
```csharp
Children = new List<SkiaControl>
{
    new SkiaLabel { Text = "hi" }.Assign(out _label),
};
```

Nested layout:
```csharp
Children = new List<SkiaControl>()
{
    new SkiaStack()
    {
        Spacing = 8,
        Children = new List<SkiaControl>()
        {
            new SkiaLabel() { Text = "hi" }.Assign(out _label),
        }
    },
};
```

`Children = { ... }` (bare collection initializer) and `.WithChildren(a, b, c)` are equivalent inline forms — fine too. `.WithContent(child)` sets the single child of `IWithContent` containers (e.g. `SkiaScroll`, `ContentLayout`). `.AssignParent(parent)` adds the control to a parent mid-chain.

WRONG for static composition:
```csharp
AddSubView(new SkiaLabel { Text = "A" });
AddSubView(new SkiaLabel { Text = "B" });
AddSubView(new SkiaLabel { Text = "C" });
```

RIGHT for static composition:
```csharp
Children = new List<SkiaControl>
{
    new SkiaLabel { Text = "A" },
    new SkiaLabel { Text = "B" },
    new SkiaLabel { Text = "C" },
};
```

Chain after `.Assign()` — it returns the control:
```csharp
new SkiaLabel() { ... }
    .ObserveProperty(source, nameof(Score), me => { me.Text = $"{source.Score}"; })
    .Assign(out LabelScore)
    .OnTapped(me => { ... })
```

Omit redundant default assignments like `Left = 0`, `Top = 0`. Keep explicit zeros only in reset/pooling paths where prior state may still be present.

Layout shortcuts: `.Center()` / `.CenterX()` / `.CenterY()` (control inside parent), `.Fill()` / `.FillX()` / `.FillY()`, `.StartX()` / `.StartY()` / `.EndX()` / `.EndY()`.
Text alignment inside a `SkiaLabel` (NOT the label's own position): `.CenterText()` / `.CenterTextX()` / `.CenterTextY()` — set `HorizontalTextAlignment` / `VerticalTextAlignment` to Center.

Property shortcuts (all chainable): `.WithHeight(n)` / `.WithWidth(n)`, `.WithMargin(all)` / `(h,v)` / `(l,t,r,b)` / `(Thickness)`, `.WithPadding(...)`, `.WithCache(SkiaCacheType.X)`, `.WithBackgroundColor(c)`, `.WithHorizontalOptions(...)` / `.WithVerticalOptions(...)`, `.WithVisibility(bool)`, `.WithTag("name")`; shape: `.Shape(ShapeType.Circle)`; image: `.WithAspect(TransformAspect.X)`; label: `.WithFontSize(n)`, `.WithTextColor(c)`, `.WithHorizontalTextAlignment(...)`.

### Grid placement helpers

Definitions are set on the grid itself (string form, chainable), placement on each child:

```csharp
new SkiaGrid()
{
    DefaultRowDefinition = new RowDefinition(GridLength.Star),   // avoids repeating N identical rows
    Children = new List<SkiaControl>()
    {
        new SkiaSvg() { ... }.SetGrid(0, 0, 1, 2),   // COLUMN first, then row, then colspan, rowspan
        new SkiaLabel() { ... }.SetGrid(1, 0),
        new SkiaLabel() { ... }.WithColumn(2).WithRow(0).WithRowSpan(2),
    }
}
.WithColumnDefinitions("32,*,40")
.WithRowDefinitions("Auto,Auto")
```

`SetGrid(column, row)` / `SetGrid(column, row, columnSpan, rowSpan)` — **column comes first**, opposite of the usual "row, column" convention (the XML doc comment on the 4-arg overload in `FluentExtensions.Maui.cs` lists them backwards — trust the signature). Individual: `.WithRow(n)`, `.WithColumn(n)`, `.WithRowSpan(n)`, `.WithColumnSpan(n)`. Also `DefaultColumnDefinition`.

### `.Initialize` vs `.Adapt`

`.Adapt(me => ...)` runs setup on the control itself mid-chain. Do NOT access OTHER `.Assign`'d references from `Adapt` — they may not exist yet. Post-build wiring that touches assigned refs goes in `.Initialize(me => ...)` on the OUTERMOST control — it runs after the whole chain is constructed.

---

## Event / Gesture Wiring — MANDATORY

Always use fluent extension methods — never `+=` events or commands wired outside the initializer.

| Task | Fluent method |
|------|--------------|
| Tap handler | `.OnTapped(me => { ... })` |
| Tap with args | `.OnTapped((me, args) => { ... })` |
| Long press | `.OnLongPressing(me => { ... })` |
| Text changed | `.OnTextChanged(text => { ... })` |
| Label text + sender | `.OnTextChanged((lbl, text) => { ... })` |
| Arbitrary setup | `.Adapt(me => { me.X = ...; })` |
| Post-build wiring (touching Assign'd refs) | `.Initialize(me => { ... })` |
| Key pressed | `.OnKeyDown((me, key) => { ... })` |
| Key released | `.OnKeyUp((me, key) => { ... })` |
| Paint hook | `.WhenPaint((me, ctx) => { ... })` |

Keyboard: `.OnKeyDown` / `.OnKeyUp` both take `(control, InputKey key)` — `InputKey.ArrowLeft/ArrowRight/ArrowUp/ArrowDown`, `Space`, `Enter`, `KeyD`… Attach them to the ROOT control of the tree, not to the focused child. Verified on the Fiddle WASM build 2026-08-30.

`OnKeyDown` repeats at the OS key-repeat rate — with a delay before the first repeat and gaps between them — so driving a value directly from it produces stepped, laggy movement. For anything held (a game paddle, a camera pan), keep a direction flag instead and move per frame:

```csharp
var keyDir = 0;

// in the per-frame animator: FPS-independent, no repeat delay
if (keyDir != 0) targetX = Math.Clamp(targetX + keyDir * speed * dt, min, max);

root.OnKeyDown((me, key) =>
     {
         if (key == InputKey.ArrowLeft) keyDir = -1;
         else if (key == InputKey.ArrowRight) keyDir = 1;
     })
    .OnKeyUp((me, key) =>
     {
         if (key == InputKey.ArrowLeft && keyDir < 0) keyDir = 0;
         else if (key == InputKey.ArrowRight && keyDir > 0) keyDir = 0;
     });
```

On Blazor the key event is not `preventDefault`ed, so Space also scrolls the hosting page while the canvas has focus.

**`.WhenPaint` draws BEFORE the control paints its own background** (verified 2026-08-30, DrawnUI Fiddle WASM). A control that has `BackgroundColor` set will cover everything the hook drew — silently, no error, blank result. Put the background on a parent (e.g. a `SkiaShape` frame) and attach `.WhenPaint` to a transparent child filling it:

```csharp
new SkiaShape { CornerRadius = 10, BackgroundColor = Color.Parse("#0B0E14"), WidthRequest = w, HeightRequest = h,
    Children = new List<SkiaControl>
    {
        new SkiaLayer().Fill().WhenPaint((me, ctx) => { /* raw SKCanvas drawing lands on top */ }),
    }
}
```

Hook coordinates: `ctx.Context.Canvas` = raw `SKCanvas`, `ctx.Destination` = the control's rect in device pixels, `ctx.Scale` = DIP→px. Convert local DIP to canvas px with `dest.Left + v * scale`.
| Self-observe any property | `.ObserveSelf((me, propName) => { ... })` |
| Raw gesture interception | `.WithGestures((me, args, apply) => { ... })` — return `this` = consumed, `null` = pass; never consume Up unless required |

WRONG:
```csharp
var btn = new SkiaButton("Reset");
btn.Tapped += (s, e) => Reset();
Children = new List<SkiaControl> { btn };
```

RIGHT:
```csharp
Children = new List<SkiaControl>
{
    new SkiaButton("Reset").OnTapped(me => Reset()),
};
```

Exception — `SkiaButton.Clicked` / `Pressed` / `Released` are **fields**, not events (`Action<SkiaButton, SkiaGesturesParameters>`, `SkiaButton.cs:883-893`). Assigning them inside the initializer is valid and does not break the chain, so it is NOT the banned `+=` pattern:

```csharp
new SkiaButton("Back") { Clicked = (me, args) => App.GoBack() }
```

Same for a factory that has no chain to break — `.Tapped += ...` inside a `DataTemplate` lambda is fine (see ItemTemplate below).

---

## Animation — `.Animate(...)` and `Animate*` shortcuts

FPS-independent looping animation driven by the framework animator (ticks off real frame time). Same visual speed on any device. **Do NOT hand-roll `.WhenPaint(...)` + `me.Update()` + `FrameTimeNanos` delta math for animation** — use `.Animate`. Auto-unregisters on control disposal; starts once the control is laid out (safe to chain at construction).

General form — callback gets `(control, animator, value 0..1, deltaSeconds)`:

```csharp
new SkiaShape { Type = ShapeType.Circle, /* ... */ }
    .Animate(1.6, (me, animator, value, dt) =>
    {
        me.Rotation = value * 360;   // value = eased 0..1 progress of the cycle
        // animator.Stop();          // stop from inside when needed
    }, repeat: -1);                  // -1 loop forever, N cycles, 0 once
```

Signature: `.Animate(double seconds, Action<T, SkiaValueAnimator, double, double> onFrame, int repeat = 0, Easing easing = null, bool pingPong = false, double delaySeconds = 0)`. `pingPong: true` bounces value 0→1→0 each cycle. `easing: null` = linear.

Typed shortcuts (all `Animate*`, share `(from, to, seconds, repeat, easing, pingPong, delaySeconds)`):

| Task | Fluent method |
|------|--------------|
| Endless spinner | `.AnimateRotation(0, 360, seconds: 1.6, repeat: -1)` |
| Heartbeat pulse | `.AnimateScale(1.0, 1.15, seconds: 0.8, repeat: -1, pingPong: true)` |
| Breathing fade | `.AnimateOpacity(0.3, 1.0, seconds: 1.0, repeat: -1, pingPong: true)` |
| Shake | `.AnimateTranslationX(-20, 20, seconds: 0.5, repeat: 3, pingPong: true)` |
| Drop | `.AnimateTranslationY(0, 100, seconds: 0.6, easing: Easing.BounceOut)` |

Each maps its property linearly by the `0..1` value; `0→360` rotation loops seamlessly. Use general `.Animate(...)` for multi-property / non-linear / delta-time physics. Source: `FluentExtensions.Shared.cs` (built on `RangeAnimator`/`PingPongAnimator`).

`.UpdateNonStop()` — infinite no-op animator keeping the surface repainting every frame; required for time-driven visuals with no property changes (e.g. a `SkiaShaderEffect` reading `iTime`).

### One-shot awaitable animations

For single transitions (press feedback, reveals) use the awaitable `*ToAsync` methods, not `.Animate`:

```csharp
await control.ScaleToAsync(1.1, 1.1, 120, Easing.CubicOut);
await control.TranslateToAsync(0, -40, 250, Easing.SpringOut);
await control.RotateToAsync(180, 300);
await control.FadeToAsync(0.0, 200);   // also the fade-in-on-load helper
```

`.Animate`/`Animate*` = looping/frame-driven; `*ToAsync` = one-shot, awaitable, composable with `Task.WhenAll` for parallel property animation.

---

## SkiaShaderEffect — uniforms and compile errors

```csharp
new SkiaShaderEffect
{
    UseBackground = PostRendererEffectUseBackgroud.Once, // static input -> snapshot once, NOT Always
    AutoCreateInputTexture = true,
    ShaderCode = mySksl,
}
.SetUniform("uIntensity", 0.7f)                               // custom uniform: float/float2/float3/float4 overloads
.OnShaderError((me, error) => Console.WriteLine($"[SkSL] {error}")) // SkSL compile errors; without a handler they throw (swallowed into log)
```

- `SetUniform` is chainable and re-appliable at runtime (e.g. from a slider) — it calls `Update()` itself.
- Standard uniforms auto-fed each frame: `iResolution`, `iImageResolution`, `iTime`, `iOffset`, `iMouse`, `iImage1` (input texture). DECLARE ALL OF THEM in the `.sksl` even if unused — the engine writes each one every frame, and writing an undeclared uniform throws, which aborts shader creation and silently kills the effect. DECLARE ALL OF THEM in the `.sksl` even if unused — the engine writes each one every frame, and writing an undeclared uniform throws, which aborts shader creation and silently kills the effect. Sample with `iImage1.eval((fragCoord - iOffset) * iImageResolution / iResolution)`.
- The fluent for the `OnCompilationError` event is named `.OnShaderError(...)` (an instance event hides a same-named extension).
- SkiaSharp v4 gotcha (fixed in framework): scalar uniforms must be written as `float`, not `float[1]`.

### Inline SkSL: always use a raw string literal

Declare shader source as a **raw string literal** hoisted to its own `const`, never as a verbatim `@"..."` string inlined at the property:

```csharp
const string brushed = """
uniform float2 iResolution;
uniform float2 iOffset;

half4 main(float2 fragCoord)
{
    float2 uv = (fragCoord - iOffset) / iResolution.xy;
    return half4(uv.x, uv.y, 0.0, 1.0);
}
""";

// ...
new SkiaShaderEffect { ShaderCode = brushed, ... }
```

Three reasons:

- No escaping. A verbatim string needs every `"` doubled; a raw literal takes the SkSL byte for byte.
- No stray indentation. The raw literal strips the indentation of the closing `"""`, so the SkSL that reaches the compiler is not carrying the C# nesting whitespace on every line (which is what makes reported error columns match the source).
- **Syntax highlighting for free** in Monaco-based editors (DrawnUI Fiddle, and anything else using Monaco's bundled `csharp` grammar). That tokenizer has a rule for `@"` — a verbatim shader is one flat string colour — but **no rule for `"""`**, so the raw-literal body falls through to ordinary code tokenization: `float`, `return`, `const` colour as keywords, numbers as numbers, `//` as comments. It reads like a shader editor. This is a tokenizer gap, not a feature: a Monaco version that learns raw strings would make it flat again, while everything else above still holds.

---

## Bindings (ObserveProperty / ObserveProperties)

Replace MAUI `{Binding}` expressions with fluent observation. No `BindingContext`, no `SetBinding`.

| XAML | Code-Behind |
|------|-------------|
| `Text="{Binding Prop}"` | `.ObserveProperty(source, nameof(Prop), me => { me.Text = source.Prop; })` |
| `IsVisible="{Binding ShowX}"` | `.ObserveProperty(source, nameof(ShowX), me => { me.IsVisible = source.ShowX; })` |
| `Value="{Binding Health}"` | `.ObserveProperty(source, nameof(Health), me => { me.Value = source.Health; })` |
| two props | `.ObserveProperties(source, [nameof(P1), nameof(P2)], me => { ... })` |
| `AddGestures.CommandTapped="{Binding Cmd}"` | `.OnTapped(me => { source.Cmd?.Execute(null); })` |

When `BindingContext = this` (control observes itself):
```csharp
new SkiaLabel()
    .ObserveProperty(this, nameof(DialogMessage), me => { me.Text = DialogMessage; })
```

### Two-way: `.ObservePropertyTwoWay(...)`

`ObserveProperty` is ONE-WAY (source → control). For MAUI `Mode=TwoWay` semantics (control property and source property kept in sync both directions, re-entrancy guarded) use `ObservePropertyTwoWay`. The control type must be `INotifyPropertyChanged` (every `SkiaControl` is, via `BindableObject`), and the source must be `INotifyPropertyChanged`. Syncs once at setup (source → control).

```csharp
// wheel.SelectedIndex <-> model.SelectedIndex (both directions)
new SkiaWheelScroll() { /* ... */ }
    .Assign(out _wheel)
    .ObservePropertyTwoWay(model,
        nameof(model.SelectedIndex),  me   => me.SelectedIndex = model.SelectedIndex,   // source -> control
        nameof(SkiaWheelScroll.SelectedIndex), (src, me) => src.SelectedIndex = me.SelectedIndex); // control -> source
```

Signature: `.ObservePropertyTwoWay(source, sourcePropName, Action<T> onSourceChanged, controlPropName, Action<TSource,T> onControlChanged)`. The control property MUST raise `PropertyChanged` (BindableProperty CLR setters do). A separate one-way `.ObserveProperty(model, nameof(model.SelectedIndex), me => me.Text = ...)` on a label then reacts to the model. Source: `FluentExtensions.Shared.cs`.

Multi-property matching (`ObserveProperties`/`ObservePropertiesOn`) filters `PropertyChanged` names through a `HashSet<string>` internally (O(1)), not a linear array scan — matters for controls observing many source properties.

`ObserveProperties` (all overloads) automatically adds `BindingContext` to the watched set AND fires the callback once at subscription (synthetic BindingContext event). It is therefore the drop-in compiled replacement for the legacy raw pattern `.Observe(src, (me, prop) => { if (prop.IsEither(nameof(BindingContext), nameof(X))) ... })` — prefer `.ObserveProperties(src, me => ..., x => x.X)`; lazy-target `() => field` overload included (verified 2026-07, SkiaSlider builders converted).

Note: a guarded setter (`if (field == value) return;`) won't re-fire on an unchanged value, so it won't force a side-effecting reposition — drive the control directly when you need the setter to run even for the same value (e.g. wheel re-anchor on mode switch).

### Compiled property names (lambda instead of `nameof`/string)

`ObserveProperty`, `ObservePropertyTwoWay` and `ObserveProperties` all have lambda overloads — pass `x => x.Prop` instead of a string. Rename-safe, same behavior. Implemented via `Expression<Func<...>>` tree inspection (`Member.Name`), never `.Compile()`, so it's safe under iOS/NativeAOT (no JIT dependency). Both string and lambda overloads coexist — use whichever reads better; lambda catches renames at compile time.

```csharp
.ObserveProperty(source, x => x.Prop, me => { me.Text = source.Prop; })

.ObservePropertyTwoWay(model,
    vm => vm.SelectedIndex, me => me.SelectedIndex = model.SelectedIndex,
    me => me.SelectedIndex, (src, me) => src.SelectedIndex = me.SelectedIndex);
```

Lazy target (`() => Model` instead of a direct instance, for a source that's still null at construction time) also has a lambda-property overload:

```csharp
.ObserveProperty(() => Model, x => x.Title, me => { me.Text = Model.Title; })
```

Related observers for less common shapes: `.Observe(vm, (me, prop) => ...)` (raw INPC filter — legacy, prefer `ObserveProperties`); `.Observe(() => _field, ...)` (control not yet created); `.ObserveBindingContext<TControl,TVm>((me, vm, prop) => ...)` (typed own-BindingContext); `.ObserveBindingContextOn<...>(otherControl, ...)` (another control's BindingContext); `.ObservePropertyOn(parent, () => target, parentProp, ...)` / `.ObservePropertiesOn(...)` (dynamic re-resolving target, AOT-safe).

`ObserveProperties` (multi-prop) lambda overload takes the properties as trailing `params` — note `callback` moves BEFORE the property lambdas here, since `params` must be the last parameter:

```csharp
.ObserveProperties(model, me => { me.Text = $"{model.A}-{model.B}"; }, x => x.A, x => x.B)

.ObserveProperties(() => Model, me => { me.Text = $"{Model.A}-{Model.B}"; }, x => x.A, x => x.B)
```

**At least one property is mandatory — enforced by the compiler.** Signature is `(target, callback, Expression property, params Expression[] moreProperties)`, so a propertyless call does not compile:

```csharp
// CS7036 — does NOT compile (used to compile and silently observe nothing)
.ObserveProperties(vm, me => { if (vm.IsLoadingMore) me.Start(); else me.Stop(); })
```

Why it matters: `ObserveProperties` always appends `BindingContext` to the watched set, so a zero-property call produced a live subscription that fired **once at attach and never again** — no exception, no warning, just a control frozen at its initial state. The string-list overloads (`IEnumerable<string>`) can't be checked at compile time, so they throw `ArgumentException` on an empty list. Singular `ObserveProperty` was never affected (its single property argument is required).

---

## Gradients

### FillGradient (any SkiaControl)

```csharp
FillGradient = new SkiaGradient()
{
    Type = GradientType.Linear,
    StartXRatio = 0, StartYRatio = 0,
    EndXRatio = 0,   EndYRatio = 1,   // vertical: top→bottom
    // EndXRatio = 1, EndYRatio = 0   // horizontal: left→right
    Colors = new List<Color>
    {
        Color.FromHex("#FFFFFF"),
        Color.FromHex("#FF0000"),
    },
    ColorPositions = new List<double> { 0.0, 1.0 },  // optional; default evenly spaced
    Opacity = 0.8,
}
```

### Photo-legibility scrim overlay (verified user-corrected pattern, 2026-08-15)

Overlay gradient scrim over an image (tile label legibility): plain `SkiaLayer` + `FillGradient`, NO SkiaShape and NO BackgroundColor needed. Use NEAR-transparent/NEAR-opaque alpha endpoints (`#01000000` → `#FE000000`), not `#00`/`#FF`; control the fade start with `StartYRatio` (e.g. 0.5 = fade begins mid-tile). Remember `SkiaLayer` inside a `SkiaShape` needs explicit `VerticalOptions = Fill` to cover it.

```csharp
new SkiaLayer
{
    VerticalOptions = LayoutOptions.Fill,
    FillGradient = new SkiaGradient
    {
        StartYRatio = 0.5f,
        Colors = new List<Color> { Color.Parse("#01000000"), Color.Parse("#FE000000") },
    },
}
```

Related: for tighter multi-line display headlines prefer negative `ParagraphSpacing` (e.g. `-0.2`) over `LineHeight < 1`.

### StrokeGradient (SkiaShape only)

Same structure, assigned to `StrokeGradient` property.

Gradient types (`GradientType`): `Linear` (ratios or `Angle`), `Circular` (radial — center via `StartXRatio`/`StartYRatio`), `Oval`, `Sweep`, `Conical`.

### Dynamic ColorPositions (binding to computed positions)

```csharp
private SkiaGradient _fooGradient;

// In constructor — set gradient reference before wiring:
_fooGradient = new SkiaGradient() { ... };

new SomeControl() { FillGradient = _fooGradient }.Assign(out Foo);

// Wire after children created:
Foo.PropertyChanged += (s, e) =>
{
    if (e.PropertyName == nameof(Foo.Points))
        _fooGradient.ColorPositions = Foo.Points;
};
```

---

## Colors

```csharp
Color.FromHex("#ee281D")        // 6-digit: #rrggbb, full opacity
Color.FromHex("#22000000")      // 8-digit: #aarrggbb  (aa=alpha first)
Color.FromHex("#3F00")          // 4-digit: #argb
Color.FromHex("#F00")           // 3-digit: #rgb
"#ee281D".ToColor()             // equivalent string extension
```

Named colors: `Colors.White`, `Colors.Black`, `Colors.Red`, `Colors.DarkRed`,
`Colors.Orange`, `Colors.Green`, `Colors.Gray`, `Colors.Transparent`

**Pitfall:** `Colors.FromRgba(double, double, double, double)` expects 0–1 floats.
`Colors.FromRgb(int,int,int)` / `Colors.FromRgba(int,int,int,int)` take 0–255.

---

## Shadows

```csharp
Shadows = new List<SkiaShadow>()
{
    new SkiaShadow() { Blur = 8, Opacity = 0.15, X = 0, Y = 4, Color = Color.FromHex("#000000") },
}
```

Caching + shadows: cache the shadowed control DIRECTLY (`UseCache = SkiaCacheType.Image` on the shape itself) — the engine auto-expands cache/clip/dirty region to fit shadows and glow (legacy `Shadows`, MAUI `Shadow`, `DropShadowEffect`/`OuterGlowEffect` all included). Do NOT copy the OLD pattern of wrapping in a cached container "to avoid clipping the shadow" — obsolete, wastes a layer.

---

## SkiaLottie

```csharp
new SkiaLottie()
{
    Source = "Path/To/animation.json",
    AutoPlay = false,
    DefaultFrame = -1,      // -1 = last frame when stopped
    Repeat = -1,            // -1 = infinite loop
    SpeedRatio = 0.6,
    LockRatio = 1,
    UseCache = SkiaCacheType.ImageDoubleBuffered,
}
```

---

## SkiaImageTiles

```csharp
new SkiaImageTiles()
{
    Source = "Space/Sprites/stars.png",
    TileAspect = TransformAspect.Cover,    // NOT AspectCover — different enum value
    TileWidth = 300,
    TileHeight = 300,
    TileCacheType = SkiaCacheType.Image,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
}.Assign(out ParallaxLayer)
```

Scroll animation: mutate `ParallaxLayer.TileOffsetY` in game loop / animation tick.

---

## SkiaBackdrop (blur effect)

```csharp
new SkiaBackdrop()
{
    BackgroundColor = Color.FromHex("#22000000"),
    Blur = 3,
    HorizontalOptions = LayoutOptions.Fill,
    VerticalOptions = LayoutOptions.Fill,
    ZIndex = -1,
}
```

Place inside a `SkiaShape` child to clip the blur to rounded corners.
**Blazor:** requires `SkiaBackdrop.cs` in Shared project — verify before porting.

---

## ItemTemplate in code-behind

`ItemTemplateType` was REMOVED (breaking). The current idiom is a `DataTemplate` factory, which is also where per-cell wiring goes:

```csharp
new SkiaStack()
{
    RecyclingTemplate    = RecyclingTemplate.Enabled,
    MeasureItemsStrategy = MeasuringStrategy.MeasureVisible,
    ItemsSource          = Model.Items,
    ItemTemplate = new DataTemplate(() =>
    {
        var cell = new CellServiceRequest();
        cell.AnimationTapped  = SkiaTouchAnimation.Ripple;
        cell.TouchEffectColor = AppColors.PrimaryLight;
        cell.Tapped += (s, a) => Model.CommandRequestDetails.Execute(cell.BindingContext);
        return cell;
    }),
}
```

The factory body is plain imperative C# — a local `var cell` + `+=` here is correct, not the composition anti-pattern (there is no children list and no chain).

### Recycled cell — where the update code goes

Children are pre-created in the constructor; at runtime only properties change. Two hooks on `SkiaDynamicDrawnCell`:

```csharp
protected override void OnBindingContextChanged()   // whole context swapped (recycle)
{
    base.OnBindingContextChanged();
    if (BindingContext is ServiceRequest dto) { labelService.Text = dto.Service; SetStatus(dto); }
}

protected override void ContextPropertyChanged(object sender, PropertyChangedEventArgs e)  // context mutated in place
{
    base.ContextPropertyChanged(sender, e);
    if (BindingContext is ServiceRequest model
        && e.PropertyName.IsEither(nameof(ServiceRequest.Address), nameof(ServiceRequest.AddressSub)))
        SetAddressTo(model);
}
```

Prefer `ContextPropertyChanged` over per-control `.ObserveProperty(...)` inside recycled cells — one subscription per cell instead of N, and nothing to unsubscribe on rebind.

### Teardown — `OnWillDisposeWithChildren()`

The DrawnUI cleanup hook (`SkiaControl.Shared.cs:8725`), not `Dispose(bool)`. Use it to unsubscribe app-level messengers/singletons; fluent observers (`.Observe*`) and `.Animate` unregister themselves.

```csharp
public override void OnWillDisposeWithChildren()
{
    base.OnWillDisposeWithChildren();
    App.Instance.Messager.Unsubscribe(this, AppMessages.NavigatedToView);
}
```

### `IsParentIndependent`

Set on a child whose size changes at runtime (status labels, send bar, chat entry) so its remeasure does not propagate up and force the parent to remeasure. Needs an explicit size on the child; unnecessary when the parent has Fill or an explicit size.

## XAML → Code-Behind Porting

Check platform availability first — some controls are MAUI-only on other heads (`SkiaMauiElement`, `SkiaCamera`; verify others by grepping the class in `src/Shared/Shared.projitems` and the target head's csproj excludes).

- Every `x:Name="Foo"` → `private ControlType Foo;` field + `.Assign(out Foo)` on the inline construction.
- Layout type: prefer alias controls (`Type="Column"` → `SkiaStack`, `"Row"` → `SkiaRow`, `"Wrap"` → `SkiaWrap`, `"Grid"` → `SkiaGrid`, absolute → `SkiaLayer`) — but note base `SkiaLayout` doesn't Fill by default while most aliases do; preserve the original's effective alignment.
- `{Binding Prop}` → `.ObserveProperty(...)`; `AddGestures.CommandTapped` → `.OnTapped(...)`; `Tapped="Handler"` → `.OnTapped(...)`.
- `OnPlatform` overrides: single-target ports drop the irrelevant branches (e.g. Blazor = WASM only — drop `WinUI` override, keep the default value).

Enum-name traps:

| XAML | C# | Notes |
|------|----|----|
| `Aspect="AspectCover"` | `TransformAspect.AspectCover` | SkiaImage |
| `TileAspect="Cover"` | `TransformAspect.Cover` | SkiaImageTiles — NOT AspectCover |
| `UseCache="ImageDoubleBuffered"` | `SkiaCacheType.ImageDoubleBuffered` | |
| `FillBlendMode="Color"` | `SKBlendMode.Color` | needs `using SkiaSharp` |
| `HorizontalTextAlignment="Center"` | `DrawTextAlignment.Center` | NOT MAUI TextAlignment |
| `HeightRequest="-1"` | `HeightRequest = -1` | auto size |

Port checklist: fields declared → control availability per head → bindings converted → gestures converted → gradients inline → OnPlatform resolved → `Color.FromHex` 8-digit is `#aarrggbb` → `SkiaBackdrop` placed inside shape for corner clipping.
