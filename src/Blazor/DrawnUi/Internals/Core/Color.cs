namespace Microsoft.Maui.Graphics
{
    public class Color : IEquatable<Color>
    {
        public Color(float red, float green, float blue, float alpha = 1f)
        {
            Red = Clamp(red);
            Green = Clamp(green);
            Blue = Clamp(blue);
            Alpha = Clamp(alpha);
        }

        public Color(double red, double green, double blue, double alpha = 1.0)
            : this((float)red, (float)green, (float)blue, (float)alpha)
        {
        }

        public float Red { get; }

        public float Green { get; }

        public float Blue { get; }

        public float Alpha { get; }

        public float A => Alpha;

        public SKColor ToSKColor()
        {
            return new SKColor(ToByte(Red), ToByte(Green), ToByte(Blue), ToByte(Alpha));
        }

        public Color WithAlpha(double alpha)
        {
            return new Color(Red, Green, Blue, (float)alpha);
        }

        public Color WithAlpha(float alpha)
        {
            return new Color(Red, Green, Blue, alpha);
        }

        public Color WithAlpha(byte alpha)
        {
            return new Color(Red, Green, Blue, alpha / 255f);
        }

        public static Color FromRgb(byte red, byte green, byte blue)
        {
            return new Color(red / 255f, green / 255f, blue / 255f, 1f);
        }

        public static Color FromHsla(float hue, float saturation, float lightness, float alpha = 1f)
        {
            var sk = SKColor.FromHsl(hue * 360f, saturation * 100f, lightness * 100f).WithAlpha(ToByte(alpha));
            return FromSKColor(sk);
        }

        public static Color FromSKColor(SKColor color)
        {
            return new Color(color.Red / 255f, color.Green / 255f, color.Blue / 255f, color.Alpha / 255f);
        }

        public bool Equals(Color other)
        {
            return other is not null
                   && Red.Equals(other.Red)
                   && Green.Equals(other.Green)
                   && Blue.Equals(other.Blue)
                   && Alpha.Equals(other.Alpha);
        }

        public override bool Equals(object obj)
        {
            return obj is Color other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Red, Green, Blue, Alpha);
        }

        public static implicit operator SKColor(Color color)
        {
            return color?.ToSKColor() ?? SKColors.Transparent;
        }

        public static implicit operator Color(SKColor color)
        {
            return FromSKColor(color);
        }

        private static float Clamp(float value)
        {
            return Math.Clamp(value, 0f, 1f);
        }

        private static byte ToByte(float value)
        {
            return (byte)Math.Clamp((int)Math.Round(value * 255f), 0, 255);
        }
    }
}