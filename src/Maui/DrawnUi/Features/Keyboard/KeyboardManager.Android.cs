#if ANDROID

using Android.InputMethodServices;
using Android.Views;
using Activity = Android.App.Activity;
using Keycode = Android.Views.Keycode;
using View = Android.Views.View;

namespace DrawnUi.Draw;

public partial class KeyboardManager
{
    private static KeysListener _listener;

    public static Task GetControllers()
    {
        var deviceIds = InputDevice.GetDeviceIds();

        if (deviceIds is null)
        {
            return Task.CompletedTask;
        }

        foreach (var deviceId in deviceIds)
        {
            var device = InputDevice.GetDevice(deviceId);

            if (device is null)
            {
                continue;
            }

            var sources = device.Sources;

            if (sources.HasFlag(InputSourceType.Gamepad) || sources.HasFlag(InputSourceType.Joystick))
            {
                //todo something with 
                var controller = deviceId;
            }
        }

        return Task.CompletedTask;
    }

    public static void AttachToKeyboard(Activity activity) 
    {
        _listener = new(activity, (code, e) =>
        {
            var mapped = MapToMaui(code);

            if (e.Action == KeyEventActions.Down)
            {
                KeyboardPressed(mapped);
            }
            else
            if (e.Action == KeyEventActions.Up)
            {
                KeyboardReleased(mapped);
            }

        });

        //was testing
        //Tasks.StartDelayed(TimeSpan.FromSeconds(1), () =>
        //{
        //    _ = GetControllers();
        //});
    }

