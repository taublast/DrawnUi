using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace DrawnUi.Views
{

    //
    // Summary:
    //     Struct defining a 2-D point as a pair of doubles.
    //
    // Remarks:
    //     To be added.


    [DebuggerDisplay("Width={Width}, Height={Height}")]
    public struct Size
    {
        private double _width;

        private double _height;

        //
        // Summary:
        //     The Xamarin.Forms.Size whose values for height and width are 0.0.
        //
        // Remarks:
        //     To be added.
        public static readonly Size Zero;

        //
        // Summary:
        //     Whether the Xamarin.Forms.Size has Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width
        //     of 0.0.
        //
        // Value:
        //     true if both Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width are 0.0.
        //
        // Remarks:
        //     To be added.
        public bool IsZero
        {
            get
            {
                if (_width == 0.0)
                {
                    return _height == 0.0;
                }

                return false;
            }
        }

        //
        // Summary:
        //     Magnitude along the horizontal axis, in platform-defined units.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        [DefaultValue(0.0)]
        public double Width
        {
            get
            {
                return _width;
            }
            set
            {
                if (double.IsNaN(value))
                {
                    throw new ArgumentException("NaN is not a valid value for Width");
                }

                _width = value;
            }
        }

        //
        // Summary:
        //     Magnitude along the vertical axis, in platform-specific units.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        [DefaultValue(0.0)]
        public double Height
        {
            get
            {
                return _height;
            }
            set
            {
                if (double.IsNaN(value))
                {
                    throw new ArgumentException("NaN is not a valid value for Height");
                }

                _height = value;
            }
        }

        public Size(double width, double height)
        {
            if (double.IsNaN(width))
            {
                throw new ArgumentException("NaN is not a valid value for width");
            }

            if (double.IsNaN(height))
            {
                throw new ArgumentException("NaN is not a valid value for height");
            }

            _width = width;
            _height = height;
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Size whose Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width
        //     are the sum of the component's height and width.
        //
        // Parameters:
        //   s1:
        //     A Xamarin.Forms.Size to be added.
        //
        //   s2:
        //     A Xamarin.Forms.Size to be added.
        //
        // Returns:
        //     A Xamarin.Forms.Size whose Xamarin.Forms.Size.Width is equal to s1.Width + s2.Width
        //     and whose Xamarin.Forms.Size.Height is equal to sz1.Height + sz2.Height.
        //
        // Remarks:
        //     To be added.
        public static Size operator +(Size s1, Size s2)
        {
            return new Size(s1._width + s2._width, s1._height + s2._height);
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Size whose Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width
        //     are s1's height and width minus the values in s2.
        //
        // Parameters:
        //   s1:
        //     A Xamarin.Forms.Size from whose values a size will be subtracted.
        //
        //   s2:
        //     The Xamarin.Forms.Size to subtract from s1.
        //
        // Returns:
        //     A Xamarin.Forms.Size whose Xamarin.Forms.Size.Width is equal to s1.Width - s2.Width
        //     and whose Xamarin.Forms.Size.Height is equal to sz1.Height - sz2.Height.
        //
        // Remarks:
        //     To be added.
        public static Size operator -(Size s1, Size s2)
        {
            return new Size(s1._width - s2._width, s1._height - s2._height);
        }

        //
        // Summary:
        //     Scales both Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height.
        //
        // Parameters:
        //   s1:
        //     A Xamarin.Forms.Size to be scaled.
        //
        //   value:
        //     A factor by which to multiple s1's Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     values.
        //
        // Returns:
        //     A new Xamarin.Forms.Size whose Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     have been scaled by value.
        //
        // Remarks:
        //     To be added.
        public static Size operator *(Size s1, double value)
        {
            return new Size(s1._width * value, s1._height * value);
        }

        //
        // Summary:
        //     Whether two Xamarin.Forms.Sizes have equal values.
        //
        // Parameters:
        //   s1:
        //     A Xamarin.Forms.Size to be compared.
        //
        //   s2:
        //     A Xamarin.Forms.Size to be compared.
        //
        // Returns:
        //     true if s1 and s2 have equal values for Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width.
        //
        // Remarks:
        //     Application developers should be aware that Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     are stored internally as doubles. Values with small differences may compare incorrectly
        //     due to internal rounding limitations.
        public static bool operator ==(Size s1, Size s2)
        {
            if (s1._width == s2._width)
            {
                return s1._height == s2._height;
            }

            return false;
        }

        //
        // Summary:
        //     Whether two Xamarin.Forms.Sizes have unequal values.
        //
        // Parameters:
        //   s1:
        //     The first Xamarin.Forms.Size to compare.
        //
        //   s2:
        //     The second Xamarin.Forms.Size to compare.
        //
        // Returns:
        //     true if s1 and s2 have unequal values for either Xamarin.Forms.Size.Height or
        //     Xamarin.Forms.Size.Width.
        //
        // Remarks:
        //     Application developers should be aware that Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     are stored internally as doubles. Values with small differences may compare incorrectly
        //     due to internal rounding limitations.
        public static bool operator !=(Size s1, Size s2)
        {
            if (s1._width == s2._width)
            {
                return s1._height != s2._height;
            }

            return true;
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Point based on a Xamarin.Forms.Size.
        //
        // Parameters:
        //   size:
        //     The Xamarin.Forms.Size to be converted to a Xamarin.Forms.Point.
        //
        // Returns:
        //     A Xamarin.Forms.Point whose Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y are
        //     equal to size's Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height, respectively.
        //
        // Remarks:
        //     To be added.
        public static explicit operator Point(Size size)
        {
            return new Point(size.Width, size.Height);
        }

        //
        // Summary:
        //     Whether thisXamarin.Forms.Size is equivalent to other.
        //
        // Parameters:
        //   other:
        //     The Xamarin.Forms.Size to which this is being compared.
        //
        // Returns:
        //     true if other's values are identical to thisXamarin.Forms.Size's Xamarin.Forms.Size.Height
        //     and Xamarin.Forms.Size.Width.
        //
        // Remarks:
        //     To be added.
        public bool Equals(Size other)
        {
            if (_width.Equals(other._width))
            {
                return _height.Equals(other._height);
            }

            return false;
        }

        //
        // Summary:
        //     Whether thisXamarin.Forms.Size is equivalent to obj.
        //
        // Parameters:
        //   obj:
        //     The object to which this is being compared.
        //
        // Returns:
        //     true if obj is a Xamarin.Forms.Size whose values are identical to thisXamarin.Forms.Size's
        //     Xamarin.Forms.Size.Height and Xamarin.Forms.Size.Width.
        //
        // Remarks:
        //     To be added.
        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj is Size)
            {
                return Equals((Size)obj);
            }

            return false;
        }

        //
        // Summary:
        //     Returns a hash value for the Xamarin.Forms.Size.
        //
        // Returns:
        //     A value intended for efficient insertion and lookup in hashtable-based data structures.
        //
        // Remarks:
        //     To be added.
        public override int GetHashCode()
        {
            return (_width.GetHashCode() * 397) ^ _height.GetHashCode();
        }

        //
        // Summary:
        //     Returns a human-readable representation of the Xamarin.Forms.Size.
        //
        // Returns:
        //     The format has the pattern "{Width={0} Height={1}}".
        //
        // Remarks:
        //     To be added.
        public override string ToString()
        {
            return $"{{Width={_width.ToString(CultureInfo.InvariantCulture)} Height={_height.ToString(CultureInfo.InvariantCulture)}}}";
        }

        public void Deconstruct(out double width, out double height)
        {
            width = Width;
            height = Height;
        }
    }
}