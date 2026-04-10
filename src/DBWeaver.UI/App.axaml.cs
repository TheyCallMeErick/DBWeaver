using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.ConnectionManager.Contracts;
using DBWeaver.UI.Services.Modal;
using DBWeaver.UI.Services.Settings;
using DBWeaver.UI.Services.Theming;
using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DBWeaver.UI;

public partial class App : Application
{
    private static readonly ILogger<App> _logger = NullLogger<App>.Instance;
    private IServiceProvider? _services;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ApplySavedThemeVariant();
        ApplyUserThemeIfPresent();

        _services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = _services.GetRequiredService<MainWindow>();

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddDBWeaver();
        services.AddSingleton<IConnectionErrorMessageMapper, ConnectionErrorMessageMapper>();
        services.AddSingleton<IConnectionStatusPresenter, ConnectionStatusPresenter>();
        services.AddSingleton<IConnectionCanvasPromptCoordinator, ConnectionCanvasPromptCoordinator>();
        services.AddSingleton<IConnectionHealthMonitorService, ConnectionHealthMonitorService>();
        services.AddSingleton<IConnectionSessionOrchestrator, ConnectionSessionOrchestrator>();
        services.AddSingleton<IConnectionProfileStore, ConnectionProfileStore>();
        services.AddSingleton<IConnectionProfileFormMapper, ConnectionProfileFormMapper>();
        services.AddSingleton<IConnectionActivationWorkflow, ConnectionActivationWorkflow>();
        services.AddSingleton<IFireAndForgetSafetyExecutor>(sp =>
            new FireAndForgetSafetyExecutor(sp.GetRequiredService<ILogger<ConnectionManagerViewModel>>()));
        services.AddSingleton<IConnectionHealthLifecycleCoordinator>(sp =>
            new ConnectionHealthLifecycleCoordinator(sp.GetRequiredService<IConnectionHealthMonitorService>()));
        services.AddSingleton<IConnectionCatalogService, ConnectionCatalogService>();
        services.AddSingleton<IConnectionValidationService, ConnectionValidationService>();
        services.AddSingleton<IConnectionUrlParserService, ConnectionUrlParserService>();
        services.AddSingleton<IProviderCapabilityService, ProviderCapabilityService>();
        services.AddSingleton<IConnectionTelemetryService, ConnectionTelemetryService>();
        services.AddSingleton<IConnectionTestExecutor, DbOrchestratorConnectionTestExecutor>();
        services.AddSingleton<IConnectionTestService, ConnectionTestService>();
        services.AddSingleton<IConnectionSessionService, ConnectionSessionService>();
        services.AddSingleton<IConnectionManagerViewModelFactory, ConnectionManagerViewModelFactory>();
        services.AddSingleton<IAliasConvention, SnakeCaseConvention>();
        services.AddSingleton<IAliasConvention, CamelCaseConvention>();
        services.AddSingleton<IAliasConvention, PascalCaseConvention>();
        services.AddSingleton<IAliasConvention, ScreamingSnakeCaseConvention>();
        services.AddSingleton<IAliasConventionRegistry, AliasConventionRegistry>();
        services.AddSingleton<IGlobalModalManager>(_ => GlobalModalManager.Instance);
        services.AddSingleton<ThemeJsonSettingsService>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static void ApplySavedThemeVariant()
    {
        if (Application.Current is null)
            return;

        AppSettings settings = AppSettingsStore.Load();
        Application.Current.RequestedThemeVariant =
            settings.ThemeVariant.Equals("Light", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
    }

    private static void ApplyUserThemeIfPresent()
    {
        string path = ThemeLoader.GetDefaultThemePath();
        ThemeLoadResult load = ThemeLoader.LoadFromPath(path);
        if (load.Status == ThemeLoadStatus.NotFound)
            return;

        if (load.Status != ThemeLoadStatus.Loaded || load.Config is null)
        {
            _logger.LogWarning("Theme fallback: {Status} - {Message}", load.Status, load.Message);
            return;
        }

        ThemeValidationResult validation = ThemeValidator.Validate(load.Config);
        foreach (string error in validation.Errors)
            _logger.LogError("Theme validation error: {Error}", error);
        foreach (string warning in validation.Warnings)
            _logger.LogWarning("Theme validation warning: {Warning}", warning);

        if (!validation.IsValid)
        {
            _logger.LogWarning("Theme fallback: invalid configuration");
            return;
        }

        ThemeTokenMapResult mapped = ThemeTokenMapper.Map(load.Config);
        foreach (string warning in mapped.Warnings)
            _logger.LogWarning("Theme mapping warning: {Warning}", warning);

        int applied = ThemeRuntimeApplier.ApplyToCurrentApplication(mapped.TokenOverrides);
        _logger.LogInformation("Theme loaded: applied {AppliedCount} token override(s) from {Path}", applied, path);
    }
}

// ── Program entry point ───────────────────────────────────────────────────────

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
