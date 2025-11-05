namespace DrawnUi.Infrastructure;

public class VisualTreeChain
{
    public VisualTreeChain(SkiaControl child)
    {
        Child = child;
        Nodes = new();
        NodeIndices = new();
        Transform = new();
    }

    public void AddNode(SkiaControl node)
    {
        Nodes.Add(node);
        NodeIndices.Add(node, Nodes.Count - 1);
    }

    /// <summary>
    /// The final node the tree leads to
    /// </summary>
    public SkiaControl Child { get; set; }

    /// <summary>
    /// Parents leading to the final node
    /// </summary>
    public List<SkiaControl> Nodes { get; set; }
    /// <summary>
    /// Perf cache for node indices
    /// </summary>
    public Dictionary<SkiaControl, int> NodeIndices { get; set; }

    /// <summary>
    /// Final transform of the chain
    /// </summary>
    public VisualTransform Transform { get; set; }
}