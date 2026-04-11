using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.Services.Localization;
using System;

namespace DBWeaver.UI.Controls.SqlEditor;

public sealed class SqlToolOutputDialogWindow : Window
{
    public SqlToolOutputDialogWindow(string title, string summary, string details)
    {
        Width = 860;
        Height = 520;
        MinWidth = 620;
        MinHeight = 360;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = -1;
        CanResize = true;
        Title = title;

        var closeButton = new Button
        {
            Content = L("common.close", "Fechar"),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
            Padding = new Thickness(12, 6),
        };
        closeButton.Click += (_, _) => Close();

        var summaryBlock = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(summary)
                ? L("sqlEditor.tools.noSummary", "Sem resumo disponivel.")
                : summary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ResolveBrush("TextPrimaryBrush", Brushes.White),
            FontWeight = FontWeight.SemiBold,
        };

        var detailsBox = new TextBox
        {
            Text = string.IsNullOrWhiteSpace(details)
                ? L("sqlEditor.tools.noDetails", "Sem detalhes disponiveis.")
                : details,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
            Background = ResolveBrush("Bg0Brush", new SolidColorBrush(Color.Parse("#101420"))),
            Foreground = ResolveBrush("TextSecondaryBrush", Brushes.Gainsboro),
        };

        var detailsBorder = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = ResolveBrush("BorderSubtleBrush", new SolidColorBrush(Color.Parse("#2A3556"))),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8),
            Child = detailsBox,
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { closeButton },
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };
        rootGrid.Children.Add(summaryBlock);
        Grid.SetRow(summaryBlock, 0);
        rootGrid.Children.Add(detailsBorder);
        Grid.SetRow(detailsBorder, 1);
        rootGrid.Children.Add(footer);
        Grid.SetRow(footer, 2);

        Content = new Border
        {
            Padding = new Thickness(16),
            Background = ResolveBrush("Bg1Brush", new SolidColorBrush(Color.Parse("#121B30"))),
            Child = rootGrid,
        };
    }

    private static IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (Application.Current?.TryGetResource(resourceKey, theme: null, out object? resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}
