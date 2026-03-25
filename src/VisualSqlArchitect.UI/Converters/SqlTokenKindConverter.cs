using System.Globalization;
using Avalonia.Data.Converters;
using VisualSqlArchitect.UI.ViewModels;

namespace VisualSqlArchitect.UI.Converters;

/// <summary>Converts a SqlTokenKind enum value to bool for CSS class binding.</summary>
public sealed class SqlTokenKindConverter : IValueConverter
{
    public static readonly SqlTokenKindConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is SqlTokenKind kind && parameter is string param)
            return kind.ToString().Equals(param, StringComparison.Ordinal);
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
