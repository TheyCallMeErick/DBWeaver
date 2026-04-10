using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorViewModelFactoryTests
{
    [Fact]
    public void Create_WithDefaultContext_BuildsReadyViewModel()
    {
        var sut = new SqlEditorViewModelFactory();

        SqlEditorViewModel vm = sut.Create(new SqlEditorViewModelFactoryContext
        {
            ConnectionConfigResolver = () => null,
            ConnectionConfigByProfileIdResolver = _ => null,
            ConnectionProfilesResolver = () => [],
            MetadataResolver = () => null,
            SharedConnectionManagerResolver = () => null,
        });

        Assert.NotNull(vm);
        Assert.Equal("Ready.", vm.ExecutionStatusText);
        Assert.Equal(DatabaseProvider.Postgres, vm.ActiveTabProvider);
    }

    [Fact]
    public void Create_WithInitialProviderAndProfile_AppliesInitialTabState()
    {
        var sut = new SqlEditorViewModelFactory();

        SqlEditorViewModel vm = sut.Create(new SqlEditorViewModelFactoryContext
        {
            InitialProvider = DatabaseProvider.MySql,
            InitialConnectionProfileId = "profile-1",
            ConnectionConfigResolver = () => null,
            ConnectionConfigByProfileIdResolver = _ => null,
            ConnectionProfilesResolver = () => [],
            MetadataResolver = () => null,
            SharedConnectionManagerResolver = () => null,
        });

        Assert.Equal(DatabaseProvider.MySql, vm.ActiveTabProvider);
        Assert.Equal("profile-1", vm.ActiveTabConnectionProfileId);
    }
}
