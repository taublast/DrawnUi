namespace MauiNet8;

public partial class TestPage
{

    public TestPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception e)
        {
            Super.DisplayException(this, e);
        }
    }

    public byte[] ImageBytes
    {
        get
        {
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync("Images/logo.png").GetAwaiter().GetResult();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }
    }





}
