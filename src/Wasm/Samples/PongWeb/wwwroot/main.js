// Entry point for PongWeb (pure WASM, no Blazor).
// Loads the .NET runtime, wires DrawnUI.Web callbacks, runs the app.

import { dotnet } from './_framework/dotnet.js';
import { setModuleExports, showLoader, hideLoader, showError } from './_content/DrawnUi.Web/drawnui-web.js';

// Recursively find the [JSExport] Main in an exports tree (no per-app namespace hardcode).
function findExport(obj, name) {
    for (const key in obj) {
        const val = obj[key];
        if (key === name && typeof val === 'function') return val;
        if (val && typeof val === 'object') {
            const found = findExport(val, name);
            if (found) return found;
        }
    }
    return null;
}

console.log('PongWeb: Starting...');

// Expose dotnet runtime globally so drawnui-web.js can find Emscripten GL module
globalThis.dotnet = dotnet;

showLoader();

try {
    const { getAssemblyExports, getConfig } = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = getConfig();

    // Input / frame / resize callbacks live in the DrawnUi.Wasm library assembly
    // (assembly name is DrawnUi.Wasm; the DrawnUi.Web name is only the RCL asset base path).
    const lib = await getAssemblyExports('DrawnUi.Wasm');
    const Input = lib.DrawnUi.Draw.WebInput;
    const Super = lib.DrawnUi.Draw.Super;
    const Host = lib.DrawnUi.Draw.BrowserHost;

    setModuleExports({
        onBrowserFrame: Super.OnBrowserFrame,
        onCanvasResize: Host.OnCanvasResize,
        onPointerDown: Input.OnPointerDown,
        onPointerMove: Input.OnPointerMove,
        onPointerUp: Input.OnPointerUp,
        onPointerCancel: Input.OnPointerCancel,
        onWheel: Input.OnWheel,
        onContextMenu: Input.OnContextMenu,
        onKeyDown: Input.OnKeyDown,
        onKeyUp: Input.OnKeyUp,
    });

    // App entry point: builds DrawnUI and runs the Canvas (RunAsync does all glue).
    const app = await getAssemblyExports(config.mainAssemblyName);
    const main = findExport(app, 'Main');
    if (!main) throw new Error(`No [JSExport] Main found in ${config.mainAssemblyName}`);
    main();

    hideLoader();
    console.log('PongWeb: Ready!');

} catch (error) {
    console.error('PongWeb: Failed to start:', error);
    showError('Failed to load: ' + error.message);
}
