using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Metadata;

// ─────────────────────────────────────────────────────────────────────────────
// Helpers — in-memory DbMetadata builder for tests
// ─────────────────────────────────────────────────────────────────────────────

internal static class MetadataFixtures
{
    // Build a minimal ColumnMetadata
    public static ColumnMetadata Col(string name, string type = "integer",
        bool isPk = false, bool isFk = false, bool isNullable = true,
        bool isUnique = false, bool isIndexed = false) =>
        new(name, type, type, isNullable, isPk, isFk, isUnique, isIndexed, 1);

    // Build a FK relation
    public static ForeignKeyRelation Fk(
        string childSchema, string childTable, string childCol,
        string parentSchema, string parentTable, string parentCol,
        ReferentialAction onDelete = ReferentialAction.NoAction,
        string constraint = "fk_test") =>
        new(constraint, childSchema, childTable, childCol,
            parentSchema, parentTable, parentCol, onDelete, ReferentialAction.NoAction);

    // Build a complete table
    public static TableMetadata Table(
        string schema, string name,
        IReadOnlyList<ColumnMetadata> columns,
        IReadOnlyList<ForeignKeyRelation>? outbound = null,
        IReadOnlyList<ForeignKeyRelation>? inbound  = null) =>
        new(schema, name, TableKind.Table, 1000, columns,
            Array.Empty<IndexMetadata>(),
            outbound ?? Array.Empty<ForeignKeyRelation>(),
            inbound  ?? Array.Empty<ForeignKeyRelation>());

