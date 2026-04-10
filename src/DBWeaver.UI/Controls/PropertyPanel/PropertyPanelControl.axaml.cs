using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public sealed partial class PropertyPanelControl : UserControl
{
    public PropertyPanelControl()
    {
        InitializeComponent();

        Button? applyBtn = this.FindControl<Button>("ApplyBtn");
        if (applyBtn is not null)
            applyBtn.Click += (_, _) => Commit();

        AddHandler(ComboBox.SelectionChangedEvent, OnComboSelectionChanged, Avalonia.Interactivity.RoutingStrategies.Bubble);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // Enter while focused in a parameter field commits the edit
        if (e.Key == Key.Return && DataContext is PropertyPanelViewModel vm)
        {
            vm.CommitDirty();
            e.Handled = true;
        }
    }

    private void Commit()
    {
        if (DataContext is PropertyPanelViewModel vm)
            vm.CommitDirty();
    }

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.Source is not ComboBox combo)
            return;

        if (!combo.Classes.Contains("param-combo"))
            return;

        // Defer commit to avoid mutating parameter collections during ComboBox selection pipeline.
        Dispatcher.UIThread.Post(Commit, DispatcherPriority.Background);
    }
}
