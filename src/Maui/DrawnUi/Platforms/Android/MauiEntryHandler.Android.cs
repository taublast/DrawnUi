using Android.Runtime;
using Android.Text;
using Android.Text.Method;
using Android.Util;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using AndroidX.AppCompat.Widget;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
 
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
 

namespace DrawnUi.Controls;

public partial class MauiEntryHandler : EntryHandler
{
    static IPropertyMapper<IEntry, IEntryHandler> ChangeMapper()
    {
        var mapper = new PropertyMapper<IEntry, IEntryHandler>(Mapper);

        // Hook into the properties that affect input type
        mapper.ReplaceMapping<IEntry, IEntryHandler>(nameof(IEntry.Keyboard), MapKeyboardAndInputType);
        mapper.ReplaceMapping<IEntry, IEntryHandler>(nameof(IEntry.IsPassword), MapKeyboardAndInputType);
        mapper.ReplaceMapping<IEntry, IEntryHandler>(nameof(MauiEntry.MaxLines), MapKeyboardAndInputType);

        return mapper;
    }

    public MauiEntryHandler() : base(ChangeMapper())
    {
    }

    public MauiEntryHandler(IPropertyMapper? mapper) : base(mapper ?? ChangeMapper())
    {
    }

    public MauiEntryHandler(IPropertyMapper? mapper, CommandMapper? commandMapper) : base(mapper ?? ChangeMapper(), commandMapper ?? CommandMapper)
    {
    }

    public static void MapKeyboardAndInputType(IEntryHandler handler, IEntry entry)
    {
        if (handler is MauiEntryHandler me && me.NativeControl != null)
        {
            me.SetInputType();
            me.ApplySettings();
        }
    }

    public AppCompatEditText NativeControl;
    protected MauiEntry Control => VirtualView as MauiEntry;
    private int _implyMaxLines;

    protected override void ConnectHandler(AppCompatEditText platformView)
    {
        base.ConnectHandler(platformView);

        NativeControl = platformView;

        platformView.EditorAction += OnEditorAction;
        platformView.TextChanged += OnTextChanged;

        // Apply initial settings
        SetInputType();
        ApplySettings();
    }

    protected override void DisconnectHandler(AppCompatEditText platformView)
    {
        base.DisconnectHandler(platformView);

        NativeControl = null;

        platformView.EditorAction -= OnEditorAction;
        platformView.TextChanged -= OnTextChanged;
    }

    public void SetInputType()
    {
        if (NativeControl == null || Control == null)
            return;

        var previousCursorPosition = NativeControl.SelectionStart;
        var keyboard = Control.Keyboard;

        var inputType = keyboard.ToInputType();

        // Handle single line vs multi-line
        if (Control.MaxLines == 1)
        {
            // Single line behavior
            _implyMaxLines = 1;
            inputType &= ~InputTypes.TextFlagMultiLine; // Remove multi-line flag
            NativeControl.SetSingleLine(true);
            NativeControl.SetHorizontallyScrolling(true);
        }
        else
        {
            // Multi-line behavior
            inputType |= InputTypes.TextFlagMultiLine | InputTypes.TextFlagCapSentences;

            if (Control.MaxLines > 1)
            {
                _implyMaxLines = Control.MaxLines;
            }
            else
            {
                _implyMaxLines = int.MaxValue;
            }

            NativeControl.SetSingleLine(false);
            NativeControl.SetHorizontallyScrolling(false);
        }

        // Handle password
        if (Control.IsPassword)
        {
            if (keyboard == Keyboard.Numeric || keyboard == Keyboard.Telephone)
            {
                inputType = InputTypes.ClassNumber | InputTypes.NumberVariationPassword;
            }
            else
            {
                inputType |= InputTypes.TextVariationPassword;
            }
        }

        NativeControl.InputType = inputType;
        NativeControl.SetMaxLines(_implyMaxLines);

        if (keyboard is not CustomKeyboard)
        {
            UpdateIsTextPredictionEnabled(NativeControl, Control);
            UpdateIsSpellCheckEnabled(NativeControl, Control);
        }

        if (keyboard == Keyboard.Numeric)
        {
            NativeControl.KeyListener = LocalizedDigitsKeyListener.Create(NativeControl.InputType);
        }

        if (Control is IElement element)
        {
            var services = element.Handler?.MauiContext?.Services;
            if (services != null)
            {
                var fontManager = services.GetRequiredService<IFontManager>();
                NativeControl.UpdateFont(Control, fontManager);
            }
        }

        NativeControl.SetSelection(Math.Min(previousCursorPosition, NativeControl.Text?.Length ?? 0));

        Debug.WriteLine($"[MauiEntryHandler] SetInputType: {inputType}, MaxLines: {_implyMaxLines}, SingleLine: {Control.MaxLines == 1}");
    }

    void ApplySettings()
    {
        if (NativeControl != null && MainThread.IsMainThread && Control != null)
        {
            if (Control.Text != NativeControl.Text)
                NativeControl.Text = Control.Text;

            NativeControl.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            NativeControl.SetPadding(0, 0, 0, 0);

            NativeControl.SetMaxLines(_implyMaxLines);
            NativeControl.RequestLayout();
            NativeControl.Invalidate();
        }
    }

    protected override AppCompatEditText CreatePlatformView()
    {
        NativeControl = base.CreatePlatformView();

        NativeControl.SetPadding(0, 0, 0, 0);
        NativeControl.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

        return NativeControl;
    }

    private void OnEditorAction(object sender, Android.Widget.TextView.EditorActionEventArgs e)
    {
        var actionId = e.ActionId;
        var evt = e.Event;

        if (actionId == ImeAction.Done ||
            (actionId == ImeAction.ImeNull && evt?.KeyCode == Keycode.Enter && evt?.Action == KeyEventActions.Up))
            return; // Already handled by base class

        if (actionId != ImeAction.ImeNull)
            VirtualView?.Completed();

        e.Handled = true;
    }

    private void OnTextChanged(object sender, Android.Text.TextChangedEventArgs e)
    {
        Debug.WriteLine($"Text: '{NativeControl.Text}' IsSingleLine {NativeControl.IsSingleLine} lines {NativeControl.MaxLines} inputType {NativeControl.InputType}");

        // Handle scrolling for single line
        if (Control?.MaxLines == 1)
        {
            // Auto-scroll to end for single line
            NativeControl.SetSelection(NativeControl.Text?.Length ?? 0);
        }
    }

    public static void UpdateIsTextPredictionEnabled(EditText editText, ITextInput textInput)
    {
        if (textInput.IsTextPredictionEnabled)
            editText.InputType |= InputTypes.TextFlagAutoCorrect;
        else
            editText.InputType &= ~InputTypes.TextFlagAutoCorrect;
    }

    public static void UpdateIsSpellCheckEnabled(EditText editText, ITextInput textInput)
    {
        if (!textInput.IsSpellCheckEnabled)
            editText.InputType |= InputTypes.TextFlagNoSuggestions;
        else
            editText.InputType &= ~InputTypes.TextFlagNoSuggestions;
    }
}
