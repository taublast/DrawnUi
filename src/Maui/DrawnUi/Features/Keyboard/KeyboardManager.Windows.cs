#if WINDOWS

using Windows.System;
using Microsoft.UI.Xaml;

namespace DrawnUi.Draw;

public partial class KeyboardManager
{

    public static void AttachToKeyboard(UIElement window)
    {
        window.PreviewKeyUp += (sender, args) =>
        {
            var mapped = MapToMaui(args.Key);
            KeyboardReleased(mapped);

            //Trace.WriteLine($"[KEY] {args.Key} => {mapped}");
        };
        window.PreviewKeyDown += (sender, args) =>
        {
            var mapped = MapToMaui(args.Key);
            KeyboardPressed(mapped);
        };
    }

    /// <summary>
    /// Same as map to Java tbh
    /// </summary>
    /// <param name="virtualKey"></param>
    /// <returns></returns>
    public static InputKey MapToMaui(VirtualKey virtualKey)
    {
        switch (virtualKey)
        {
        case VirtualKey.Space: return InputKey.Space;
        case VirtualKey.Left: return InputKey.ArrowLeft;
        case VirtualKey.Up: return InputKey.ArrowUp;
        case VirtualKey.Right: return InputKey.ArrowRight;
        case VirtualKey.Down: return InputKey.ArrowDown;
        case VirtualKey.Number0: return InputKey.Digit0;
        case VirtualKey.Number1: return InputKey.Digit1;
        case VirtualKey.Number2: return InputKey.Digit2;
        case VirtualKey.Number3: return InputKey.Digit3;
        case VirtualKey.Number4: return InputKey.Digit4;
        case VirtualKey.Number5: return InputKey.Digit5;
        case VirtualKey.Number6: return InputKey.Digit6;
        case VirtualKey.Number7: return InputKey.Digit7;
        case VirtualKey.Number8: return InputKey.Digit8;
        case VirtualKey.Number9: return InputKey.Digit9;
        case VirtualKey.A: return InputKey.KeyA;
        case VirtualKey.B: return InputKey.KeyB;
        case VirtualKey.C: return InputKey.KeyC;
        case VirtualKey.D: return InputKey.KeyD;
        case VirtualKey.E: return InputKey.KeyE;
        case VirtualKey.F: return InputKey.KeyF;
        case VirtualKey.G: return InputKey.KeyG;
        case VirtualKey.H: return InputKey.KeyH;
        case VirtualKey.I: return InputKey.KeyI;
        case VirtualKey.J: return InputKey.KeyJ;
        case VirtualKey.K: return InputKey.KeyK;
        case VirtualKey.L: return InputKey.KeyL;
        case VirtualKey.M: return InputKey.KeyM;
        case VirtualKey.N: return InputKey.KeyN;
        case VirtualKey.O: return InputKey.KeyO;
        case VirtualKey.P: return InputKey.KeyP;
        case VirtualKey.Q: return InputKey.KeyQ;
        case VirtualKey.R: return InputKey.KeyR;
        case VirtualKey.S: return InputKey.KeyS;
        case VirtualKey.T: return InputKey.KeyT;
        case VirtualKey.U: return InputKey.KeyU;
        case VirtualKey.V: return InputKey.KeyV;
        case VirtualKey.W: return InputKey.KeyW;
        case VirtualKey.X: return InputKey.KeyX;
        case VirtualKey.Y: return InputKey.KeyY;
        case VirtualKey.Z: return InputKey.KeyZ;
        case VirtualKey.CapitalLock: return InputKey.CapsLock;
        case VirtualKey.Insert: return InputKey.Insert;
        case VirtualKey.Delete: return InputKey.Delete;
        case VirtualKey.Snapshot: return InputKey.PrintScreen;
        case VirtualKey.Home: return InputKey.Home;
        case VirtualKey.End: return InputKey.End;
        case VirtualKey.PageDown: return InputKey.PageDown;
        case VirtualKey.PageUp: return InputKey.PageUp;
        case VirtualKey.Escape: return InputKey.Escape;
        case VirtualKey.Pause: return InputKey.Pause;
        case VirtualKey.Menu: return InputKey.AltLeft;
        case VirtualKey.LeftMenu: return InputKey.AltLeft;
        case VirtualKey.RightMenu: return InputKey.AltRight;
        case VirtualKey.Shift: return InputKey.ShiftLeft;
        case VirtualKey.LeftShift: return InputKey.ShiftLeft;
        case VirtualKey.RightShift: return InputKey.ShiftRight;
        case VirtualKey.LeftControl: return InputKey.ControlLeft;
        case VirtualKey.RightControl: return InputKey.ControlRight;
        case VirtualKey.Control: return InputKey.ControlLeft;
        case VirtualKey.Enter: return InputKey.Enter;
        case VirtualKey.Tab: return InputKey.Tab;
        case VirtualKey.Back: return InputKey.Backspace;
        case VirtualKey.F1: return InputKey.F1;
        case VirtualKey.F2: return InputKey.F2;
        case VirtualKey.F3: return InputKey.F3;
        case VirtualKey.F4: return InputKey.F4;
        case VirtualKey.F5: return InputKey.F5;
        case VirtualKey.F6: return InputKey.F6;
        case VirtualKey.F7: return InputKey.F7;
        case VirtualKey.F8: return InputKey.F8;
        case VirtualKey.F9: return InputKey.F9;
        case VirtualKey.F10: return InputKey.F10;
        case VirtualKey.F11: return InputKey.F11;
        case VirtualKey.F12: return InputKey.F12;
        case VirtualKey.NumberKeyLock: return InputKey.NumLock;
        case VirtualKey.Scroll: return InputKey.ScrollLock;
        case VirtualKey.NumberPad0: return InputKey.Numpad0;
        case VirtualKey.NumberPad1: return InputKey.Numpad1;
        case VirtualKey.NumberPad2: return InputKey.Numpad2;
        case VirtualKey.NumberPad3: return InputKey.Numpad3;
        case VirtualKey.NumberPad4: return InputKey.Numpad4;
        case VirtualKey.NumberPad5: return InputKey.Numpad5;
        case VirtualKey.NumberPad6: return InputKey.Numpad6;
        case VirtualKey.NumberPad7: return InputKey.Numpad7;
        case VirtualKey.NumberPad8: return InputKey.Numpad8;
        case VirtualKey.NumberPad9: return InputKey.Numpad9;
        case VirtualKey.LeftWindows: return InputKey.MetaLeft;
        case VirtualKey.RightWindows: return InputKey.MetaRight;
        case VirtualKey.Divide: return InputKey.NumpadDivide;
        case VirtualKey.Multiply: return InputKey.NumpadMultiply;
        case VirtualKey.Subtract: return InputKey.NumpadSubtract;
        case VirtualKey.Add: return InputKey.NumpadAdd;
        }

        switch ((int)virtualKey)
        {
        case 187: return InputKey.Equal;
        case 189: return InputKey.Minus;
        case 192: return InputKey.Backquote;
        case 188: return InputKey.Comma;
        case 190: return InputKey.Period;
        case 191: return InputKey.Slash;
        case 219: return InputKey.BracketLeft;
        case 221: return InputKey.BracketRight;
        case 220: return InputKey.Backslash;
        case 186: return InputKey.Semicolon;
        }

        return InputKey.Unknown;

    }



}

#endif