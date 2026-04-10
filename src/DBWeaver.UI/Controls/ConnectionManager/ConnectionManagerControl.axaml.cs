using System.Linq;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Controls;

public sealed partial class ConnectionManagerControl : UserControl
{
    public ConnectionManagerControl()
    {
        InitializeComponent();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && DataContext is ConnectionManagerViewModel vm)
        {
            vm.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private async void BrowseSqliteFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionManagerViewModel vm)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SQLite database",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SQLite")
                {
                    Patterns = ["*.sqlite", "*.sqlite3", "*.db", "*.db3"],
                },
                FilePickerFileTypes.All,
            ],
        });

        IStorageFile? selected = files.FirstOrDefault();
        if (selected is null)
            return;

        string? localPath = selected.TryGetLocalPath();
        vm.EditDatabase = string.IsNullOrWhiteSpace(localPath)
            ? selected.Path.LocalPath
            : localPath;
    }

    private async void CreateSqliteFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionManagerViewModel vm)
            return;

        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
            return;

        IStorageFile? file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create SQLite database",
            SuggestedFileName = "database.sqlite3",
            FileTypeChoices =
            [
                new FilePickerFileType("SQLite")
                {
                    Patterns = ["*.sqlite", "*.sqlite3", "*.db", "*.db3"],
                },
            ],
        });

        if (file is null)
            return;

        string? localPath = file.TryGetLocalPath();
        string targetPath = string.IsNullOrWhiteSpace(localPath)
            ? file.Path.LocalPath
            : localPath;

        if (!string.IsNullOrWhiteSpace(targetPath) && !File.Exists(targetPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllBytes(targetPath, []);
        }

        vm.EditDatabase = targetPath;
    }
}
