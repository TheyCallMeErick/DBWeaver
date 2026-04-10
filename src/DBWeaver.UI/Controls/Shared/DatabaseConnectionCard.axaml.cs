using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using DBWeaver.UI.Services.ConnectionManager.Models;

namespace DBWeaver.UI.Controls.Shared;

public partial class DatabaseConnectionCard : UserControl
{
    public static readonly StyledProperty<string?> ConnectionNameProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(ConnectionName));

    public static readonly StyledProperty<string?> ConnectionSubtitleProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(ConnectionSubtitle));

    public static readonly StyledProperty<ConnectionProfile?> SelectedConnectionProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ConnectionProfile?>(nameof(SelectedConnection));

    public static readonly StyledProperty<string?> SelectedSchemaProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(SelectedSchema));

    public static readonly StyledProperty<IEnumerable?> AvailableConnectionsProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, IEnumerable?>(nameof(AvailableConnections));

    public static readonly StyledProperty<IEnumerable?> AvailableSchemasProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, IEnumerable?>(nameof(AvailableSchemas));

    public static readonly StyledProperty<string?> MetadataSummaryProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(MetadataSummary));

    public static readonly StyledProperty<string?> MetadataDetailsProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, string?>(nameof(MetadataDetails));

    public static readonly StyledProperty<int?> LatencyMsProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, int?>(nameof(LatencyMs));

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, bool>(nameof(IsConnected));

    public static readonly StyledProperty<bool> IsReloadingProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, bool>(nameof(IsReloading));

    public static readonly StyledProperty<ICommand?> DisconnectCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(DisconnectCommand));

    public static readonly StyledProperty<ICommand?> SwitchConnectionCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(SwitchConnectionCommand));

    public static readonly StyledProperty<ICommand?> SwitchSchemaCommandProperty =
        AvaloniaProperty.Register<DatabaseConnectionCard, ICommand?>(nameof(SwitchSchemaCommand));

    public string? ConnectionName
    {
        get => GetValue(ConnectionNameProperty);
        set => SetValue(ConnectionNameProperty, value);
    }

    public string? ConnectionSubtitle
    {
        get => GetValue(ConnectionSubtitleProperty);
        set => SetValue(ConnectionSubtitleProperty, value);
    }

    public ConnectionProfile? SelectedConnection
    {
        get => GetValue(SelectedConnectionProperty);
        set => SetValue(SelectedConnectionProperty, value);
    }

    public string? SelectedSchema
    {
        get => GetValue(SelectedSchemaProperty);
        set => SetValue(SelectedSchemaProperty, value);
    }

    public IEnumerable? AvailableConnections
    {
        get => GetValue(AvailableConnectionsProperty);
        set => SetValue(AvailableConnectionsProperty, value);
    }

    public IEnumerable? AvailableSchemas
    {
        get => GetValue(AvailableSchemasProperty);
        set => SetValue(AvailableSchemasProperty, value);
    }

    public string? MetadataSummary
    {
        get => GetValue(MetadataSummaryProperty);
        set => SetValue(MetadataSummaryProperty, value);
    }

    public string? MetadataDetails
    {
        get => GetValue(MetadataDetailsProperty);
        set => SetValue(MetadataDetailsProperty, value);
    }

    public int? LatencyMs
    {
        get => GetValue(LatencyMsProperty);
        set => SetValue(LatencyMsProperty, value);
    }

    public bool IsConnected
    {
        get => GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    public bool IsReloading
    {
        get => GetValue(IsReloadingProperty);
        set => SetValue(IsReloadingProperty, value);
    }

    public ICommand? DisconnectCommand
    {
        get => GetValue(DisconnectCommandProperty);
        set => SetValue(DisconnectCommandProperty, value);
    }

    public ICommand? SwitchConnectionCommand
    {
        get => GetValue(SwitchConnectionCommandProperty);
        set => SetValue(SwitchConnectionCommandProperty, value);
    }

    public ICommand? SwitchSchemaCommand
    {
        get => GetValue(SwitchSchemaCommandProperty);
        set => SetValue(SwitchSchemaCommandProperty, value);
    }

    public DatabaseConnectionCard()
    {
        InitializeComponent();
    }

    private void OnConnectionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!ConnectionComboBox.IsDropDownOpen)
            return;

        if (SelectedConnection is null)
            return;

        if (SwitchConnectionCommand?.CanExecute(SelectedConnection) == true)
            SwitchConnectionCommand.Execute(SelectedConnection);
    }

    private void OnSchemaSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!DatabaseComboBox.IsDropDownOpen)
            return;

        if (string.IsNullOrWhiteSpace(SelectedSchema))
            return;

        if (SwitchSchemaCommand?.CanExecute(SelectedSchema) == true)
            SwitchSchemaCommand.Execute(SelectedSchema);
    }

    private void OnDatabaseSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        OnSchemaSelectionChanged(sender, e);
}
