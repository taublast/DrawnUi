using System.Diagnostics;

namespace DrawnUi.Views
{

    //
    // Summary:
    //     Values that represent LayoutAlignment.
    //
    // Remarks:
    //     To be added.

    //
    // Summary:
    //     A struct whose static members define various alignment and expansion options.
    //
    // Remarks:
    //     To be added.
    //
    // Summary:
    //     Struct defining height and width as a pair of doubles.
    //
    // Remarks:
    //     Application developers should be aware of the limits of floating-point representations,
    //     specifically the possibility of incorrect comparisons and equality checks for
    //     values with small differences. David Goldberg's paper What Every Computer Scientist
    //     Should Know About Floating-Point Arithmetic describes the issues excellently.

    //
    // Summary:
    //     Struct defining thickness around the edges of a Xamarin.Forms.Rectangle using
    //     doubles.
    //
    // Remarks:
    //     To be added.
    [DebuggerDisplay("Left={Left}, Top={Top}, Right={Right}, Bottom={Bottom}, HorizontalThickness={HorizontalThickness}, VerticalThickness={VerticalThickness}")]
    public struct Thickness
    {
        //
        // Summary:
        //     The thickness of the left side of a rectangle.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double Left
        {
            get;
            set;
        }
            public static readonly Thickness Zero = new Thickness(0);

        //
        // Summary:
        //     The thickness of the top of a rectangle.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double Top
        {
            get;
            set;
        }

        //
        // Summary:
        //     The thickness of the right side of a rectangle.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double Right
        {
            get;
            set;
        }

        //
        // Summary:
        //     The thickness of the bottom of a rectangle.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double Bottom
        {
            get;
            set;
        }

        //
        // Summary:
        //     The sum of Xamarin.Forms.Thickness.Left and Xamarin.Forms.Thickness.Right.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double HorizontalThickness => Left + Right;

        //
        // Summary:
        //     The sum of Xamarin.Forms.Thickness.Top and Xamarin.Forms.Thickness.Bottom.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double VerticalThickness => Top + Bottom;

        //
        // Summary:
        //     To be added.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public bool IsEmpty
        {
            get
            {
                if (Left == 0.0 && Top == 0.0 && Right == 0.0)
                {
                    return Bottom == 0.0;
                }

                return false;
            }
        }

        public Thickness(double uniformSize)
            : this(uniformSize, uniformSize, uniformSize, uniformSize)
        {
        }

        public Thickness(double horizontalSize, double verticalSize)
            : this(horizontalSize, verticalSize, horizontalSize, verticalSize)
        {
        }

        public Thickness(double left, double top, double right, double bottom)
        {
            this = default(Thickness);
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        //
        // Summary:
        //     Converts a Xamarin.Forms.Size into a Xamarin.Forms.Thickness.
        //
        // Parameters:
        //   size:
        //     A Xamarin.Forms.Size to convert to a Xamarin.Forms.Thickness
        //
        // Returns:
        //     To be added.
        //
        // Remarks:
        //     The Xamarin.Forms.Thickness's Xamarin.Forms.Thickness.Left and Xamarin.Forms.Thickness.Right
        //     are both set equal to the size's Xamarin.Forms.Size.Width and the Xamarin.Forms.Thickness's
        //     Xamarin.Forms.Thickness.Top and Xamarin.Forms.Thickness.Bottom are both set equal
        //     to the size's Xamarin.Forms.Size.Height.
        public static implicit operator Thickness(Size size)
        {
            return new Thickness(size.Width, size.Height, size.Width, size.Height);
        }

        //
        // Summary:
        //     Implicit cast operator from System.Double.
        //
        // Parameters:
        //   uniformSize:
        //     The value for the uniform Thickness.
        //
        // Returns:
        //     A Thickness with an uniform size.
        //
        // Remarks:
        //     Application developers should bear in mind that Xamarin.Forms.Thickness.HorizontalThickness
        //     and Xamarin.Forms.Thickness.VerticalThickness are the sums of their components,
        //     so a Xamarin.Forms.Thickness created from a uniformSize of, for instance, 1.0,
        //     will have Xamarin.Forms.Thickness.HorizontalThickness of 2.0.
        public static implicit operator Thickness(double uniformSize)
        {
            return new Thickness(uniformSize);
        }

        private bool Equals(Thickness other)
        {
            if (Left.Equals(other.Left) && Top.Equals(other.Top) && Right.Equals(other.Right))
            {
                return Bottom.Equals(other.Bottom);
            }

            return false;
        }

        //
        // Summary:
        //     Whether the obj is a Xamarin.Forms.Thickness with equivalent values.
        //
        // Parameters:
        //   obj:
        //     A Xamarin.Forms.Thickness to be compared.
        //
        // Returns:
        //     true if obj is a Xamarin.Forms.Thickness and has equivalent values.
        //
        // Remarks:
        //     To be added.
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is Thickness)
            {
                return Equals((Thickness)obj);
            }

            return false;
        }

        //
        // Summary:
        //     A has value for this Xamarin.Forms.Thickness.
        //
        // Returns:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public override int GetHashCode()
        {
            return (((((Left.GetHashCode() * 397) ^ Top.GetHashCode()) * 397) ^ Right.GetHashCode()) * 397) ^ Bottom.GetHashCode();
        }

        //
        // Summary:
        //     Whether two Xamarin.Forms.Thicknesses have identical values.
        //
        // Parameters:
        //   left:
        //     A Xamarin.Forms.Thickness to be compared.
        //
        //   right:
        //     A Xamarin.Forms.Thickness to be compared.
        //
        // Returns:
        //     true if left and right have identical values for Xamarin.Forms.Thickness.Left,Xamarin.Forms.Thickness.Right,
        //     Xamarin.Forms.Thickness.Top, and Xamarin.Forms.Thickness.Bottom.
        //
        // Remarks:
        //     To be added.
        public static bool operator ==(Thickness left, Thickness right)
        {
            return left.Equals(right);
        }

        //
        // Summary:
        //     Whether the values of two Xamarin.Forms.Thickness's have at least one difference.
        //
        // Parameters:
        //   left:
        //     A Xamarin.Forms.Thickness to be compared.
        //
        //   right:
        //     A Xamarin.Forms.Thickness to be compared.
        //
        // Returns:
        //     true if any of the Xamarin.Forms.Thickness.Left,Xamarin.Forms.Thickness.Right,
        //     Xamarin.Forms.Thickness.Top, and Xamarin.Forms.Thickness.Bottom values differ
        //     between left and right.
        //
        // Remarks:
        //     To be added.
        public static bool operator !=(Thickness left, Thickness right)
        {
            return !left.Equals(right);
        }

        public void Deconstruct(out double left, out double top, out double right, out double bottom)
        {
            left = Left;
            top = Top;
            right = Right;
            bottom = Bottom;
        }
    }
}
#if false // Decompilation log
'119' items in cache
------------------
Resolve: 'netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Found single assembly: 'netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51'
Load from: 'C:\Program Files\dotnet\sdk\NuGetFallbackFolder\netstandard.library\2.0.3\build\netstandard2.0\ref\netstandard.dll'
------------------
Resolve: 'Xamarin.Forms.Platform, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null'
Found single assembly: 'Xamarin.Forms.Platform, Version=2.0.0.0, Culture=neutral, PublicKeyToken=null'
Load from: 'C:\Users\nicky\.nuget\packages\xamarin.forms\5.0.0.2012\lib\netstandard2.0\Xamarin.Forms.Platform.dll'
#endif
