using Microsoft.JSInterop;

namespace DrawnUi.Draw;

public partial class KeyboardManager
{
    private static IJSObjectReference? _module;
    private static bool _attached;

    private const string KeyboardModulePath = "./_content/DrawnUi.Blazor/drawnui-keyboard.js";

    public static async Task AttachToKeyboardAsync(IJSRuntime jsRuntime)
    {
        if (_attached)
        {
            return;
        }

        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", KeyboardModulePath);
        await _module.InvokeVoidAsync("attachGlobalKeyboard");
        _attached = true;
    }

    [JSInvokable]
    public static void HandleGlobalKeyDown(string? code)
    {
        KeyboardPressed(MapToMaui(code));
    }

    [JSInvokable]
    public static void HandleGlobalKeyUp(string? code)
    {
        KeyboardReleased(MapToMaui(code));
    }

    public static InputKey MapToMaui(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return InputKey.Unknown;
        }

        return Enum.TryParse<InputKey>(code, ignoreCase: false, out var mapped)
            ? mapped
            : InputKey.Unknown;
    }
}
