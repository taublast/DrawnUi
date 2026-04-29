namespace Microsoft.Maui.Controls
{
    public class Easing
    {
        private readonly Func<double, double> _ease;

        public Easing(Func<double, double> ease)
        {
            _ease = ease ?? throw new ArgumentNullException(nameof(ease));
        }

        public double Ease(double value)
        {
            return _ease(value);
        }

        public static Easing Linear { get; } = new(value => value);

        public static Easing CubicIn { get; } = new(value => value * value * value);

        public static Easing SpringOut { get; } = new(value =>
        {
            var inverse = 1d - value;
            return 1d - inverse * inverse * inverse;
        });
    }
}