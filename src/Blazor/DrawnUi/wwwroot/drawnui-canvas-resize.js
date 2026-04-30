const observers = new WeakMap();

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

    observers.set(element, resizeObserver);
    resizeObserver.observe(element);

    const rect = element.getBoundingClientRect();
    notifySize(element, dotNetRef, rect.width, rect.height);
}

export function detachCanvasHost(element) {
    const observer = observers.get(element);
    if (!observer) {
        return;
    }

    observer.disconnect();
    observers.delete(element);
}