namespace DBWeaver.CanvasKit;

public interface ICanvasLayerNode
{
    bool IsSelected { get; }
    int ZOrder { get; }
}

public static class CanvasLayerOrdering
{
    public static List<TNode> OrderByZ<TNode>(IEnumerable<TNode> nodes)
        where TNode : ICanvasLayerNode =>
        [.. nodes.OrderBy(n => n.ZOrder)];

    public static List<TNode> BringToFront<TNode>(IEnumerable<TNode> nodes)
        where TNode : ICanvasLayerNode
    {
        List<TNode> ordered = OrderByZ(nodes);
        HashSet<TNode> selected = ordered.Where(n => n.IsSelected).ToHashSet();
        var back = ordered.Where(n => !selected.Contains(n)).ToList();
        var front = ordered.Where(n => selected.Contains(n)).ToList();
        return [.. back, .. front];
    }

    public static List<TNode> SendToBack<TNode>(IEnumerable<TNode> nodes)
        where TNode : ICanvasLayerNode
    {
        List<TNode> ordered = OrderByZ(nodes);
        HashSet<TNode> selected = ordered.Where(n => n.IsSelected).ToHashSet();
        var back = ordered.Where(n => selected.Contains(n)).ToList();
        var front = ordered.Where(n => !selected.Contains(n)).ToList();
        return [.. back, .. front];
    }

    public static List<TNode> BringForward<TNode>(IEnumerable<TNode> nodes)
        where TNode : ICanvasLayerNode
    {
        List<TNode> ordered = OrderByZ(nodes);
        for (int i = ordered.Count - 2; i >= 0; i--)
        {
            if (!ordered[i].IsSelected)
                continue;
            if (ordered[i + 1].IsSelected)
                continue;
            (ordered[i], ordered[i + 1]) = (ordered[i + 1], ordered[i]);
        }
        return ordered;
    }

    public static List<TNode> SendBackward<TNode>(IEnumerable<TNode> nodes)
        where TNode : ICanvasLayerNode
    {
        List<TNode> ordered = OrderByZ(nodes);
        for (int i = 1; i < ordered.Count; i++)
        {
            if (!ordered[i].IsSelected)
                continue;
            if (ordered[i - 1].IsSelected)
                continue;
            (ordered[i - 1], ordered[i]) = (ordered[i], ordered[i - 1]);
        }
        return ordered;
    }

    public static Dictionary<TNode, int> BuildNormalizedMap<TNode>(IEnumerable<TNode> ordered)
        where TNode : ICanvasLayerNode
    {
        Dictionary<TNode, int> map = [];
        int z = 0;
        foreach (TNode node in ordered)
            map[node] = z++;
        return map;
    }
}
