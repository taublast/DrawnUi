namespace DrawnUi.Draw
{
    public static class Colors
    {
        public static Color Transparent { get; } = Color.FromSKColor(SKColors.Transparent);

        public static Color White { get; } = Color.FromSKColor(SKColors.White);

        public static Color Black { get; } = Color.FromSKColor(SKColors.Black);

        public static Color Red { get; } = Color.FromSKColor(SKColors.Red);

        public static Color DarkRed { get; } = Color.FromSKColor(SKColors.DarkRed);

        public static Color Green { get; } = Color.FromSKColor(SKColors.Green);

        public static Color GreenYellow { get; } = Color.FromSKColor(SKColors.GreenYellow);

        public static Color Gray { get; } = Color.FromSKColor(SKColors.Gray);

        public static Color DarkGray { get; } = Color.FromSKColor(SKColors.DarkGray);

        public static Color Orange { get; } = Color.FromSKColor(SKColors.Orange);

        public static Color LimeGreen { get; } = Color.FromSKColor(SKColors.LawnGreen);

        public static Color Default
        {
            get { return Colors.Transparent; }
        }

        /// <summary>
        /// Creates Color from 0-255 int channels.
        /// </summary>
        public static Color FromRgb(int r, int g, int b)
        {
            return Color.FromSKColor(new SKColor((byte)r, (byte)g, (byte)b));
        }

        /// <summary>
        /// Creates Color from 0-255 int channels with alpha.
        /// </summary>
        public static Color FromRgba(int r, int g, int b, int a)
        {
            return Color.FromSKColor(new SKColor((byte)r, (byte)g, (byte)b, (byte)a));
        }

        /// <summary>
        /// Creates Color from 0-1 double channels with alpha.
        /// </summary>
        public static Color FromRgba(double r, double g, double b, double a)
        {
            return new Color(r, g, b, a);
        }

        /// <summary>
        /// Creates Color from 0-1 double channels.
        /// </summary>
        public static Color FromRgb(double r, double g, double b)
        {
            return new Color(r, g, b, 1d);
        }

        public static string ToArgbString(Color color)
        {
            var a = (int)(color.A * 255);
            var r = (int)(color.Red * 255);
            var g = (int)(color.Green * 255);
            var b = (int)(color.Blue * 255);
            return $"#{a:X2}{r:X2}{g:X2}{b:X2}";
        }

        static uint ToHex(char c)
        {
            ushort x = (ushort)c;
            if (x >= '0' && x <= '9')
                return (uint)(x - '0');

            x |= 0x20;
            if (x >= 'a' && x <= 'f')
                return (uint)(x - 'a' + 10);
            return 0;
        }

        static uint ToHexD(char c)
        {
            var j = ToHex(c);
            return (j << 4) | j;
        }

        public static Color ToColor(this string hex)
        {
            if (string.IsNullOrWhiteSpace(hex) || hex.Length < 3)
                return Default;

            int idx = (hex[0] == '#') ? 1 : 0;

            switch (hex.Length - idx)
            {
                case 3: // #rgb
                {
                    var r = (byte)ToHexD(hex[idx]);
                    var g = (byte)ToHexD(hex[idx + 1]);
                    var b = (byte)ToHexD(hex[idx + 2]);
                    return Color.FromSKColor(new SKColor(r, g, b));
                }

                case 4: // #argb
                {
                    var a = (byte)ToHexD(hex[idx]);
                    var r = (byte)ToHexD(hex[idx + 1]);
                    var g = (byte)ToHexD(hex[idx + 2]);
                    var b = (byte)ToHexD(hex[idx + 3]);
                    return Color.FromSKColor(new SKColor(r, g, b, a));
                }

                case 6: // #rrggbb
                {
                    var r = (byte)((ToHex(hex[idx]) << 4) | ToHex(hex[idx + 1]));
                    var g = (byte)((ToHex(hex[idx + 2]) << 4) | ToHex(hex[idx + 3]));
                    var b = (byte)((ToHex(hex[idx + 4]) << 4) | ToHex(hex[idx + 5]));
                    return Color.FromSKColor(new SKColor(r, g, b));
                }

                case 8: // #aarrggbb
                {
                    var a = (byte)((ToHex(hex[idx]) << 4) | ToHex(hex[idx + 1]));
                    var r = (byte)((ToHex(hex[idx + 2]) << 4) | ToHex(hex[idx + 3]));
                    var g = (byte)((ToHex(hex[idx + 4]) << 4) | ToHex(hex[idx + 5]));
                    var b = (byte)((ToHex(hex[idx + 6]) << 4) | ToHex(hex[idx + 7]));
                    return Color.FromSKColor(new SKColor(r, g, b, a));
                }

                default:
                    return Default;
            }
        }
    }
}
