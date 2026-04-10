using Avalonia.Controls;
using AvaloniaEdit;
using System.ComponentModel;
using DBWeaver.UI.Services;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.Shell;

public partial class OutputPreviewModalControl : UserControl
{
    private OutputPreviewModalViewModel? _vm;

    public OutputPreviewModalControl()
    {
        InitializeComponent();
        ConfigureDdlSqlEditor();
        DataContextChanged += (_, _) => AttachViewModel();
    }

    private void ConfigureDdlSqlEditor()
    {
        TextEditor? ddlSqlEditor = this.FindControl<TextEditor>("DdlSqlEditor");
        if (ddlSqlEditor is null)
            return;

        ddlSqlEditor.IsReadOnly = true;
        ddlSqlEditor.Options.EnableHyperlinks = false;
        ddlSqlEditor.Options.EnableEmailHyperlinks = false;
        ddlSqlEditor.Options.HighlightCurrentLine = false;
        ddlSqlEditor.Options.AllowScrollBelowDocument = false;
        ddlSqlEditor.SyntaxHighlighting = SqlEditorHighlightingService.GetSqlDefinition();
    }

    private void AttachViewModel()
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as OutputPreviewModalViewModel;
        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        SyncLiveSqlBinding();
        SyncDdlSqlText();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OutputPreviewModalViewModel.QueryLiveSql))
            SyncLiveSqlBinding();

        if (e.PropertyName == nameof(OutputPreviewModalViewModel.DdlSqlText))
            SyncDdlSqlText();
    }

    private void SyncLiveSqlBinding()
    {
        if (_vm is null)
            return;

        PreviewPanel.LiveSqlViewModel = _vm.QueryLiveSql;
    }

    private void SyncDdlSqlText()
    {
        if (_vm is null)
            return;

        TextEditor? ddlSqlEditor = this.FindControl<TextEditor>("DdlSqlEditor");
        if (ddlSqlEditor is null)
            return;

        string nextText = SqlDisplayFormatter.Format(_vm.DdlSqlText);
        if (string.Equals(ddlSqlEditor.Text, nextText, StringComparison.Ordinal))
            return;

        ddlSqlEditor.Text = nextText;
        ddlSqlEditor.TextArea.Caret.Offset = 0;
        ddlSqlEditor.TextArea.Caret.BringCaretToView();
    }
}
