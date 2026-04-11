using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Controls.SqlEditor;

public sealed class SqlEditorCellExpandDialogWindow : Window
{
    public SqlEditorCellExpandDialogWindow(string columnName, string columnType, string value)
    {
        Title = L("sqlEditor.results.expand.title", "Conteudo da celula");
        Width = 860;
        Height = 560;
        MinWidth = 620;
        MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var header = new TextBlock
        {
            Text = $"{columnName} ({columnType})",
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var textBox = new TextBox
        {
            Text = value,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = ResolveFontFamily("MonoFont", "JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var copyButton = new Button
        {
            Content = L("sqlEditor.results.expand.copy", "Copiar"),
            Padding = new Thickness(12, 6),
            MinWidth = 90,
        };
        copyButton.Click += async (_, _) =>
        {
            if (Clipboard is null)
                return;

            await Clipboard.SetTextAsync(textBox.Text ?? string.Empty);
        };

        var closeButton = new Button
        {
            Content = L("common.close", "Fechar"),
            Padding = new Thickness(12, 6),
            MinWidth = 90,
        };
        closeButton.Click += (_, _) => Close();

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Margin = new Thickness(12),
            Children =
            {
                header,
                PlaceAtRow(textBox, 1),
                PlaceAtRow(
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { copyButton, closeButton },
                    },
                    2),
            },
        };
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static FontFamily ResolveFontFamily(string key, string fallback)
    {
        if (Application.Current?.TryGetResource(key, null, out object? resource) == true
            && resource is FontFamily fontFamily)
        {
            return fontFamily;
        }

        return new FontFamily(fallback);
    }

    private static T PlaceAtRow<T>(T control, int row)
        where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }
}
