namespace DrawnUI.Tutorials.NewsFeed
{
    public class BannerImage : SkiaImage
    {
        public override void SetImageSource(ImageSource source)
        {
            this.Opacity = 0.0;

            base.SetImageSource(source);
        }

        public override void OnSuccess(string source)
        {
            base.OnSuccess(source);

            this.Opacity = 0.0;
            _ = this.FadeToAsync(1, 250, Easing.SinIn);
        }
    }
}
