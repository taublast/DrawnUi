namespace DrawnUi.Draw;

public struct MeasureRequest
{
    public static MeasureRequest Empty = new MeasureRequest();

    public MeasureRequest(float width, float height, float scale)
    {
        WidthRequest = width;
        HeightRequest = height;
        Scale = scale;
    }

    public MeasureRequest(SKRect rectForChildrenPixels, float width, float height, float scale)
    {
        Destination = rectForChildrenPixels;
        WidthRequest = width;
        HeightRequest = height;
        Scale = scale;
    }

    public bool IsSame { get; set; }
    public SKRect Destination { get; set; }
    public float WidthRequest { get; set; }
    public float HeightRequest { get; set; }
    public float Scale { get; set; }
}