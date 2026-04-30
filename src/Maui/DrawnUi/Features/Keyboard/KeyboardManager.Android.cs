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

    public static InputKey MapToMaui(Keycode keycode)
    {
        switch (keycode)
        {
            case Keycode.Space: return InputKey.Space;
            case Keycode.DpadLeft: return InputKey.ArrowLeft;
            case Keycode.DpadUp: return InputKey.ArrowUp;
            case Keycode.DpadRight: return InputKey.ArrowRight;
            case Keycode.DpadDown: return InputKey.ArrowDown;

            case Keycode.Num0: return InputKey.Digit0;
            case Keycode.Num1: return InputKey.Digit1;
            case Keycode.Num2: return InputKey.Digit2;
            case Keycode.Num3: return InputKey.Digit3;
            case Keycode.Num4: return InputKey.Digit4;
            case Keycode.Num5: return InputKey.Digit5;
            case Keycode.Num6: return InputKey.Digit6;
            case Keycode.Num7: return InputKey.Digit7;
            case Keycode.Num8: return InputKey.Digit8;
            case Keycode.Num9: return InputKey.Digit9;

            case Keycode.A: return InputKey.KeyA;
            case Keycode.B: return InputKey.KeyB;
            case Keycode.C: return InputKey.KeyC;
            case Keycode.D: return InputKey.KeyD;
            case Keycode.E: return InputKey.KeyE;
            case Keycode.F: return InputKey.KeyF;
            case Keycode.G: return InputKey.KeyG;
            case Keycode.H: return InputKey.KeyH;
            case Keycode.I: return InputKey.KeyI;
            case Keycode.J: return InputKey.KeyJ;
            case Keycode.K: return InputKey.KeyK;
            case Keycode.L: return InputKey.KeyL;
            case Keycode.M: return InputKey.KeyM;
            case Keycode.N: return InputKey.KeyN;
            case Keycode.O: return InputKey.KeyO;
            case Keycode.P: return InputKey.KeyP;
            case Keycode.Q: return InputKey.KeyQ;
            case Keycode.R: return InputKey.KeyR;
            case Keycode.S: return InputKey.KeyS;
            case Keycode.T: return InputKey.KeyT;
            case Keycode.U: return InputKey.KeyU;
            case Keycode.V: return InputKey.KeyV;
            case Keycode.W: return InputKey.KeyW;
            case Keycode.X: return InputKey.KeyX;
            case Keycode.Y: return InputKey.KeyY;
            case Keycode.Z: return InputKey.KeyZ;

            case Keycode.CapsLock: return InputKey.CapsLock;
            case Keycode.Insert: return InputKey.Insert;
            case Keycode.Del: return InputKey.Delete;
            // Android doesn’t have a dedicated Print Screen key in most cases.
            case Keycode.Home: return InputKey.Home;
            case Keycode.MoveEnd: return InputKey.End;
            case Keycode.PageDown: return InputKey.PageDown;
            case Keycode.PageUp: return InputKey.PageUp;
            case Keycode.Escape: return InputKey.Escape;
            case Keycode.MediaPause: return InputKey.Pause;

            case Keycode.Menu: return InputKey.AltLeft; // Often maps to the Alt key
            case Keycode.ShiftLeft: return InputKey.ShiftLeft;
            case Keycode.ShiftRight: return InputKey.ShiftRight;
            case Keycode.CtrlLeft: return InputKey.ControlLeft;
            case Keycode.CtrlRight: return InputKey.ControlRight;
            case Keycode.Enter: return InputKey.Enter;
            case Keycode.Tab: return InputKey.Tab;
            case Keycode.Back: return InputKey.Backspace;

            case Keycode.F1: return InputKey.F1;
            case Keycode.F2: return InputKey.F2;
            case Keycode.F3: return InputKey.F3;
            case Keycode.F4: return InputKey.F4;
            case Keycode.F5: return InputKey.F5;
            case Keycode.F6: return InputKey.F6;
            case Keycode.F7: return InputKey.F7;
            case Keycode.F8: return InputKey.F8;
            case Keycode.F9: return InputKey.F9;
            case Keycode.F10: return InputKey.F10;
            case Keycode.F11: return InputKey.F11;
            case Keycode.F12: return InputKey.F12;

            case Keycode.NumLock: return InputKey.NumLock;
            case Keycode.ScrollLock: return InputKey.ScrollLock;

            case Keycode.MetaLeft: return InputKey.MetaLeft;
            case Keycode.MetaRight: return InputKey.MetaRight;
            case Keycode.NumpadDivide: return InputKey.NumpadDivide;
            case Keycode.NumpadMultiply: return InputKey.NumpadMultiply;
            case Keycode.NumpadSubtract: return InputKey.NumpadSubtract;
            case Keycode.NumpadAdd: return InputKey.NumpadAdd;

            // Punctuation and symbol keys
            case Keycode.Equals: return InputKey.Equal;
            case Keycode.Minus: return InputKey.Minus;
            case Keycode.Grave: return InputKey.Backquote;
            case Keycode.Comma: return InputKey.Comma;
            case Keycode.Period: return InputKey.Period;
            case Keycode.Slash: return InputKey.Slash;
            case Keycode.LeftBracket: return InputKey.BracketLeft;
            case Keycode.RightBracket: return InputKey.BracketRight;
            case Keycode.Backslash: return InputKey.Backslash;
            case Keycode.Semicolon: return InputKey.Semicolon;

            default:
                return InputKey.Unknown;
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
