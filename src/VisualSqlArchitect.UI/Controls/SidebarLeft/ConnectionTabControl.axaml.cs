using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using VisualSqlArchitect.UI.Services.Localization;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public partial class ConnectionTabControl : UserControl
{
    public ICommand? ProfileActionCommand { get; private set; }
    private ConnectionManagerViewModel? _vm;

    public ConnectionTabControl()
    {
        InitializeComponent();
        this.DataContextChanged += (_, _) =>
        {
            if (DataContext is ConnectionManagerViewModel vm)
            {
                _vm = vm;

                // Create command to handle connect/disconnect
                ProfileActionCommand = new RelayCommand<ConnectionProfile?>(profile =>
                {
                    if (profile != null && vm != null)
                    {
                        // Check if this profile is already active
                        if (vm.ActiveProfileId == profile.Id)
                        {
                            // Disconnect
                            vm.DisconnectCommand.Execute(null);
                        }
                        else
                        {
                            // Connect
                            vm.SelectedProfile = profile;
                            if (vm.ConnectCommand.CanExecute(null))
                                vm.ConnectCommand.Execute(null);
                        }
                        UpdateButtonStates();
                    }
                });

                // Update button states when connection changes or connecting status changes
                vm.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(ConnectionManagerViewModel.ActiveProfileId) ||
                        e.PropertyName == nameof(ConnectionManagerViewModel.IsConnecting))
                    {
                        UpdateButtonStates();
                    }
                };

                UpdateButtonStates();
            }
        };
    }

    private void UpdateButtonStates()
    {
        if (_vm == null) return;

        // Find the ItemsControl with profiles
        var itemsControl = this.FindControl<ItemsControl>("ProfilesItemsControl");
        if (itemsControl?.ItemsPanel == null) return;

        var panel = itemsControl.ItemsPanel as Panel;
        if (panel == null) return;

        foreach (var child in panel.Children)
        {
            if (child is Border border && border.Child is Grid grid)
            {
                // Try to get the profile from the data context
                if (border.DataContext is ConnectionProfile profile)
                {
                    var isActive = profile.Id == _vm.ActiveProfileId;

                    // Find button and status dot in this grid
                    var button = grid.Children.OfType<Button>().FirstOrDefault();
                    var dot = grid.Children.OfType<Ellipse>().FirstOrDefault();

                    if (button != null)
                    {
                        button.IsEnabled = !_vm.IsConnecting;
                        button.Content = isActive
                            ? LocalizationService.Instance["connection.disconnect"]
                            : LocalizationService.Instance["connection.connect"];
                        if (isActive)
                        {
                            button.Background = new SolidColorBrush(Color.Parse("#D63031")); // Red for disconnect
                            button.Foreground = new SolidColorBrush(Color.Parse("#FF6B6B")); // Light red
                        }
                        else
                        {
                            button.Background = new SolidColorBrush(Color.Parse("#4C1F6B")); // Purple for connect
                            button.Foreground = new SolidColorBrush(Color.Parse("#C084FC")); // Light purple
                        }
                    }

                    if (dot != null)
                    {
                        dot.Fill = new SolidColorBrush(Color.Parse(isActive ? "#10B981" : "#6B7280")); // Green if active
                    }
                }
            }
        }
    }
}
