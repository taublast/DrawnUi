using Microsoft.JSInterop;

namespace DrawnUi.Draw;

public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
{
    private bool _isSubscribedToKeyboard;
    private int _stubSelectionStop = -1;
    // Active (moving) edge of a keyboard shift-selection. _stubSelectionStop is the fixed anchor;
    // this is the caret side that grows/shrinks. Tracked separately because CursorPosition is
    // pinned to the selection's LEFT edge (the overlay renders [CursorPosition, +SelectionLength]).
    private int _selectionMovingEdge = -1;
    private CancellationTokenSource _deferCts;

    // On Blazor, Label.Lines is populated during the render pass, which happens after
    // property-change events. Restarting the timer on every text change ensures we wait
    // until after the LAST mutation before repositioning the cursor.
    // _suppressImmediateCursorMove must be set BEFORE CursorPosition is changed by the
    // caller so the property-changed MoveInternalCursor() is blocked while label data is stale.
    private async void DeferVisualCursorUpdate()
    {
        _deferCts?.Cancel();
        _deferCts = new CancellationTokenSource();
        var token = _deferCts.Token;
        try
        {
            await Task.Delay(32, token);
            _suppressImmediateCursorMove = false;
            MoveInternalCursor();
        }
        catch (OperationCanceledException)
        {
            // New timer started; its caller already set _suppressImmediateCursorMove=true
            // before touching CursorPosition, so clearing here is safe and prevents
            // the flag from getting permanently stuck if no further call follows.
            _suppressImmediateCursorMove = false;
        }
    }

    public int NativeSelectionStart => CursorPosition;

    public void SetCursorPositionNative(int position, int stop = -1)
    {
        _stubSelectionStop = stop;
    }

    // No native text input here: keyboard type, password mode and IsSpellCheckEnabled
    // have no platform counterpart to apply.
    public void ApplyKeyboardType() { }

    public void DisposePlatform()
    {
        SubscribeToKeyboard(false);
    }

    public void SetFocusNative(bool focus)
    {
        // Guard against timer race: if the 50ms defocus task fires AFTER a refocus task
        // has already run, IsFocused will be true while focus=false — skip the unsubscribe.
        if (!focus && IsFocused) return;
        SubscribeToKeyboard(focus);

        // Take real DOM focus away from any external page text input (e.g. a Monaco code
        // editor sharing the page): a drawn editor has no DOM element, so without this the
        // browser keeps sending physical keys — Enter especially — to that input too.
        if (focus)
            KeyboardManager.BlurExternalTextInput();
    }

    public void UpdateNativePosition() { }

    private void SubscribeToKeyboard(bool subscribe)
    {
        if (subscribe == _isSubscribedToKeyboard)
            return;
        _isSubscribedToKeyboard = subscribe;
        if (subscribe)
        {
            KeyboardManager.KeyDown += OnKeyDown;
            KeyboardManager.KeyChar += OnKeyChar;
        }
        else
        {
            KeyboardManager.KeyDown -= OnKeyDown;
            KeyboardManager.KeyChar -= OnKeyChar;
        }
    }

    private void OnKeyDown(object? sender, InputKey key)
    {
        var shift = KeyboardManager.IsShiftPressed;
        var ctrl = KeyboardManager.IsControlPressed;
        var alt = KeyboardManager.IsAltPressed;

        switch (key)
        {
            case InputKey.Backspace:
                StubBackspace();
                break;
            case InputKey.Delete:
                StubDelete();
                break;
            case InputKey.Enter:
                StubPressEnter(alt, shift);
                break;
            case InputKey.ArrowLeft:
                StubMoveCursor(-1, shift);
                break;
            case InputKey.ArrowRight:
                StubMoveCursor(1, shift);
                break;
            case InputKey.ArrowUp when IsMultiline:
                HandleVerticalArrow(true);
                break;
            case InputKey.ArrowDown when IsMultiline:
                HandleVerticalArrow(false);
                break;
            case InputKey.Home:
                StubMoveCursor(-CursorPosition, shift);
                break;
            case InputKey.End:
                StubMoveCursor((Text?.Length ?? 0) - CursorPosition, shift);
                break;
            case InputKey.KeyA when ctrl:
                StubSelectAll();
                break;
            case InputKey.KeyC when ctrl:
                CopySelection();
                break;
            case InputKey.KeyX when ctrl:
                CutSelection();
                break;
            case InputKey.KeyV when ctrl:
                PasteFromClipboard();
                break;
        }
    }

    private void OnKeyChar(object? sender, string ch)
    {
        StubTypeText(ch);
    }

    public void StubTypeText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return;
        ReplaceSelection(value);
    }

    public void StubPressEnter(bool splitLine = false, bool shift = false)
    {
        if (IsMultiline)
        {
            if (!splitLine && !shift && ShouldSubmitOnEnter)
            {
                ExecuteSubmit(clearFocus: false);
                return;
            }

            ReplaceSelection(GetEditorBreakText(splitLine));
            return;
        }

        ExecuteSubmit(clearFocus: false);
    }

    public void StubBackspace(int count = 1)
    {
        if (count <= 0)
            return;

        if (HasSelection)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var text = Text ?? string.Empty;
        if (text.Length == 0)
            return;

        var remove = CodeUnitsBeforeCaret(text, CursorPosition, count);
        if (remove == 0)
            return;

        var start = CursorPosition - remove;
        Text = text.Remove(start, remove);
        _suppressImmediateCursorMove = true;
        CursorPosition = start;
        SelectionLength = 0;
        DeferVisualCursorUpdate();
    }

    public void StubDelete(int count = 1)
    {
        if (count <= 0)
            return;

        if (HasSelection)
        {
            ReplaceSelection(string.Empty);
            return;
        }

        var text = Text ?? string.Empty;
        if (text.Length == 0 || CursorPosition >= text.Length)
            return;

        var remove = CodeUnitsAfterCaret(text, CursorPosition, count);
        if (remove == 0)
            return;
        Text = text.Remove(CursorPosition, remove);
        _suppressImmediateCursorMove = true;
        SelectionLength = 0;
        DeferVisualCursorUpdate();
    }

    public void StubMoveCursor(int delta, bool extendSelection = false)
    {
        var textLength = Text?.Length ?? 0;

        if (extendSelection)
        {
            // Anchor stays put; only the moving edge follows the arrow. Re-seed both when there is
            // no live selection (including after a shift-select collapsed back onto the anchor).
            if (!HasSelection)
            {
                _stubSelectionStop = CursorPosition;
                _selectionMovingEdge = CursorPosition;
            }

            _selectionMovingEdge = Math.Clamp(_selectionMovingEdge + delta, 0, textLength);
            SelectionLength = Math.Abs(_selectionMovingEdge - _stubSelectionStop);
            CursorPosition = Math.Min(_selectionMovingEdge, _stubSelectionStop);
            return;
        }

        // Plain arrow with a live selection collapses to the edge in the move direction.
        if (HasSelection)
            CursorPosition = delta < 0 ? CursorPosition : CursorPosition + SelectionLength;
        else
            CursorPosition = Math.Clamp(CursorPosition + delta, 0, textLength);

        SelectionLength = 0;
        _stubSelectionStop = -1;
        _selectionMovingEdge = -1;
    }

    public void StubSelectRange(int start, int length)
    {
        var textLength = Text?.Length ?? 0;
        var normalizedStart = Math.Clamp(start, 0, textLength);
        var normalizedLength = Math.Clamp(length, 0, textLength - normalizedStart);
        CursorPosition = normalizedStart;
        SelectionLength = normalizedLength;
        _stubSelectionStop = normalizedStart;
        _selectionMovingEdge = normalizedStart + normalizedLength;
    }

    public void StubSelectAll()
    {
        StubSelectRange(0, Text?.Length ?? 0);
    }

    partial void SeedKeyboardSelection(int anchor, int movingEdge)
    {
        _stubSelectionStop = anchor;
        _selectionMovingEdge = movingEdge;
    }

    private bool HasSelection => SelectionLength > 0;

    partial void OnSelectionDeleted() => DeferVisualCursorUpdate();

    partial void OnTextInsertedAtCursor() => DeferVisualCursorUpdate();

    public void CopySelection()
    {
        var text = GetSelectedText();
        if (string.IsNullOrEmpty(text)) return;
        _ = CopyToClipboardAsync(text);
    }

    public void PasteFromClipboard()
    {
        _ = PasteFromClipboardAsync();
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            var js = Super.Services?.GetService(typeof(IJSRuntime)) as IJSRuntime;
            if (js != null)
                await js.InvokeVoidAsync("navigator.clipboard.writeText", text);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaEditor] CopyToClipboard: {e.Message}");
        }
    }

    private async Task PasteFromClipboardAsync()
    {
        try
        {
            var js = Super.Services?.GetService(typeof(IJSRuntime)) as IJSRuntime;
            if (js != null)
            {
                var text = await js.InvokeAsync<string>("navigator.clipboard.readText");
                if (!string.IsNullOrEmpty(text))
                {
                    InsertAtCursor(text);
                    DeferVisualCursorUpdate();
                }
            }
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"[SkiaEditor] PasteFromClipboard: {e.Message}");
        }
    }

    private void ReplaceSelection(string insertedText)
    {
        var text = Text ?? string.Empty;
        var selectionStart = Math.Clamp(CursorPosition, 0, text.Length);
        var selectionLength = Math.Clamp(SelectionLength, 0, text.Length - selectionStart);
        var normalized = NormalizeEditorInput(insertedText);

        Text = text.Remove(selectionStart, selectionLength).Insert(selectionStart, normalized);
        _suppressImmediateCursorMove = true;
        CursorPosition = selectionStart + normalized.Length;
        SelectionLength = 0;
        _stubSelectionStop = -1;
        DeferVisualCursorUpdate();
    }
}
