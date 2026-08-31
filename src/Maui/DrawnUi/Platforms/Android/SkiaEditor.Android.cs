using Android.Content;
using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using DrawnUi.Draw;
using Java.Lang;
using System.Diagnostics;
using Exception = System.Exception;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        private int _hiddenEditTextId;
        private ViewGroup _layout;
        private MyTextWatcher _textListener;
        private EventHandler<Android.Views.View.FocusChangeEventArgs> _focusChangeListener;
        private bool _updatingText;
        protected Android.Widget.EditText Control { get; set; }

        public int NativeSelectionStart => Control?.SelectionStart ?? 0;

        public void ApplyKeyboardType()
        {
            if (Control == null) return;

            InputTypes inputType;
            if (IsPassword)
            {
                inputType = InputTypes.ClassText | InputTypes.TextVariationPassword;
                Control.TransformationMethod = Android.Text.Method.PasswordTransformationMethod.Instance;
            }
            else
            {
                Control.TransformationMethod = null;
                inputType = KeyboardType switch
                {
                    SkiaEditorKeyboard.Numeric  => InputTypes.ClassNumber,
                    SkiaEditorKeyboard.Decimal  => InputTypes.ClassNumber | InputTypes.NumberFlagDecimal,
                    SkiaEditorKeyboard.Phone    => InputTypes.ClassPhone,
                    SkiaEditorKeyboard.Email    => InputTypes.ClassText | InputTypes.TextVariationEmailAddress,
                    _                           => InputTypes.ClassText
                };
                if (inputType == InputTypes.ClassText && MaxLines != 1)
                    inputType |= InputTypes.TextFlagMultiLine;
            }

            if (!IsSpellCheckEnabled)
                inputType |= InputTypes.TextFlagNoSuggestions;

            Control.InputType = inputType;
        }

        public void DisposePlatform()
        {
            try
            {
                AddObservers(false);
                if (_focusChangeListener != null)
                {
                    Control.FocusChange -= _focusChangeListener;
                    _focusChangeListener = null;
                }
                _layout?.RemoveView(Control);
                _layout = null;
                Control = null;
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public void UpdateNativePosition()
        {
            if (Control != null && _layout != null)
            {
                // Get the layout's position on the screen
                int[] layoutLocation = new int[2];
                _layout.GetLocationOnScreen(layoutLocation);

                // Calculate the absolute position of the pseudo-entry on the screen
                int pseudoEntryPositionX = layoutLocation[0] + (int)DrawingRect.Left;
                int pseudoEntryPositionY = layoutLocation[1] + (int)DrawingRect.Bottom;

                // Set the position of the hidden EditText
                Control.SetX(pseudoEntryPositionX);
                Control.SetY(pseudoEntryPositionY);
            }
        }

        protected void AddObservers(bool add)
        {
            if (add)
            {
                _textListener = new MyTextWatcher(this);
                Control.AddTextChangedListener(_textListener);
                Control.EditorAction += Control_EditorAction;

                _focusChangeListener = (s, e) =>
                {
                    if (e.HasFocus && Control != null)
                    {
                        var pos = System.Math.Max(0, System.Math.Min(CursorPosition, Control.Text?.Length ?? 0));
                        try
                        {
                            if (SelectionLength > 0)
                                Control.SetSelection(pos, System.Math.Min(pos + SelectionLength, Control.Text?.Length ?? 0));
                            else
                                Control.SetSelection(pos);
                        }
                        catch { }
                    }
                };
                Control.FocusChange += _focusChangeListener;
            }
            else
            {
                if (Control != null)
                {
                    Control.RemoveTextChangedListener(_textListener);
                    Control.EditorAction -= Control_EditorAction;
                    if (_focusChangeListener != null)
                    {
                        Control.FocusChange -= _focusChangeListener;
                        _focusChangeListener = null;
                    }
                }
                _textListener?.Dispose();
                _textListener = null;
            }
        }

        partial void SyncNativeText()
        {
            if (Control == null || _updatingText)
                return;

            var newText = Text ?? string.Empty;
            if ((Control.Text ?? string.Empty) == newText)
                return;

            _updatingText = true;
            try { Control.Text = newText; }
            finally { _updatingText = false; }
        }

        private void Control_EditorAction(object sender, TextView.EditorActionEventArgs e)
        {
            if (IsMultiline)
            {
                // Hardware Shift+Enter keeps inserting a line break even with ReturnType.Send.
                var shiftPressed = e.Event?.IsShiftPressed ?? false;
                if (!ShouldSubmitOnEnter || shiftPressed)
                {
                    // Let the native EditText insert the line break and advance selection.
                    e.Handled = false;
                    return;
                }
            }

            e.Handled = true;
            ExecuteSubmit(clearFocus: false);
            return;

            if (e.ActionId == ImeAction.Done ||
                (e.Event != null && e.Event.KeyCode == Keycode.Enter && e.Event.Action == KeyEventActions.Down))
            {
                // User has pressed the "Done" key or the "Enter" key.
                // Insert your own logic here.
                e.Handled = true;
                ExecuteSubmit(clearFocus: false);
            }
            else
            {
                e.Handled = false;
            }
        }

        protected override void OnPropertyChanged(string propertyName = null)
        {
            base.OnPropertyChanged(propertyName);

            //todo use mapper
            if (Control == null)
                return;

            if (propertyName == nameof(MaxLines))
            {
                if (MaxLines == 1)
                {
                    Control.SetMaxLines(1);
                    Control.SetSingleLine(true);
                }
                else
                {
                    if (MaxLines > 1)
                    {
                        Control.SetMaxLines(1);
                    }
                    else
                    {
                        Control.SetMaxLines(0);
                    }
                    Control.SetSingleLine(false);
                }
            }
        }


        void CreateNativeControl()
        {
            _hiddenEditTextId = GenerateUniqueId();
            _layout = (ViewGroup)Superview.Handler?.PlatformView;
            if (_layout != null)
            {
                Control = new HiddenEditText(_layout.Context);
                // 1x1 minimum — 0x0 blocks IME input connection on Android 10+
                Control.LayoutParameters = new ViewGroup.LayoutParams(1, 1);
                Control.Id = _hiddenEditTextId;
                Control.Alpha = 0f;

                _updatingText = true;
                try { Control.Text = this.Text ?? string.Empty; }
                finally { _updatingText = false; }

                UpdateNativePosition();
                AddObservers(true);
                _layout.AddView(Control);
            }
        }

        public void SetFocusNative(bool focus, bool closeKeyboard = true)
        {
            try
            {
                if (Control == null)
                {
                    CreateNativeControl();
                }

                if (Control == null) return;

                if (focus)
                {
                    _updatingText = true;
                    try { ApplyKeyboardType(); }
                    finally { _updatingText = false; }

                    // Guard SetReturnType: SetSingleLine() internally triggers onTextChanged
                    // with SelectionStart=0 on first focus (selection not yet positioned),
                    // which would queue SetCursorPositionWithDelay(50, 0) and jump cursor.
                    _updatingText = true;
                    try { SetReturnType(this.ReturnType); }
                    finally { _updatingText = false; }

                    if (!_updatingText)
                    {
                        _updatingText = true;
                        try { Control.Text = this.Text ?? string.Empty; }
                        finally { _updatingText = false; }
                    }

                    Control.RequestFocus();

                    // Defer IME show and cursor to after the view layout pass so:
                    // 1. ShowSoftInput fires after the view is properly focused (no toggle needed)
                    // 2. SetSelection fires after IME connects (prevents IME from resetting cursor to 0)
                    // Flags NONE (explicit request): this path runs from a user tap or a programmatic
                    // SetFocus — an IMPLICIT request is ignorable by the IME and reliably fails to
                    // re-show the keyboard after a BACK dismiss (the hidden EditText never lost native
                    // focus — ClearFocus() re-focuses the only focusable view — so RequestFocus has no
                    // transition for the implicit show to piggyback on).
                    InputMethodManager imm = (InputMethodManager)Platform.AppContext.GetSystemService(Context.InputMethodService);
                    imm.ShowSoftInput(Control, (ShowFlags)0); // 0 = explicit request (binding has no ShowFlags.None)
                    var pos = System.Math.Max(0, System.Math.Min(CursorPosition, Control.Text?.Length ?? 0));
                    try
                    {
                        if (SelectionLength > 0)
                            Control.SetSelection(pos,
                                System.Math.Min(pos + SelectionLength, Control.Text?.Length ?? 0));
                        else
                            Control.SetSelection(pos);
                    }
                    catch (Exception e)
                    {
                        Super.Log(e);
                    }
                }
                else
                {
                    PlatformClearFocusNow();

                    Control.ClearFocus();
                    if (closeKeyboard)
                        CloseKeyboard();
                }
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e);
            }

        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (Control == null)
                return;
            try
            {
                if (stop > 0)
                    Control.SetSelection(position, stop);
                else
                    Control.SetSelection(position);
            }
            catch (System.Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        protected void CloseKeyboard()
        {
            InputMethodManager imm = (InputMethodManager)Platform.AppContext.GetSystemService(Context.InputMethodService);
            imm.HideSoftInputFromWindow(Control.WindowToken, HideSoftInputFlags.None);
        }

        private CancellationTokenSource? _deferCts;

        private async void DeferVisualCursorUpdate()
        {
            _deferCts?.Cancel();
            _deferCts = new CancellationTokenSource();
            var token = _deferCts.Token;
            try
            {
                await Task.Delay(50, token);
                _suppressImmediateCursorMove = false;
                MoveInternalCursor();
            }
            catch (OperationCanceledException)
            {
                _suppressImmediateCursorMove = false;
            }
        }

        partial void OnSelectionDeleted() => DeferVisualCursorUpdate();

        partial void OnTextInsertedAtCursor() => DeferVisualCursorUpdate();

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }

        public void SetReturnType(ReturnType type)
        {

            switch (type)
            {
            case ReturnType.Go:
            Control.ImeOptions = ImeAction.Go;
            Control.SetSingleLine(true);
            Control.SetImeActionLabel(ActionGo, ImeAction.Go);
            break;
            case ReturnType.Next:
            Control.ImeOptions = ImeAction.Next;
            Control.SetSingleLine(true);
            Control.SetImeActionLabel(ActionNext, ImeAction.Next);
            break;
            case ReturnType.Send:
            if (IsMultiline)
            {
                // Send action on a multiline field: keep TextView multiline behavior but
                // report a non-multiline raw input type so the IME shows Send instead of Enter.
                Control.SetSingleLine(false);
                Control.ImeOptions = ImeAction.Send;
                Control.SetRawInputType(Android.Text.InputTypes.ClassText |
                                        Android.Text.InputTypes.TextFlagCapSentences);
            }
            else
            {
                Control.SetSingleLine(true);
                Control.ImeOptions = ImeAction.Send;
            }
            Control.SetImeActionLabel(ActionSend, ImeAction.Send);
            break;
            case ReturnType.Search:
            Control.SetSingleLine(true);
            Control.ImeOptions = ImeAction.Search;
            Control.SetImeActionLabel(ActionSearch, ImeAction.Search);
            break;
            default:
            Control.ImeOptions = ImeAction.Done;
            Control.SetSingleLine(this.MaxLines == 1);
            Control.SetImeActionLabel(ActionDone, ImeAction.Done);
            break;
            }
        }

        public class MyTextWatcher : Java.Lang.Object, ITextWatcher
        {
            protected override void Dispose(bool disposing)
            {
                _parent = null;

                base.Dispose(disposing);
            }

            private SkiaEditor _parent;

            public MyTextWatcher(SkiaEditor parent)
            {
                _parent = parent;
            }


            public void AfterTextChanged(IEditable s)
            {

                // Called when the text has been changed and the editing process is over.
                // This is where you can check the new cursor position.
                //_parent.Text = s.ToString();

                int selectionStart = _parent.Control.SelectionStart;
                int selectionEnd = _parent.Control.SelectionEnd;

                if (selectionStart != selectionEnd) // there is a text selection
                {
                    //todo process selection, not implemented yet
                    _parent.SetSelection(selectionStart, selectionEnd);
                }
            }


            public void BeforeTextChanged(ICharSequence s, int start, int count, int after)
            {
                // Called before the text is changed.
            }


            public void OnTextChanged(ICharSequence s, int start, int before, int count)
            {
                if (_parent._updatingText)
                    return;

                _parent._updatingText = true;
                try { _parent.Text = s.ToString(); }
                finally { _parent._updatingText = false; }

                int selectionStart = _parent.Control.SelectionStart;
                int selectionEnd = _parent.Control.SelectionEnd;

                if (selectionStart == selectionEnd)
                {
                    _parent.SetCursorPositionWithDelay(50, selectionStart);
                }
            }
        }

        // Intercepts soft-keyboard deleteSurroundingText and deletes directly in the Editable
        // buffer so the TextWatcher fires on every keyboard (Gboard, Samsung, MTK, CJK IMEs).
        // sendKeyEvent(KEYCODE_DEL) re-enters the input dispatch system and is unreliable.
        private class HiddenEditText : EditText
        {
            public HiddenEditText(Context context) : base(context) { }

            public override IInputConnection OnCreateInputConnection(EditorInfo outAttrs)
            {
                var inner = base.OnCreateInputConnection(outAttrs);
                return inner != null ? new BackspaceInputConnection(inner, true, this) : inner;
            }
        }

        private class BackspaceInputConnection : Android.Views.InputMethods.InputConnectionWrapper
        {
            private readonly EditText _view;

            public BackspaceInputConnection(IInputConnection target, bool mutable, EditText view)
                : base(target, mutable)
            {
                _view = view;
            }

            private bool DeleteAtCursor()
            {
                var content = _view.EditableText;
                if (content == null) return false;

                int selStart = _view.SelectionStart;
                int selEnd   = _view.SelectionEnd;

                if (selStart != selEnd)
                {
                    int from = System.Math.Min(selStart, selEnd);
                    int to   = System.Math.Max(selStart, selEnd);
                    content.Delete(from, to);
                    return true;
                }
                if (selStart > 0)
                {
                    content.Delete(selStart - 1, selStart);
                    return true;
                }
                return false;
            }

            public override bool DeleteSurroundingText(int beforeLength, int afterLength)
            {
                if (beforeLength == 1 && afterLength == 0)
                {
                    DeleteAtCursor();
                    return true;
                }
                return base.DeleteSurroundingText(beforeLength, afterLength);
            }

            // Some keyboards (e.g. older Samsung, AOSP) send KEYCODE_DEL via sendKeyEvent
            // instead of deleteSurroundingText — intercept that path too.
            public override bool SendKeyEvent(KeyEvent e)
            {
                if (e.Action == KeyEventActions.Down && e.KeyCode == Keycode.Del)
                {
                    if (DeleteAtCursor())
                        return true;
                }
                return base.SendKeyEvent(e);
            }
        }

    }
}
