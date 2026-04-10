using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace DBWeaver.UI.Controls.Shell;

public partial class AppHeaderBar : UserControl
{
    public event EventHandler? TitleMenuRequested;

    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<AppHeaderBar, string>(
        nameof(Title),
        string.Empty
    );

    public static readonly StyledProperty<object?> LeftContentProperty = AvaloniaProperty.Register<AppHeaderBar, object?>(
        nameof(LeftContent)
    );

    public static readonly StyledProperty<object?> CenterContentProperty = AvaloniaProperty.Register<AppHeaderBar, object?>(
        nameof(CenterContent)
    );

    public static readonly StyledProperty<object?> RightContentProperty = AvaloniaProperty.Register<AppHeaderBar, object?>(
        nameof(RightContent)
    );

    public static readonly StyledProperty<bool> ShowWindowControlsProperty = AvaloniaProperty.Register<AppHeaderBar, bool>(
        nameof(ShowWindowControls),
        true
    );

    public static readonly StyledProperty<bool> ShowBrandBadgeProperty = AvaloniaProperty.Register<AppHeaderBar, bool>(
        nameof(ShowBrandBadge),
        true
    );

    public AppHeaderBar()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    public object? CenterContent
    {
        get => GetValue(CenterContentProperty);
        set => SetValue(CenterContentProperty, value);
    }

    public object? RightContent
    {
        get => GetValue(RightContentProperty);
        set => SetValue(RightContentProperty, value);
    }

    public bool ShowWindowControls
    {
        get => GetValue(ShowWindowControlsProperty);
        set => SetValue(ShowWindowControlsProperty, value);
    }

    public bool ShowBrandBadge
    {
        get => GetValue(ShowBrandBadgeProperty);
        set => SetValue(ShowBrandBadgeProperty, value);
    }

    private void Header_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
            window.BeginMoveDrag(e);
    }

    private void TitleButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TitleMenuRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
            window.WindowState = WindowState.Minimized;
    }

    private void Maximize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window)
            return;

        window.WindowState =
            window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window window)
            window.Close();
    }
}
