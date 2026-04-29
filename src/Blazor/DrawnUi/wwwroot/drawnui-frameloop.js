let rafHandle = null;
let dotNetRef = null;
let maxFps = 0;
let lastTime = 0;

function tick(timestamp) {
    if (dotNetRef === null) return;

    rafHandle = requestAnimationFrame(tick);

    if (maxFps > 0) {
        const minInterval = 1000 / maxFps;
        if (timestamp - lastTime < minInterval) return;
    }

    lastTime = timestamp;
    dotNetRef.invokeMethodAsync('OnBrowserFrame', timestamp);
}

export function startFrameLoop(ref, fps) {
    dotNetRef = ref;
    maxFps = fps;
    if (rafHandle === null) {
        rafHandle = requestAnimationFrame(tick);
    }
}

export function updateMaxFps(fps) {
    maxFps = fps;
}

export function stopFrameLoop() {
    if (rafHandle !== null) {
        cancelAnimationFrame(rafHandle);
        rafHandle = null;
    }
    dotNetRef = null;
}
