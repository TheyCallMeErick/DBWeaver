using System.Data;
using Avalonia.Data.Converters;

namespace VisualSqlArchitect.UI.Converters;

/// <summary>
/// Converts a DataTable to its DefaultView for binding to Avalonia DataGrid.
/// Avalonia DataGrid doesn't work well with raw DataTable binding; it needs
/// a DataView which properly implements IEnumerable with change notification.
/// </summary>
public class DataTableConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        System.Console.WriteLine($"[DataTableConverter] Convert called with value type: {value?.GetType().Name ?? "null"}");

        if (value is DataTable dt)
        {
            // Return the DefaultView which supports proper binding and change notifications
            System.Console.WriteLine($"[DataTableConverter] Converting DataTable with {dt.Rows.Count} rows to DefaultView");
            var view = dt.DefaultView;
            System.Console.WriteLine($"[DataTableConverter] DefaultView created, RowFilter: '{view.RowFilter}', Count: {view.Count}");
            return view;
        }

        System.Console.WriteLine($"[DataTableConverter] Value is not DataTable, returning null");
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo? culture)
    {
        throw new NotSupportedException();
    }
}
