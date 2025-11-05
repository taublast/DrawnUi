namespace DrawnUi.Draw;

public interface ICanBeUpdated
{
    /// <summary>
    /// Force redrawing, without invalidating the measured size
    /// </summary>
    /// <returns></returns>
    void Update();

}

public interface ICanBeUpdatedWithContext : ICanBeUpdated
{
    object BindingContext { get; set; }

}