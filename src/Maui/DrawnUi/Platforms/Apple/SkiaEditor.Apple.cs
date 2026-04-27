using CoreGraphics;
using DrawnUi.Draw;
using UIKit;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaLayout, ISkiaGestureListener
    {

        public class TextFieldDelegate : UITextFieldDelegate
        {
            private SkiaEditor _editor;
            public TextFieldDelegate(SkiaEditor editor)
            {
                _editor = editor;
            }

            bool firstSynced;
            public override void DidChangeSelection(UITextField textField)
            {
                if (!firstSynced)
                {
                    firstSynced = true;
                    return;
                }
                var newPosition = textField.GetOffsetFromPosition(textField.BeginningOfDocument, textField.SelectedTextRange.Start);
#if DEBUG
                Trace.WriteLine($"[DidChangeSelection] {newPosition}");
#endif                
                _editor.SetCursorPositionWithDelay(50, (int)newPosition);
            }
        }

        private void Control_EditingChanged(object sender, EventArgs e)
        {
            //Trace.WriteLine("Text has changed: " + Control.Text);
            this.Text = Control.Text;
            //MainThread.BeginInvokeOnMainThread(async () =>
            //{
            //	SetCursorPositionWithDelay(50, NativeSelectionStart);
            //});
        }

        /// <summary>
        /// This will be read by the parent to check the cursor position at will. For Apple we must read this on ui thread.
        /// </summary>
        public int NativeSelectionStart
        {
            get
            {
                var newPosition = (int)Control.GetOffsetFromPosition(Control.BeginningOfDocument, Control.SelectedTextRange.Start);
#if DEBUG
                Trace.WriteLine($"[Reported NativeSelectionStart] {newPosition}");
#endif
                return newPosition;
            }
        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (Control == null)
                return;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                UITextPosition start = Control.GetPosition(Control.BeginningOfDocument, position);
                if (start != null)
                {
                    //System.Diagnostics.Debug.WriteLine("SetCursorPositionNative " + start);

                    UITextPosition end = stop >= 0 ? Control.GetPosition(Control.BeginningOfDocument, stop) : start;
#if DEBUG
                    Trace.WriteLine($"[SetCursorPositionNative] {start}");
#endif
                    Control.SelectedTextRange = Control.GetTextRange(start, end);
                }
            });
        }

        public void DisposePlatform()
        {
            if (Control != null)
            {
                Control.EditingChanged -= Control_EditingChanged;
                Control.ResignFirstResponder();
                Control.RemoveFromSuperview();
                Control.Delegate = null;
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
                Control.Frame = new CGRect(DrawingRect.Right / RenderingScale, DrawingRect.Bottom / RenderingScale,
                    1, 1);
            }
        }

        protected NativeEntryField Control;
        private UIView _layout;

        void CreateNativeControl()
        {
            Control = new()
            {
                Frame = new CGRect(50, 50, 0, 0),
                UserInteractionEnabled = true,
                //BackgroundColor = UIColor.Red,
                AccessibilityIdentifier = "NativeEntry" + GenerateUniqueId(),
                Text = this.Text
            };

            Control.Delegate = new TextFieldDelegate(this);

            Control.EditingChanged += Control_EditingChanged;

            _layout.AddSubview(Control);
        }



        public void SetFocusNative(bool focus)
        {


            try
            {
                _layout = (UIView)Superview.Handler?.PlatformView;

                System.Diagnostics.Debug.WriteLine("SetFocusNative " + focus);

                if (focus)
                {
                    if (Control == null)
                    {
                        CreateNativeControl();
                    }

                    Control.IsFocused = true;

                    // Request focus and show the keyboard
                    Control.BecomeFirstResponder();
                }
                else
                {
                    if (Control != null)
                    {
                        Control.IsFocused = false;

                        // Remove focus and hide the keyboard
                        Control.ResignFirstResponder();
                    }
                }
#if DEBUG

                if (Control.IsFirstResponder)
                {
                    Trace.WriteLine("Control is focused, okay " + Control.CanBecomeFirstResponder + " okay focued " + Control.CanBecomeFocused);
                }
                else
                {
                    Trace.WriteLine("Control is not focused");
                }
#endif
            }
            catch (Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public void SetReturnType(ReturnType type)
        {
            switch (type)
            {
            case ReturnType.Go:
            Control.ReturnKeyType = UIReturnKeyType.Go;
            break;
            case ReturnType.Next:
            Control.ReturnKeyType = UIReturnKeyType.Next;
            break;
            case ReturnType.Send:
            Control.ReturnKeyType = UIReturnKeyType.Send;
            break;
            case ReturnType.Search:
            Control.ReturnKeyType = UIReturnKeyType.Search;
            break;
            default:
            Control.ReturnKeyType = UIReturnKeyType.Done;
            break;
            }
        }

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }


        public class NativeEntryField : UITextField
        {
            /// <summary>
            /// Without this the field will get unfocused everytime we click outside
            /// </summary>
            public bool IsFocused { get; set; }



            public override bool CanResignFirstResponder
            {
                get
                {
                    return !IsFocused;
                }
            }
        }

    }

}
