using System.Collections.ObjectModel;
using VisualSqlArchitect.UI.Serialization;

namespace VisualSqlArchitect.UI.ViewModels.Canvas;

public sealed class FileVersionHistoryViewModel(CanvasViewModel canvas) : ViewModelBase
{
    private readonly CanvasViewModel _canvas = canvas;
    private bool _isVisible;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private LocalFileVersionInfo? _selectedVersion;

    public ObservableCollection<LocalFileVersionInfo> Versions { get; } = [];

    public bool IsVisible
    {
        get => _isVisible;
        set => Set(ref _isVisible, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
                RaisePropertyChanged(nameof(CanRestore));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public LocalFileVersionInfo? SelectedVersion
    {
        get => _selectedVersion;
        set
        {
            if (Set(ref _selectedVersion, value))
                RaisePropertyChanged(nameof(CanRestore));
        }
    }

    public bool HasVersions => Versions.Count > 0;

    public bool CanRestore => SelectedVersion is not null && !IsBusy;

    public string CurrentFileLabel => string.IsNullOrWhiteSpace(_canvas.CurrentFilePath)
        ? "No file selected"
        : Path.GetFileName(_canvas.CurrentFilePath);

    public void Open()
    {
        IsVisible = true;
        _ = ReloadAsync();
    }

    public void Close() => IsVisible = false;

    public Task ReloadAsync()
    {
        if (string.IsNullOrWhiteSpace(_canvas.CurrentFilePath))
        {
            Versions.Clear();
            SelectedVersion = null;
            StatusMessage = "Save the canvas first to enable local history.";
            RaisePropertyChanged(nameof(HasVersions));
            RaisePropertyChanged(nameof(CurrentFileLabel));
            return Task.CompletedTask;
        }

        IsBusy = true;
        try
        {
            IReadOnlyList<LocalFileVersionInfo> found = CanvasSerializer.GetLocalFileVersions(_canvas.CurrentFilePath);
            Versions.Clear();
            foreach (LocalFileVersionInfo version in found)
                Versions.Add(version);

            SelectedVersion = Versions.FirstOrDefault();
            StatusMessage = Versions.Count == 0
                ? "No local versions found yet. Save this file to create history entries."
                : $"{Versions.Count} local version(s) available.";

            RaisePropertyChanged(nameof(HasVersions));
            RaisePropertyChanged(nameof(CurrentFileLabel));
        }
        finally
        {
            IsBusy = false;
        }
        return Task.CompletedTask;
    }

    public async Task RestoreSelectedAsync()
    {
        if (SelectedVersion is null || string.IsNullOrWhiteSpace(_canvas.CurrentFilePath))
            return;

        IsBusy = true;
        try
        {
            await CanvasSerializer.RestoreLocalVersionAsync(_canvas.CurrentFilePath, SelectedVersion.VersionPath);
            CanvasLoadResult result = await CanvasSerializer.LoadFromFileAsync(_canvas.CurrentFilePath, _canvas);

            if (!result.Success)
            {
                StatusMessage = $"Restore failed: {result.Error}";
                return;
            }

            _canvas.IsDirty = false;
            StatusMessage = $"Restored version from {SelectedVersion.CreatedAtLocalLabel}.";

            if (result.Warnings is { Count: > 0 })
            {
                foreach (string w in result.Warnings)
                {
                    _canvas.Diagnostics.AddWarning(
                        area: "Canvas Migration",
                        message: $"Version restore: {w}",
                        recommendation: "Review diagnostics and save the canvas to persist the migrated schema.",
                        openPanel: true
                    );
                }
            }

            await ReloadAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Restore failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
