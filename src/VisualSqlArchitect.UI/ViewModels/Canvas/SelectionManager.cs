using System.Collections.ObjectModel;
using System.Windows.Input;
using VisualSqlArchitect.UI.ViewModels.UndoRedo.Commands;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

/// <summary>
/// Manages multi-selection and node alignment operations on the canvas.
/// Handles user interactions related to selecting/deselecting nodes and aligning them.
/// </summary>
public sealed class SelectionManager
{
    private readonly ObservableCollection<NodeViewModel> _nodes;
    private readonly PropertyPanelViewModel _propertyPanel;
    private readonly UndoRedoStack _undoRedo;

    public RelayCommand SelectAllCommand { get; }
    public RelayCommand DeselectAllCommand { get; }
    public RelayCommand AlignLeftCommand { get; }
    public RelayCommand AlignRightCommand { get; }
    public RelayCommand AlignTopCommand { get; }
    public RelayCommand AlignBottomCommand { get; }
    public RelayCommand AlignCenterHCommand { get; }
    public RelayCommand AlignCenterVCommand { get; }
    public RelayCommand DistributeHCommand { get; }
    public RelayCommand DistributeVCommand { get; }

    public SelectionManager(
        ObservableCollection<NodeViewModel> nodes,
        PropertyPanelViewModel propertyPanel,
        UndoRedoStack undoRedo
    )
    {
        _nodes = nodes;
        _propertyPanel = propertyPanel;
        _undoRedo = undoRedo;

        SelectAllCommand = new RelayCommand(SelectAll);
        DeselectAllCommand = new RelayCommand(DeselectAll);

        AlignLeftCommand = new RelayCommand(
            () => AlignNodes(AlignMode.Left),
            () => SelectedNodes().Count >= 2
        );
        AlignRightCommand = new RelayCommand(
            () => AlignNodes(AlignMode.Right),
            () => SelectedNodes().Count >= 2
        );
        AlignTopCommand = new RelayCommand(
            () => AlignNodes(AlignMode.Top),
            () => SelectedNodes().Count >= 2
        );
        AlignBottomCommand = new RelayCommand(
            () => AlignNodes(AlignMode.Bottom),
            () => SelectedNodes().Count >= 2
        );
        AlignCenterHCommand = new RelayCommand(
            () => AlignNodes(AlignMode.CenterH),
            () => SelectedNodes().Count >= 2
        );
        AlignCenterVCommand = new RelayCommand(
            () => AlignNodes(AlignMode.CenterV),
            () => SelectedNodes().Count >= 2
        );
        DistributeHCommand = new RelayCommand(
            () => AlignNodes(AlignMode.DistributeH),
            () => SelectedNodes().Count >= 3
        );
        DistributeVCommand = new RelayCommand(
            () => AlignNodes(AlignMode.DistributeV),
            () => SelectedNodes().Count >= 3
        );
    }

    public void SelectAll()
    {
        foreach (NodeViewModel n in _nodes)
            n.IsSelected = true;
    }

    public void DeselectAll()
    {
        foreach (NodeViewModel n in _nodes)
            n.IsSelected = false;
        _propertyPanel.Clear();
    }

    public void SelectNode(NodeViewModel node, bool add = false)
    {
        if (!add)
            DeselectAll();
        node.IsSelected = true;

        List<NodeViewModel> sel = SelectedNodes();
        if (sel.Count == 1)
            _propertyPanel.ShowNode(sel[0]);
        else if (sel.Count > 1)
            _propertyPanel.ShowMultiSelection(sel);
    }

    public List<NodeViewModel> SelectedNodes() => [.. _nodes.Where(n => n.IsSelected)];

    public void AlignNodes(AlignMode mode)
    {
        List<NodeViewModel> sel = SelectedNodes();
        if (sel.Count < 2)
            return;
        _undoRedo.Execute(new AlignNodesCommand(sel, mode));
    }

    /// <summary>
    /// Notifies alignment command buttons to refresh their CanExecute state.
    /// Call when selection changes.
    /// </summary>
    public void NotifyAlignmentCommandsStateChanged()
    {
        AlignLeftCommand.NotifyCanExecuteChanged();
        AlignRightCommand.NotifyCanExecuteChanged();
        AlignTopCommand.NotifyCanExecuteChanged();
        AlignBottomCommand.NotifyCanExecuteChanged();
        AlignCenterHCommand.NotifyCanExecuteChanged();
        AlignCenterVCommand.NotifyCanExecuteChanged();
        DistributeHCommand.NotifyCanExecuteChanged();
        DistributeVCommand.NotifyCanExecuteChanged();
    }
}
