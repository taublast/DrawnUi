namespace DrawnUi.Draw;

public partial class SkiaEditor
{
    private int _stubSelectionStop = -1;
    // Active (moving) edge of a keyboard shift-selection. _stubSelectionStop is the fixed anchor;
    // this is the caret side that grows/shrinks. Tracked separately because CursorPosition is
    // pinned to the selection's LEFT edge (the overlay renders [CursorPosition, +SelectionLength]).
    private int _selectionMovingEdge = -1;
    private CancellationTokenSource? _deferCts;

    // Label.Lines is populated during the render pass, which happens after property-change
    // events. Suppress the immediate MoveInternalCursor() and defer until after the next render.
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
            _suppressImmediateCursorMove = false;
        }
    }

    public void SetCursorPositionNative(int position, int stop = -1)
    {
        _stubSelectionStop = stop;
    }

    public void DisposePlatform()
    {
    }

    public void SetFocusNative(bool focus)
    {
    }

    // No native text input here: keyboard type, password mode and IsSpellCheckEnabled
    // have no platform counterpart to apply.
    public void ApplyKeyboardType()
    {
    }

    partial void PlatformClearFocusNow()
    {
    }

    public void UpdateNativePosition()
    {
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

    private void ReplaceSelection(string insertedText)
    {
        var text = Text ?? string.Empty;
        var selectionStart = Math.Clamp(CursorPosition, 0, text.Length);
        var selectionLength = Math.Clamp(SelectionLength, 0, text.Length - selectionStart);
        var normalizedInsertedText = NormalizeEditorInput(insertedText);

        var updated = text.Remove(selectionStart, selectionLength).Insert(selectionStart, normalizedInsertedText);

        Text = updated;
        _suppressImmediateCursorMove = true;
        CursorPosition = selectionStart + normalizedInsertedText.Length;
        SelectionLength = 0;
        _stubSelectionStop = -1;
        DeferVisualCursorUpdate();
    }
}
