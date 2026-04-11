using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using DBWeaver.UI.Services.Localization;
using DBWeaver.UI.ViewModels;
using Material.Icons;
using Material.Icons.Avalonia;
using DBWeaver.UI.Services.Theming;

namespace DBWeaver.UI.Controls.SqlEditor;

public sealed class SqlEditorReportExportDialogWindow : Window
{
    private readonly SqlEditorReportExportDialogViewModel _vm;
    private readonly Button _confirmButton;
    private readonly TextBlock _typeDescription;
    private readonly TextBlock _typeBadge;

    public SqlEditorReportExportDialogWindow(SqlEditorReportExportDialogViewModel vm)
    {
        _vm = vm;

        Title = L("sqlEditor.export.dialog.windowTitle", "Exportar Dados SQL");
        Width = 700;
        Height = 650;
        MinWidth = 620;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = ResolveBrush("Bg0Brush", UiColorConstants.C_070A12);

        _confirmButton = new Button
        {
            Content = L("sqlEditor.export.dialog.confirm", "Exportar"),
            Padding = new Thickness(14, 8),
            MinWidth = 120,
            IsEnabled = _vm.CanConfirm,
            Background = ResolveBrush("BtnSuccessBgBrush", UiColorConstants.C_10291A),
            BorderBrush = ResolveBrush("StatusOkBrush", UiColorConstants.C_34D399),
            Foreground = ResolveBrush("StatusOkBrush", UiColorConstants.C_34D399),
            BorderThickness = new Thickness(1),
            CornerRadius = ResolveCornerRadius("RadiusSM", 6),
        };
        _confirmButton.Click += OnConfirmClicked;

        _typeDescription = new TextBlock
        {
            Foreground = ResolveBrush("TextMutedBrush", UiColorConstants.C_8B95A8),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        };

        _typeBadge = new TextBlock
        {
            FontSize = 10,
            FontFamily = ResolveFontFamily("MonoFont", "JetBrains Mono,IBM Plex Mono,Cascadia Code,Consolas,monospace"),
            Foreground = ResolveBrush("StatusOkBrush", UiColorConstants.C_34D399),
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.CanConfirm))
                _confirmButton.IsEnabled = _vm.CanConfirm;

            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.SelectedType))
                RefreshTypeDetails();
        };

        Content = BuildContent();
        RefreshTypeDetails();
    }

    public bool WasConfirmed { get; private set; }

    private Control BuildContent()
    {
        var typeCombo = new ComboBox
        {
            ItemsSource = _vm.ReportTypes,
            SelectedItem = _vm.SelectedType,
            MinWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        typeCombo.SelectionChanged += (_, _) => _vm.SelectedType = typeCombo.SelectedItem as SqlEditorReportTypeOption;

        var fileNameBox = new TextBox
        {
            Text = _vm.FileName,
            Watermark = L("sqlEditor.export.dialog.fileNameWatermark", "relatorio.html"),
        };
        fileNameBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                _vm.FileName = fileNameBox.Text ?? string.Empty;
        };

        var titleBox = new TextBox
        {
            Text = _vm.Title,
            Watermark = L("sqlEditor.export.dialog.titleWatermark", "Relatorio SQL"),
        };
        titleBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                _vm.Title = titleBox.Text ?? string.Empty;
        };

        var descriptionBox = new TextBox
        {
            Text = _vm.Description,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 84,
            Watermark = L("sqlEditor.export.dialog.descriptionWatermark", "Contexto adicional para auditoria e compartilhamento."),
        };
        descriptionBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text))
                _vm.Description = descriptionBox.Text ?? string.Empty;
        };

        var includeSchemaCheck = new CheckBox
        {
            Content = L("sqlEditor.export.option.includeSchema", "Incluir schema de saida"),
            IsChecked = _vm.IncludeSchema,
            IsVisible = _vm.ShowIncludeSchema,
        };
        includeSchemaCheck.IsCheckedChanged += (_, _) => _vm.IncludeSchema = includeSchemaCheck.IsChecked ?? false;

        var includeNodeDetailsCheck = new CheckBox
        {
            Content = L("sqlEditor.export.option.includeNodeDetails", "Incluir placeholders de no/conexao no JSON"),
            IsChecked = _vm.IncludeNodeDetails,
            IsVisible = _vm.ShowIncludeNodeDetails,
        };
        includeNodeDetailsCheck.IsCheckedChanged += (_, _) => _vm.IncludeNodeDetails = includeNodeDetailsCheck.IsChecked ?? false;

        var includeMetadataCheck = new CheckBox
        {
            Content = L("sqlEditor.export.option.includeMetadata", "Incluir metadados opcionais"),
            IsChecked = _vm.IncludeMetadata,
            IsVisible = _vm.ShowIncludeMetadata,
        };
        includeMetadataCheck.IsCheckedChanged += (_, _) => _vm.IncludeMetadata = includeMetadataCheck.IsChecked ?? false;

        var useDashForEmptyCheck = new CheckBox
        {
            Content = L("sqlEditor.export.option.useDashForEmpty", "Usar '-' para campos vazios"),
            IsChecked = _vm.UseDashForEmptyFields,
            IsVisible = _vm.ShowUseDashForEmptyFields,
        };
        useDashForEmptyCheck.IsCheckedChanged += (_, _) => _vm.UseDashForEmptyFields = useDashForEmptyCheck.IsChecked ?? false;

        var optionsPanel = new StackPanel
        {
            Spacing = 8,
            Children = { includeSchemaCheck, includeMetadataCheck, useDashForEmptyCheck, includeNodeDetailsCheck },
            IsVisible = _vm.ShowOptions,
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.ShowIncludeNodeDetails))
                includeNodeDetailsCheck.IsVisible = _vm.ShowIncludeNodeDetails;

            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.ShowOptions))
                optionsPanel.IsVisible = _vm.ShowOptions;

            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.ShowIncludeSchema))
                includeSchemaCheck.IsVisible = _vm.ShowIncludeSchema;

            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.ShowIncludeMetadata))
                includeMetadataCheck.IsVisible = _vm.ShowIncludeMetadata;

            if (e.PropertyName == nameof(SqlEditorReportExportDialogViewModel.ShowUseDashForEmptyFields))
                useDashForEmptyCheck.IsVisible = _vm.ShowUseDashForEmptyFields;
        };

        Button cancelButton = new()
        {
            Content = L("common.cancel", "Cancelar"),
            Padding = new Thickness(14, 8),
            MinWidth = 120,
            Background = ResolveBrush("Bg2Brush", UiColorConstants.C_12172A),
            BorderBrush = ResolveBrush("BorderBrush", UiColorConstants.C_20314A),
            Foreground = ResolveBrush("TextSecondaryBrush", UiColorConstants.C_B8C3D9),
            BorderThickness = new Thickness(1),
            CornerRadius = ResolveCornerRadius("RadiusSM", 6),
        };
        cancelButton.Click += (_, _) => Close();

        Button headerCloseButton = new()
        {
            Content = "×",
            Padding = new Thickness(8, 4),
            MinWidth = 32,
            Background = ResolveBrush("Bg2Brush", UiColorConstants.C_12172A),
            BorderBrush = ResolveBrush("BorderBrush", UiColorConstants.C_20314A),
            Foreground = ResolveBrush("TextSecondaryBrush", UiColorConstants.C_B8C3D9),
            BorderThickness = new Thickness(1),
            CornerRadius = ResolveCornerRadius("RadiusSM", 6),
        };
        headerCloseButton.Click += (_, _) => Close();

        Border section(string title, Control child)
        {
            return new Border
            {
                Background = ResolveBrush("CanvasBgBrush", UiColorConstants.C_0B1220),
                BorderBrush = ResolveBrush("BorderSoftBrush", UiColorConstants.C_1E2A3F),
                BorderThickness = new Thickness(1),
                CornerRadius = ResolveCornerRadius("RadiusSM", 6),
                Padding = new Thickness(12),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            Foreground = ResolveBrush("TextMutedBrush", UiColorConstants.C_8B95A8),
                            FontWeight = FontWeight.Bold,
                            FontSize = 10,
                            LetterSpacing = 1.3,
                        },
                        child,
                    },
                },
            };
        }

        var root = new Grid
        {
            Margin = new Thickness(14),
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        };

        root.Children.Add(new Border
        {
            Background = ResolveBrush("Bg0Brush", UiColorConstants.C_0A111F),
            BorderBrush = ResolveBrush("BorderSoftBrush", UiColorConstants.C_1E2A3F),
            BorderThickness = new Thickness(1),
            CornerRadius = ResolveCornerRadius("RadiusMD", 10),
            Padding = new Thickness(14, 12),
            Margin = new Thickness(0, 0, 0, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                Children =
                {
                    new Border
                    {
                        Width = 30,
                        Height = 30,
                        CornerRadius = ResolveCornerRadius("RadiusSM", 6),
                        Background = ResolveBrush("BtnSuccessBgBrush", UiColorConstants.C_10291A),
                        Child = new MaterialIcon
                        {
                            Kind = MaterialIconKind.FileExportOutline,
                            Foreground = ResolveBrush("StatusOkBrush", UiColorConstants.C_34D399),
                            Width = 16,
                            Height = 16,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                        },
                    },
                    PlaceAtCol(
                        new StackPanel
                        {
                            Margin = new Thickness(10, 0, 0, 0),
                            Spacing = 1,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = L("sqlEditor.export.dialog.title", "Exportar Dados SQL"),
                                    Foreground = ResolveBrush("TextPrimaryBrush", UiColorConstants.C_E6EDF8),
                                    FontWeight = FontWeight.SemiBold,
                                    FontSize = 14,
                                },
                                new TextBlock
                                {
                                    Text = L("sqlEditor.export.dialog.subtitle", "Escolha o formato e os metadados do artefato antes de exportar."),
                                    Foreground = ResolveBrush("TextMutedBrush", UiColorConstants.C_8B95A8),
                                    FontSize = 11,
                                },
                            },
                        },
                        1),
                    PlaceAtCol(headerCloseButton, 2),
                },
            },
        });

        var typeInfoPanel = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                typeCombo,
                _typeDescription,
                new Border
                {
                    Background = ResolveBrush("BtnSuccessBgBrush", UiColorConstants.C_10291A),
                    BorderBrush = ResolveBrush("StatusOkBrush", UiColorConstants.C_34D399),
                    BorderThickness = new Thickness(1),
                    CornerRadius = ResolveCornerRadius("RadiusXS", 4),
                    Padding = new Thickness(6, 2),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = _typeBadge,
                },
            },
        };

        var formPanel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                section(L("sqlEditor.export.dialog.section.reportType", "TIPO DE RELATORIO"), typeInfoPanel),
                section(L("sqlEditor.export.dialog.section.fileName", "NOME DO ARQUIVO"), fileNameBox),
                section(L("sqlEditor.export.dialog.section.reportTitle", "TITULO"), titleBox),
                section(L("sqlEditor.export.dialog.section.description", "DESCRICAO"), descriptionBox),
                section(L("sqlEditor.export.dialog.section.options", "OPCOES"), optionsPanel),
            },
        };
        var formScroll = new ScrollViewer
        {
            Content = formPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        Grid.SetRow(formScroll, 1);
        root.Children.Add(formScroll);

        var footer = new Border
        {
            BorderBrush = ResolveBrush("BorderSubtleBrush", UiColorConstants.C_1E2A3F),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = ResolveBrush("Bg1Brush", UiColorConstants.C_0F1220),
            CornerRadius = ResolveCornerRadius("RadiusSM", 6),
            Padding = new Thickness(0, 10, 0, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Children = { cancelButton, _confirmButton },
            },
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private void RefreshTypeDetails()
    {
        _typeDescription.Text = _vm.SelectedType?.Description ?? string.Empty;

        _typeBadge.Text = _vm.SelectedType?.Type switch
        {
            SqlEditorReportType.HtmlFullFeature => L("sqlEditor.export.badge.offline", "PRONTO PARA OFFLINE"),
            SqlEditorReportType.JsonContract => L("sqlEditor.export.badge.structured", "PAYLOAD ESTRUTURADO"),
            SqlEditorReportType.CsvData => L("sqlEditor.export.badge.dataOnly", "SOMENTE DADOS"),
            SqlEditorReportType.ExcelWorkbook => L("sqlEditor.export.badge.dataOnly", "SOMENTE DADOS"),
            _ => L("sqlEditor.export.badge.offline", "PRONTO PARA OFFLINE"),
        };
    }

    private void OnConfirmClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!_vm.CanConfirm)
            return;

        WasConfirmed = true;
        Close();
    }

    private static Control PlaceAtCol(Control control, int col)
    {
        Grid.SetColumn(control, col);
        return control;
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

    private static FontFamily ResolveFontFamily(string key, string fallbackFamily)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out object? resource) == true && resource is FontFamily font)
            return font;

        return new FontFamily(fallbackFamily);
    }
}
