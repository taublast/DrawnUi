using DrawnUi.Draw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Diagnostics;
using InputScope = Microsoft.UI.Xaml.Input.InputScope;
using InputScopeName = Microsoft.UI.Xaml.Input.InputScopeName;
using InputScopeNameValue = Microsoft.UI.Xaml.Input.InputScopeNameValue;
using TextChangedEventArgs = Microsoft.UI.Xaml.Controls.TextChangedEventArgs;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaShape, ISkiaGestureListener
    {
        private TextBox _hiddenTextBox;
        private bool _updatingText;
        private bool _suppressSelectionChanged;

        public int NativeSelectionStart
        {
            get
            {
                if (_hiddenTextBox != null)
                {
                    return _hiddenTextBox.SelectionStart;
                }
                return 0;
            }
        }

        public void SetCursorPositionNative(int position, int stop = -1)
        {
            if (_hiddenTextBox == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    if (_hiddenTextBox == null)
                        return;
                    var len = _hiddenTextBox.Text?.Length ?? 0;
                    _hiddenTextBox.SelectionStart = Math.Min(position, len);
                    _hiddenTextBox.SelectionLength = stop >= 0
                        ? Math.Max(0, Math.Min(stop, len) - _hiddenTextBox.SelectionStart)
                        : 0;
                }
                catch (Exception e)
                {
                    Debug.WriteLine($"[SetCursorPositionNative] {e}");
                }
            });
        }

        public void DisposePlatform()
        {
            try
            {
                if (_hiddenTextBox != null)
                {
                    _hiddenTextBox.TextChanged -= HiddenTextBox_TextChanged;
                    _hiddenTextBox.SelectionChanged -= HiddenTextBox_SelectionChanged;
                    _hiddenTextBox.GotFocus -= HiddenTextBox_GotFocus;
                    _hiddenTextBox.PreviewKeyDown -= HiddenTextBox_PreviewKeyDown;
                    _hiddenTextBox.KeyDown -= HiddenTextBox_KeyDown;
                    _hiddenTextBox.Loaded -= HiddenTextBox_Loaded;
                    _hiddenTextBox.LostFocus -= HiddenTextBox_LostFocus;

                    var layout = (Panel)Superview?.Handler?.PlatformView;
                    if (layout != null)
                    {
                        layout.Children.Remove(_hiddenTextBox);
                    }
                    _hiddenTextBox = null;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[DisposePlatform] {e}");
            }
        }

        // Block stray keystrokes during the 100 ms focus-delay window that follows a tap.
        // IsReadOnly=true keeps the TextBox in the focus chain but silently discards input.
        // SetFocusNative restores IsReadOnly=false before calling Focus(Programmatic).
        partial void PlatformClearFocusNow()
        {
            if (_hiddenTextBox != null)
            {
                _hiddenTextBox.IsReadOnly = true;
                // SetFocusNative restores IsReadOnly=false before Focus(Programmatic)
            }
        }

        // TextBox is an off-screen keyboard sink — size and position are fixed.
        public void UpdateNativePosition() { }

        partial void SyncNativeText()
        {
            if (_hiddenTextBox == null || _updatingText)
                return;

            var newText = Text ?? string.Empty;
            if (_hiddenTextBox.Text == newText)
                return;

            _updatingText = true;
            try { _hiddenTextBox.Text = newText; }
            finally { _updatingText = false; }
        }

        private void EnsureTextBox()
        {
            if (_hiddenTextBox != null)
                return;

            var layout = (Panel)Superview?.Handler?.PlatformView;
            Debug.WriteLine($"[SkiaEditor] EnsureTextBox layout={layout?.GetType().Name ?? "NULL"} superview={Superview?.GetType().Name ?? "NULL"}");
            if (layout == null)
                return;

            _hiddenTextBox = new TextBox
            {
                IsReadOnly = false,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Width = 1,
                Height = 1,
                Visibility = Visibility.Visible,
                Name = "HiddenTextBox" + GenerateUniqueId(),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(1, 0, 0, 0))
            };

            // Exclude from AT navigation — our drawn virtual peer is the a11y representative.
            // The TextBox still receives WinUI keyboard focus for text input; Narrator ignores it.
            // This is the same pattern Flutter/Uno use for hidden native input sinks.
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAccessibilityView(
                _hiddenTextBox,
                Microsoft.UI.Xaml.Automation.Peers.AccessibilityView.Raw);

            _hiddenTextBox.TextChanged += HiddenTextBox_TextChanged;
            _hiddenTextBox.SelectionChanged += HiddenTextBox_SelectionChanged;
            _hiddenTextBox.GotFocus += HiddenTextBox_GotFocus;
            _hiddenTextBox.PreviewKeyDown += HiddenTextBox_PreviewKeyDown;
            _hiddenTextBox.KeyDown += HiddenTextBox_KeyDown;
            _hiddenTextBox.Loaded += HiddenTextBox_Loaded;
            _hiddenTextBox.LostFocus += HiddenTextBox_LostFocus;

            layout.Children.Add(_hiddenTextBox);

            // keep off-screen so WinUI native hit-testing never intercepts canvas taps
            _hiddenTextBox.Measure(new Windows.Foundation.Size(1, 1));
            _hiddenTextBox.Arrange(new Windows.Foundation.Rect(-10, -10, 1, 1));
        }

        public void ApplyKeyboardType()
        {
            if (_hiddenTextBox == null) return;

            _hiddenTextBox.IsSpellCheckEnabled = IsSpellCheckEnabled && !IsPassword;

            if (IsPassword || KeyboardType == SkiaEditorKeyboard.Default)
            {
                _hiddenTextBox.InputScope = null;
                return;
            }

            var nameValue = KeyboardType switch
            {
                SkiaEditorKeyboard.Numeric  => InputScopeNameValue.Number,
                SkiaEditorKeyboard.Decimal  => InputScopeNameValue.Number,
                SkiaEditorKeyboard.Phone    => InputScopeNameValue.TelephoneNumber,
                SkiaEditorKeyboard.Email    => InputScopeNameValue.EmailSmtpAddress,
                _                           => InputScopeNameValue.Default
            };

            var scope = new InputScope();
            scope.Names.Add(new InputScopeName { NameValue = nameValue });
            _hiddenTextBox.InputScope = scope;
        }

        public void SetFocusNative(bool focus)
        {
            Debug.WriteLine($"[SetFocusNative] focus={focus} IsFocused={IsFocused} textBox={_hiddenTextBox != null} CursorPosition={CursorPosition}");
            try
            {
                if (focus)
                {
                    EnsureTextBox();

                    if (_hiddenTextBox == null)
                        return;

                    // Suppress SelectionChanged during the whole focus sequence so text sync
                    // and selection-reset from Focus() don't race back into CursorPosition.
                    _suppressSelectionChanged = true;

                    // always sync text before focusing — TextBox may be stale if Text changed while unfocused
                    if (!_updatingText)
                    {
                        _updatingText = true;
                        _hiddenTextBox.Text = this.Text ?? string.Empty;
                        _updatingText = false;
                    }

                    ApplyKeyboardType();

                    // PlatformClearFocusNow sets IsReadOnly=true to block stray keystrokes
                    // during the 100 ms focus delay. Restore before focusing.
                    _hiddenTextBox.IsReadOnly = false;

                    // GotFocus fires only on focus *transitions*. If this TextBox was already
                    // focused (re-tap of same editor), GotFocus won't fire and
                    // _suppressSelectionChanged stays true, blocking all future SelectionChanged.
                    // Detect this case up-front so we can handle selection manually.
                    bool alreadyFocused = _hiddenTextBox.FocusState != FocusState.Unfocused;

                    // Skip Focus() when TextBox already has WinUI focus. Calling Focus(Programmatic)
                    // twice in rapid succession causes WinUI to flash LostFocus, leaving the TextBox
                    // Unfocused by the time the async GotFocus handler fires — killing keyboard input.
                    bool focused = alreadyFocused || _hiddenTextBox.Focus(FocusState.Programmatic);
                    Debug.WriteLine($"[SetFocusNative] alreadyFocused={alreadyFocused} Focus()={focused} FocusState={_hiddenTextBox.FocusState}");

                    if (alreadyFocused || !focused)
                    {
                        if (!focused)
                        {
                            Debug.WriteLine($"[SkiaEditor] Focus() returned false — scheduling retry");
                            // Retry once after a frame; GotFocus won't fire so clear flag now.
                            _suppressSelectionChanged = false;
                            Tasks.StartDelayed(TimeSpan.FromMilliseconds(32), () =>
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    if (_hiddenTextBox != null && IsFocused)
                                        _hiddenTextBox.Focus(FocusState.Programmatic);
                                }));
                        }
                        else
                        {
                            Debug.WriteLine($"[SkiaEditor] AlreadyFocused");
                            // GotFocus won't fire — manually position selection and clear flag.
                            var pos = Math.Max(0, Math.Min(CursorPosition, _hiddenTextBox.Text?.Length ?? 0));
                            var len = SelectionLength > 0
                                ? Math.Min(SelectionLength, (_hiddenTextBox.Text?.Length ?? 0) - pos)
                                : 0;
                            _hiddenTextBox.SelectionStart = pos;
                            _hiddenTextBox.SelectionLength = len;
                            _suppressSelectionChanged = false;
                        }
                    }
                    // else: GotFocus fires synchronously during Focus() and clears the flag.
                }
                // on defocus: do nothing — TextBox stays in tree, just loses WinUI focus naturally.
                // No destroy/recreate race possible.
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[SetFocusNative] {e}");
            }
        }

        // Focus(FocusState.Programmatic) returns false while the TextBox is not yet loaded
        // into the visual tree (programmatic IsFocused before first layout). Retry on load.
        private void HiddenTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsFocused && _hiddenTextBox != null && _hiddenTextBox.FocusState == FocusState.Unfocused)
                _hiddenTextBox.Focus(FocusState.Programmatic);
        }

        // WinUI hands pointer focus to the canvas on tap/press-release even while a drawn
        // editor is logically focused (e.g. focus set programmatically during an ongoing
        // press — the UP lands on canvas AFTER our Focus(Programmatic) and silently steals
        // WinUI focus; keyboard goes dead with the cursor still blinking). While IsFocused
        // is true, take focus back. Posted via dispatcher: calling Focus() inside a
        // LostFocus handler is re-entrant in WinUI. When the editor is logically unfocused
        // (IsFocused=false set by ReportFocus before any rival native focus), this no-ops.
        private void HiddenTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[LostFocus] IsFocused={IsFocused} FocusState={_hiddenTextBox?.FocusState}");
            if (!IsFocused || _hiddenTextBox == null)
                return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (IsFocused && _hiddenTextBox != null && _hiddenTextBox.FocusState == FocusState.Unfocused)
                {
                    var got = _hiddenTextBox.Focus(FocusState.Programmatic);
                    Debug.WriteLine($"[LostFocus] re-steal Focus()={got}");
                }
            });
        }

        private void HiddenTextBox_TextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            //Debug.WriteLine($"[SkiaEditor] TextChanged updatingText={_updatingText} newText='{_hiddenTextBox?.Text}'");
            if (!_updatingText)
            {
                _updatingText = true;
                // normalize Windows line endings
                Text = _hiddenTextBox.Text?.Replace("\r\n", "\n").Replace("\r", "\n");
                Debug.WriteLine($"[SkiaEditor] TextChanged '{Text}'");
                // WinUI can fire a spurious pointer event immediately after keyboard input.
                // Block out-of-bounds Down processing for 250 ms to prevent focus theft.
                _spuriousDownBlockUntilMs = Environment.TickCount64 + 250;
                _updatingText = false;
            }
        }

        // PreviewKeyDown fires before the TextBox processes the key — used for Up/Down so the
        // 1×1-pixel TextBox never navigates its own (garbage) wrap layout and never fires
        // a spurious SelectionChanged that would override the drawn cursor position.
        private void HiddenTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            // Tab while editor has native focus: exit editing mode and advance virtual UIA focus.
            if (e.Key == Windows.System.VirtualKey.Tab)
            {
                bool shift = Microsoft.UI.Input.InputKeyboardSource
                    .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                bool moved = Superview?.HandleEditorA11yTabOut(!shift) ?? false;
                e.Handled = moved; // if past boundary, let WinUI Tab continue naturally
                return;
            }

            // Must block Enter here, before TextBox processes it.
            // KeyDown fires after TextBox inserts the character; AcceptsReturn=true
            // means a \n would already be in the text by the time KeyDown runs.
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (!IsMultiline)
                {
                    e.Handled = true;
                    ExecuteSubmit(clearFocus: false);
                    return;
                }

                if (ShouldSubmitOnEnter)
                {
                    bool shiftDown = Microsoft.UI.Input.InputKeyboardSource
                        .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                    if (!shiftDown)
                    {
                        e.Handled = true;
                        ExecuteSubmit(clearFocus: false);
                        return;
                    }
                    // Shift+Enter falls through: native TextBox inserts the line break.
                }
            }

            if (IsMultiline && (e.Key == Windows.System.VirtualKey.Up || e.Key == Windows.System.VirtualKey.Down))
            {
                e.Handled = true;
                HandleVerticalArrow(e.Key == Windows.System.VirtualKey.Up);
                return;
            }

            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            {
                if (e.Key == Windows.System.VirtualKey.C)
                {
                    e.Handled = true;
                    CopySelection();
                }
                else if (e.Key == Windows.System.VirtualKey.X)
                {
                    e.Handled = true;
                    CutSelection();
                }
                else if (e.Key == Windows.System.VirtualKey.V)
                {
                    e.Handled = true;
                    PasteFromClipboard();
                }
            }
        }

        private void HiddenTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down) && e.Key == Windows.System.VirtualKey.A)
            {
                e.Handled = true;
                SelectAll();
            }
        }

        private void HiddenTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            var start = _hiddenTextBox.SelectionStart;
            var length = _hiddenTextBox.SelectionLength;
            SelectionLength = length;
            // CursorPosition tracks the start of the selection; UpdateCursorVisibility draws
            // cursor at CursorPosition+SelectionLength (end) when selection is non-empty.
            SetCursorPositionWithDelay(16, start);
        }

        private void HiddenTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"[GotFocus] fired CursorPosition={CursorPosition} textLen={_hiddenTextBox?.Text?.Length ?? -1} FocusState={_hiddenTextBox?.FocusState}");
            if (_hiddenTextBox == null) return;
            var pos = Math.Max(0, Math.Min(CursorPosition, _hiddenTextBox.Text?.Length ?? 0));
            var len = SelectionLength > 0
                ? Math.Min(SelectionLength, (_hiddenTextBox.Text?.Length ?? 0) - pos)
                : 0;
            _hiddenTextBox.SelectionStart = pos;
            _hiddenTextBox.SelectionLength = len;
            _suppressSelectionChanged = false;
        }

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
                await Task.Delay(50, token);
                _suppressImmediateCursorMove = false;
                MoveInternalCursor();
            }
            catch (OperationCanceledException)
            {
                _suppressImmediateCursorMove = false;
            }
        }

        //partial void OnSelectionDeleted() => DeferVisualCursorUpdate();

        partial void OnTextInsertedAtCursor() => DeferVisualCursorUpdate();

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }

        public override void OnAccessibilityActivated()
        {
            // Do NOT call base — base injects a synthetic Tapped gesture which SkiaEditor
            // ignores for focus (only Down opens the keyboard). Set native focus directly.
            if (Superview != null)
                Superview.FocusedChild = this;
            SetFocusInternal(true);
        }

        public override void OnAccessibilityFocused(bool focused)
        {
            if (focused)
            {
                // Tab arrived — activate cursor + native input sink.
                if (Superview != null)
                    Superview.FocusedChild = this;
                SetFocusInternal(true);
            }
            else
            {
                // Tab left — hide cursor, release native input.
                SetFrameworkFocus(false);
            }
        }
    }
}
