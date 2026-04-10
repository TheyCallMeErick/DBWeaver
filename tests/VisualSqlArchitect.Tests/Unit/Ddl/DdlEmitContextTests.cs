using DBWeaver.Core;
using DBWeaver.Ddl;
using DBWeaver.Registry;

namespace DBWeaver.Tests.Unit.Ddl;

public sealed class DdlEmitContextTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres, "\"my_table\"")]
    [InlineData(DatabaseProvider.SqlServer, "[my_table]")]
    [InlineData(DatabaseProvider.MySql, "`my_table`")]
    [InlineData(DatabaseProvider.SQLite, "\"my_table\"")]
    public void Constructor_WithDefaultRegistry_ResolvesProviderDialect(
        DatabaseProvider provider,
        string expectedQuotedIdentifier)
    {
        var context = new DdlEmitContext(provider);

        Assert.Equal(provider, context.Provider);
        Assert.Equal(expectedQuotedIdentifier, context.Dialect.QuoteIdentifier("my_table"));
    }

    [Fact]
    public void Constructor_WithInjectedRegistry_UsesInjectedDialect()
    {
        IProviderRegistry registry = ProviderRegistry.CreateDefault();
        var context = new DdlEmitContext(DatabaseProvider.Postgres, registry);

        Assert.Equal("\"my_table\"", context.Dialect.QuoteIdentifier("my_table"));
    }

    [Fact]
    public void Constructor_WithUnknownProvider_ThrowsNotSupportedException()
    {
        IProviderRegistry registry = ProviderRegistry.CreateDefault();
        DatabaseProvider unknown = (DatabaseProvider)999;

        Assert.Throws<NotSupportedException>(() => new DdlEmitContext(unknown, registry));
    }
}
