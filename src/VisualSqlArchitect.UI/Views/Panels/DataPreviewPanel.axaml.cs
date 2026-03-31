using System.ComponentModel;
using System.Data;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Controls;

public partial class DataPreviewPanel : UserControl
{
    public static readonly StyledProperty<LiveSqlBarViewModel?> LiveSqlViewModelProperty =
        AvaloniaProperty.Register<DataPreviewPanel, LiveSqlBarViewModel?>(nameof(LiveSqlViewModel));

    public LiveSqlBarViewModel? LiveSqlViewModel
    {
        get => GetValue(LiveSqlViewModelProperty);
        set => SetValue(LiveSqlViewModelProperty, value);
    }

    public DataPreviewPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        TabPreviewButton.Click += (_, _) => { if (_vm != null) _vm.ActiveTab = PreviewTab.DataPreview; };
        TabSqlButton.Click     += (_, _) => { if (_vm != null) _vm.ActiveTab = PreviewTab.LiveSql; };
    }

    private DataPreviewViewModel? _vm;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as DataPreviewViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DataPreviewViewModel.ResultData))
            RefreshGrid(_vm?.ResultData);
    }

    private void RefreshGrid(DataTable? dt)
    {
        ResultGrid.Columns.Clear();
        ResultGrid.ItemsSource = null;

        if (dt is null || dt.Rows.Count == 0) return;

        for (int i = 0; i < dt.Columns.Count; i++)
        {
            int capturedIndex = i;

            var column = new DataGridTemplateColumn
            {
                Header = dt.Columns[i].ColumnName,
                IsReadOnly = true,
                CellTemplate = new FuncDataTemplate<object?[]>((row, _) =>
                    new TextBlock
                    {
                        Text = row?[capturedIndex]?.ToString() ?? "",
                        Padding = new Thickness(10, 5),
                        VerticalAlignment = VerticalAlignment.Center,
                    }),
            };

            ResultGrid.Columns.Add(column);
        }

        var rows = new System.Collections.ObjectModel.ObservableCollection<object?[]>();
        foreach (DataRow row in dt.Rows)
        {
            var arr = new object?[dt.Columns.Count];
            for (int i = 0; i < dt.Columns.Count; i++)
                arr[i] = row.IsNull(i) ? null : row[i];
            rows.Add(arr);
        }

        ResultGrid.ItemsSource = rows;
    }
}
