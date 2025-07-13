using System.Runtime.CompilerServices;

namespace DrawnUi.Draw;

public abstract class StackLayoutStructure
{
    protected readonly SkiaLayout _layout;

    public long ChildrenCount;

    public StackLayoutStructure(SkiaLayout layout)
    {
        _layout = layout;
    }

   

    public virtual IEnumerable<SkiaControl> EnumerateViewsForMeasurement()
    {
        SkiaControl template = null;
        IReadOnlyList<SkiaControl> views = null;

        bool useOneTemplate = _layout.IsTemplated && //ItemSizingStrategy == ItemSizingStrategy.MeasureFirstItem &&
                              _layout.RecyclingTemplate != RecyclingTemplate.Disabled;

        if (_layout.IsTemplated)
        {
            if (useOneTemplate)
            {
                template = _layout.ChildrenFactory.GetTemplateInstance();
            }
            ChildrenCount = _layout.ChildrenFactory.GetChildrenCount();
        }
        else
        {
            views = _layout.GetUnorderedSubviews();
            ChildrenCount = views.Count;
        }

        var cellsToRelease = new List<SkiaControl>();

        try
        {
            for (int index = 0; index < ChildrenCount; index++)
            {
                SkiaControl child = null;
                if (_layout.IsTemplated)
                {
                    child = _layout.ChildrenFactory.GetViewForIndex(index, template, 0, useOneTemplate);
                }
                else
                {
                    child = views[index];
                }

                if (child == null)
                    continue;

                if (!useOneTemplate && _layout.IsTemplated)
                {
                    cellsToRelease.Add(child);
                }
                yield return child;
            }
        }
        finally
        {

            if (useOneTemplate)
            {
                _layout.ChildrenFactory.ReleaseTemplateInstance(template);
            }
            else
            if (_layout.IsTemplated)
            {
                foreach (var view in cellsToRelease)
                {
                    _layout.ChildrenFactory.ReleaseViewInUse(view.ContextIndex, view);
                }
            }
        }

    }

    /// <summary>
    /// Will measure children and build appropriate stack structure for the layout
    /// </summary>
    public abstract ScaledSize Build(SKRect rectForChildrenPixels, float scale);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected virtual ScaledSize MeasureCell(SKRect destination,
        ControlInStack cell, SkiaControl child,
        float scale)
    {
        cell.Area = destination;

        var measured = _layout.MeasureChild(child, cell.Area.Width, cell.Area.Height, scale);

        cell.Measured = measured;
        cell.WasMeasured = true;

        return measured;
    }

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //protected virtual ScaledSize LayoutCell(
    //    ControlInStack cell, SkiaControl child,
    //    float scale)
    //{
    //    var measured = cell.Measured;

    //    _layout.LayoutCell(measured, cell, child, scale);

    //    return measured;
    //}

    //[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //protected virtual void LayoutCell(
    //    ScaledSize measured,
    //    ControlInStack cell,
    //    SkiaControl child,
    //    float scale)
    //{
    //    if (!measured.IsEmpty)
    //    {
    //        child.Arrange(cell.Area, measured.Units.Width, measured.Units.Height, scale);

    //        var maybeArranged = child.Destination;

    //        var arranged =
    //            new SKRect(cell.Area.Left, cell.Area.Top,
    //                cell.Area.Left + cell.Measured.Pixels.Width,
    //                cell.Area.Top + cell.Measured.Pixels.Height);

    //        if (float.IsNormal(maybeArranged.Height))
    //        {
    //            arranged.Top = maybeArranged.Top;
    //            arranged.Bottom = maybeArranged.Bottom;
    //        }
    //        if (float.IsNormal(maybeArranged.Width))
    //        {
    //            arranged.Left = maybeArranged.Left;
    //            arranged.Right = maybeArranged.Right;
    //        }

    //        cell.Destination = arranged;

    //        cell.Layout = arranged;
    //    }
    //}

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual float GetSpacingForIndex(int forIndex, float scale)
    {
        var spacing = 0.0f;
        if (forIndex > 0)
        {
            spacing = (float)Math.Round(_layout.Spacing * scale);
        }
        return spacing;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual ControlInStack CreateWrapper(int i, SkiaControl control)
    {
        var add = new ControlInStack
        {
            ControlIndex = i
        };
        if (control != null)
        {
            add.ZIndex = control.ZIndex;
            add.View = control;
        }

        return add;
    }

}
