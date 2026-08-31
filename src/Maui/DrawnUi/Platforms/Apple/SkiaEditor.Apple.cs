using CoreGraphics;
using DrawnUi.Draw;
using Foundation;
using UIKit;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        private bool _updatingText;

        public class TextViewDelegate : UITextViewDelegate
        {
            private readonly SkiaEditor _editor;
            private bool _firstSynced;

            public TextViewDelegate(SkiaEditor editor) => _editor = editor;

            public override void Changed(UITextView textView)
            {
                if (_editor._updatingText)
                    return;

                _editor._updatingText = true;
                _editor.Text = textView.Text?.Replace("\r\n", "\n").Replace("\r", "\n");
                _editor._updatingText = false;
            }

            public override void SelectionChanged(UITextView textView)
            {
                if (!_firstSynced)
                {
                    _firstSynced = true;
                    return;
                }

                var range = textView.SelectedRange;
                var location = (int)range.Location;
                var length = (int)range.Length;

                _editor.SelectionLength = length;
                _editor.SetCursorPositionWithDelay(50, location + length);
            }

            public override bool ShouldChangeText(UITextView textView, NSRange range, string text)
            {
                // Multiline + ReturnType.Send: return key submits instead of inserting a break.
                // Hardware Shift+Enter is not distinguishable here (no modifier info in this
                // delegate); soft keyboards have no Shift+Enter, so Send wins for "\n".
                if (text == "\n" && (!_editor.IsMultiline || _editor.ShouldSubmitOnEnter))
                {
                    _editor.ExecuteSubmit(clearFocus: false);
                    return false;
                }
                return true;
            }
        }

        protected NativeEntryView Control;
        private UIView _layout;

        public int NativeSelectionStart
        {
            get
            {
                if (Control == null) return 0;
                return (int)Control.SelectedRange.Location;
            }
        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (Control == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (Control == null) return;
                var len = (int)(Control.Text?.Length ?? 0);
                var clampedPos = Math.Min(position, len);
                var clampedStop = stop >= 0 ? Math.Min(stop, len) : clampedPos;
                Control.SelectedRange = new NSRange(clampedPos, Math.Max(0, clampedStop - clampedPos));
            });
        }

        public void DisposePlatform()
        {
            if (Control != null)
            {
                Control.Delegate = null;
                Control.ResignFirstResponder();
                Control.RemoveFromSuperview();
                Control = null;
            }
            _layout = null;
        }

        public void UpdateNativePosition()
        {
            if (Control != null)
            {
                Control.InputAccessoryView = null;
                Control.AutocorrectionType = UITextAutocorrectionType.No;
                Control.Frame = new CGRect(DrawingRect.Right / RenderingScale, DrawingRect.Bottom / RenderingScale, 1, 1);
            }
        }

        void CreateNativeControl()
        {
            Control = new NativeEntryView
            {
                Frame = new CGRect(-10, -10, 1, 1),
                AccessibilityIdentifier = "NativeEntry" + GenerateUniqueId(),
                ScrollEnabled = false,
            };

            Control.TextContainerInset = UIEdgeInsets.Zero;
            Control.TextContainer.LineFragmentPadding = 0;

            _updatingText = true;
            Control.Text = this.Text ?? string.Empty;
            _updatingText = false;

            Control.Delegate = new TextViewDelegate(this);

            _layout.AddSubview(Control);
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

        public void SetFocusNative(bool focus)
        {
            try
            {
                _layout = (UIView)Superview?.Handler?.PlatformView;

                System.Diagnostics.Debug.WriteLine("[SkiaEditor] SetFocusNative " + focus);

                if (focus)
                {
                    if (Control == null)
                        CreateNativeControl();

                    if (!_updatingText)
                    {
                        _updatingText = true;
                        Control.Text = this.Text ?? string.Empty;
                        _updatingText = false;
                    }

                    ApplyKeyboardType();

                    Control.IsFocused = true;
                    Control.BecomeFirstResponder();
                }
                else
                {
                    if (Control != null)
                    {
                        Control.IsFocused = false;
                        Control.ResignFirstResponder();
                    }
                }
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public void ApplyKeyboardType()
        {
            if (Control == null) return;

            Control.SecureTextEntry = IsPassword;
            Control.AutocorrectionType = IsPassword
                ? UITextAutocorrectionType.No
                : UITextAutocorrectionType.Default;

            Control.SpellCheckingType = IsPassword || !IsSpellCheckEnabled
                ? UITextSpellCheckingType.No
                : UITextSpellCheckingType.Default;

            Control.KeyboardType = IsPassword ? UIKeyboardType.Default : KeyboardType switch
            {
                SkiaEditorKeyboard.Numeric  => UIKeyboardType.NumberPad,
                SkiaEditorKeyboard.Decimal  => UIKeyboardType.DecimalPad,
                SkiaEditorKeyboard.Phone    => UIKeyboardType.PhonePad,
                SkiaEditorKeyboard.Email    => UIKeyboardType.EmailAddress,
                _                           => UIKeyboardType.Default
            };
        }

        public void SetReturnType(ReturnType type)
        {
            if (Control == null) return;
            switch (type)
            {
                case ReturnType.Go:     Control.ReturnKeyType = UIReturnKeyType.Go;     break;
                case ReturnType.Next:   Control.ReturnKeyType = UIReturnKeyType.Next;   break;
                case ReturnType.Send:   Control.ReturnKeyType = UIReturnKeyType.Send;   break;
                case ReturnType.Search: Control.ReturnKeyType = UIReturnKeyType.Search; break;
                default:                Control.ReturnKeyType = UIReturnKeyType.Done;   break;
            }
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

        public class NativeEntryView : UITextView
        {
            public bool IsFocused { get; set; }

            public override bool CanResignFirstResponder => !IsFocused;
        }
    }
}