    public static MauiKey MapToMaui(Keycode keycode)
    {
        switch (keycode)
        {
            case Keycode.Space: return MauiKey.Space;
            case Keycode.DpadLeft: return MauiKey.ArrowLeft;
            case Keycode.DpadUp: return MauiKey.ArrowUp;
            case Keycode.DpadRight: return MauiKey.ArrowRight;
            case Keycode.DpadDown: return MauiKey.ArrowDown;

            case Keycode.Num0: return MauiKey.Digit0;
            case Keycode.Num1: return MauiKey.Digit1;
            case Keycode.Num2: return MauiKey.Digit2;
            case Keycode.Num3: return MauiKey.Digit3;
            case Keycode.Num4: return MauiKey.Digit4;
            case Keycode.Num5: return MauiKey.Digit5;
            case Keycode.Num6: return MauiKey.Digit6;
            case Keycode.Num7: return MauiKey.Digit7;
            case Keycode.Num8: return MauiKey.Digit8;
            case Keycode.Num9: return MauiKey.Digit9;

            case Keycode.A: return MauiKey.KeyA;
            case Keycode.B: return MauiKey.KeyB;
            case Keycode.C: return MauiKey.KeyC;
            case Keycode.D: return MauiKey.KeyD;
            case Keycode.E: return MauiKey.KeyE;
            case Keycode.F: return MauiKey.KeyF;
            case Keycode.G: return MauiKey.KeyG;
            case Keycode.H: return MauiKey.KeyH;
            case Keycode.I: return MauiKey.KeyI;
            case Keycode.J: return MauiKey.KeyJ;
            case Keycode.K: return MauiKey.KeyK;
            case Keycode.L: return MauiKey.KeyL;
            case Keycode.M: return MauiKey.KeyM;
            case Keycode.N: return MauiKey.KeyN;
            case Keycode.O: return MauiKey.KeyO;
            case Keycode.P: return MauiKey.KeyP;
            case Keycode.Q: return MauiKey.KeyQ;
            case Keycode.R: return MauiKey.KeyR;
            case Keycode.S: return MauiKey.KeyS;
            case Keycode.T: return MauiKey.KeyT;
            case Keycode.U: return MauiKey.KeyU;
            case Keycode.V: return MauiKey.KeyV;
            case Keycode.W: return MauiKey.KeyW;
            case Keycode.X: return MauiKey.KeyX;
            case Keycode.Y: return MauiKey.KeyY;
            case Keycode.Z: return MauiKey.KeyZ;

            case Keycode.CapsLock: return MauiKey.CapsLock;
            case Keycode.Insert: return MauiKey.Insert;
            case Keycode.Del: return MauiKey.Delete;
            // Android doesn’t have a dedicated Print Screen key in most cases.
            case Keycode.Home: return MauiKey.Home;
            case Keycode.MoveEnd: return MauiKey.End;
            case Keycode.PageDown: return MauiKey.PageDown;
            case Keycode.PageUp: return MauiKey.PageUp;
            case Keycode.Escape: return MauiKey.Escape;
            case Keycode.MediaPause: return MauiKey.Pause;

            case Keycode.Menu: return MauiKey.AltLeft; // Often maps to the Alt key
            case Keycode.ShiftLeft: return MauiKey.ShiftLeft;
            case Keycode.ShiftRight: return MauiKey.ShiftRight;
            case Keycode.CtrlLeft: return MauiKey.ControlLeft;
            case Keycode.CtrlRight: return MauiKey.ControlRight;
            case Keycode.Enter: return MauiKey.Enter;
            case Keycode.Tab: return MauiKey.Tab;
            case Keycode.Back: return MauiKey.Backspace;

            case Keycode.F1: return MauiKey.F1;
            case Keycode.F2: return MauiKey.F2;
            case Keycode.F3: return MauiKey.F3;
            case Keycode.F4: return MauiKey.F4;
            case Keycode.F5: return MauiKey.F5;
            case Keycode.F6: return MauiKey.F6;
            case Keycode.F7: return MauiKey.F7;
            case Keycode.F8: return MauiKey.F8;
            case Keycode.F9: return MauiKey.F9;
            case Keycode.F10: return MauiKey.F10;
            case Keycode.F11: return MauiKey.F11;
            case Keycode.F12: return MauiKey.F12;

            case Keycode.NumLock: return MauiKey.NumLock;
            case Keycode.ScrollLock: return MauiKey.ScrollLock;

            case Keycode.MetaLeft: return MauiKey.MetaLeft;
            case Keycode.MetaRight: return MauiKey.MetaRight;
            case Keycode.NumpadDivide: return MauiKey.NumpadDivide;
            case Keycode.NumpadMultiply: return MauiKey.NumpadMultiply;
            case Keycode.NumpadSubtract: return MauiKey.NumpadSubtract;
            case Keycode.NumpadAdd: return MauiKey.NumpadAdd;

            // Punctuation and symbol keys
            case Keycode.Equals: return MauiKey.Equal;
            case Keycode.Minus: return MauiKey.Minus;
            case Keycode.Grave: return MauiKey.Backquote;
            case Keycode.Comma: return MauiKey.Comma;
            case Keycode.Period: return MauiKey.Period;
            case Keycode.Slash: return MauiKey.Slash;
            case Keycode.LeftBracket: return MauiKey.BracketLeft;
            case Keycode.RightBracket: return MauiKey.BracketRight;
            case Keycode.Backslash: return MauiKey.Backslash;
            case Keycode.Semicolon: return MauiKey.Semicolon;

            default:
                return MauiKey.Unknown;
        }
    }

    public class KeysListener : Java.Lang.Object, ViewTreeObserver.IOnGlobalFocusChangeListener, View.IOnKeyListener
    {
        readonly Activity _activity;
        readonly Action<Keycode, KeyEvent> _callback;

        public KeysListener(Activity activity, Action<Keycode, KeyEvent> callback)
        {
            _callback = callback;
            _activity = activity;
            _activity.Window.DecorView.ViewTreeObserver.AddOnGlobalFocusChangeListener(this);

            var currentFocus = _activity.CurrentFocus;
            if (currentFocus != null)
            {
                currentFocus.SetOnKeyListener(this);
            }
        }

        public void OnGlobalFocusChanged(View oldFocus, View newFocus)
        {
            if (oldFocus != null)
                oldFocus.SetOnKeyListener(null);

            if (newFocus != null)
            {
                newFocus.SetOnKeyListener(this);
            }
        }

        /// <summary>
        /// You have to return `true` if the key was handled. We will return `true` always in this implementation.
        /// </summary>
        /// <param name="v"></param>
        /// <param name="keyCode"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public bool OnKey(View v, Keycode keyCode, KeyEvent e)
        {
            if (_callback != null)
            {
                _callback.Invoke(keyCode, e);
                return true;
            }
            return false;
        }

    }



}

#endif
