namespace DBWeaver.UI.ViewModels.Canvas;

public interface ISelectionManager
{
    RelayCommand SelectAllCommand { get; }
    RelayCommand DeselectAllCommand { get; }
    RelayCommand AlignLeftCommand { get; }
    RelayCommand AlignRightCommand { get; }
    RelayCommand AlignTopCommand { get; }
    RelayCommand AlignBottomCommand { get; }
    RelayCommand AlignCenterHCommand { get; }
    RelayCommand AlignCenterVCommand { get; }
    RelayCommand DistributeHCommand { get; }
    RelayCommand DistributeVCommand { get; }

    void SelectAll();

    void DeselectAll();

    void SelectNode(NodeViewModel node, bool add = false);

    List<NodeViewModel> SelectedNodes();
}
