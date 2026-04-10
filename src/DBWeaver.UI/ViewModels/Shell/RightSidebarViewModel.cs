namespace DBWeaver.UI.ViewModels;

/// <summary>
/// Shell-level right sidebar host for property editing surface.
/// </summary>
public sealed class RightSidebarViewModel : ViewModelBase
{
    private PropertyPanelViewModel? _propertyPanel;
    private bool _isVisible;

    public PropertyPanelViewModel? PropertyPanel
    {
        get => _propertyPanel;
        private set => Set(ref _propertyPanel, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    public void BindPropertyPanel(PropertyPanelViewModel? panel)
    {
        PropertyPanel = panel;
    }

    public void SyncVisibility(bool show)
    {
        IsVisible = show && PropertyPanel is not null;
    }
}
