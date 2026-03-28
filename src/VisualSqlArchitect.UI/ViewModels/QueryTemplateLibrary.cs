using Avalonia;
using VisualSqlArchitect.Nodes;

namespace VisualSqlArchitect.UI.ViewModels;

// ── Template descriptor ───────────────────────────────────────────────────────

public sealed record QueryTemplate(
    string Name,
    string Description,
    string Category,
    string Tags,
    Action<CanvasViewModel> Build
);

// ── Template library ──────────────────────────────────────────────────────────

/// <summary>
/// Built-in catalogue of canvas templates.
/// Each template wires a set of demo-catalog nodes into a ready-to-use flow.
/// After loading, the canvas is clean (undo history cleared, IsDirty = false).
/// </summary>
public static class QueryTemplateLibrary
{
    public static IReadOnlyList<QueryTemplate> All { get; } = BuildAll();

    // ── Lookup helpers ────────────────────────────────────────────────────────

    private static NodeViewModel Table(string fullName, Point pos)
    {
        (string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols) =
            CanvasViewModel.DemoCatalog.First(t => t.FullName == fullName);
        return new NodeViewModel(fullName, Cols, pos);
    }

    private static NodeViewModel Node(NodeType type, Point pos, string? alias = null)
    {
        var vm = new NodeViewModel(NodeDefinitionRegistry.Get(type), pos);
        if (alias is not null)
            vm.Alias = alias;
        return vm;
    }

