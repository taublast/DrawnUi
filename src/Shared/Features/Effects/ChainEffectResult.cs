namespace DrawnUi.Draw;

public struct ChainEffectResult
{
    /// <summary>
    /// Set this to true if you drawn the control of false if you just rendered something else
    /// </summary>
    public bool DrawnControl { get; set; }


    public static ChainEffectResult Create(bool drawnControl)
    {
        return new ChainEffectResult
        {
            DrawnControl = drawnControl,
        };
    }

    public static ChainEffectResult Default
    {
        get
        {
            return new ChainEffectResult();
        }
    }
}