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

    [Fact]
    public void GetSuggestions_InSecondStatement_UsesCurrentStatementContext()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT 1; SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
    }

    [Fact]
    public void GetSuggestions_IgnoresCommentKeywordsForContextDetection()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "/* FROM users */ SELECT * FROM public.orders o WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.DoesNotContain(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
    }

    [Fact]
    public void GetSuggestions_InOrderByContext_ReturnsColumnsInScope()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o ORDER BY o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "status");
    }

    [Fact]
    public void GetSuggestions_InInsertColumnsContext_ReturnsTableSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "INSERT INTO pub";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.customers");
    }

    [Fact]
    public void GetSuggestions_InTableContext_IncludesCteSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "WITH recent_orders AS (SELECT * FROM public.orders) SELECT * FROM ";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "recent_orders");
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "recent_orders AS ro");
    }

    [Fact]
    public void GetSuggestions_WithCteAliasQualifier_DoesNotThrowAndKeepsBaselineSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "WITH recent_orders AS (SELECT * FROM public.orders) SELECT * FROM recent_orders r WHERE r.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_WithUnknownQualifier_DoesNotThrowAndReturnsBaselineSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE x.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Keyword && s.Label == "SELECT");
    }

    [Fact]
    public void GetSuggestions_WithLargeInput_DoesNotThrowAndReturnsSuggestions()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        string largeSql = "SELECT * FROM public.orders o WHERE o.id = 1 " + new string(' ', 10_500) + "o.";

        SqlCompletionRequest request = sut.GetSuggestions(largeSql, largeSql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotEmpty(request.Suggestions);
        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "id");
    }

    [Fact]
    public void GetSuggestions_FuzzyPrefix_FindsCreatedAt()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.crat";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Column && s.Label == "created_at");
    }

    [Fact]
    public void GetSuggestions_FuzzyPrefix_FindsUsersTable()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM usrs";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.Contains(request.Suggestions, s => s.Kind == SqlCompletionKind.Table && s.Label == "public.users");
    }

    [Fact]
    public void GetSuggestions_WithIdPrefix_ExactColumnComesBeforeFuzzyColumns()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT * FROM public.orders o WHERE o.id";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);
        List<SqlCompletionSuggestion> columns = request.Suggestions
            .Where(static s => s.Kind == SqlCompletionKind.Column)
            .ToList();

        Assert.NotEmpty(columns);
        Assert.Equal("id", columns[0].Label);
    }

    [Fact]
    public void GetSuggestions_WithMalformedComment_DoesNotThrow()
    {
        var sut = new SqlCompletionProvider();
        DbMetadata metadata = BuildMetadata();
        const string sql = "SELECT /* unfinished comment WHERE o.";

        SqlCompletionRequest request = sut.GetSuggestions(sql, sql.Length, metadata, DatabaseProvider.Postgres);

        Assert.NotNull(request);
        Assert.NotEmpty(request.Suggestions);
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
            new("user_id", "int", "int", true, false, true, false, true, 4),
            new("created_at", "timestamp", "timestamp", true, false, false, false, false, 5),
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

        var users = new TableMetadata(
            Schema: "public",
            Name: "users",
            Kind: TableKind.Table,
            EstimatedRowCount: 1200,
            Columns:
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("full_name", "text", "text", true, false, false, false, false, 2),
            ],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []);

        return new DbMetadata(
            DatabaseName: "testdb",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata("public", [orders, customers, users])],
            AllForeignKeys: [fk]);
    }
}
