using System.Data;
using DBWeaver;
using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlEditorExecutionServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidSqlAndConnection_UsesOrchestratorPreview()
    {
        var factory = new FakeOrchestratorFactory(
            new PreviewResult(Success: true, Data: BuildTable(rows: 2), ErrorMessage: null, ExecutionTime: TimeSpan.FromMilliseconds(8), RowsAffected: 2));
        var sut = new SqlEditorExecutionService(factory);
        ConnectionConfig config = BuildConfig();

        SqlEditorResultSet result = await sut.ExecuteAsync(" SELECT 1; ", config);

        Assert.True(result.Success);
        Assert.Equal("SELECT 1;", result.StatementSql);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Rows.Count);
        Assert.Equal(1, factory.CreateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptySql_ReturnsFailureResult()
    {
        var sut = new SqlEditorExecutionService();

        SqlEditorResultSet result = await sut.ExecuteAsync("   ", BuildConfig());

        Assert.False(result.Success);
        Assert.Equal(string.Empty, result.StatementSql);
        Assert.Equal("No SQL statement selected for execution.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutConnection_ReturnsFailureResult()
    {
        var sut = new SqlEditorExecutionService();

        SqlEditorResultSet result = await sut.ExecuteAsync("SELECT 1", config: null);

        Assert.False(result.Success);
        Assert.Equal("No active database connection for SQL execution.", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCanceled_ReturnsCanceledResult()
    {
        var factory = new FakeOrchestratorFactory(previewResult: null, throwCanceled: true);
        var sut = new SqlEditorExecutionService(factory);

        SqlEditorResultSet result = await sut.ExecuteAsync("SELECT 1", BuildConfig(), ct: CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("SQL execution was canceled.", result.ErrorMessage);
    }

    private static ConnectionConfig BuildConfig() =>
        new(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "vsa",
            Username: "u",
            Password: "p");

    private static DataTable BuildTable(int rows)
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        for (int i = 0; i < rows; i++)
            table.Rows.Add(i + 1);
        return table;
    }

    private sealed class FakeOrchestratorFactory(PreviewResult? previewResult, bool throwCanceled = false) : IDbOrchestratorFactory
    {
        public int CreateCalls { get; private set; }

        public IDbOrchestrator Create(ConnectionConfig config)
        {
            CreateCalls++;
            return new FakeOrchestrator(config, previewResult, throwCanceled);
        }

        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;
    }

    private sealed class FakeOrchestrator(ConnectionConfig config, PreviewResult? previewResult, bool throwCanceled) : IDbOrchestrator
    {
        public DatabaseProvider Provider => config.Provider;
        public ConnectionConfig Config => config;

        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConnectionTestResult(true));

        public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
            Task.FromResult(new DatabaseSchema("db", config.Provider, []));

        public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
        {
            if (throwCanceled)
                throw new OperationCanceledException();

            return Task.FromResult(previewResult ?? new PreviewResult(false, ErrorMessage: "missing"));
        }

        public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
            Task.FromResult(new DdlExecutionResult(true, []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
