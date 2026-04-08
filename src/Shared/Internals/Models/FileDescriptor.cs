namespace DrawnUi.Infrastructure;

public class FileDescriptor
{
    public string Filename { get; set; }
    public string Path { get; set; }
    public string FullFilename { get; set; }
    public FileStream Handler { get; set; }

    public void Dispose()
    {
        if (this.Handler != null)
        {
            this.Handler.Close();
            this.Handler.Dispose();
        }
    }
}