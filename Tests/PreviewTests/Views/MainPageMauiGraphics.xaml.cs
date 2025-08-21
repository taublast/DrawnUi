namespace PreviewTests.Views
{
    public partial class MainPageMauiGraphics 
    {

        public MainPageMauiGraphics()
        {
            try
            {
                InitializeComponent();

                //avoid setting context BEFORE InitializeComponent, can bug 
                //having parent BindingContext still null when constructing from xaml
                BindingContext = new MainPageViewModel();
            }
            catch (Exception e)
            {
                Super.DisplayException(this, e);
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// To refresh page constantly
        /// </summary>
        private SkiaValueAnimator _animatorWatch;

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (_animatorWatch == null)
            {
                _animatorWatch = new SkiaValueAnimator(MainLayout)
                {
                    Speed = 1000,
                    OnUpdated = (v) =>
                    {
                        //do not need 
                    },
                    Easing = Easing.Linear,
                    Repeat = -1 //forever
                };
            }

            _animatorWatch.Start();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            _animatorWatch?.Stop();
        }
    }

    /// <summary>
    /// Example taken from https://www.youtube.com/watch?v=SH8oQZp0oF0&ab_channel=ProgrammingWithChris
    /// </summary>
    public class ClockDrawable : IDrawable
    {

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            DateTime curTime = DateTime.Now;
            var clockCenterPoint = new PointF(dirtyRect.Center.X, dirtyRect.Center.Y);
            var circleRadius = 150;

            canvas.StrokeColor = Colors.Aqua;
            canvas.StrokeSize = 6;

            canvas.DrawCircle(clockCenterPoint, circleRadius);
            canvas.DrawCircle(clockCenterPoint, 5);

            canvas.StrokeSize = 4;
            var hourPoint = GetHourHand(curTime, circleRadius, clockCenterPoint);
            canvas.DrawLine(clockCenterPoint, hourPoint);


            var minutePoint = GetMinuteHand(curTime, circleRadius, clockCenterPoint);
            canvas.DrawLine(clockCenterPoint, minutePoint);

            var secondPoint = GetSecondHand(curTime, circleRadius, clockCenterPoint);
            canvas.DrawLine(clockCenterPoint, secondPoint);

        }

        internal static PointF GetHourHand(DateTime curTime, int radius, PointF center)
        {

            int currentHour = curTime.Hour;

            if (currentHour > 12)
                currentHour -= 12;

            var angleDegrees = (currentHour * 360) / 12;
            var angle = (Math.PI / 180.0) * angleDegrees;

            var hourShorter = radius * .8;
            PointF outerPoint = new((float)(hourShorter * Math.Sin(angle)) + center.X, (float)(-hourShorter * Math.Cos(angle)) + center.Y);

            return outerPoint;
        }

        internal static PointF GetMinuteHand(DateTime curTime, int radius, PointF center)
        {

            int currentMin = curTime.Minute;

            var angleDegrees = (currentMin * 360) / 60;
            var angle = (Math.PI / 180.0) * angleDegrees;

            PointF outerPoint = new((float)(radius * Math.Sin(angle)) + center.X, (float)(-radius * Math.Cos(angle)) + center.Y);

            return outerPoint;
        }

        internal static PointF GetSecondHand(DateTime curTime, int radius, PointF center)
        {

            int currentSecond = curTime.Second;

            var angleDegrees = (currentSecond * 360) / 60;
            var angle = (Math.PI / 180.0) * angleDegrees;

            PointF outerPoint = new((float)(radius * Math.Sin(angle) + center.X), (float)(-radius * Math.Cos(angle)) + center.Y);

            return outerPoint;
        }
    }
}
