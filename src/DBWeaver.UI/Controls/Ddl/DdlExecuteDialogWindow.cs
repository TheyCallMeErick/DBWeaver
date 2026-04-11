using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.Core;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls.Ddl;

public sealed class DdlExecuteDialogWindow : Window
{
    private readonly DdlExecuteDialogViewModel _vm;
    private readonly Func<bool, CancellationToken, Task<DdlExecutionResult>> _executor;
    private Button? _runButton;
    private Button? _cancelButton;
    private CheckBox? _stopOnErrorCheck;
    private CheckBox? _confirmDestructiveCheck;
    private TextBox? _resultBox;
    private TextBlock? _summaryText;
    private CancellationTokenSource? _runCts;

    public DdlExecuteDialogWindow(
        DdlExecuteDialogViewModel vm,
        Func<bool, CancellationToken, Task<DdlExecutionResult>> executor)
    {
        _vm = vm;
        _executor = executor;

        Title = L("ddl.dialog.title", "Execute DDL");
        Width = 920;
        Height = 680;
        MinWidth = 740;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse(UiColorConstants.C_0D0F14));

        KeyDown += OnKeyDown;
        Content = BuildContent();
    }

    private Control BuildContent()
    {
        _runButton = new Button
        {
            Content = L("ddl.dialog.execute", "Execute"),
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 110,
            IsEnabled = _vm.CanExecute,
        };
        _runButton.Click += async (_, _) => await RunAsync();

        _cancelButton = new Button
        {
            Content = L("ddl.dialog.cancel", "Cancel"),
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 110,
            IsEnabled = false,
        };
        _cancelButton.Click += (_, _) => _runCts?.Cancel();

        Button closeButton = new()
        {
            Content = L("ddl.dialog.close", "Close"),
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 110,
        };
        closeButton.Click += (_, _) => Close();

        _stopOnErrorCheck = new CheckBox
        {
            Content = L("ddl.dialog.stopOnError", "Stop on first failure"),
            IsChecked = _vm.StopOnError,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _stopOnErrorCheck.IsCheckedChanged += (_, _) => _vm.StopOnError = _stopOnErrorCheck.IsChecked ?? false;

        _confirmDestructiveCheck = new CheckBox
        {
            Content = L(
                "ddl.dialog.confirmDestructive",
                "I confirm execution of destructive statements (DROP TABLE)"
            ),
            IsVisible = _vm.HasDestructiveStatements,
            IsChecked = _vm.ConfirmDestructiveExecution,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_FCA5A5)),
        };
        _confirmDestructiveCheck.IsCheckedChanged += (_, _) =>
        {
            _vm.ConfirmDestructiveExecution = _confirmDestructiveCheck.IsChecked ?? false;
            if (_runButton is not null)
                _runButton.IsEnabled = _vm.CanExecute;
        };

        TextBox sqlPreview = new()
        {
            Text = _vm.SqlPreview,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
            MinHeight = 260,
        };

        _summaryText = new TextBlock
        {
            Text = L("ddl.dialog.reviewBeforeRun", "Review the DDL script before confirming."),
            Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_C8D0DC)),
            FontSize = 12,
        };

        ItemsControl statementList = new()
        {
            ItemsSource = _vm.StatementPreviews,
            ItemTemplate = new FuncDataTemplate<DdlStatementPreviewItem>((item, _) =>
                new TextBlock
                {
                    Text = item.CompactSql,
                    FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
                    FontSize = 11,
                    Margin = new Thickness(0, 1, 0, 1),
                    Foreground = item.IsDestructive
                        ? new SolidColorBrush(Color.Parse(UiColorConstants.C_FCA5A5))
                        : new SolidColorBrush(Color.Parse(UiColorConstants.C_9CA3AF)),
                }
            ),
            Margin = new Thickness(0, 6, 0, 0),
        };

        Border statementListHost = new()
        {
            BorderBrush = new SolidColorBrush(Color.Parse(UiColorConstants.C_1F2937)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8),
            MaxHeight = 120,
            Child = new ScrollViewer
            {
                Content = statementList,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            },
        };

        Button headerCloseButton = new()
        {
            Content = "×",
            Padding = new Thickness(8, 4),
            MinWidth = 32,
        };
        headerCloseButton.Click += (_, _) => Close();

        _resultBox = new TextBox
        {
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
            MinHeight = 200,
        };

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(UiColorConstants.C_070A12)),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*,Auto"),
                Children =
                {
                    new Border
                    {
                        Background = ResolveBrush("Bg1Brush", UiColorConstants.C_0F1220),
                        BorderBrush = ResolveBrush("BorderSubtleBrush", UiColorConstants.C_1E2A3F),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(16, 12),
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                            Children =
                            {
                                new Border
                                {
                                    Width = 28,
                                    Height = 28,
                                    CornerRadius = ResolveCornerRadius("RadiusSM", 6),
                                    Background = ResolveBrush("BtnWarningBgBrush", UiColorConstants.C_10291A),
                                    BorderBrush = ResolveBrush("StatusWarningBrush", UiColorConstants.C_FBBF24),
                                    BorderThickness = new Thickness(1),
                                    Child = new TextBlock
                                    {
                                        Text = "!",
                                        FontWeight = FontWeight.SemiBold,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Foreground = ResolveBrush("StatusWarningBrush", UiColorConstants.C_FBBF24),
                                    },
                                },
                                PlaceAtCol(new StackPanel
                                {
                                    Margin = new Thickness(10, 0, 0, 0),
                                    Spacing = 2,
                                    Children =
                                    {
                                        new TextBlock
                                        {
                                            Text = L("ddl.dialog.title", "Execute DDL"),
                                            FontSize = 14,
                                            FontWeight = FontWeight.SemiBold,
                                            Foreground = ResolveBrush("TextPrimaryBrush", UiColorConstants.C_E8EAED),
                                        },
                                        new TextBlock
                                        {
                                            Text = L("ddl.dialog.subtitle", "Review and execute DDL changes safely"),
                                            FontSize = 11,
                                            Foreground = ResolveBrush("TextMutedBrush", UiColorConstants.C_9CA3AF),
                                        },
                                    },
                                }, 1),
                                PlaceAtCol(headerCloseButton, 2),
                            },
                        },
                    },
                    PlaceAtRow(new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = new Border
                        {
                            Padding = new Thickness(16, 12),
                            Child = new StackPanel
                            {
                                Spacing = 10,
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = L("ddl.dialog.confirmQuestion", "Confirm DDL execution on the connected database?"),
                                        FontSize = 18,
                                        FontWeight = FontWeight.SemiBold,
                                        Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_E8EAED)),
                                    },
                                    new TextBlock
                                    {
                                        Text = L("ddl.dialog.irreversibleWarning", "This action can change the schema irreversibly."),
                                        Foreground = new SolidColorBrush(Color.Parse(UiColorConstants.C_FBBF24)),
                                        FontSize = 12,
                                    },
                                    sqlPreview,
                                    _summaryText,
                                    statementListHost,
                                    _resultBox,
                                },
                            },
                        },
                    }, 1),
                    PlaceAtRow(new Border
                    {
                        Background = ResolveBrush("Bg1Brush", UiColorConstants.C_0F1220),
                        BorderBrush = ResolveBrush("BorderSubtleBrush", UiColorConstants.C_1E2A3F),
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        Padding = new Thickness(16, 10),
                        Child = new Grid
                        {
                            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"),
                            Children =
                            {
                                new StackPanel
                                {
                                    Spacing = 4,
                                    Children =
                                    {
                                        _stopOnErrorCheck,
                                        _confirmDestructiveCheck,
                                    },
                                },
                                PlaceAtCol(_runButton, 1),
                                PlaceAtCol(_cancelButton, 2),
                                PlaceAtCol(closeButton, 3),
                            },
                        },
                    }, 2),
                },
            },
        };
    }

    private static Control PlaceAtRow(Control control, int row)
    {
        Grid.SetRow(control, row);
        return control;
    }

    private static Control PlaceAtCol(Control control, int col)
    {
        Grid.SetColumn(control, col);
        return control;
    }

    private async Task RunAsync()
    {
        if (_runButton is null || _cancelButton is null || _stopOnErrorCheck is null || _resultBox is null || _summaryText is null || _confirmDestructiveCheck is null)
            return;

        if (_vm.HasDestructiveStatements && !_vm.ConfirmDestructiveExecution)
        {
            _summaryText.Text = L("ddl.dialog.mustConfirmDestructive", "Confirm destructive execution to continue.");
            return;
        }

        _vm.IsExecuting = true;
        _runButton.IsEnabled = false;
        _cancelButton.IsEnabled = true;
        _stopOnErrorCheck.IsEnabled = false;
        _confirmDestructiveCheck.IsEnabled = false;
        _summaryText.Text = L("ddl.dialog.executing", "Executing...");
        _runCts = new CancellationTokenSource();

        try
        {
            DdlExecutionResult result = await _executor(_vm.StopOnError, _runCts.Token);
            _vm.ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
            _vm.ApplyCancelled();
        }
        catch (Exception ex)
        {
            _vm.ApplyError(ex);
        }
        finally
        {
            _runCts?.Dispose();
            _runCts = null;
            _vm.IsExecuting = false;
            _runButton.IsEnabled = _vm.CanExecute;
            _cancelButton.IsEnabled = false;
            _stopOnErrorCheck.IsEnabled = true;
            _confirmDestructiveCheck.IsEnabled = true;
            _summaryText.Text = _vm.ResultSummary;
            _resultBox.Text = _vm.ResultDetails;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        if (_vm.IsExecuting)
            _runCts?.Cancel();
        else
            Close();
        e.Handled = true;
    }

    private static string L(string key, string fallback)
    {
        string value = LocalizationService.Instance[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }

    private static IBrush ResolveBrush(string key, string fallbackHex)
    {
        if (Application.Current?.TryFindResource(key, out object? resource) == true && resource is IBrush brush)
            return brush;

        return new SolidColorBrush(Color.Parse(fallbackHex));
    }

    private static CornerRadius ResolveCornerRadius(string key, double fallbackValue)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is CornerRadius radius)
            return radius;

        return new CornerRadius(fallbackValue);
    }
}
