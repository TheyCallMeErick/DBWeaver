using System.Data;
using DBWeaver.Core;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlMutationDiffServiceTests
{
    [Fact]
    public async Task BuildPreviewAsync_Delete_ReturnsRollbackSummary()
    {
        var sut = new SqlMutationDiffService(
            (sql, _, _, _) =>
            {
                if (sql.Contains("WHERE", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(CreateScalarResult(4));

                return Task.FromResult(CreateScalarResult(10));
            });

        MutationGuardResult guard = new()
        {
            IsSafe = false,
            RequiresConfirmation = true,
            CountQuery = "SELECT COUNT(*) FROM orders WHERE status = 'x'",
            SupportsDiff = true,
        };

        SqlMutationDiffPreview preview = await sut.BuildPreviewAsync(
            "DELETE FROM orders WHERE status = 'x';",
            guard,
            BuildConfig(),
            estimatedAffectedRows: null);

        Assert.True(preview.Available);
        Assert.Contains("ROLLBACK guaranteed", preview.Message);
        Assert.Contains("before 10", preview.Message);
        Assert.Contains("affected 4", preview.Message);
        Assert.Contains("after 6", preview.Message);
    }

    [Fact]
    public async Task BuildPreviewAsync_UnsupportedOrMissingCountQuery_ReturnsUnavailable()
    {
        var sut = new SqlMutationDiffService((_, _, _, _) => Task.FromResult(CreateScalarResult(1)));
        MutationGuardResult guard = new()
        {
            IsSafe = false,
            RequiresConfirmation = true,
            CountQuery = null,
            SupportsDiff = false,
        };

        SqlMutationDiffPreview preview = await sut.BuildPreviewAsync(
            "TRUNCATE TABLE orders;",
            guard,
            BuildConfig(),
            estimatedAffectedRows: null);

        Assert.False(preview.Available);
        Assert.Contains("No transactional diff preview", preview.Message);
    }

    private static SqlEditorResultSet CreateScalarResult(long value)
    {
        var table = new DataTable();
        table.Columns.Add("count", typeof(long));
        table.Rows.Add(value);

        return new SqlEditorResultSet
        {
            StatementSql = "SELECT COUNT(*)",
            Success = true,
            Data = table,
            RowsAffected = 1,
            ExecutionTime = TimeSpan.FromMilliseconds(1),
            ExecutedAt = DateTimeOffset.UtcNow,
        };
    }

    private static ConnectionConfig BuildConfig() =>
        new(
            Provider: DatabaseProvider.Postgres,
            Host: "localhost",
            Port: 5432,
            Database: "vsa",
            Username: "u",
            Password: "p");
}
