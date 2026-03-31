using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using System.Collections.Generic;
using System.Linq;

namespace VisualSqlArchitect.UI.Controls;

public sealed class KeyboardShortcutsWindow : Window
{
    private sealed record ShortcutItem(string Section, string Key, string Action);

    private readonly List<ShortcutItem> _allShortcuts =
    [
        // Arquivo e geral
        new("Arquivo e geral", "F1", "Abrir esta tela de atalhos"),
        new("Arquivo e geral", "Ctrl+N", "Novo canvas"),
        new("Arquivo e geral", "Ctrl+O", "Abrir arquivo"),
        new("Arquivo e geral", "Ctrl+S", "Salvar"),
        new("Arquivo e geral", "Ctrl+Shift+S", "Salvar como"),
        new("Arquivo e geral", "Ctrl+K", "Command Palette"),

        // Edição
        new("Edição", "Ctrl+Z", "Undo"),
        new("Edição", "Ctrl+Y", "Redo"),
        new("Edição", "Ctrl+A", "Selecionar todos"),
        new("Edição", "Del ou Backspace", "Excluir seleção"),
        new("Edição", "Esc", "Fechar overlays / cancelar ações"),

        // Canvas e navegação
        new("Canvas e navegação", "Shift+A", "Abrir busca de nodes"),
        new("Canvas e navegação", "Ctrl+F", "Abrir busca de nodes"),
        new("Canvas e navegação", "Ctrl+0", "Reset de viewport"),
        new("Canvas e navegação", "F", "Centralizar seleção"),
        new("Canvas e navegação", "Shift+F", "Enquadrar seleção"),
        new("Canvas e navegação", "Ctrl+L", "Auto Layout"),
        new("Canvas e navegação", "Ctrl+G", "Toggle Snap to Grid"),
        new("Canvas e navegação", "Ctrl+PgUp", "Bring Forward"),
        new("Canvas e navegação", "Ctrl+PgDown", "Send Backward"),
        new("Canvas e navegação", "Ctrl+Shift+PgUp", "Bring to Front"),
        new("Canvas e navegação", "Ctrl+Shift+PgDown", "Send to Back"),

        // Zoom, pan e precisão
        new("Zoom, pan e precisão", "Ctrl++ / Ctrl+-", "Zoom in / out"),
        new("Zoom, pan e precisão", "Botão do meio + arrastar", "Pan"),
        new("Zoom, pan e precisão", "Botão direito + arrastar", "Pan"),
        new("Zoom, pan e precisão", "Space + arrastar", "Pan temporário"),
        new("Zoom, pan e precisão", "Alt + arrastar esquerdo", "Pan alternativo"),
        new("Zoom, pan e precisão", "Setas", "Nudge fino da seleção"),
        new("Zoom, pan e precisão", "Shift + Setas", "Nudge acelerado"),

        // Preview e inspeção
        new("Preview e inspeção", "F3", "Toggle data preview"),
        new("Preview e inspeção", "F4", "Explain plan"),
        new("Preview e inspeção", "F5", "Run preview"),
        new("Preview e inspeção", "Ctrl+Shift+C", "Connection manager"),
        new("Preview e inspeção", "Ctrl+Shift+H", "Flow version history"),
    ];

    private TextBox? _searchBox;
    private TextBlock? _resultInfo;
    private StackPanel? _sectionsHost;

    public KeyboardShortcutsWindow()
    {
        Title = "Keyboard Shortcuts";
        Width = 760;
        Height = 700;
        MinWidth = 620;
        MinHeight = 520;
        Background = new SolidColorBrush(Color.Parse("#0D0F14"));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        KeyDown += OnKeyDown;

        Content = new Border
        {
            Padding = new Thickness(16),
            Child = BuildContent(),
        };
    }

    private Control BuildContent()
    {
        var root = new StackPanel
        {
            Spacing = 14,
        };

        root.Children.Add(new TextBlock
        {
            Text = "Visual SQL Architect — Atalhos",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
        });

        root.Children.Add(new TextBlock
        {
            Text = "Dica: use Ctrl+K para abrir a Command Palette e pesquisar comandos.",
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
        });

        _searchBox = new TextBox
        {
            Watermark = "Filtrar atalho por tecla ou ação...",
            Background = new SolidColorBrush(Color.Parse("#101521")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E2335")),
            BorderThickness = new Thickness(1),
            Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
        };
        _searchBox.TextChanged += (_, _) => RenderSections(_searchBox.Text);
        root.Children.Add(_searchBox);

        _resultInfo = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
        };
        root.Children.Add(_resultInfo);

        _sectionsHost = new StackPanel { Spacing = 10 };

        RenderSections();

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    root,
                    _sectionsHost,
                },
            },
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrWhiteSpace(_searchBox?.Text))
            {
                _searchBox!.Text = string.Empty;
                e.Handled = true;
                return;
            }

            Close();
            e.Handled = true;
        }
    }

    private void RenderSections(string? filter = null)
    {
        if (_sectionsHost is null)
            return;

        string f = (filter ?? string.Empty).Trim();
        IEnumerable<ShortcutItem> rows = _allShortcuts;

        if (!string.IsNullOrWhiteSpace(f))
        {
            rows = rows.Where(x =>
                x.Key.Contains(f, StringComparison.OrdinalIgnoreCase)
                || x.Action.Contains(f, StringComparison.OrdinalIgnoreCase)
                || x.Section.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        List<ShortcutItem> filtered = rows.ToList();
        _sectionsHost.Children.Clear();

        if (_resultInfo is not null)
            _resultInfo.Text = string.IsNullOrWhiteSpace(f)
                ? $"{_allShortcuts.Count} atalhos"
                : $"{filtered.Count} resultado(s) para \"{f}\"";

        if (filtered.Count == 0)
        {
            _sectionsHost.Children.Add(new TextBlock
            {
                Text = "Nenhum atalho encontrado.",
                Foreground = new SolidColorBrush(Color.Parse("#8B95A8")),
            });
            return;
        }

        foreach (IGrouping<string, ShortcutItem> group in filtered.GroupBy(x => x.Section))
            _sectionsHost.Children.Add(Section(group.Key, [.. group.Select(x => (x.Key, x.Action))]));
    }

    private static Border Section(string title, params (string Key, string Action)[] rows)
    {
        var list = new StackPanel { Spacing = 8 };
        list.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#60A5FA")),
        });

        foreach ((string key, string action) in rows)
        {
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("190,*"),
            };

            var keyBorder = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#171B26")),
                BorderBrush = new SolidColorBrush(Color.Parse("#252C3F")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 4),
                Child = new TextBlock
                {
                    Text = key,
                    FontFamily = new FontFamily("Consolas,monospace"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.Parse("#E8EAED")),
                },
            };
            Grid.SetColumn(keyBorder, 0);
            row.Children.Add(keyBorder);

            var actionText = new TextBlock
            {
                Margin = new Thickness(10, 4, 0, 0),
                Text = action,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#C8D0DC")),
            };
            Grid.SetColumn(actionText, 1);
            row.Children.Add(actionText);

            list.Children.Add(row);
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#101521")),
            BorderBrush = new SolidColorBrush(Color.Parse("#1E2335")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            Child = list,
        };
    }
}
