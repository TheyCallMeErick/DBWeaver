using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.SqlEditor;

namespace DBWeaver.Tests.Unit.Services.SqlEditor;

public sealed class SqlCompletionProviderTests
{
    [Fact]
    public void GetSuggestions_WithKeywordPrefix_ReturnsKeywordCompletion()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("SEL", 3, metadata: null, DatabaseProvider.Postgres);

        Assert.Equal(3, request.PrefixLength);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_InTableContext_ReturnsTableSuggestionsWithAliasVariant()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM ord";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label.StartsWith("public.orders AS ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetSuggestions_WithAliasQualifier_ReturnsColumnsFromResolvedTable()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "status");
    }

    [Fact]
    public void GetSuggestions_ForPostgres_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions(string.Empty, 0, metadata: null, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "NOW()");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label.StartsWith("DATE_TRUNC", StringComparison.Ordinal));
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
    }

    [Fact]
    public void GetSuggestions_ForSqlServer_IncludesProviderSpecificFunctions()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions("GE", 2, metadata: null, DatabaseProvider.SqlServer);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "GETDATE()");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Function && s.Label == "NOW()");
    }

    [Fact]
    public void GetSuggestions_WithoutMetadata_DoesNotReturnJoinSuggestions()
    {
        var sut = new SqlCompletionProvider();
        const string sql = "SELECT * FROM public.orders o JOIN ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata: null, DatabaseProvider.Postgres);

        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Join);
    }

    [Fact]
    public void GetSuggestions_InJoinContext_ReturnsSmartJoinSuggestionFromForeignKey()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o JOIN ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        SqlCompletionSuggestion join = Assert.Single(request.Suggestions, s => s.Kind == SqlCompletionKind.Join);
        Assert.Contains("public.customers", join.InsertText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON", join.InsertText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("o.customer_id", join.InsertText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetSuggestions_IncludesSnippets()
    {
        var sut = new SqlCompletionProvider();

        SqlCompletionRequest request = sut.GetSuggestions(string.Empty, 0, metadata: null, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Snippet && s.Label == "SELECT ... FROM ...");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Snippet && s.Label == "UPDATE ... SET ... WHERE ...");
    }

    private static DbMetadata BuildMetadata()
    {
        var fk = new ForeignKeyRelation(
            ConstraintName: "fk_orders_customers",
            ChildSchema: "public",
            ChildTable: "orders",
            ChildColumn: "customer_id",
            ParentSchema: "public",
            ParentTable: "customers",
            ParentColumn: "id",
            OnDelete: ReferentialAction.NoAction,
            OnUpdate: ReferentialAction.NoAction,
            OrdinalPosition: 1);

        var orderColumns = new List<ColumnMetadata>
        {
            new("id", "int", "int", false, true, false, true, true, 1),
            new("status", "text", "text", true, false, false, false, false, 2),
            new("customer_id", "int", "int", true, false, true, false, true, 3),
        };

        var customerColumns = new List<ColumnMetadata>
        {
            new("id", "int", "int", false, true, false, true, true, 1),
            new("full_name", "text", "text", false, false, false, false, false, 2),
        };

        var orders = new TableMetadata(
            Schema: "public",
            Name: "orders",
            Kind: TableKind.Table,
            EstimatedRowCount: 1000,
            Columns: orderColumns,
            Indexes: [],
            OutboundForeignKeys: [fk],
            InboundForeignKeys: []);

        var customers = new TableMetadata(
            Schema: "public",
            Name: "customers",
            Kind: TableKind.Table,
            EstimatedRowCount: 200,
            Columns: customerColumns,
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: [fk]);

        return new DbMetadata(
            DatabaseName: "testdb",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders, customers])],
            AllForeignKeys: [fk]);
    }
}
