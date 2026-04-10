using Avalonia;
using DBWeaver.Nodes;

namespace DBWeaver.UI.ViewModels.Canvas;

public interface INodeManager
{
    NodeViewModel SpawnNode(NodeDefinition def, Point pos);

    NodeViewModel SpawnTableNode(
        string table,
        IEnumerable<(string n, PinDataType t)> cols,
        Point pos);

    void DeleteSelected();

    void CleanupOrphans();

    void SpawnDemoNodes(UndoRedoStack undoRedo);
}
