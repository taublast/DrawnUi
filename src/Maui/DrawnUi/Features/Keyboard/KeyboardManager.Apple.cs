#if MACCATALYST


namespace DrawnUi.Draw;

public partial class KeyboardManager
{

    /*
      public override void PressesBegan(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        foreach (UIPress press in presses)
        {
            var mapped = AppleKeyMapper.MapToMaui((int)press.Type);
            KeyboardManager.KeyboardPressed(mapped);
        }

        //if (!consumed)
        //{
        //    base.PressesBegan(presses, evt);
        //}
    }

    void ReleaseKeys(NSSet<UIPress> presses)
    {
        foreach (UIPress press in presses)
        {
            var mapped = AppleKeyMapper.MapToMaui((int)press.Type);
            KeyboardManager.KeyboardReleased(mapped);

            //Trace.WriteLine($"[KEY] {press.Type}/{(int)press.Type} => {mapped}");
        }
    }

    public override void PressesEnded(NSSet<UIPress> presses, UIPressesEvent evt)
    {
        base.PressesEnded(presses, evt);

        ReleaseKeys(presses);
    }
     */

    public static InputKey MapToMaui(int virtualKey)
    {
        switch (virtualKey)
        {
        case 2044: return InputKey.Space;
        case 2079: return InputKey.ArrowRight;
        case 2080: return InputKey.ArrowLeft;
        case 2081: return InputKey.ArrowDown;
        case 2082: return InputKey.ArrowUp;
        case 2030: return InputKey.Digit0;
        case 2031: return InputKey.Digit1;
        case 2032: return InputKey.Digit2;
        case 2033: return InputKey.Digit3;
        case 2034: return InputKey.Digit4;
        case 2035: return InputKey.Digit5;
        case 2036: return InputKey.Digit6;
        case 2037: return InputKey.Digit7;
        case 2038: return InputKey.Digit8;
        case 2039: return InputKey.Digit9;

        case 2004: return InputKey.KeyA;
        case 2005: return InputKey.KeyB;
        case 2006: return InputKey.KeyC;
        case 2007: return InputKey.KeyD;
        case 2008: return InputKey.KeyE;
        case 2009: return InputKey.KeyF;
        case 2010: return InputKey.KeyG;
        case 2011: return InputKey.KeyH;
        case 2012: return InputKey.KeyI;
        case 2013: return InputKey.KeyJ;
        case 2014: return InputKey.KeyK;
        case 2015: return InputKey.KeyL;
        case 2016: return InputKey.KeyM;
        case 2017: return InputKey.KeyN;
        case 2018: return InputKey.KeyO;
        case 2019: return InputKey.KeyP;
        case 2020: return InputKey.KeyQ;
        case 2021: return InputKey.KeyR;
        case 2022: return InputKey.KeyS;
        case 2023: return InputKey.KeyT;
        case 2024: return InputKey.KeyU;
        case 2025: return InputKey.KeyV;
        case 2026: return InputKey.KeyW;
        case 2027: return InputKey.KeyX;
        case 2028: return InputKey.KeyY;
        case 2029: return InputKey.KeyZ;

        case 2057: return InputKey.CapsLock;

        case 2117: return InputKey.Insert;
        //case VirtualKey.Delete: return InputKey.Delete;
        case 2104: return InputKey.PrintScreen;
        case 2074: return InputKey.Home;
        case 2077: return InputKey.End;

        case 2075: return InputKey.PageUp;
        case 2078: return InputKey.PageDown;

        case 2041: return InputKey.Escape;
        //case VirtualKey.Pause: return InputKey.Pause;
        case 2226: return InputKey.AltLeft;
        case 2230: return InputKey.AltRight;
        case 2225: return InputKey.ShiftLeft;
        case 2229: return InputKey.ShiftRight;

        case 2100: return InputKey.IntBackslash;
        case 2053: return InputKey.Backquote;

        case 2228: return InputKey.ControlRight;
        case 2224: return InputKey.ControlLeft;
        case 2040: return InputKey.Enter;
        case 2043: return InputKey.Tab;
        case 2042: return InputKey.Backspace;
        case 2046: return InputKey.Equal;
        case 2045: return InputKey.Minus;
        case 2058: return InputKey.F1;
        case 2059: return InputKey.F2;
        case 2060: return InputKey.F3;
        case 2061: return InputKey.F4;
        case 2062: return InputKey.F5;
        case 2063: return InputKey.F6;
        case 2064: return InputKey.F7;
        case 2065: return InputKey.F8;
        case 2066: return InputKey.F9;
        case 2067: return InputKey.F10;
        case 2068: return InputKey.F11;
        case 2069: return InputKey.F12;
        case 2083: return InputKey.NumLock;
        //case VirtualKey.Scroll: return InputKey.ScrollLock;
        case 2098: return InputKey.Numpad0;
        case 2089: return InputKey.Numpad1;
        case 2090: return InputKey.Numpad2;
        case 2091: return InputKey.Numpad3;
        case 2092: return InputKey.Numpad4;
        case 2093: return InputKey.Numpad5;
        case 2094: return InputKey.Numpad6;
        case 2095: return InputKey.Numpad7;
        case 2096: return InputKey.Numpad8;
        case 2097: return InputKey.Numpad9;

        //case CommandLeft: return InputKey.MetaLeft;

        case 2084: return InputKey.NumpadDivide;
        case 2085: return InputKey.NumpadMultiply;
        case 2086: return InputKey.NumpadSubtract;
        case 2087: return InputKey.NumpadAdd;

        case 2054: return InputKey.Comma;
        case 2055: return InputKey.Period;
        case 2056: return InputKey.Slash;

        case 2047: return InputKey.BracketLeft;
        case 2048: return InputKey.BracketRight;
        case 2049: return InputKey.Backslash;
        case 2052: return InputKey.Quote;
        case 2051: return InputKey.Semicolon;


        }

        return InputKey.Unknown;

    }

}

#endif