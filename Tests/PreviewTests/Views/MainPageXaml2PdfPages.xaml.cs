using DrawnUi.Infrastructure;
using PreviewTests.Views.Xaml2Pdf;

namespace PreviewTests.Views
{
    public partial class MainPageXaml2PdfPages : BasePageCodeBehind
    {

        public MainPageXaml2PdfPages()
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
            _ = CreatePdfPages(new SKSize(4.13f, 2.11f), 150);
        }

        private void SkiaButton_OnTapped2(object sender, ControlTappedEventArgs controlTappedEventArgs)
        {
            _ = CreatePdfPages(Pdf.GetPaperSizeInInches(PaperFormat.A6), 150);
        }

        async Task CreatePdfPages(SKSize inches, int dpi)
        {
            //setup our report to print
            var paper = Pdf.GetPaperSizePixels(inches, dpi);
            BindableText = "This text came from bindings";
            var vendor = "DrawnUI";
            var filename = GenerateFileName(DateTime.Now, "pdf");

            //introduce page margins
            var margins = 0.1f * dpi; //add margins 0.1 inches, change this as you wish
            var pageSizeAccountMargins = new SKSize(paper.Width - margins * 2, paper.Height - margins * 2);

            //====================
            // Create our report to be printed
            //====================
            var content = new ReportSample()
            {
                HorizontalOptions = LayoutOptions.Fill,
                BindingContext = this //whatever you want, you can have bindings inside your report
            };

            //====================
            // Create wrappers for output
            //====================
            var ctx = content.BindingContext;
            SkiaScroll viewport = null; //to scroll though visible parts for each page
            SkiaLayout wrapper = new() //need wrapper for margins, will use padding
            {
                //Background = Colors.Red, //debug margins
                BindingContext = ctx,
                Padding = new(margins),
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                Children = new List<SkiaControl>()
                {
                    new SkiaScroll()
                    {
                        Tag = "PdfScroll",
                        BackgroundColor = Colors.White,
                        VerticalOptions = LayoutOptions.Fill,
                        HorizontalOptions = LayoutOptions.Fill,
                        Content = content,
                    }.Adapt((c) =>
                    {
                        viewport = c;
                    })},
            };

            //====================
            // Render and share
            //====================
            Files.CheckPermissionsAsync(async () =>
             {

                 //in this example we will split a large content into pages
                 //when we do not for on a single page format
                 try
                 {
                     _lockLogs = true;
                     string fullFilename = null;
                     var subfolder = "Pdf";

                     //====================
                     //STEP 1: Measure content
                     //====================
                     var scale = 1; //do not change this

                     wrapper.Measure(paper.Width, paper.Height, scale);
                     wrapper.Arrange(new SKRect(0, 0, paper.Width, paper.Height),
                         wrapper.MeasuredSize.Pixels.Width, wrapper.MeasuredSize.Pixels.Height, scale);

                     var contentSize = new SKSize(content.MeasuredSize.Units.Width, content.MeasuredSize.Units.Height);

                     //====================
                     //STEP 2: Render pages
                     //====================
                     var pages = Pdf.SplitToPages(contentSize, pageSizeAccountMargins);

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
                             foreach (PdfPagePosition page in pages)
                             {
                                 viewport.ViewportOffsetY = -page.Position.Y;

                                 using (var canvas = document.BeginPage(paper.Width, paper.Height))
                                 {
                                     var ctx = new SkiaDrawingContext()
                                     {
                                         Canvas = canvas,
                                         Width = paper.Width,
                                         Height = paper.Height
                                     };

                                     var drawingContext = new DrawingContext(ctx, new SKRect(0, 0, paper.Width, paper.Height), scale);
                                     //first rendering to launch loading images and first layout
                                     wrapper.Render(drawingContext);

                                     //in our specific case we have images inside that load async,
                                     //so wait for them and render final result
                                     while (!content.AllImagesLoaded)
                                     {
                                         await Task.Delay(50);
                                     }

                                     //second rendering required to reflect layout changes and async images are loaded
                                     canvas.Clear(SKColors.White); //non-transparent reserves space inside pdf
                                     wrapper.Render(drawingContext);

                                 }
                                 document.EndPage();
                             }

                             document.Close();
                         }

                         ms.Position = 0;
                         var bytes = ms.ToArray();

                         var file = Files.OpenFile(fullFilename, StorageType.Cache, subfolder);

                         // Write the bytes to the FileStream of the FileDescriptor
                         await file.Handler.WriteAsync(bytes, 0, bytes.Length);

                         // Ensure all bytes are written to the underlying device
                         await file.Handler.FlushAsync();

                         Files.CloseFile(file, true);
                         await Task.Delay(500); //we need this for slow file system
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
