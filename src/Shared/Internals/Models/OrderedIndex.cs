namespace DrawnUi.Infrastructure.Models;

public record OrderedIndex(int Index, bool? Animate)
{
    public int Index { get; set; } = Index;

    public bool? Animate { get; set; } = Animate;
}
