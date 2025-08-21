using DrawnUi.Infrastructure.Models;

namespace PreviewTests.Views.Xaml2Pdf;

public partial class ReportSample : SkiaLayout
{
    public ReportSample()
    {
        InitializeComponent();
    }

    private int imagesLoaded = 0;


    public bool AllImagesLoaded
    {
        get
        {
            return imagesLoaded > 0;
        }
    }

    private void SkiaImage_OnOnSuccess(object sender, ContentLoadedEventArgs e)
    {
        imagesLoaded++;
        OnPropertyChanged(nameof(AllImagesLoaded));
    }
}