    /// <summary>
    /// Builds a canonical e-commerce schema:
    /// customers ←── orders ←── order_items ──→ products
    /// </summary>
    public static DbMetadata EcommerceDb()
    {
        var fk_orders_customers = Fk(
            "public", "orders",      "customer_id",
            "public", "customers",   "id",
            ReferentialAction.Restrict,
            "fk_orders_customer");

        var fk_items_orders = Fk(
            "public", "order_items", "order_id",
            "public", "orders",      "id",
            ReferentialAction.Cascade,
            "fk_items_order");

        var fk_items_products = Fk(
            "public", "order_items", "product_id",
            "public", "products",    "id",
            ReferentialAction.Restrict,
            "fk_items_product");

        var allFks = new[] { fk_orders_customers, fk_items_orders, fk_items_products };

        var customers = Table("public", "customers", new[]
        {
            Col("id",    isPk: true, isNullable: false),
            Col("name",  type: "varchar"),
            Col("email", type: "varchar", isUnique: true, isIndexed: true)
        },
        inbound: new[] { fk_orders_customers });

        var orders = Table("public", "orders", new[]
        {
            Col("id",          isPk: true, isNullable: false),
            Col("customer_id", isFk: true, isNullable: false, isIndexed: true),
            Col("total",       type: "decimal"),
            Col("status",      type: "varchar")
        },
        outbound: new[] { fk_orders_customers },
        inbound:  new[] { fk_items_orders });

        var orderItems = Table("public", "order_items", new[]
        {
            Col("id",         isPk: true, isNullable: false),
            Col("order_id",   isFk: true, isNullable: false, isIndexed: true),
            Col("product_id", isFk: true, isNullable: false, isIndexed: true),
            Col("qty",        type: "integer"),
            Col("price",      type: "decimal")
        },
        outbound: new[] { fk_items_orders, fk_items_products });

        var products = Table("public", "products", new[]
        {
            Col("id",    isPk: true, isNullable: false),
            Col("sku",   type: "varchar", isUnique: true),
            Col("name",  type: "varchar"),
            Col("price", type: "decimal")
        },
        inbound: new[] { fk_items_products });

        var schema = new SchemaMetadata("public",
            new[] { customers, orders, orderItems, products });

        return new DbMetadata(
            "ecommerce", DatabaseProvider.Postgres, "PostgreSQL 16",
            DateTimeOffset.UtcNow,
            new[] { schema }, allFks);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DbMetadata model tests
// ─────────────────────────────────────────────────────────────────────────────

public class DbMetadataModelTests
{
    private readonly DbMetadata _db = MetadataFixtures.EcommerceDb();

    [Fact]
    public void FindTable_ReturnsTable_CaseInsensitive()
    {
        var t = _db.FindTable("public.Orders");
        Assert.NotNull(t);
        Assert.Equal("orders", t!.Name);
    }

    [Fact]
    public void FindTable_BySchemaAndName_Works()
    {
        var t = _db.FindTable("public", "products");
        Assert.NotNull(t);
    }

    [Fact]
    public void FindTable_Missing_ReturnsNull()
    {
        Assert.Null(_db.FindTable("public.nonexistent"));
    }

    [Fact]
    public void AllTables_Count_IsCorrect()
    {
        Assert.Equal(4, _db.TotalTables);
    }

    [Fact]
    public void AllForeignKeys_Count_IsCorrect()
    {
        Assert.Equal(3, _db.TotalForeignKeys);
    }

    [Fact]
    public void GetRelationsBetween_DirectFK_Found()
    {
        var rels = _db.GetRelationsBetween("public.orders", "public.customers");
        Assert.Single(rels);
        Assert.Equal("customer_id", rels[0].ChildColumn);
    }

    [Fact]
    public void GetRelationsBetween_ReverseDirection_AlsoFound()
    {
        // Same query, reversed argument order
        var rels = _db.GetRelationsBetween("public.customers", "public.orders");
        Assert.Single(rels);
    }

    [Fact]
    public void GetRelationsToCanvas_ReturnsOnlyCanvasMatches()
    {
        var canvas  = new[] { "public.customers", "public.products" };
        var relOrders = _db.GetRelationsToCanvas("public.orders", canvas);

        // orders→customers is a canvas match; orders→order_items is NOT (not on canvas)
        Assert.Single(relOrders);
        Assert.Equal("fk_orders_customer", relOrders[0].ConstraintName);
    }

    [Fact]
    public void TableMetadata_ReferencedTables_Correct()
    {
        var orders = _db.FindTable("public", "orders")!;
        Assert.Contains("public.customers", orders.ReferencedTables,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TableMetadata_ReferencingTables_Correct()
    {
        var customers = _db.FindTable("public", "customers")!;
        Assert.Contains("public.orders", customers.ReferencingTables,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ColumnSemanticType_Inference_Correct()
    {
        var intCol  = MetadataFixtures.Col("qty",  "integer");
        var textCol = MetadataFixtures.Col("name", "varchar");
        var dtCol   = MetadataFixtures.Col("ts",   "timestamp");
        var boolCol = MetadataFixtures.Col("flag", "boolean");

        Assert.Equal(ColumnSemanticType.Numeric,  intCol.SemanticType);
        Assert.Equal(ColumnSemanticType.Text,     textCol.SemanticType);
        Assert.Equal(ColumnSemanticType.DateTime, dtCol.SemanticType);
        Assert.Equal(ColumnSemanticType.Boolean,  boolCol.SemanticType);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// ForeignKeyRelation tests
// ─────────────────────────────────────────────────────────────────────────────

public class ForeignKeyRelationTests
{
    [Fact]
    public void Involves_BothDirections_ReturnsTrue()
    {
        var fk = MetadataFixtures.Fk("public", "orders", "customer_id",
                                     "public", "customers", "id");

        Assert.True(fk.Involves("public.orders", "public.customers"));
        Assert.True(fk.Involves("public.customers", "public.orders"));
    }

    [Fact]
    public void Involves_UnrelatedTable_ReturnsFalse()
    {
        var fk = MetadataFixtures.Fk("public", "orders", "customer_id",
                                     "public", "customers", "id");

        Assert.False(fk.Involves("public.orders", "public.products"));
    }

    [Fact]
    public void ToJoinOnClause_FormatsCorrectly()
    {
        var fk = MetadataFixtures.Fk("public", "orders", "customer_id",
                                     "public", "customers", "id");
        Assert.Equal("public.orders.customer_id = public.customers.id",
            fk.ToJoinOnClause());
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// AutoJoinDetector tests
// ─────────────────────────────────────────────────────────────────────────────

public class AutoJoinDetectorTests
{
    private readonly DbMetadata _db    = MetadataFixtures.EcommerceDb();
    private readonly AutoJoinDetector _sut;

    public AutoJoinDetectorTests() => _sut = new AutoJoinDetector(_db);

    // ── Catalog FK detection ──────────────────────────────────────────────────

    [Fact]
    public void Suggest_DirectFK_ReturnsHighConfidence()
    {
        // orders.customer_id → customers.id (FK defined in catalog)
        var canvas = new[] { "public.customers" };
        var results = _sut.Suggest("public.orders", canvas);

        Assert.NotEmpty(results);
        var top = results[0];
        Assert.True(top.Score >= 0.9);
        Assert.Equal(JoinConfidence.CatalogDefinedFk, top.Confidence);
        Assert.Contains("customer_id", top.LeftColumn);
    }

    [Fact]
    public void Suggest_ReverseFK_ReturnsHighConfidence()
    {
        // Drop 'orders' onto canvas that already has 'order_items'
        // (order_items.order_id → orders.id — orders is the parent)
        var canvas = new[] { "public.order_items" };
        var results = _sut.Suggest("public.orders", canvas);

        Assert.NotEmpty(results);
        var catalogResult = results.FirstOrDefault(r =>
            r.Confidence >= JoinConfidence.CatalogDefinedReverse);
        Assert.NotNull(catalogResult);
    }

    [Fact]
    public void Suggest_CatalogFK_HasSourceFkPopulated()
    {
        var results = _sut.Suggest("public.orders", new[] { "public.customers" });
        Assert.NotNull(results[0].SourceFk);
        Assert.Equal("fk_orders_customer", results[0].SourceFk!.ConstraintName);
    }

    [Fact]
    public void Suggest_OnClause_IsWellFormed()
    {
        var results = _sut.Suggest("public.orders", new[] { "public.customers" });
        var clause = results[0].OnClause;

        Assert.Contains("=", clause);
        Assert.Contains("customer_id", clause);
        Assert.Contains("customers", clause);
    }

    [Fact]
    public void Suggest_CascadeDelete_RecommendsInnerJoin()
    {
        // fk_items_order has ON DELETE CASCADE → should suggest INNER
        var canvas  = new[] { "public.orders" };
        var results = _sut.Suggest("public.order_items", canvas);
        var catalogResult = results.First(r => r.SourceFk?.ConstraintName == "fk_items_order");
        Assert.Equal("INNER", catalogResult.JoinType);
    }

    [Fact]
    public void Suggest_NonCascadeDelete_RecommendsLeftJoin()
    {
        // fk_orders_customer has ON DELETE RESTRICT → should suggest LEFT
        var canvas  = new[] { "public.customers" };
        var results = _sut.Suggest("public.orders", canvas);
        Assert.Equal("LEFT", results[0].JoinType);
    }

    // ── No canvas / unknown table ─────────────────────────────────────────────

    [Fact]
    public void Suggest_EmptyCanvas_ReturnsEmpty()
    {
        var results = _sut.Suggest("public.orders", Enumerable.Empty<string>());
        Assert.Empty(results);
    }

    [Fact]
    public void Suggest_UnknownTable_ReturnsEmpty()
    {
        var results = _sut.Suggest("public.ghost_table", new[] { "public.orders" });
        Assert.Empty(results);
    }

    // ── Deduplication ─────────────────────────────────────────────────────────

    [Fact]
    public void Suggest_MultipleCanvasTables_DeduplicatesColumnPairs()
    {
        var canvas  = new[] { "public.customers", "public.products" };
        var results = _sut.Suggest("public.order_items", canvas);

        // Each (leftCol, rightCol) pair should appear at most once
        var keys = results.Select(r => $"{r.LeftColumn}|{r.RightColumn}").ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ── Naming heuristic tests ────────────────────────────────────────────────

    [Fact]
    public void Suggest_NamingHeuristic_DetectsConvention()
    {
        // Build a schema where there is NO catalog FK, but column names imply a join
        var fkOrders = MetadataFixtures.Fk("app", "invoices", "customer_id",
                                            "app", "clients", "id");

        // No FK defined — remove outbound FK from the table
        var invoices = MetadataFixtures.Table("app", "invoices", new[]
        {
            MetadataFixtures.Col("id",          isPk: true),
            MetadataFixtures.Col("customer_id", isFk: false), // no FK in catalog
        });

        var clients = MetadataFixtures.Table("app", "clients", new[]
        {
            MetadataFixtures.Col("id",   isPk: true),
            MetadataFixtures.Col("name", type: "varchar")
        });

        var schema  = new SchemaMetadata("app", new[] { invoices, clients });
        var db = new DbMetadata("test", DatabaseProvider.Postgres, "v16",
            DateTimeOffset.UtcNow, new[] { schema },
            Array.Empty<ForeignKeyRelation>());   // ← no catalog FKs

        var detector = new AutoJoinDetector(db);
        var results  = detector.Suggest("app.invoices", new[] { "app.clients" });

        // Should still detect via naming heuristic (invoices.customer_id → clients.id)
        Assert.NotEmpty(results);
        Assert.True(results[0].Score >= 0.7);
        Assert.Equal(JoinConfidence.HeuristicStrong, results[0].Confidence);
    }

    // ── Sorted order ──────────────────────────────────────────────────────────

    [Fact]
    public void Suggest_ResultsAreSortedByScoreDescending()
    {
        var canvas  = new[] { "public.customers", "public.products" };
        var results = _sut.Suggest("public.order_items", canvas);

        for (int i = 0; i < results.Count - 1; i++)
            Assert.True(results[i].Score >= results[i + 1].Score,
                $"Suggestions not sorted: [{i}]={results[i].Score} < [{i+1}]={results[i+1].Score}");
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Singulariser tests
// ─────────────────────────────────────────────────────────────────────────────

public class SingulariserTests
{
    [Theory]
    [InlineData("orders",      "order")]
    [InlineData("customers",   "customer")]
    [InlineData("products",    "product")]
    [InlineData("categories",  "category")]
    [InlineData("addresses",   "address")]
    [InlineData("boxes",       "box")]
    [InlineData("buses",       "bus")]
    [InlineData("status",      "status")]   // no change — ends in 'ss' guard
    [InlineData("user",        "user")]     // already singular
    public void Singularize_ConvertsCorrectly(string plural, string expected)
    {
        Assert.Equal(expected, AutoJoinDetector.Singularize(plural));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// JoinSuggestion → JoinDefinition conversion
// ─────────────────────────────────────────────────────────────────────────────

public class JoinSuggestionConversionTests
{
    [Fact]
    public void ToJoinDefinition_MapsFieldsCorrectly()
    {
        var db      = MetadataFixtures.EcommerceDb();
        var sut     = new AutoJoinDetector(db);
        var results = sut.Suggest("public.orders", new[] { "public.customers" });

        Assert.NotEmpty(results);
        var def = results[0].ToJoinDefinition();

        Assert.Equal("public.orders",  def.TargetTable);
        Assert.Equal(results[0].JoinType, def.Type);
    }
}
