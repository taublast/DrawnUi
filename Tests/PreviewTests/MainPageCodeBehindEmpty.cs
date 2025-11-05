using PreviewTests.Views;
using Canvas = DrawnUi.Views.Canvas;

namespace PreviewTests
{
    public class MainPageCodeBehindEmpty : BasePageCodeBehind, IDisposable
    {
        Canvas Canvas;

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                this.Content = null;
                Canvas?.Dispose();
            }

            base.Dispose(isDisposing);
        }



        public override void Build()
        {
            Canvas?.Dispose();

            Canvas = new Canvas()
            {
                VerticalOptions = LayoutOptions.Fill,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = Colors.LightGray
            };

 

            Content = new Grid() //due to maui layout specifics we are forced to use a Grid as root wrapper
            {
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill,
                Children = { Canvas }
            };
        }

 
    }
}
