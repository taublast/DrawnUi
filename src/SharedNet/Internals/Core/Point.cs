using System;
using System.Diagnostics;
using System.Globalization;

namespace DrawnUi.Views
{
    [DebuggerDisplay("X={X}, Y={Y}")]
    public struct Point
    {
        //
        // Summary:
        //     The Xamarin.Forms.Point at {0,0}.
        //
        // Remarks:
        //     To be added.
        public static Point Zero;

        //
        // Summary:
        //     Location along the horizontal axis.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double X
        {
            get;
            set;
        }

        //
        // Summary:
        //     Location along the vertical axis.
        //
        // Value:
        //     To be added.
        //
        // Remarks:
        //     To be added.
        public double Y
        {
            get;
            set;
        }

        //
        // Summary:
        //     Whether both X and Y are 0.
        //
        // Value:
        //     true if both Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y are 0.0.
        //
        // Remarks:
        //     To be added.
        public bool IsEmpty
        {
            get
            {
                if (X == 0.0)
                {
                    return Y == 0.0;
                }

                return false;
            }
        }

        //
        // Summary:
        //     A human-readable representation of the Xamarin.Forms.Point.
        //
        // Returns:
        //     The string is formatted as "{{X={0} Y={1}}}".
        //
        // Remarks:
        //     To be added.
        public override string ToString()
        {
            return $"{{X={X.ToString(CultureInfo.InvariantCulture)} Y={Y.ToString(CultureInfo.InvariantCulture)}}}";
        }

        public Point(double x, double y)
        {
            this = default(Point);
            X = x;
            Y = y;
        }

        public Point(Size sz)
        {
            this = default(Point);
            X = sz.Width;
            Y = sz.Height;
        }

        //
        // Summary:
        //     Returns true if the X and Y values of this are exactly equal to those in the
        //     argument.
        //
        // Parameters:
        //   o:
        //     Another Xamarin.Forms.Point.
        //
        // Returns:
        //     true if the X and Y values are equal to those in o. Returns false if o is not
        //     a Xamarin.Forms.Point.
        //
        // Remarks:
        //     The Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y values of the Xamarin.Forms.Point
        //     are stored as doubles. Developers should be aware of the precision limits and
        //     issues that can arise when comparing floating-point values. In some circumstances,
        //     developers should consider the possibility of measuring approximate equality
        //     using the (considerably slower) Xamarin.Forms.Point.Distance(Xamarin.Forms.Point)
        //     method.
        public override bool Equals(object o)
        {
            if (!(o is Point))
            {
                return false;
            }

            return this == (Point)o;
        }

        //
        // Summary:
        //     Returns a hash value for the Xamarin.Forms.Point.
        //
        // Returns:
        //     A value intended for efficient insertion and lookup in hashtable-based data structures.
        //
        // Remarks:
        //     To be added.
        public override int GetHashCode()
        {
            return X.GetHashCode() ^ (Y.GetHashCode() * 397);
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Point that translates the current Xamarin.Forms.Point
        //     by dx and dy.
        //
        // Parameters:
        //   dx:
        //     The amount to add along the X axis.
        //
        //   dy:
        //     The amount to add along the Y axis.
        //
        // Returns:
        //     A new Xamarin.Forms.Point at [this.X + dx, this.Y + dy].
        //
        // Remarks:
        //     To be added.
        public Point Offset(double dx, double dy)
        {
            Point result = this;
            result.X += dx;
            result.Y += dy;
            return result;
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Point whose Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y
        //     have been rounded to the nearest integral value.
        //
        // Returns:
        //     A new Xamarin.Forms.Point whose Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y
        //     have been rounded to the nearest integral value, per the behavior of Math.Round(Double).
        //
        // Remarks:
        //     To be added.
        public Point Round()
        {
            return new Point(Math.Round(X), Math.Round(Y));
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Size whose Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     and equivalent to the pt's Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y properties.
        //
        // Parameters:
        //   pt:
        //     The Xamarin.Forms.Point to be translated as a Xamarin.Forms.Size.
        //
        // Returns:
        //     A new Xamarin.Forms.Size based on the pt.
        //
        // Remarks:
        //     To be added.
        public static explicit operator Size(Point pt)
        {
            return new Size(pt.X, pt.Y);
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Point by adding a Xamarin.Forms.Size to a Xamarin.Forms.Point.
        //
        // Parameters:
        //   pt:
        //     The Xamarin.Forms.Point to which sz is being added.
        //
        //   sz:
        //     The values to add to pt.
        //
        // Returns:
        //     A new Xamarin.Forms.Point at [pt.X + sz.Width, pt.Y + sz.Height].
        //
        // Remarks:
        //     To be added.
        public static Point operator +(Point pt, Size sz)
        {
            return new Point(pt.X + sz.Width, pt.Y + sz.Height);
        }

        //
        // Summary:
        //     Returns a new Xamarin.Forms.Point by subtracting a Xamarin.Forms.Size from a
        //     Xamarin.Forms.Point.
        //
        // Parameters:
        //   pt:
        //     The Xamarin.Forms.Point from which sz is to be subtracted.
        //
        //   sz:
        //     The Xamarin.Forms.Size whose Xamarin.Forms.Size.Width and Xamarin.Forms.Size.Height
        //     will be subtracted from pt's Xamarin.Forms.Point.X and Xamarin.Forms.Point.Y.
        //
        // Returns:
        //     A new Xamarin.Forms.Point at [pt.X - sz.Width, pt.Y - sz.Height].
        //
        // Remarks:
        //     To be added.
        public static Point operator -(Point pt, Size sz)
        {
            return new Point(pt.X - sz.Width, pt.Y - sz.Height);
        }

        //
        // Summary:
        //     Whether the two Xamarin.Forms.Points are equal.
        //
        // Parameters:
        //   ptA:
        //     The first point to compare.
        //
        //   ptB:
        //     The second point to compare.
        //
        // Returns:
        //     true if the two Xamarin.Forms.Points have equal values.
        //
        // Remarks:
        //     To be added.
        public static bool operator ==(Point ptA, Point ptB)
        {
            if (ptA.X == ptB.X)
            {
                return ptA.Y == ptB.Y;
            }

            return false;
        }

        //
        // Summary:
        //     Whether two points are not equal.
        //
        // Parameters:
        //   ptA:
        //     The first point to compare.
        //
        //   ptB:
        //     The second point to compare.
        //
        // Returns:
        //     true if pt_a and pt_b do not have equivalent X and Y values.
        //
        // Remarks:
        //     To be added.
        public static bool operator !=(Point ptA, Point ptB)
        {
            if (ptA.X == ptB.X)
            {
                return ptA.Y != ptB.Y;
            }

            return true;
        }

        //
        // Summary:
        //     Calculates the distance between two points.
        //
        // Parameters:
        //   other:
        //     The Xamarin.Forms.Point to which the distance is calculated.
        //
        // Returns:
        //     The distance between this and the other.
        //
        // Remarks:
        //     To be added.
        public double Distance(Point other)
        {
            return Math.Sqrt(Math.Pow(X - other.X, 2.0) + Math.Pow(Y - other.Y, 2.0));
        }

        public void Deconstruct(out double x, out double y)
        {
            x = X;
            y = Y;
        }
    }
}