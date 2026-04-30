using System.Diagnostics;

namespace DrawnUi.Draw;


public partial class KeyboardManager
{

    public static event EventHandler<InputKey> KeyDown;

    public static event EventHandler<InputKey> KeyUp;

    public static void KeyboardPressed(InputKey key)
    {
        CheckAndApplyModifiers(key, true);

        //Debug.WriteLine($"[KEY UP] {key}");

        KeyDown?.Invoke(null, key);
    }

    public static void KeyboardReleased(InputKey key)
    {
        CheckAndApplyModifiers(key, false);

        KeyUp?.Invoke(null, key);
    }

    public static bool IsShiftPressed
    {
        get
        {
            return IsLeftShiftDown || IsRightShiftDown;
        }
    }

    public static bool IsAltPressed
    {
        get
        {
            return IsLeftAltDown || IsRightAltDown;
        }
    }

    public static bool IsControlPressed
    {
        get
        {
            return IsLeftControlDown || IsRightControlDown;
        }
    }

    static bool IsLeftShiftDown { get; set; }

    static bool IsRightShiftDown { get; set; }

    static bool IsLeftAltDown { get; set; }

    static bool IsRightAltDown { get; set; }

    static bool IsLeftControlDown { get; set; }

    static bool IsRightControlDown { get; set; }

    static void CheckAndApplyModifiers(InputKey key, bool state)
    {
        if (key == InputKey.ShiftLeft)
        {
            IsLeftShiftDown = state;
        }
        else
        if (key == InputKey.ShiftRight)
        {
            IsRightShiftDown = state;
        }
        else
        if (key == InputKey.AltLeft)
        {
            IsLeftAltDown = state;
        }
        else
        if (key == InputKey.AltRight)
        {
            IsRightAltDown = state;
        }
        else
        if (key == InputKey.ControlLeft)
        {
            IsLeftControlDown = state;
        }
        else
        if (key == InputKey.ControlRight)
        {
            IsRightControlDown = state;
        }
    }


}