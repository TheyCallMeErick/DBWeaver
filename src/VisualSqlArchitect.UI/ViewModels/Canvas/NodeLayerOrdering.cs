using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Pure layer-ordering helpers for node Z-order operations.
/// Used by canvas interactions and unit tests.
/// </summary>
public static class NodeLayerOrdering
{
    public static List<NodeViewModel> OrderByZ(IEnumerable<NodeViewModel> nodes) =>
        [.. nodes.OrderBy(n => n.ZOrder)];

    public static List<NodeViewModel> BringToFront(IEnumerable<NodeViewModel> nodes)
    {
        List<NodeViewModel> ordered = OrderByZ(nodes);
        HashSet<NodeViewModel> selected = ordered.Where(n => n.IsSelected).ToHashSet();
        var back = ordered.Where(n => !selected.Contains(n)).ToList();
        var front = ordered.Where(n => selected.Contains(n)).ToList();
        return [.. back, .. front];
    }

    public static List<NodeViewModel> SendToBack(IEnumerable<NodeViewModel> nodes)
    {
        List<NodeViewModel> ordered = OrderByZ(nodes);
        HashSet<NodeViewModel> selected = ordered.Where(n => n.IsSelected).ToHashSet();
        var back = ordered.Where(n => selected.Contains(n)).ToList();
        var front = ordered.Where(n => !selected.Contains(n)).ToList();
        return [.. back, .. front];
    }

    public static List<NodeViewModel> BringForward(IEnumerable<NodeViewModel> nodes)
    {
        List<NodeViewModel> ordered = OrderByZ(nodes);
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

    public static List<NodeViewModel> SendBackward(IEnumerable<NodeViewModel> nodes)
    {
        List<NodeViewModel> ordered = OrderByZ(nodes);
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

    public static Dictionary<NodeViewModel, int> BuildNormalizedMap(IEnumerable<NodeViewModel> ordered)
    {
        Dictionary<NodeViewModel, int> map = [];
        int z = 0;
        foreach (NodeViewModel n in ordered)
            map[n] = z++;
        return map;
    }
}
