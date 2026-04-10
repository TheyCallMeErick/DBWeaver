namespace DBWeaver.UI.ViewModels;

/// <summary>
/// Shell-level left sidebar host, decoupled from concrete canvas implementations.
/// </summary>
public sealed class LeftSidebarViewModel : ViewModelBase
{
    private SidebarViewModel? _querySidebar;
    private bool _isVisible;

    public SidebarViewModel? QuerySidebar
    {
        get => _querySidebar;
        private set => Set(ref _querySidebar, value);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => Set(ref _isVisible, value);
    }

    public void BindQuerySidebar(SidebarViewModel? sidebar)
    {
        QuerySidebar = sidebar;
    }

    public void SyncVisibility(bool show)
    {
        IsVisible = show && QuerySidebar is not null;
    }
}
