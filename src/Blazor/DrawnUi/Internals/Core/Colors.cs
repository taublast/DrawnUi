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

        public static Color FromRgb(int r, int g, int b)
        {
            return FromRgba(r, g, b, 255);
        }

        public static Color FromRgba(double r, double g, double b, double a)
        {
            return new Color(r, g, b, a);
        }

        public static Color FromRgb(double r, double g, double b)
        {
            return new Color(r, g, b, 1d);
        }

        public static string ToArgbString(Color color)
        {
            // Format the color as a hex string with alpha
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
            // Undefined
            if (hex.Length < 3)
                return Default;
            int idx = (hex[0] == '#') ? 1 : 0;

            switch (hex.Length - idx)
            {
                case 3: //#rgb => ffrrggbb
                    var t1 = ToHexD(hex[idx++]);
                    var t2 = ToHexD(hex[idx++]);
                    var t3 = ToHexD(hex[idx]);

                    return FromRgb((int)t1, (int)t2, (int)t3);

                case 4: //#argb => aarrggbb
                    var f1 = ToHexD(hex[idx++]);
                    var f2 = ToHexD(hex[idx++]);
                    var f3 = ToHexD(hex[idx++]);
                    var f4 = ToHexD(hex[idx]);
                    return FromRgba((int)f2, (int)f3, (int)f4, (int)f1);

                case 6: //#rrggbb => ffrrggbb
                    return FromRgb((int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx++])),
                        (int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx++])),
                        (int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx])));

                case 8: //#aarrggbb
                    var a1 = ToHex(hex[idx++]) << 4 | ToHex(hex[idx++]);
                    return FromRgba((int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx++])),
                        (int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx++])),
                        (int)(ToHex(hex[idx++]) << 4 | ToHex(hex[idx])),
                        (int)a1);

                default: //everything else will result in unexpected results
                    return Default;
            }
        }
    }
}
