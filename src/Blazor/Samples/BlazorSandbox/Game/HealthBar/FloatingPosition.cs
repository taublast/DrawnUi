namespace SpaceShooter.Game;

public class FloatingPosition
{
    /// <summary>
    /// Not changes with Value
    /// </summary>
    public bool IsFixed { get; set; }

    /// <summary>
    /// Moves along with Value, how much stick to the previous point
    /// </summary>
    public double Stick { get; set; }

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double Base { get; set; }

    /// <summary>
    /// 0.0 - 1.0
    /// </summary>
    public double Value { get; set; }

}