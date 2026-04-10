using DBWeaver.UI.ViewModels;
using DBWeaver.CanvasKit;

namespace DBWeaver.UI.ViewModels.Canvas;

/// <summary>
/// Pure layer-ordering helpers for node Z-order operations.
/// Used by canvas interactions and unit tests.
/// </summary>
public static class NodeLayerOrdering
{
    public static List<NodeViewModel> OrderByZ(IEnumerable<NodeViewModel> nodes) =>
        CanvasLayerOrdering.OrderByZ(nodes);

    public static List<NodeViewModel> BringToFront(IEnumerable<NodeViewModel> nodes)
        => CanvasLayerOrdering.BringToFront(nodes);

    public static List<NodeViewModel> SendToBack(IEnumerable<NodeViewModel> nodes)
        => CanvasLayerOrdering.SendToBack(nodes);

    public static List<NodeViewModel> BringForward(IEnumerable<NodeViewModel> nodes)
        => CanvasLayerOrdering.BringForward(nodes);

    public static List<NodeViewModel> SendBackward(IEnumerable<NodeViewModel> nodes)
        => CanvasLayerOrdering.SendBackward(nodes);

    public static Dictionary<NodeViewModel, int> BuildNormalizedMap(IEnumerable<NodeViewModel> ordered)
        => CanvasLayerOrdering.BuildNormalizedMap(ordered);
}
