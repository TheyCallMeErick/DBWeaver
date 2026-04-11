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
        Assert.True(
            preview.Message.Contains("ROLLBACK garantido", StringComparison.OrdinalIgnoreCase)
            || preview.Message.Contains("ROLLBACK guaranteed", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            preview.Message.Contains("antes 10", StringComparison.OrdinalIgnoreCase)
            || preview.Message.Contains("before 10", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            preview.Message.Contains("afetadas 4", StringComparison.OrdinalIgnoreCase)
            || preview.Message.Contains("affected 4", StringComparison.OrdinalIgnoreCase));
        Assert.True(
            preview.Message.Contains("depois 6", StringComparison.OrdinalIgnoreCase)
            || preview.Message.Contains("after 6", StringComparison.OrdinalIgnoreCase));
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
        Assert.True(
            preview.Message.Contains("Sem previa de diff transacional", StringComparison.OrdinalIgnoreCase)
            || preview.Message.Contains("No transactional diff preview", StringComparison.OrdinalIgnoreCase));
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
