using DrawnUi.Draw;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Diagnostics;
using TextChangedEventArgs = Microsoft.UI.Xaml.Controls.TextChangedEventArgs;
using Visibility = Microsoft.UI.Xaml.Visibility;

namespace DrawnUi.Draw
{
    public partial class SkiaEditor : SkiaLayout, ISkiaGestureListener
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

        // TextBox is an off-screen keyboard sink — size and position are fixed.
        public void UpdateNativePosition() { }

        private void EnsureTextBox()
        {
            if (_hiddenTextBox != null)
                return;

            var layout = (Panel)Superview?.Handler?.PlatformView;
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

            _hiddenTextBox.TextChanged += HiddenTextBox_TextChanged;
            _hiddenTextBox.SelectionChanged += HiddenTextBox_SelectionChanged;
            _hiddenTextBox.GotFocus += HiddenTextBox_GotFocus;

            layout.Children.Add(_hiddenTextBox);

            // keep off-screen so WinUI native hit-testing never intercepts canvas taps
            _hiddenTextBox.Measure(new Windows.Foundation.Size(1, 1));
            _hiddenTextBox.Arrange(new Windows.Foundation.Rect(-10, -10, 1, 1));
        }

        public void SetFocusNative(bool focus)
        {
            try
            {
                if (focus)
                {
                    EnsureTextBox();

                    if (_hiddenTextBox == null)
                        return;

                    // always sync text before focusing — TextBox may be stale if Text changed while unfocused
                    if (!_updatingText)
                    {
                        _updatingText = true;
                        _hiddenTextBox.Text = this.Text ?? string.Empty;
                        _updatingText = false;
                    }

                    _suppressSelectionChanged = true;
                    _hiddenTextBox.Focus(FocusState.Programmatic);
                }
                // on defocus: do nothing — TextBox stays in tree, just loses WinUI focus naturally.
                // No destroy/recreate race possible.
            }
            catch (Exception e)
            {
                Debug.WriteLine($"[SetFocusNative] {e}");
            }
        }

        private void HiddenTextBox_TextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            if (!_updatingText)
            {
                _updatingText = true;
                Text = _hiddenTextBox.Text;
                _updatingText = false;
            }
        }

        private void HiddenTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_suppressSelectionChanged) return;
            SetCursorPositionWithDelay(50, _hiddenTextBox.SelectionStart);
        }

        private void HiddenTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_hiddenTextBox == null) return;
            var pos = Math.Min(CursorPosition, _hiddenTextBox.Text?.Length ?? 0);
            _hiddenTextBox.SelectionStart = pos;
            _hiddenTextBox.SelectionLength = 0;
            _suppressSelectionChanged = false;
        }

        public int GenerateUniqueId()
        {
            long currentTime = DateTime.Now.Ticks;
            int uniqueId = unchecked((int)currentTime);
            return uniqueId;
        }
    }
}
