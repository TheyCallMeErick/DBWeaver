using Avalonia.Controls;
using Avalonia.Input;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Controls;

public sealed partial class PropertyPanelControl : UserControl
{
    public PropertyPanelControl()
    {
        InitializeComponent();

        Button? applyBtn = this.FindControl<Button>("ApplyBtn");
        if (applyBtn is not null)
            applyBtn.Click += (_, _) => Commit();
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
}
