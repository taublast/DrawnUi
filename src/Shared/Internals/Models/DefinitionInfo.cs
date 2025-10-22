namespace DrawnUi.Models;

public class DefinitionInfo
{
    // now using DefaultRowDefinition now and DefaultColumnDefinition
    //public static DefinitionInfo CreateDefault()
    //{
    //    return new DefinitionInfo(new GridLength(1, GridUnitType.Auto));
    //}

    readonly GridLength _gridLength;
    public double Size { get; set; }

    public void Update(double size)
    {
        if (size > Size)
        {
            Size = size;
        }
    }

    public bool IsAuto => _gridLength.IsAuto;
    public bool IsStar => _gridLength.IsStar;
    public bool IsAbsolute => _gridLength.IsAbsolute;

    public GridLength GridLength => _gridLength;

    public DefinitionInfo(GridLength gridLength)
    {
        if (gridLength.IsAbsolute)
        {
            Size = gridLength.Value;
        }

        _gridLength = gridLength;
    }
}