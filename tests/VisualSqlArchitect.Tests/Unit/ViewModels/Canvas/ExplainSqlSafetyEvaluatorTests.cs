using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainSqlSafetyEvaluatorTests
{
    [Theory]
    [InlineData("INSERT INTO t VALUES (1)")]
    [InlineData("update t set c = 1")]
    [InlineData("DELETE FROM t")]
    [InlineData(" merge into t using s on t.id=s.id when matched then update set c=1")]
    [InlineData("ALTER TABLE t ADD COLUMN c INT")]
    [InlineData("DROP TABLE t")]
    [InlineData("TRUNCATE TABLE t")]
    public void LooksMutating_ReturnsTrue_ForMutatingCommands(string sql)
    {
        var sut = new ExplainSqlSafetyEvaluator();
        Assert.True(sut.LooksMutating(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM t")]
    [InlineData(" WITH cte AS (SELECT 1) SELECT * FROM cte")]
    [InlineData("  -- comment\nSELECT 1")]
    [InlineData("/* comment */ SELECT 1")]
    [InlineData("")]
    [InlineData("   ")]
    public void LooksMutating_ReturnsFalse_ForReadOnlyOrEmptySql(string sql)
    {
        var sut = new ExplainSqlSafetyEvaluator();
        Assert.False(sut.LooksMutating(sql));
    }

    [Fact]
    public void LooksMutating_IgnoresMultipleLeadingComments()
    {
        const string sql = """
            -- first
            /* second */
            DELETE FROM orders
            """;
        var sut = new ExplainSqlSafetyEvaluator();

        Assert.True(sut.LooksMutating(sql));
    }
}


