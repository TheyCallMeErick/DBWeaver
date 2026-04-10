using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.Shell;

public sealed class ShellSqlEditorFactoryTests
{
    [Fact]
    public void Constructor_UsesInjectedSqlEditorFactory()
    {
        var expectedVm = new SqlEditorViewModel();
        var factory = new FakeSqlEditorViewModelFactory(expectedVm);

        var shell = new ShellViewModel(sqlEditorViewModelFactory: factory, connectionManagerViewModelFactory: global::DBWeaver.UI.Services.ConnectionManager.ConnectionManagerViewModelFactory.CreateDefault());

        Assert.Same(expectedVm, shell.SqlEditor);
        Assert.Equal(1, factory.CreateCalls);
    }

    private sealed class FakeSqlEditorViewModelFactory(SqlEditorViewModel vm) : ISqlEditorViewModelFactory
    {
        public int CreateCalls { get; private set; }

        public SqlEditorViewModel Create(SqlEditorViewModelFactoryContext context)
        {
            CreateCalls++;
            _ = context.ConnectionConfigResolver();
            _ = context.ConnectionConfigByProfileIdResolver(null);
            _ = context.ConnectionProfilesResolver();
            _ = context.MetadataResolver();
            return vm;
        }
    }
}
