function getOffset(element, event) {
    const rect = element.getBoundingClientRect();
    return {
        x: event.clientX - rect.left,
        y: event.clientY - rect.top,
        inside: event.clientX >= rect.left && event.clientX <= rect.right && event.clientY >= rect.top && event.clientY <= rect.bottom
    };
}

function detachInternal(element) {
    const state = element.__drawnUiGestures;
    if (!state) {
        return;
    }

    for (const [name, handler] of Object.entries(state.handlers)) {
        element.removeEventListener(name, handler);
    }

    delete element.__drawnUiGestures;
}

export function attachCanvasGestures(element, dotNetRef, enabled) {
    detachInternal(element);

    if (!enabled) {
        return;
    }

    const pointerHandler = (type) => async (event) => {
        event.preventDefault();
        const offset = getOffset(element, event);
        try {
            await dotNetRef.invokeMethodAsync('OnCanvasPointer', {
                type,
                pointerId: event.pointerId ?? 0,
                offsetX: offset.x,
                offsetY: offset.y,
                button: event.button ?? 0,
                buttons: event.buttons ?? 0,
                pointerType: event.pointerType ?? 'mouse',
                pressure: event.pressure ?? 0,
                isInsideView: offset.inside
            });
        } catch (error) {
            console.error('[canvasGestures] pointer failed', type, error?.message ?? error);
        }
    };

    const wheelHandler = async (event) => {
        event.preventDefault();
        const offset = getOffset(element, event);
        try {
            await dotNetRef.invokeMethodAsync('OnCanvasWheel', {
                offsetX: offset.x,
                offsetY: offset.y,
                deltaY: event.deltaY ?? 0,
                buttons: event.buttons ?? 0
            });
        } catch (error) {
            console.error('[canvasGestures] wheel failed', error?.message ?? error);
        }
    };

    const handlers = {
        pointerdown: pointerHandler('pointerdown'),
        pointermove: pointerHandler('pointermove'),
        pointerup: pointerHandler('pointerup'),
        pointercancel: pointerHandler('pointercancel'),
        pointerleave: pointerHandler('pointerleave'),
        wheel: wheelHandler
    };

    for (const [name, handler] of Object.entries(handlers)) {
        element.addEventListener(name, handler, { passive: false });
    }

    element.__drawnUiGestures = { handlers };
}

export function detachCanvasGestures(element) {
    detachInternal(element);
}

export function requestCanvasFrame(element, width, height) {
    if (!element) {
        return false;
    }

    const canvas = element.querySelector('canvas');
    if (!canvas || !canvas.SKHtmlCanvas) {
        return false;
    }

    canvas.SKHtmlCanvas.requestAnimationFrame(undefined, width, height);
    return true;
}