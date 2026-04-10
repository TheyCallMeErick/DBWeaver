using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.Services.Input.ShortcutRegistry;
using DBWeaver.UI.ViewModels.Shortcuts;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls;

/// <summary>
/// Reusable keyboard shortcuts editor and viewer for F1 and Settings.
/// </summary>
public sealed class KeyboardShortcutsEditorControl : UserControl
{
    private readonly KeyboardShortcutsViewModel _viewModel;
    private readonly bool _showHeader;
    private readonly Action<string, bool>? _statusCallback;
    private readonly Dictionary<string, TextBox> _gestureEditors = new(StringComparer.OrdinalIgnoreCase);

    private TextBox? _searchBox;
    private TextBlock? _resultInfo;
    private StackPanel? _sectionsHost;

    public KeyboardShortcutsEditorControl(
        KeyboardShortcutsViewModel viewModel,
        bool showHeader,
        Action<string, bool>? statusCallback = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _showHeader = showHeader;
        _statusCallback = statusCallback;

        Content = BuildContent();
        RenderSections();
    }

    private Control BuildContent()
    {
        var root = new StackPanel { Spacing = 10 };

        if (_showHeader)
        {
            root.Children.Add(new TextBlock
            {
                Text = L("shortcuts.headerTitle", "DBWeaver — Shortcuts"),
                FontSize = 20,
                FontWeight = FontWeight.SemiBold,
                Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
            });
            root.Children.Add(new TextBlock
            {
                Text = L("shortcuts.headerHint", "Tip: edit and reset shortcuts below."),
                FontSize = 12,
                Foreground = ResourceBrush("TextMutedBrush", UiColorConstants.C_7F8AAE),
            });
        }

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        };

        _searchBox = new TextBox
        {
            Watermark = L("shortcuts.filterWatermark", "Filter shortcuts by key, action, section, or tag..."),
            Background = ResourceBrush("Bg1Brush", UiColorConstants.C_0F1220),
            BorderBrush = ResourceBrush("BorderSubtleBrush", UiColorConstants.C_2A3554),
            BorderThickness = new Thickness(1),
            Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
        };
        _searchBox.TextChanged += (_, _) =>
        {
            _viewModel.SetSearchText(_searchBox.Text);
            RenderSections();
        };
        toolbar.Children.Add(_searchBox);

        var resetAllButton = new Button
        {
            Content = L("shortcuts.resetAll", "Reset all"),
            Classes = { "secondary" },
            Padding = new Thickness(10, 6),
            CornerRadius = ResourceCornerRadius("RadiusSM", 6),
        };
        Grid.SetColumn(resetAllButton, 1);
        resetAllButton.Margin = new Thickness(8, 0, 0, 0);
        resetAllButton.Click += (_, _) =>
        {
            ShortcutUpdateResult result = _viewModel.ResetAll();
            ReportStatus(result.Success
                ? L("shortcuts.status.resetAllSuccess", "All shortcuts reset to defaults.")
                : BuildFailureMessage(result), !result.Success);
            RenderSections();
        };
        toolbar.Children.Add(resetAllButton);

        root.Children.Add(toolbar);

        _resultInfo = new TextBlock
        {
            FontSize = 11,
            Foreground = ResourceBrush("TextMutedBrush", UiColorConstants.C_7F8AAE),
        };
        root.Children.Add(_resultInfo);

