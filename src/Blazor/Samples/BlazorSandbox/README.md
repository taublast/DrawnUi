# BlazorSandbox

## Full-height page layout in a fresh Blazor template

The default Blazor WebAssembly template gives you the standard `MainLayout` shell, but it does not make the page content area stretch to the full viewport height. That becomes a problem for DrawnUI or canvas-based pages because `LayoutOptions.Fill` can only fill the height that the HTML layout actually gives it.

The fix is in the layout CSS, not in the page content.

### What to change

In `wwwroot/css/app.css`, make the shell establish a full-height chain from `body` down to the content container:

```css
html,
body,
#app {
	min-height: 100%;
}

body {
	min-height: 100vh;
}

#app > .page {
	min-height: 100vh;
}

#app > .page > main {
	display: flex;
	flex: 1 1 auto;
	flex-direction: column;
	min-width: 0;
}

#app > .page > main > .content {
	display: flex;
	flex: 1 1 auto;
	flex-direction: column;
	min-height: 0;
}
```

That gives the page body a real vertical size to fill.

### If your page hosts a canvas or DrawnUI root

If the page contains a single host element that should consume the remaining height, make that host flexible inside the content column.

In this sandbox the generated DrawnUI host is `.xaml-element`, so the extra rule is:

```css
#app > .page > main > .content > .xaml-element {
	flex: 1 1 auto;
	min-height: 0;
}
```

This keeps the fix layout-only. The page markup and DrawnUI content do not need to change.

### Why this is needed

Without these rules, the template layout lets `article.content` size itself to its children. A canvas-based surface then gets only its intrinsic HTML height instead of the remaining viewport height, so it looks like vertical fill is broken even when the DrawnUI control is configured correctly.

### Current sandbox result

The homepage in this sample was verified in the browser after this layout fix:

- `main` stretches to the viewport height.
- `article.content` stretches to the remaining height under the top row.
- The DrawnUI host fills the remaining vertical space without changing `Pages/Home.razor`.
