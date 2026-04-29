using System;

namespace DrawnUi.Views
{
    public struct LayoutOptions
    {
        private int _flags;

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that appears
        //     at the start of its parent and does not expand.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions Start = new LayoutOptions(LayoutAlignment.Start, expands: false);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that is centered
        //     and does not expand.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions Center = new LayoutOptions(LayoutAlignment.Center, expands: false);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that appears
        //     at the end of its parent and does not expand.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions End = new LayoutOptions(LayoutAlignment.End, expands: false);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions stucture that describes an element that has no
        //     padding around itself and does not expand.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions Fill = new LayoutOptions(LayoutAlignment.Fill, expands: false);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that appears
        //     at the start of its parent and expands.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions StartAndExpand = new LayoutOptions(LayoutAlignment.Start, expands: true);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that is centered
        //     and expands.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions CenterAndExpand = new LayoutOptions(LayoutAlignment.Center, expands: true);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions object that describes an element that appears at
        //     the end of its parent and expands.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions EndAndExpand = new LayoutOptions(LayoutAlignment.End, expands: true);

        //
        // Summary:
        //     A Xamarin.Forms.LayoutOptions structure that describes an element that has no
        //     padding around itself and expands.
        //
        // Remarks:
        //     To be added.
        public static readonly LayoutOptions FillAndExpand = new LayoutOptions(LayoutAlignment.Fill, expands: true);

        //
        // Summary:
        //     Gets or sets a value that indicates how an element will be aligned.
        //
        // Value:
        //     The Xamarin.Forms.LayoutAlignment flags that describe the behavior of an element.
        //
        // Remarks:
        //     To be added.
        public LayoutAlignment Alignment
        {
            get
            {
                return (LayoutAlignment)(_flags & 3);
            }
            set
            {
                _flags = ((_flags & -4) | (int)value);
            }
        }

        //
        // Summary:
        //     Gets or sets a value that indicates whether or not the element that is described
        //     by this Xamarin.Forms.LayoutOptions structure will occupy the largest space that
        //     its parent will give to it.
        //
        // Value:
        //     Whether or not the element that is described by this Xamarin.Forms.LayoutOptions
        //     structure will occupy the largest space that its parent will give it. true if
        //     the element will occupy the largest space the parent will give to it. false if
        //     the element will be as compact as it can be.
        //
        // Remarks:
        //     To be added.
        public bool Expands
        {
            get
            {
                return (_flags & 4) != 0;
            }
            set
            {
                _flags = ((_flags & 3) | (value ? 4 : 0));
            }
        }

        public LayoutOptions(LayoutAlignment alignment, bool expands)
        {
            if (alignment < LayoutAlignment.Start || alignment > LayoutAlignment.Fill)
            {
                throw new ArgumentOutOfRangeException();
            }

            _flags = ((int)alignment | (expands ? 4 : 0));
        }

        public static implicit operator LayoutAlignment(LayoutOptions options) => options.Alignment;
        public static implicit operator LayoutOptions(LayoutAlignment alignment) => new LayoutOptions(alignment, false);

        public static bool operator ==(LayoutOptions opts, LayoutAlignment align) => opts.Alignment == align;
        public static bool operator !=(LayoutOptions opts, LayoutAlignment align) => opts.Alignment != align;
        public static bool operator ==(LayoutAlignment align, LayoutOptions opts) => opts.Alignment == align;
        public static bool operator !=(LayoutAlignment align, LayoutOptions opts) => opts.Alignment != align;

        public override bool Equals(object obj)
        {
            if (obj is LayoutOptions other) return _flags == other._flags;
            if (obj is LayoutAlignment align) return Alignment == align;
            return false;
        }

        public override int GetHashCode() => _flags.GetHashCode();
    }
}