using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui.Handlers;
using System.Diagnostics.CodeAnalysis;
using Context = Android.Content.Context;
using Size = Microsoft.Maui.Graphics.Size;
using TextChangedEventArgs = Android.Text.TextChangedEventArgs;


namespace DrawnUi.Controls;
/*
public partial class MauiEditorHandler : EditorHandler
{
    AppCompatEditText _control;

    void ApplySettings()
    {
        if (_control != null)
        {
            _control.SetPadding(0, 0, 0, 0);
            _control.VerticalScrollBarEnabled = true;
            _control.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

            _control.RequestLayout();
            _control.Invalidate();
        }
    }

    protected override void ConnectHandler(AppCompatEditText platformView)
    {
        base.ConnectHandler(platformView);

        _control = platformView;

        platformView.TextChanged += OnTextChanged;

        ApplySettings();

    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        Android.Graphics.Rect visibleRect = new();
        _control.GetLocalVisibleRect(visibleRect);

        //need this to apply our parent control new size to dynamic "Layout" property
        _control.RequestLayout();
        _control.Invalidate();


        if (_control.Layout != null)
        {
            Debug.WriteLine($"[LAYOUT] {_control.Layout.Width} {_control.Layout.Height}"); ;

            //var scrollAmount = _control.Layout.GetLineTop(_control.LineCount) - _control.Height;
            //if (scrollAmount > 0)
            //    _control.ScrollTo(0, scrollAmount);
            //else
            //    _control.ScrollTo(0, 0);


            int x = 0, y = 0;

            var scrollX = _control.ScrollX; // Current horizontal scroll position
            var contentWidth = _control.Layout.GetLineWidth(0); // Width of the content
            var controlWidth = _control.Width - _control.PaddingLeft - _control.PaddingRight; // Width of the control

            var scrollAmount = contentWidth - (controlWidth + scrollX);
            if (scrollAmount > 0)
            {
                x = (int)scrollAmount;
            }

            var scrollY = _control.Layout.GetLineTop(_control.LineCount) - _control.Height;
            // if there is no need to scroll, scrollAmount will be <=0
            if (scrollY > 0)
                y = scrollY;

            _control.ScrollTo(x, y);
        }
    }

    protected override void DisconnectHandler(AppCompatEditText platformView)
    {
        _control = null;

        platformView.TextChanged -= OnTextChanged;

        base.DisconnectHandler(platformView);
    }

}
*/

public partial class MauiEditorHandler : EditorHandler
{
    AppCompatEditText _control;

    protected override void ConnectHandler(AppCompatEditText platformView)
    {
        base.ConnectHandler(platformView);

        _control = platformView;
        //platformView.EditorAction += OnEditorAction;

        platformView.TextChanged += OnTextChanged;

        ApplySettings();
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        Android.Graphics.Rect visibleRect = new();
        _control.GetLocalVisibleRect(visibleRect);

        _control.RequestLayout();
        _control.Invalidate();

        if (_control.Layout != null)
        {
            var scrollAmount = _control.Layout.GetLineTop(_control.LineCount) - _control.Height;
            // if there is no need to scroll, scrollAmount will be <=0
            if (scrollAmount > 0)
                _control.ScrollTo(0, scrollAmount);
            else
                _control.ScrollTo(0, 0);
        }
    }


    void ApplySettings()
    {
        if (_control != null)
        {
            if (_control is SubclassedAppCompatEditText custom)
            {
                custom.SetReturnType(Control.ReturnType);
            }
            _control.SetPadding(0, 0, 0, 0);
            _control.VerticalScrollBarEnabled = Control.MaxLines != 1;
            _control.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
        }
    }

    public override void PlatformArrange(Rect frame)
    {
        base.PlatformArrange(frame);

        ApplySettings();
    }

    protected override void DisconnectHandler(AppCompatEditText platformView)
    {
        _control = null;

        platformView.TextChanged -= OnTextChanged;

        base.DisconnectHandler(platformView);
        //platformView.EditorAction -= OnEditorAction;
    }

    private MauiEditor Control;

    protected override AppCompatEditText CreatePlatformView()
    {
        //var native = base.CreatePlatformView();
        Control = this.VirtualView as MauiEditor;

        var editText = new SubclassedAppCompatEditText(Context)
        {
            ImeOptions = ImeAction.Done,
            Gravity = GravityFlags.Top,
            TextAlignment = Android.Views.TextAlignment.ViewStart,
        };

        editText.SetSingleLine(false);
        editText.SetHorizontallyScrolling(false);

        return editText;

        //return base.CreatePlatformView();
    }

    //public override Size GetDesiredSize(double widthConstraint, double heightConstraint)
    //{
    //    return base.GetDesiredSize(widthConstraint, heightConstraint);
    //}

    private bool once;

    public override void SetVirtualView(IView view)
    {
        base.SetVirtualView(view);

        // TODO: NET8 issoto - Remove the casting once we can set the TPlatformView generic type as MauiAppCompatEditText
        if (!once && PlatformView is SubclassedAppCompatEditText editText)
            editText.SelectionChanged += MyOnSelectionChanged;

        once = true;
    }

    private void MyOnSelectionChanged(object? sender, EventArgs e)
    {
        var cursorPosition = GetCursorPosition(PlatformView);
        var selectedTextLength = GetSelectedTextLength(PlatformView);

        if (VirtualView.CursorPosition != cursorPosition) 
            VirtualView.CursorPosition = cursorPosition;

        if (VirtualView.SelectionLength != selectedTextLength)
            VirtualView.SelectionLength = selectedTextLength;
    }

    int GetCursorPosition(EditText editText, int cursorOffset = 0)
    {
        var newCursorPosition = editText.SelectionStart + cursorOffset;
        return Math.Max(0, newCursorPosition);
    }

    int GetSelectedTextLength(EditText editText)
    {
        var selectedLength = editText.SelectionEnd - editText.SelectionStart;
        return Math.Max(0, selectedLength);
    }

    public class SubclassedAppCompatEditText : AppCompatEditText
    {

        public void SetReturnType(ReturnType returnType)
        {
            ImeAction imeOptions;

            switch (returnType)
            {
            case ReturnType.Done:
            imeOptions = ImeAction.Done;
            break;
            case ReturnType.Go:
            imeOptions = ImeAction.Go;
            break;
            case ReturnType.Next:
            imeOptions = ImeAction.Next;
            break;
            case ReturnType.Search:
            imeOptions = ImeAction.Search;
            break;
            case ReturnType.Send:
            imeOptions = ImeAction.Send;
            break;
            default:
            imeOptions = ImeAction.Unspecified;
            break;
            }

            ImeOptions = imeOptions;

            RequestLayout();
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        protected override void OnSelectionChanged(int selStart, int selEnd)
        {
            base.OnSelectionChanged(selStart, selEnd);

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler? SelectionChanged;

        protected SubclassedAppCompatEditText(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        { }

        public SubclassedAppCompatEditText([NotNull] Context context) : base(context)
        { }

        public SubclassedAppCompatEditText([NotNull] Context context, IAttributeSet attrs) : base(context, attrs)
        { }

        public SubclassedAppCompatEditText([NotNull] Context context, IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        { }

        public override IInputConnection OnCreateInputConnection(EditorInfo outAttrs)
        {
            RequestLayout();

            Invalidate();

            var conn = base.OnCreateInputConnection(outAttrs);

            if (ImeOptions != ImeAction.Unspecified)
            {
                outAttrs.ImeOptions &= ~Android.Views.InputMethods.ImeFlags.NoEnterAction;
            }

            return conn;
        }
    }
}
