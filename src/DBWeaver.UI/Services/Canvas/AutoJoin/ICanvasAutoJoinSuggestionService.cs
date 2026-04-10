using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Canvas.AutoJoin;

internal interface ICanvasAutoJoinSuggestionService
{
    IReadOnlyList<JoinSuggestion> AnalyzeNewTable(string newTableFullName, IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzeAllTables(IReadOnlyCollection<NodeViewModel> nodes);
    IReadOnlyList<JoinSuggestion> AnalyzePair(NodeViewModel left, NodeViewModel right, IReadOnlyCollection<NodeViewModel> nodes);
}





