using DrawnUi.Infrastructure;
using PreviewTests.Views.Xaml2Pdf;

namespace PreviewTests.Views
{
    public partial class MainPageXaml2Pdf : BasePageCodeBehind
    {

        public MainPageXaml2Pdf()
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

        string GenerateFileName(DateTime timeStamp, string extension)
        {
            var filename = $"DrawnUi_{timeStamp:yyyy-MM-dd_HHmmss}.{extension}";

            return filename;
        }

        private bool _lockLogs;

        private string _BindableText;
        public string BindableText
        {
            get
            {
                return _BindableText;
            }
            set
            {
                if (_BindableText != value)
                {
                    _BindableText = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SkiaButton_OnTapped(object sender, ControlTappedEventArgs controlTappedEventArgs)
        {
            _ = CreatePdf(PaperFormat.A4, 150);
        }

        private void SkiaButton_OnTapped2(object sender, ControlTappedEventArgs controlTappedEventArgs)
        {
            _ = CreatePdf(PaperFormat.A6, 150);
        }

        async Task CreatePdf(PaperFormat format, int dpi)
        {
            //setup our report to print
            BindableText = "This text came from bindings";
            var vendor = "DrawnUI";
            var filename = GenerateFileName(DateTime.Now, "pdf");
            var paper = Pdf.GetPaperSizePixels(format, dpi);

            var layout = new ReportSample()
            {
                BindingContext = this //whatever you want, you can have bindings inside your report
            };

            //render and share
            Files.CheckPermissionsAsync(async () =>
             {

                 //in this example PDF content size is less or equal to the page format.
                 //in another example we will see how to split a large content into pages
                 //when we do not for on a single page format
                 try
                 {
                     _lockLogs = true;
                     string fullFilename = null;
                     var subfolder = "Pdf";
                     var scale = 1; //do not change this
                     var destination = new SKRect(0, 0, paper.Width, float.PositiveInfinity);
                     var measured = layout.Measure(destination.Width, destination.Height, scale);

                     //prepare DrawingRect
                     layout.Arrange(new SKRect(0, 0, layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height),
                         layout.MeasuredSize.Pixels.Width, layout.MeasuredSize.Pixels.Height, scale);

                     var reportSize = new SKSize(measured.Units.Width, measured.Units.Height);

                     //we need a local file to ba saved in order to share it
                     fullFilename = Files.GetFullFilename(filename, StorageType.Cache, subfolder);

                     if (File.Exists(fullFilename))
                     {
                         File.Delete(fullFilename);
                     }

                     using (var ms = new MemoryStream())
                     using (var stream = new SKManagedWStream(ms))
                     {
                         using (var document = SKDocument.CreatePdf(stream, new SKDocumentPdfMetadata
                         {
                             Author = vendor,
                             Producer = vendor,
                             Subject = this.Title
                         }))
                         {
                             using (var canvas = document.BeginPage(reportSize.Width, reportSize.Height))
                             {
                                 var ctx = new SkiaDrawingContext()
                                 {
                                     Canvas = canvas,
                                     Width = reportSize.Width,
                                     Height = reportSize.Height
                                 };
                                 var c1 = new DrawingContext(ctx, new SKRect(0, 0, reportSize.Width, reportSize.Height),
                                     scale);
                                 //with no async stuff this is enough for most cases
                                 layout.Render(c1);

                                 //in our specific case we have images inside that load async,
                                 //so wait for them and render final result
                                 while (!layout.AllImagesLoaded)
                                 {
                                     await Task.Delay(50);
                                 }

                                 //second rendering required to reflect layout changes
                                 canvas.Clear(SKColor.Empty);
                                 var c = new DrawingContext(ctx, new SKRect(0, 0, reportSize.Width, reportSize.Height),
                                     scale, null);
                                 layout.Render(c);
                             }
                             document.EndPage();
                             document.Close();
                         }

                         ms.Position = 0;
                         var content = ms.ToArray();

                         var file = Files.OpenFile(fullFilename, StorageType.Cache, subfolder);

                         // Write the bytes to the FileStream of the FileDescriptor
                         await file.Handler.WriteAsync(content, 0, content.Length);

                         // Ensure all bytes are written to the underlying device
                         await file.Handler.FlushAsync();

                         Files.CloseFile(file, true);
                         await Task.Delay(500);
                     }

                     //can share the file now
                     Files.Share("PDF", new string[] { fullFilename });
                 }
                 catch (Exception e)
                 {
                     Super.Log(e);
                 }
                 finally
                 {
                     _lockLogs = false;
                 }
             });
        }

    }
}
