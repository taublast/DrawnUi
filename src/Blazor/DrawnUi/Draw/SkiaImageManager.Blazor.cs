namespace DrawnUi.Draw;

public sealed class SkiaImageManager
{
    public static string NativeFilePrefix = "file://";

    private SkiaImageManager()
    {
    }

    public static SkiaImageManager Instance { get; } = new();

    public bool IsLoadingLocked { get; set; }
}