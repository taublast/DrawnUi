const observers = new WeakMap();

function isElementFullscreen(element) {
    return document.fullscreenElement === element || document.webkitFullscreenElement === element;
}

function notifyFullscreen(element, dotNetRef) {
    dotNetRef.invokeMethodAsync('OnFullscreenChanged', isElementFullscreen(element));
}

function notifySize(element, dotNetRef, width, height) {
    const nextWidth = Math.max(1, Math.round(width));
    const nextHeight = Math.max(1, Math.round(height));
    dotNetRef.invokeMethodAsync('OnHostResized', nextWidth, nextHeight);
}

export function attachCanvasHost(element, dotNetRef) {
    detachCanvasHost(element);

    const resizeObserver = new ResizeObserver((entries) => {
        for (const entry of entries) {
            const box = entry.contentRect;
            notifySize(entry.target, dotNetRef, box.width, box.height);
        }
    });

    const onFullscreenChange = () => {
        const rect = element.getBoundingClientRect();
        notifySize(element, dotNetRef, rect.width, rect.height);
        notifyFullscreen(element, dotNetRef);
    };

    observers.set(element, { resizeObserver, onFullscreenChange });
    resizeObserver.observe(element);
    document.addEventListener('fullscreenchange', onFullscreenChange);
    document.addEventListener('webkitfullscreenchange', onFullscreenChange);

    const rect = element.getBoundingClientRect();
    notifySize(element, dotNetRef, rect.width, rect.height);
    notifyFullscreen(element, dotNetRef);
}

export function detachCanvasHost(element) {
    const state = observers.get(element);
    if (!state) {
        return;
    }

    state.resizeObserver.disconnect();
    document.removeEventListener('fullscreenchange', state.onFullscreenChange);
    document.removeEventListener('webkitfullscreenchange', state.onFullscreenChange);
    observers.delete(element);
}

export async function setCanvasFullscreen(element, enabled) {
    if (!element) {
        return false;
    }

    try {
        if (enabled) {
            if (isElementFullscreen(element)) {
                return true;
            }

            if (document.fullscreenElement && document.fullscreenElement !== element) {
                await document.exitFullscreen();
            }

            if (element.requestFullscreen) {
                await element.requestFullscreen();
            } else if (element.webkitRequestFullscreen) {
                await element.webkitRequestFullscreen();
            }

            return isElementFullscreen(element);
        }

        if (isElementFullscreen(element)) {
            if (document.exitFullscreen) {
                await document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                await document.webkitExitFullscreen();
            }
        }
    } catch {
    }

    return isElementFullscreen(element);
}