        _sectionsHost = new StackPanel { Spacing = 10 };
        root.Children.Add(_sectionsHost);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = root,
        };
    }

    private void RenderSections()
    {
        if (_sectionsHost is null)
            return;

        _gestureEditors.Clear();
        _sectionsHost.Children.Clear();

        int issueCount = _viewModel.Issues.Count;
        if (_resultInfo is not null)
        {
            string suffix = issueCount > 0
                ? $" · {issueCount} issue(s)"
                : string.Empty;
            _resultInfo.Text = string.IsNullOrWhiteSpace(_viewModel.SearchText)
                ? $"{_viewModel.TotalCount} shortcuts{suffix}"
                : $"{_viewModel.FilteredCount} result(s) for \"{_viewModel.SearchText}\"{suffix}";
        }

        if (_viewModel.Sections.Count == 0)
        {
            _sectionsHost.Children.Add(new TextBlock
            {
                Text = L("shortcuts.noneFound", "No shortcuts found."),
                Foreground = ResourceBrush("TextMutedBrush", UiColorConstants.C_7F8AAE),
            });
            return;
        }

        foreach (ShortcutSectionViewModel section in _viewModel.Sections)
            _sectionsHost.Children.Add(BuildSection(section));
    }

    private Control BuildSection(ShortcutSectionViewModel section)
    {
        var rows = new StackPanel { Spacing = 8 };
        rows.Children.Add(new TextBlock
        {
            Text = section.Name,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResourceBrush("AccentPrimaryHoverBrush", UiColorConstants.C_6C8CFF),
        });

        foreach (ShortcutItemViewModel item in section.Items)
            rows.Children.Add(BuildRow(item));

        return new Border
        {
            Background = ResourceBrush("Bg1Brush", UiColorConstants.C_0F1220),
            BorderBrush = ResourceBrush("BorderSubtleBrush", UiColorConstants.C_2A3554),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("RadiusSM", 6),
            Padding = new Thickness(12),
            Child = rows,
        };
    }

    private Control BuildRow(ShortcutItemViewModel item)
    {
        var panel = new StackPanel { Spacing = 6 };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("190,*,Auto"),
        };

        var keyBadge = new Border
        {
            Background = ResourceBrush("Bg2Brush", UiColorConstants.C_151A2C),
            BorderBrush = ResourceBrush("BorderBrush", UiColorConstants.C_334164),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("RadiusSM", 6),
            Padding = new Thickness(8, 4),
            Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.EffectiveGesture) ? "-" : item.EffectiveGesture,
                FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
                FontSize = 12,
                Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
            },
        };
        header.Children.Add(keyBadge);

        var desc = new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = ResourceBrush("TextSecondaryBrush", UiColorConstants.C_AEB9D9),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(desc, 1);
        header.Children.Add(desc);

        var tag = new Border
        {
            Background = item.IsCustomized
                ? ResourceBrush("BtnSuccessBgBrush", UiColorConstants.C_153A2C)
                : ResourceBrush("Bg2Brush", UiColorConstants.C_151A2C),
            BorderBrush = item.IsCustomized
                ? ResourceBrush("StatusOkBrush", UiColorConstants.C_2FBF84)
                : ResourceBrush("BorderSubtleBrush", UiColorConstants.C_2A3554),
            BorderThickness = new Thickness(1),
            CornerRadius = ResourceCornerRadius("RadiusPill", 999),
            Padding = new Thickness(8, 2),
            Child = new TextBlock
            {
                Text = item.IsCustomized
                    ? L("shortcuts.customized", "Customized")
                    : L("shortcuts.default", "Default"),
                FontSize = 10,
                Foreground = ResourceBrush("TextSecondaryBrush", UiColorConstants.C_AEB9D9),
            },
        };
        Grid.SetColumn(tag, 2);
        header.Children.Add(tag);

        panel.Children.Add(header);

        panel.Children.Add(new TextBlock
        {
            Text = item.Description,
            FontSize = 11,
            Foreground = ResourceBrush("TextMutedBrush", UiColorConstants.C_7F8AAE),
        });

        var editorRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
        };

        var editor = new TextBox
        {
            Watermark = $"{L("shortcuts.default", "Default")}: {item.DefaultGesture}",
            Text = item.IsCustomized ? item.EffectiveGesture : string.Empty,
            Background = ResourceBrush("Bg0Brush", UiColorConstants.C_090B14),
            BorderBrush = ResourceBrush("BorderSubtleBrush", UiColorConstants.C_2A3554),
            BorderThickness = new Thickness(1),
            Foreground = ResourceBrush("TextPrimaryBrush", UiColorConstants.C_E7ECFF),
        };
        _gestureEditors[item.ActionId] = editor;
        editorRow.Children.Add(editor);

        var apply = new Button
        {
            Content = L("shortcuts.apply", "Apply"),
            Classes = { "info" },
            Padding = new Thickness(10, 6),
            CornerRadius = ResourceCornerRadius("RadiusSM", 6),
        };
        Grid.SetColumn(apply, 1);
        apply.Margin = new Thickness(8, 0, 0, 0);
        apply.Click += (_, _) =>
        {
            ShortcutUpdateResult result = _viewModel.ApplyOverride(item.ActionId, editor.Text);
            ReportStatus(result.Success
                ? L("shortcuts.status.updated", "Shortcut updated.")
                : BuildFailureMessage(result), !result.Success);
            RenderSections();
        };
        editorRow.Children.Add(apply);

        var reset = new Button
        {
            Content = L("shortcuts.reset", "Reset"),
            Classes = { "secondary" },
            Padding = new Thickness(10, 6),
            CornerRadius = ResourceCornerRadius("RadiusSM", 6),
        };
        Grid.SetColumn(reset, 2);
        reset.Margin = new Thickness(8, 0, 0, 0);
        reset.Click += (_, _) =>
        {
            ShortcutUpdateResult result = _viewModel.ResetShortcut(item.ActionId);
            ReportStatus(result.Success
                ? L("shortcuts.status.reset", "Shortcut reset to default.")
                : BuildFailureMessage(result), !result.Success);
            RenderSections();
        };
        editorRow.Children.Add(reset);

        panel.Children.Add(editorRow);

        if (!string.IsNullOrWhiteSpace(item.IssueCode) || !string.IsNullOrWhiteSpace(item.IssueMessage))
        {
            string resolvedIssueMessage = ShortcutValidationMessageResolver.Resolve(item.IssueCode, item.IssueMessage);
            panel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.IssueCode)
                    ? resolvedIssueMessage
                    : $"{item.IssueCode}: {resolvedIssueMessage}",
                FontSize = 10,
                Foreground = ResourceBrush("StatusWarningBrush", UiColorConstants.C_D9A441),
                FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
            });
        }

        return panel;
    }

    private void ReportStatus(string message, bool isError)
    {
        if (_statusCallback is not null)
        {
            _statusCallback(message, isError);
            return;
        }

        if (_resultInfo is not null && !string.IsNullOrWhiteSpace(message))
            _resultInfo.Text = message;
    }

    private static string BuildFailureMessage(ShortcutUpdateResult result)
    {
        ShortcutValidationIssue? issue = result.Issues.FirstOrDefault();
        string resolved = ShortcutValidationMessageResolver.Resolve(issue);
        if (issue is null || string.IsNullOrWhiteSpace(issue.Code))
            return resolved;

        return $"{issue.Code}: {resolved}";
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static IBrush ResourceBrush(string key, string fallbackHex)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static CornerRadius ResourceCornerRadius(string key, double fallbackValue)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is CornerRadius radius)
            return radius;

        return new CornerRadius(fallbackValue);
    }
}
