using System.ComponentModel;
using DrawnUi.Infrastructure.Xaml;

namespace DrawnUi.Draw;

[TypeConverter(typeof(FrameworkImageSourceConverter))]
public abstract class ImageSource
{
    public virtual bool IsEmpty => false;

    public static implicit operator ImageSource(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return FrameworkImageSourceConverter.FromInvariantString(value);
    }

    public static implicit operator ImageSource(Uri uri)
    {
        return uri == null ? null : FromUri(uri);
    }

    public static ImageSource FromFile(string file)
    {
        return new FileImageSource { File = file };
    }

    public static ImageSource FromUri(Uri uri)
    {
        return new UriImageSource { Uri = uri };
    }

    public static ImageSource FromStream(Func<CancellationToken, Task<Stream>> stream)
    {
        return new StreamImageSource { Stream = stream };
    }
}

public class FileImageSource : ImageSource
{
    public string File { get; set; }

    public override bool IsEmpty => string.IsNullOrWhiteSpace(File);
}

public class UriImageSource : ImageSource
{
    public Uri Uri { get; set; }

    public override bool IsEmpty => Uri == null;
}

public class StreamImageSource : ImageSource
{
    public Func<CancellationToken, Task<Stream>> Stream { get; set; }

    public override bool IsEmpty => Stream == null;
}