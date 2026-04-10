using System.Globalization;
using Avalonia.Data.Converters;
using Material.Icons;

namespace DBWeaver.UI.Converters;

public class StringToMaterialIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        if (value is string iconName && Enum.TryParse<MaterialIconKind>(iconName, out var kind))
        {
            return kind;
        }
        return MaterialIconKind.HelpCircleOutline;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo? culture)
    {
        throw new NotImplementedException();
    }
}
