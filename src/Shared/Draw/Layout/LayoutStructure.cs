namespace DrawnUi.Draw;

public class LayoutStructure : DynamicGrid<ControlInStack>
{
    public LayoutStructure()
    {

    }

    /// <summary>
    /// Returns a new instance of LayoutStructure with the same items.
    /// This performs a shallow copy of the existing structure.
    /// </summary>
    public LayoutStructure Clone()
    {
        var clone = new LayoutStructure();
        foreach (var kvp in grid)
        {
            var item = kvp.Value;
            clone.Add(item, item.Column, item.Row);
        }
        return clone;
    }

    public ControlInStack GetForIndex(int index)
    {
        return grid.Values.FirstOrDefault(x => x.ControlIndex == index);
    }

    public LayoutStructure(List<List<ControlInStack>> grid)
    {
        int row = 0;
        foreach (var line in grid)
        {
            var col = 0;
            foreach (var controlInStack in line)
            {
                controlInStack.Column = col;
                controlInStack.Row = row;

                Add(controlInStack, col, row);
                col++;
            }
            row++;
        }
    }

    public void Append(List<List<ControlInStack>> grid)
    {
        int row = MaxRows;
        foreach (var line in grid)
        {
            var col = 0;
            foreach (var controlInStack in line)
            {
                controlInStack.Column = col;
                controlInStack.Row = row;

                Add(controlInStack, col, row);
                col++;
            }
            row++;
        }
    }

}
