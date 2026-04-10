

namespace Fixtures;

/// <summary>
/// Shared test fixtures used across all test suites
/// </summary>
internal static class TestFixtures
{
    // ─────────────────────────────────────────────────────────────────────────
    // NODE FIXTURES
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an EmitContext for a given database provider
    /// </summary>
    public static EmitContext CreateEmitContext(DatabaseProvider provider) =>
        new(provider, new SqlFunctionRegistry(provider));

    public static class Node
    {
        public static EmitContext PostgresContext => CreateEmitContext(DatabaseProvider.Postgres);
        public static EmitContext MySqlContext => CreateEmitContext(DatabaseProvider.MySql);
        public static EmitContext SqlServerContext => CreateEmitContext(DatabaseProvider.SqlServer);

        /// <summary>
        /// Creates a ColumnExpr for the given table and column
        /// </summary>
        public static ColumnExpr Column(
            string table,
            string column,
            PinDataType type = PinDataType.Expression
        ) => new(table, column, type);

        public static ColumnExpr OrderTotal => Column("orders", "total", PinDataType.Number);
        public static ColumnExpr UserEmail => Column("users", "email", PinDataType.Text);
        public static ColumnExpr EventPayload => Column("events", "payload", PinDataType.Json);
        public static ColumnExpr ProductDescription =>
            Column("products", "description", PinDataType.Text);
        public static ColumnExpr ProductPrice => Column("products", "price", PinDataType.Number);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // METADATA FIXTURES
    // ─────────────────────────────────────────────────────────────────────────

    public static class Metadata
    {
        /// <summary>
        /// Creates a ColumnMetadata with sensible defaults
        /// </summary>
        public static ColumnMetadata Column(
            string name,
            string type = "integer",
            bool isPk = false,
            bool isFk = false,
            bool isNullable = true,
            bool isUnique = false,
            bool isIndexed = false
        ) => new(name, type, type, isNullable, isPk, isFk, isUnique, isIndexed, 1);

        /// <summary>
        /// Creates a ForeignKeyRelation
        /// </summary>
        public static ForeignKeyRelation ForeignKey(
            string childSchema,
            string childTable,
            string childColumn,
            string parentSchema,
            string parentTable,
            string parentColumn,
            ReferentialAction onDelete = ReferentialAction.NoAction,
            string constraintName = "fk_test"
        ) =>
            new(
                constraintName,
                childSchema,
                childTable,
                childColumn,
                parentSchema,
                parentTable,
                parentColumn,
                onDelete,
                ReferentialAction.NoAction
            );

        /// <summary>
        /// Creates a complete TableMetadata
        /// </summary>
        public static TableMetadata Table(
            string schema,
            string name,
            IReadOnlyList<ColumnMetadata> columns,
            IReadOnlyList<ForeignKeyRelation>? outboundFks = null,
            IReadOnlyList<ForeignKeyRelation>? inboundFks = null
        ) =>
            new(
                schema,
                name,
                TableKind.Table,
                1000,
                columns,
                [],
                outboundFks ?? [],
                inboundFks ?? []
            );

        /// <summary>
        /// Builds a canonical e-commerce schema for testing:
        /// customers ←── orders ←── order_items ──→ products
        /// </summary>
        public static DbMetadata CreateEcommerceSchema()
        {
            ForeignKeyRelation fkOrdersCustomers = ForeignKey(
                "public",
                "orders",
                "customer_id",
                "public",
                "customers",
                "id",
                ReferentialAction.Restrict,
                "fk_orders_customer"
            );

            ForeignKeyRelation fkOrderItemsOrders = ForeignKey(
                "public",
                "order_items",
                "order_id",
                "public",
                "orders",
                "id",
                ReferentialAction.Cascade,
                "fk_order_items_order"
            );

            ForeignKeyRelation fkOrderItemsProducts = ForeignKey(
                "public",
                "order_items",
                "product_id",
                "public",
                "products",
                "id",
                ReferentialAction.Restrict,
                "fk_order_items_product"
            );

            TableMetadata customersTable = Table(
                "public",
                "customers",
                [
                    Column("id", "integer", isPk: true, isNullable: false),
                    Column("name", "text", isNullable: false),
                    Column("email", "text", isUnique: true, isIndexed: true),
                    Column("created_at", "timestamp", isNullable: false),
                ]
            );

            TableMetadata ordersTable = Table(
                "public",
                "orders",
                [
                    Column("id", "integer", isPk: true, isNullable: false),
                    Column(
                        "customer_id",
                        "integer",
                        isFk: true,
                        isNullable: false,
                        isIndexed: true
                    ),
                    Column("total", "numeric", isNullable: false),
                    Column("created_at", "timestamp", isNullable: false),
                ],
                outboundFks: [fkOrdersCustomers]
            );

            TableMetadata productsTable = Table(
                "public",
                "products",
                [
                    Column("id", "integer", isPk: true, isNullable: false),
                    Column("name", "text", isNullable: false, isIndexed: true),
                    Column("price", "numeric", isNullable: false),
                    Column("stock", "integer", isNullable: false),
                    Column("description", "text", isNullable: true),
                ]
            );

            TableMetadata orderItemsTable = Table(
                "public",
                "order_items",
                [
                    Column("id", "integer", isPk: true, isNullable: false),
                    Column("order_id", "integer", isFk: true, isNullable: false, isIndexed: true),
                    Column("product_id", "integer", isFk: true, isNullable: false, isIndexed: true),
                    Column("quantity", "integer", isNullable: false),
                    Column("unit_price", "numeric", isNullable: false),
                ],
                outboundFks: [fkOrderItemsOrders, fkOrderItemsProducts]
            );

            var publicSchema = new SchemaMetadata(
                "public",
                [customersTable, ordersTable, productsTable, orderItemsTable]
            );

            ForeignKeyRelation[] allFks = new[]
            {
                fkOrdersCustomers,
                fkOrderItemsOrders,
                fkOrderItemsProducts,
            };

            return new DbMetadata(
                "test_db",
                DatabaseProvider.Postgres,
                "14.0",
                DateTimeOffset.UtcNow,
                [publicSchema],
                allFks
            );
        }
    }
}
