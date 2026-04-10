using Avalonia;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;
using DBWeaver.Metadata;
using DBWeaver.Nodes;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinApplicationService
{
    bool TryApplySuggestion(
        JoinSuggestion suggestion,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins);

    bool TryCreateManualJoin(
        NodeViewModel leftTable,
        string leftColumn,
        NodeViewModel rightTable,
        string rightColumn,
        string? joinType,
        IReadOnlyCollection<NodeViewModel> nodes,
        IReadOnlyCollection<ConnectionViewModel> connections,
        Func<NodeDefinition, Point, NodeViewModel> spawnNode,
        Action<PinViewModel, PinViewModel> connectPins);
}





