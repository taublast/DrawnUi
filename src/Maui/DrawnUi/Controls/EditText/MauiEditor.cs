namespace DrawnUi.Controls;

public interface ISmartNative
{
    double NeededHeight { get; set; }
}

public partial class MauiEditor : Editor, ISmartNative
{
    public double NeededHeight { get; set; }

    [Obsolete("Use Microsoft.Maui.Size instead.")]
    public override SizeRequest Measure(double widthConstraint, double heightConstraint, MeasureFlags flags = MeasureFlags.None)
    {
        var ret = base.Measure(widthConstraint, heightConstraint, flags);

        return ret;
    }

    protected override Size ArrangeOverride(Rect bounds)
    {
        var ret = base.ArrangeOverride(bounds);

        return ret;
    }

    public static readonly BindableProperty MaxLinesProperty = BindableProperty.Create(nameof(MaxLines),
        typeof(int), typeof(MauiEditor), -1);
    /// <summary>
    /// WIth 1 will behave like an ordinary Entry, with -1 (auto) or explicitly set you get an Editor
    /// </summary>
    public int MaxLines
    {
        get { return (int)GetValue(MaxLinesProperty); }
        set { SetValue(MaxLinesProperty, value); }
    }

    public static readonly BindableProperty ReturnTypeProperty = BindableProperty.Create(nameof(ReturnType),
    typeof(ReturnType),
    typeof(MauiEditor),
    ReturnType.Default);
    public ReturnType ReturnType
    {
        get { return (ReturnType)GetValue(ReturnTypeProperty); }
        set { SetValue(ReturnTypeProperty, value); }
    }

    /*
     
       protected MauiEditor Control => VirtualView as MauiEditor;

       private void OnEditorAction(object sender, TextView.EditorActionEventArgs e)
       {
           var returnType = Control.ReturnType;

           // Inside of the android implementations that map events to listeners, the default return value for "Handled" is always true
           // This means, just by subscribing to EditorAction/KeyPressed/etc.. you change the behavior of the control
           // So, we are setting handled to false here in order to maintain default behavior
           bool handled = false;
           if (returnType != null)
           {
               var actionId = e.ActionId;
               var evt = e.Event;
               ImeAction currentInputImeFlag = PlatformView.ImeOptions;

               // On API 34 it looks like they fixed the issue where the actionId is ImeAction.ImeNull when using a keyboard
               // so I'm just setting the actionId here to the current ImeOptions so the logic can all be simplified
               if (actionId == ImeAction.ImeNull && evt?.KeyCode == Keycode.Enter)
               {
                   actionId = currentInputImeFlag;
               }

               // keyboard path
               if (evt?.KeyCode == Keycode.Enter && evt?.Action == KeyEventActions.Down)
               {
                   handled = true;
               }
               else if (evt?.KeyCode == Keycode.Enter && evt?.Action == KeyEventActions.Up)
               {
                   VirtualView?.Completed();
               }
               // InputPaneView Path
               else if (evt?.KeyCode is null && (actionId == ImeAction.Done || actionId == currentInputImeFlag))
               {
                   VirtualView?.Completed();
               }
           }

           e.Handled = handled;
       }


     */
}