    private static ConnectionViewModel Wire(
        NodeViewModel from,
        string fromPin,
        NodeViewModel to,
        string toPin
    )
    {
        PinViewModel fp = from.OutputPins.First(p => p.Name == fromPin);
        PinViewModel tp = to.InputPins.First(p => p.Name == toPin);
        return new ConnectionViewModel(fp, default, default) { ToPin = tp };
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    private static IReadOnlyList<QueryTemplate> BuildAll() =>
        [
            // ── 1. Simple SELECT ──────────────────────────────────────────────────
            new(
                Name: "Simple SELECT",
                Description: "Select all columns from a single table",
                Category: "Basic",
                Tags: "select basic all columns table starter",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(80, 100));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(500, 100));

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(result);

                    foreach (PinViewModel pin in orders.OutputPins)
                        canvas.Connections.Add(Wire(orders, pin.Name, result, "columns"));
                }
            ),
            // ── 2. Filtered SELECT (WHERE status = 'ACTIVE') ──────────────────────
            new(
                Name: "Filtered SELECT",
                Description: "Select with a WHERE equality filter on status",
                Category: "Basic",
                Tags: "select where filter equals condition basic",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(60, 100));
                    NodeViewModel eq = Node(
                        NodeType.Equals,
                        new Point(380, 260),
                        alias: "status_filter"
                    );
                    NodeViewModel where = Node(NodeType.WhereOutput, new Point(600, 260));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(600, 80));

                    eq.PinLiterals["right"] = "ACTIVE";

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(eq);
                    canvas.Nodes.Add(where);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(orders, "id", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "status", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "total", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "created_at", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "status", eq, "left"));
                    canvas.Connections.Add(Wire(eq, "result", where, "condition"));
                }
            ),
            // ── 3. JOIN (Orders + Customers) ──────────────────────────────────────
            new(
                Name: "JOIN Orders + Customers",
                Description: "Inner join orders to customers via customer_id",
                Category: "Join",
                Tags: "join inner two tables foreign key orders customers",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(60, 80));
                    NodeViewModel customers = Table("public.customers", new Point(60, 320));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(560, 180));

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(customers);
                    canvas.Nodes.Add(result);

                    // Select order fields
                    canvas.Connections.Add(Wire(orders, "id", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "status", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "total", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "created_at", result, "columns"));
                    // Select customer fields
                    canvas.Connections.Add(Wire(customers, "name", result, "columns"));
                    canvas.Connections.Add(Wire(customers, "email", result, "columns"));
                    canvas.Connections.Add(Wire(customers, "city", result, "columns"));
                    // Join key wire
                    canvas.Connections.Add(Wire(orders, "customer_id", customers, "id"));
                }
            ),
            // ── 4. COUNT Aggregate ────────────────────────────────────────────────
            new(
                Name: "COUNT Aggregate",
                Description: "Count all rows from the orders table",
                Category: "Aggregate",
                Tags: "count aggregate group all rows star",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(80, 120));
                    NodeViewModel count = Node(
                        NodeType.CountStar,
                        new Point(400, 120),
                        alias: "total_orders"
                    );
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(640, 120));

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(count);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(count, "count", result, "columns"));
                }
            ),
            // ── 5. SUM + GROUP aggregate ──────────────────────────────────────────
            new(
                Name: "SUM by Status",
                Description: "Sum order totals grouped by status (SUM + ALIAS)",
                Category: "Aggregate",
                Tags: "sum aggregate group by status total amount",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(60, 120));
                    NodeViewModel sum = Node(
                        NodeType.Sum,
                        new Point(380, 200),
                        alias: "total_revenue"
                    );
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(620, 120));

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(sum);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(orders, "total", sum, "value"));
                    canvas.Connections.Add(Wire(orders, "status", result, "columns"));
                    canvas.Connections.Add(Wire(sum, "total", result, "columns"));
                }
            ),
            // ── 6. String Transform (UPPER + ALIAS) ───────────────────────────────
            new(
                Name: "String Transform",
                Description: "Apply UPPER() to customer name and alias the result",
                Category: "Transform",
                Tags: "string upper transform alias rename column text",
                Build: canvas =>
                {
                    NodeViewModel customers = Table("public.customers", new Point(60, 100));
                    NodeViewModel upper = Node(
                        NodeType.Upper,
                        new Point(380, 120),
                        alias: "name_upper"
                    );
                    NodeViewModel alias_em = Node(
                        NodeType.Alias,
                        new Point(380, 220),
                        alias: "email_clean"
                    );
                    NodeViewModel trim = Node(NodeType.Trim, new Point(560, 220));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(760, 120));

                    alias_em.Parameters["alias"] = "email_clean";
                    trim.Alias = "email_trimmed";

                    canvas.Nodes.Add(customers);
                    canvas.Nodes.Add(upper);
                    canvas.Nodes.Add(alias_em);
                    canvas.Nodes.Add(trim);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(customers, "name", upper, "text"));
                    canvas.Connections.Add(Wire(customers, "email", alias_em, "expression"));
                    canvas.Connections.Add(Wire(alias_em, "result", trim, "text"));
                    canvas.Connections.Add(Wire(upper, "result", result, "columns"));
                    canvas.Connections.Add(Wire(trim, "result", result, "columns"));
                    canvas.Connections.Add(Wire(customers, "city", result, "columns"));
                    canvas.Connections.Add(Wire(customers, "country", result, "columns"));
                }
            ),
            // ── 7. Date Range Filter ──────────────────────────────────────────────
            new(
                Name: "Date Range Filter",
                Description: "Filter orders by date range using BETWEEN",
                Category: "Filter",
                Tags: "date range between filter where datetime created_at",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(60, 100));
                    NodeViewModel between = Node(NodeType.Between, new Point(380, 240));
                    NodeViewModel where = Node(NodeType.WhereOutput, new Point(620, 240));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(620, 80));

                    between.PinLiterals["low"] = "2024-01-01";
                    between.PinLiterals["high"] = "2024-12-31";

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(between);
                    canvas.Nodes.Add(where);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(orders, "id", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "status", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "total", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "created_at", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "created_at", between, "value"));
                    canvas.Connections.Add(Wire(between, "result", where, "condition"));
                }
            ),
            // ── 8. NULL Safety with COALESCE ─────────────────────────────────────
            new(
                Name: "NULL Safety (COALESCE)",
                Description: "Replace NULL values with a fallback using NullFill / COALESCE",
                Category: "Transform",
                Tags: "null coalesce nullfill fallback default safety replace",
                Build: canvas =>
                {
                    NodeViewModel orders = Table("public.orders", new Point(60, 100));
                    NodeViewModel nullFill = Node(
                        NodeType.NullFill,
                        new Point(380, 160),
                        alias: "safe_status"
                    );

                    nullFill.Parameters["fallback"] = "UNKNOWN";

                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(640, 100));

                    canvas.Nodes.Add(orders);
                    canvas.Nodes.Add(nullFill);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(orders, "id", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "status", nullFill, "value"));
                    canvas.Connections.Add(Wire(nullFill, "result", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "total", result, "columns"));
                    canvas.Connections.Add(Wire(orders, "created_at", result, "columns"));
                }
            ),
            // ── 9. Products: Price Range + Order Items JOIN ───────────────────────
            new(
                Name: "Product Catalog Filter",
                Description: "Filter products by price range using BETWEEN",
                Category: "Filter",
                Tags: "products price range filter between catalog",
                Build: canvas =>
                {
                    NodeViewModel products = Table("public.products", new Point(60, 100));
                    NodeViewModel between = Node(NodeType.Between, new Point(380, 220));
                    NodeViewModel where = Node(NodeType.WhereOutput, new Point(620, 220));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(620, 80));

                    between.PinLiterals["low"] = "10";
                    between.PinLiterals["high"] = "500";

                    canvas.Nodes.Add(products);
                    canvas.Nodes.Add(between);
                    canvas.Nodes.Add(where);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(products, "id", result, "columns"));
                    canvas.Connections.Add(Wire(products, "name", result, "columns"));
                    canvas.Connections.Add(Wire(products, "category", result, "columns"));
                    canvas.Connections.Add(Wire(products, "price", result, "columns"));
                    canvas.Connections.Add(Wire(products, "price", between, "value"));
                    canvas.Connections.Add(Wire(between, "result", where, "condition"));
                }
            ),
            // ── 10. Employees by Department ───────────────────────────────────────
            new(
                Name: "Employees by Department",
                Description: "Filter employees by a specific department with LIKE",
                Category: "Filter",
                Tags: "employees department like filter hr staff",
                Build: canvas =>
                {
                    NodeViewModel employees = Table("public.employees", new Point(60, 100));
                    NodeViewModel like = Node(NodeType.Like, new Point(380, 240));
                    NodeViewModel where = Node(NodeType.WhereOutput, new Point(620, 240));
                    NodeViewModel result = Node(NodeType.ResultOutput, new Point(620, 80));

                    like.Parameters["pattern"] = "Engineering%";

                    canvas.Nodes.Add(employees);
                    canvas.Nodes.Add(like);
                    canvas.Nodes.Add(where);
                    canvas.Nodes.Add(result);

                    canvas.Connections.Add(Wire(employees, "id", result, "columns"));
                    canvas.Connections.Add(Wire(employees, "name", result, "columns"));
                    canvas.Connections.Add(Wire(employees, "department", result, "columns"));
                    canvas.Connections.Add(Wire(employees, "salary", result, "columns"));
                    canvas.Connections.Add(Wire(employees, "hire_date", result, "columns"));
                    canvas.Connections.Add(Wire(employees, "department", like, "text"));
                    canvas.Connections.Add(Wire(like, "result", where, "condition"));
                }
            ),
        ];
}
