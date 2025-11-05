namespace DrawnUi.Draw;

public class ContainsPointResult : PointIsInsideResult
{
    public int Index { get; set; }


    public SKPoint Unmodified { get; set; }

    public static ContainsPointResult NotFound()
    {
        return new ContainsPointResult()
        {
            Index = -1
        };
    }
}
