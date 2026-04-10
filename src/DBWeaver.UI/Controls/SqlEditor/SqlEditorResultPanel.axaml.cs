using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls.SqlEditor;

public partial class SqlEditorResultPanel : UserControl
{
    private SqlEditorViewModel? _viewModel;

    public SqlEditorResultPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as SqlEditorViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RefreshGrid(_viewModel.ResultRowsView);
        }
        else
        {
            RefreshGrid(null);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SqlEditorViewModel.ResultRowsView)
            or nameof(SqlEditorViewModel.SelectedResultTabIndex)
            or nameof(SqlEditorViewModel.ResultTabs))
        {
            RefreshGrid(_viewModel?.ResultRowsView);
        }
    }

    private void RefreshGrid(DataView? rowsView)
    {
        ResultGrid.Columns.Clear();
        ResultGrid.ItemsSource = null;

        if (rowsView is null)
            return;

        DataTable? table = rowsView.Table;
        if (table is null || table.Columns.Count == 0)
            return;

        for (int columnIndex = 0; columnIndex < table.Columns.Count; columnIndex++)
        {
            int capturedColumnIndex = columnIndex;
            string header = table.Columns[columnIndex].ColumnName;

            ResultGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = header,
                IsReadOnly = true,
                CanUserSort = true,
                CellTemplate = new FuncDataTemplate<object?[]>((row, _) =>
                    new TextBlock
                    {
                        Text = FormatCellValue(row, capturedColumnIndex),
                        Padding = new Avalonia.Thickness(12, 6),
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    }),
            });
        }

        var rows = new ObservableCollection<object?[]>();
        foreach (object? rowItem in rowsView)
        {
            if (rowItem is not DataRowView rowView)
                continue;

            var values = new object?[table.Columns.Count];
            for (int i = 0; i < table.Columns.Count; i++)
                values[i] = rowView.Row.IsNull(i) ? null : rowView.Row[i];

            rows.Add(values);
        }

        ResultGrid.ItemsSource = rows;
    }

    private static string FormatCellValue(object?[]? row, int index)
    {
        if (row is null || index < 0 || index >= row.Length || row[index] is null || row[index] is DBNull)
            return string.Empty;

        return row[index]?.ToString() ?? string.Empty;
    }
}

