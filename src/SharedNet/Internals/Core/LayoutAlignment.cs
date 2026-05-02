using System;

namespace DrawnUi.Views
{
    [Flags]
    public enum LayoutAlignment
    {
        //
        // Summary:
        //     The start of an alignment. Usually the Top or Left.
        Start = 0x0,
        //
        // Summary:
        //     The center of an alignment.
        Center = 0x1,
        //
        // Summary:
        //     The end of an alignment. Usually the Bottom or Right.
        End = 0x2,
        //
        // Summary:
        //     Fill the entire area if possible.
        Fill = 0x3
    }
}