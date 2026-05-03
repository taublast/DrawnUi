namespace AppoMobi.Gestures;

public partial class TouchEffect
{
    public static bool LogEnabled { get; set; }
    public static float TappedCancelMoveThresholdPoints = 16f;
    private static float _density = 1f;

    public static float Density
    {
        get => _density;
        set => _density = value > 0 ? value : 1f;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> _locks = new();

    public static bool CheckLockAndSet(
        [System.Runtime.CompilerServices.CallerMemberName] string uid = null,
        int ms = 500)
    {
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var freq = System.Diagnostics.Stopwatch.Frequency;
        var threshold = freq * ms / 1000;

        if (_locks.TryGetValue(uid, out var last) && now - last < threshold)
            return false;

        _locks[uid] = now;
        return true;
    }

    public static void CloseKeyboard() { }
}
