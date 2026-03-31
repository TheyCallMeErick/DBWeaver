using System.Reflection;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels;

public class ConnectionManagerUxStateTests
{
    [Fact]
    public void Connect_DoesNotCloseDialogImmediately()
    {
        var vm = new ConnectionManagerViewModel
        {
            IsVisible = true,
        };

        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Local",
            Provider = DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "db",
            Username = "u",
            Password = "p",
        };

        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.ConnectCommand.Execute(null);

        Assert.True(vm.IsVisible);
    }

    [Fact]
    public async Task LoadDatabaseTablesAsync_WithoutSearchMenu_ReportsFailureAndKeepsDialogVisible()
    {
        var vm = new ConnectionManagerViewModel
        {
            IsVisible = true,
            SearchMenu = null,
        };

        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Local",
            Provider = DatabaseProvider.Postgres,
            Host = "localhost",
            Port = 5432,
            Database = "db",
            Username = "u",
            Password = "p",
        };

        MethodInfo method = typeof(ConnectionManagerViewModel)
            .GetMethod("LoadDatabaseTablesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var task = (Task)method.Invoke(vm, [profile])!;
        await task;

        Assert.True(vm.IsVisible);
        Assert.False(vm.IsConnecting);
        Assert.Contains("failed", vm.TestStatus, StringComparison.OrdinalIgnoreCase);
    }
}
