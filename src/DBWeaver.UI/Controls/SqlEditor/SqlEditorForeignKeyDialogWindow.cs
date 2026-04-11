using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Controls.SqlEditor;

public sealed class SqlEditorForeignKeyDialogWindow : Window
{
    public SqlEditorForeignKeyDialogWindow(string columnName, IReadOnlyList<ForeignKeyRelation> relations)
    {
        Width = 760;
        Height = 460;
        MinWidth = 560;
        MinHeight = 320;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
        ExtendClientAreaTitleBarHeightHint = -1;
        Title = string.Format(L("sqlEditor.relation.title", "Relacionamentos: {0}"), columnName);

        var contentPanel = new StackPanel { Spacing = 8 };
        if (relations.Count == 0)
        {
            contentPanel.Children.Add(new TextBlock
            {
                Text = L("sqlEditor.relation.empty", "Nenhum relacionamento encontrado para esta coluna."),
                Foreground = ResolveBrush("TextSecondaryBrush", Brushes.Gainsboro),
            });
        }
        else
        {
            foreach (ForeignKeyRelation relation in relations)
            {
                string line = $"{relation.ChildFullTable}.{relation.ChildColumn} -> {relation.ParentFullTable}.{relation.ParentColumn}";
                contentPanel.Children.Add(new Border
                {
                    Background = ResolveBrush("Bg0Brush", new SolidColorBrush(Color.Parse("#0D1325"))),
                    BorderBrush = ResolveBrush("BorderSubtleBrush", new SolidColorBrush(Color.Parse("#2A3556"))),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 8),
                    Child = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = relation.ConstraintName,
                                FontWeight = FontWeight.SemiBold,
                                Foreground = ResolveBrush("TextPrimaryBrush", Brushes.White),
                            },
                            new TextBlock
                            {
                                Text = line,
                                FontFamily = new FontFamily("Consolas, Menlo, Monaco, monospace"),
                                Foreground = ResolveBrush("AccentPrimaryHoverBrush", Brushes.LightBlue),
                            },
                            new TextBlock
                            {
                                Text = $"ON DELETE {relation.OnDelete} | ON UPDATE {relation.OnUpdate}",
                                Foreground = ResolveBrush("TextMutedBrush", Brushes.Gray),
                                FontSize = 11,
                            },
                        },
                    },
                });
            }
        }

        var closeButton = new Button
        {
            Content = L("common.close", "Fechar"),
            MinWidth = 96,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        closeButton.Click += (_, _) => Close();

        var listScroll = new ScrollViewer
        {
            Content = contentPanel,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Children = { closeButton },
        };

        var rootGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
        };
        rootGrid.Children.Add(listScroll);
        Grid.SetRow(listScroll, 0);
        rootGrid.Children.Add(footer);
        Grid.SetRow(footer, 1);

        Content = new Border
        {
            Padding = new Thickness(14),
            Background = ResolveBrush("Bg1Brush", new SolidColorBrush(Color.Parse("#131B30"))),
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
