using VisualSqlArchitect.Core;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.QueryEngine;
using VisualSqlArchitect.Registry;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.Nodes;

// ─── Shared fixtures ─────────────────────────────────────────────────────────

internal static class NodeFixtures
{
    public static EmitContext Ctx(DatabaseProvider p) =>
        new(p, new SqlFunctionRegistry(p));

    public static EmitContext Postgres  => Ctx(DatabaseProvider.Postgres);
    public static EmitContext MySQL     => Ctx(DatabaseProvider.MySql);
    public static EmitContext SqlServer => Ctx(DatabaseProvider.SqlServer);

    // A ColumnExpr for orders.total
    public static ColumnExpr OrderTotal =>
        new("orders", "total", PinDataType.Number);

    // A ColumnExpr for users.email
    public static ColumnExpr UserEmail =>
        new("users", "email", PinDataType.Text);

    // A ColumnExpr for events.payload (JSON)
    public static ColumnExpr EventPayload =>
        new("events", "payload", PinDataType.Json);
}

// ═════════════════════════════════════════════════════════════════════════════
// EMIT CONTEXT
// ═════════════════════════════════════════════════════════════════════════════

public class EmitContextTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres,  "\"my_col\"")]
    [InlineData(DatabaseProvider.MySql,     "`my_col`")]
    [InlineData(DatabaseProvider.SqlServer, "[my_col]")]
    public void QuoteIdentifier_ProducesCorrectDialect(DatabaseProvider p, string expected)
    {
        var ctx = NodeFixtures.Ctx(p);
        Assert.Equal(expected, ctx.QuoteIdentifier("my_col"));
    }

    [Fact]
    public void QuoteLiteral_EscapesSingleQuotes()
    {
        var ctx = NodeFixtures.Postgres;
        Assert.Equal("'O''Brien'", ctx.QuoteLiteral("O'Brien"));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// LEAF EXPRESSIONS
// ═════════════════════════════════════════════════════════════════════════════

public class LeafExpressionTests
{
    [Fact]
    public void ColumnExpr_EmitsQualifiedName()
    {
        var expr = new ColumnExpr("users", "email");
        var sql  = expr.Emit(NodeFixtures.Postgres);
        Assert.Equal("\"users\".\"email\"", sql);
    }

    [Fact]
    public void ColumnExpr_NoTable_EmitsUnqualified()
    {
        var expr = new ColumnExpr("", "name");
        Assert.Equal("\"name\"", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void LiteralExpr_PassesRawValueThrough()
    {
        Assert.Equal("42", new LiteralExpr("42").Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void StringLiteralExpr_WrapsInSingleQuotes()
    {
        Assert.Equal("'hello'", new StringLiteralExpr("hello").Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void NumberLiteralExpr_UsesInvariantCulture()
    {
        // Must use '.' not ',' regardless of thread culture
        Assert.Equal("3.14", new NumberLiteralExpr(3.14).Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void NullExpr_EmitsNULL()
    {
        Assert.Equal("NULL", NullExpr.Instance.Emit(NodeFixtures.Postgres));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// STRING TRANSFORM NODES (via FunctionCallExpr)
// ═════════════════════════════════════════════════════════════════════════════

public class StringTransformTests
{
    [Theory]
    [InlineData(DatabaseProvider.Postgres,  "UPPER(\"users\".\"email\")")]
    [InlineData(DatabaseProvider.MySql,     "UPPER(`users`.`email`)")]
    [InlineData(DatabaseProvider.SqlServer, "UPPER([users].[email])")]
    public void Upper_AllProviders(DatabaseProvider p, string expected)
    {
        var expr = new FunctionCallExpr(SqlFn.Upper,
            new[] { NodeFixtures.UserEmail }, PinDataType.Text);
        Assert.Equal(expected, expr.Emit(NodeFixtures.Ctx(p)));
    }

    [Fact]
    public void Lower_Postgres()
    {
        var expr = new FunctionCallExpr(SqlFn.Lower,
            new[] { NodeFixtures.UserEmail }, PinDataType.Text);
        Assert.Equal("LOWER(\"users\".\"email\")", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Trim_Postgres()
    {
        var expr = new FunctionCallExpr(SqlFn.Trim,
            new[] { NodeFixtures.UserEmail }, PinDataType.Text);
        Assert.Equal("TRIM(\"users\".\"email\")", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Length_MySQL_UsesCHAR_LENGTH()
    {
        var expr = new FunctionCallExpr(SqlFn.Length,
            new[] { NodeFixtures.UserEmail }, PinDataType.Number);
        Assert.Equal("CHAR_LENGTH(`users`.`email`)", expr.Emit(NodeFixtures.MySQL));
    }

    [Fact]
    public void Length_SqlServer_UsesLEN()
    {
        var expr = new FunctionCallExpr(SqlFn.Length,
            new[] { NodeFixtures.UserEmail }, PinDataType.Number);
        Assert.Equal("LEN([users].[email])", expr.Emit(NodeFixtures.SqlServer));
    }

    [Fact]
    public void Concat_ThreeArgs_AllProviders()
    {
        var a = new StringLiteralExpr("Hello ");
        var b = NodeFixtures.UserEmail as ISqlExpression;
        var c = new StringLiteralExpr("!");
        var expr = new FunctionCallExpr(SqlFn.Concat, new[] { a, b, c }, PinDataType.Text);

        foreach (var p in new[] { DatabaseProvider.Postgres, DatabaseProvider.MySql, DatabaseProvider.SqlServer })
            Assert.StartsWith("CONCAT(", expr.Emit(NodeFixtures.Ctx(p)));
    }

    [Fact]
    public void RegexMatch_Postgres_UsesTilde()
    {
        var ctx = NodeFixtures.Postgres;
        var sql = ctx.Registry.GetFunction(SqlFn.Regex,
            "\"users\".\"email\"", "'@corp\\.io'");
        Assert.Equal("\"users\".\"email\" ~ '@corp\\.io'", sql);
    }

    [Fact]
    public void RegexMatch_MySQL_UsesREGEXP()
    {
        var ctx = NodeFixtures.MySQL;
        var sql = ctx.Registry.GetFunction(SqlFn.Regex, "`users`.`email`", "'@corp'");
        Assert.Equal("`users`.`email` REGEXP '@corp'", sql);
    }

    [Fact]
    public void RegexMatch_SqlServer_UsesPATINDEX()
    {
        var ctx = NodeFixtures.SqlServer;
        var sql = ctx.Registry.GetFunction(SqlFn.Regex, "[users].[email]", "'%@corp%'");
        Assert.Contains("PATINDEX", sql);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// MATH NODES
// ═════════════════════════════════════════════════════════════════════════════

public class MathNodeTests
{
    [Fact]
    public void Arithmetic_Add_ProducesParenthesised()
    {
        var expr = new RawSqlExpr(
            $"({NodeFixtures.OrderTotal.Emit(NodeFixtures.Postgres)} + 10)",
            PinDataType.Number);
        Assert.Equal("(\"orders\".\"total\" + 10)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void AggregateExpr_Sum_WrapsInSUM()
    {
        var expr = new AggregateExpr(AggregateFunction.Sum, NodeFixtures.OrderTotal);
        Assert.Equal("SUM(\"orders\".\"total\")", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void AggregateExpr_CountStar_EmitsCountStar()
    {
        var expr = new AggregateExpr(AggregateFunction.Count, null);
        Assert.Equal("COUNT(*)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void AggregateExpr_CountDistinct_IncludesDistinct()
    {
        var expr = new AggregateExpr(AggregateFunction.Count, NodeFixtures.UserEmail, Distinct: true);
        Assert.Contains("DISTINCT", expr.Emit(NodeFixtures.Postgres));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// CAST NODE
// ═════════════════════════════════════════════════════════════════════════════

public class CastNodeTests
{
    [Fact]
    public void Cast_ToText_Postgres_EmitsTEXT()
    {
        var expr = new CastExpr(NodeFixtures.OrderTotal, CastTargetType.Text);
        Assert.Equal("CAST(\"orders\".\"total\" AS TEXT)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Cast_ToText_SqlServer_EmitsNVARCHAR()
    {
        var expr = new CastExpr(NodeFixtures.OrderTotal, CastTargetType.Text);
        Assert.Contains("NVARCHAR", expr.Emit(NodeFixtures.SqlServer));
    }

    [Fact]
    public void Cast_ToBoolean_SqlServer_EmitsBIT()
    {
        var expr = new CastExpr(new LiteralExpr("1"), CastTargetType.Boolean);
        Assert.Equal("CAST(1 AS BIT)", expr.Emit(NodeFixtures.SqlServer));
    }

    [Fact]
    public void Cast_ToTimestamp_Postgres_EmitsTIMESTAMPTZ()
    {
        var expr = new CastExpr(new StringLiteralExpr("2024-01-01"), CastTargetType.Timestamp);
        Assert.Contains("TIMESTAMPTZ", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Cast_OutputType_MatchesTarget()
    {
        Assert.Equal(PinDataType.Text,   new CastExpr(NullExpr.Instance, CastTargetType.Text).OutputType);
        Assert.Equal(PinDataType.Number, new CastExpr(NullExpr.Instance, CastTargetType.Integer).OutputType);
        Assert.Equal(PinDataType.DateTime, new CastExpr(NullExpr.Instance, CastTargetType.Date).OutputType);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// COMPARISON NODES
// ═════════════════════════════════════════════════════════════════════════════

public class ComparisonNodeTests
{
    [Fact]
    public void Equals_EmitsEqualSign()
    {
        var expr = new ComparisonExpr(
            NodeFixtures.OrderTotal, ComparisonOperator.Eq, new NumberLiteralExpr(100));
        Assert.Equal("(\"orders\".\"total\" = 100)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void NotEquals_EmitsAngleBrackets()
    {
        var expr = new ComparisonExpr(
            NodeFixtures.UserEmail, ComparisonOperator.Neq, new StringLiteralExpr("admin"));
        Assert.Contains("<>", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void GreaterThan_EmitsGt()
    {
        var expr = new ComparisonExpr(
            NodeFixtures.OrderTotal, ComparisonOperator.Gt, new NumberLiteralExpr(0));
        Assert.Contains(">", expr.Emit(NodeFixtures.Postgres));
        Assert.DoesNotContain(">=", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Between_EmitsBetweenAndClause()
    {
        var expr = new BetweenExpr(
            NodeFixtures.OrderTotal,
            new NumberLiteralExpr(100),
            new NumberLiteralExpr(999));
        var sql = expr.Emit(NodeFixtures.Postgres);
        Assert.Contains("BETWEEN", sql);
        Assert.Contains("100", sql);
        Assert.Contains("999", sql);
        Assert.Contains("AND", sql);
    }

    [Fact]
    public void NotBetween_EmitsNOT_BETWEEN()
    {
        var expr = new BetweenExpr(
            NodeFixtures.OrderTotal,
            new NumberLiteralExpr(0), new NumberLiteralExpr(10),
            Negate: true);
        Assert.Contains("NOT BETWEEN", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void IsNull_EmitsISNull()
    {
        var expr = new IsNullExpr(NodeFixtures.UserEmail);
        Assert.Contains("IS NULL", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void IsNotNull_EmitsISNotNull()
    {
        var expr = new IsNullExpr(NodeFixtures.UserEmail, Negate: true);
        Assert.Contains("IS NOT NULL", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Like_EmitsLIKE()
    {
        var expr = new ComparisonExpr(
            NodeFixtures.UserEmail, ComparisonOperator.Like, new StringLiteralExpr("%@corp%"));
        Assert.Contains("LIKE", expr.Emit(NodeFixtures.MySQL));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// LOGIC GATES
// ═════════════════════════════════════════════════════════════════════════════

public class LogicGateTests
{
    private ISqlExpression TrueExpr  => new LiteralExpr("TRUE",  PinDataType.Boolean);
    private ISqlExpression FalseExpr => new LiteralExpr("FALSE", PinDataType.Boolean);

    [Fact]
    public void And_TwoOperands_JoinsWithAND()
    {
        var expr = new LogicGateExpr(LogicOperator.And,
            new[] { TrueExpr, FalseExpr });
        Assert.Equal("(TRUE AND FALSE)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Or_TwoOperands_JoinsWithOR()
    {
        var expr = new LogicGateExpr(LogicOperator.Or,
            new[] { TrueExpr, FalseExpr });
        Assert.Equal("(TRUE OR FALSE)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Not_NegatesOperand()
    {
        var inner = new ComparisonExpr(
            NodeFixtures.OrderTotal, ComparisonOperator.Gt, new NumberLiteralExpr(0));
        var expr = new NotExpr(inner);
        Assert.StartsWith("(NOT ", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void And_ThreeOperands_AllJoined()
    {
        var a = new LiteralExpr("A", PinDataType.Boolean);
        var b = new LiteralExpr("B", PinDataType.Boolean);
        var c = new LiteralExpr("C", PinDataType.Boolean);
        var expr = new LogicGateExpr(LogicOperator.And, new[] { a, b, c });
        Assert.Equal("(A AND B AND C)", expr.Emit(NodeFixtures.Postgres));
    }

    [Fact]
    public void Nested_AndOr_ProducesCorrectPrecedence()
    {
        // (A OR B) AND C
        var orExpr  = new LogicGateExpr(LogicOperator.Or,
            new[] { new LiteralExpr("A", PinDataType.Boolean) as ISqlExpression,
                    new LiteralExpr("B", PinDataType.Boolean) });
        var andExpr = new LogicGateExpr(LogicOperator.And,
            new[] { orExpr as ISqlExpression, new LiteralExpr("C", PinDataType.Boolean) });

        Assert.Equal("((A OR B) AND C)", andExpr.Emit(NodeFixtures.Postgres));
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// JSON EXTRACT NODE
// ═════════════════════════════════════════════════════════════════════════════

public class JsonExtractTests
{
    // ── Postgres ->> operator ────────────────────────────────────────────────

    [Fact]
    public void JsonExtract_Postgres_SimpleKey_UsesArrowArrow()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.Postgres);
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.city'");
        Assert.Equal("payload->>'city'", sql);
    }

    [Fact]
    public void JsonExtract_Postgres_NestedPath_BuildsChain()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.Postgres);
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.address.city'");
        Assert.Equal("payload->'address'->>'city'", sql);
    }

    [Fact]
    public void JsonQuery_Postgres_NestedPath_UsesArrowOnly()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.Postgres);
        var sql = reg.GetFunction(SqlFn.JsonQuery, "payload", "'$.address'");
        Assert.Equal("payload->'address'", sql);
    }

    [Fact]
    public void JsonExtract_Postgres_ArrayIndex_UsesNumericNav()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.Postgres);
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.items[0].name'");
        // Should contain ->0 for array index navigation
        Assert.Contains("->0", sql);
        Assert.Contains("->>'name'", sql);
    }

    [Fact]
    public void JsonArrayLength_Postgres_EmitsJsonbArrayLength()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.Postgres);
        var sql = reg.GetFunction(SqlFn.JsonArrayLength, "payload", "'$'");
        Assert.Contains("jsonb_array_length", sql);
    }

    // ── MySQL JSON_EXTRACT ────────────────────────────────────────────────────

    [Fact]
    public void JsonExtract_MySQL_UsesJsonUnquoteJsonExtract()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.MySql);
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.city'");
        Assert.Contains("JSON_UNQUOTE", sql);
        Assert.Contains("JSON_EXTRACT", sql);
        Assert.Contains("$.city", sql);
    }

    [Fact]
    public void JsonQuery_MySQL_UsesJsonExtractWithoutUnquote()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.MySql);
        var sql = reg.GetFunction(SqlFn.JsonQuery, "payload", "'$.address'");
        Assert.StartsWith("JSON_EXTRACT(", sql);
        Assert.DoesNotContain("JSON_UNQUOTE", sql);
    }

    [Fact]
    public void JsonArrayLength_MySQL_UsesJsonLength()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.MySql);
        var sql = reg.GetFunction(SqlFn.JsonArrayLength, "payload", "'$'");
        Assert.Contains("JSON_LENGTH", sql);
    }

    // ── SQL Server JSON_VALUE ─────────────────────────────────────────────────

    [Fact]
    public void JsonExtract_SqlServer_UsesJsonValue()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.SqlServer);
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.city'");
        Assert.StartsWith("JSON_VALUE(", sql);
        Assert.Contains("lax $.city", sql);
    }

    [Fact]
    public void JsonQuery_SqlServer_UsesJsonQuery()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.SqlServer);
        var sql = reg.GetFunction(SqlFn.JsonQuery, "payload", "'$.address'");
        Assert.StartsWith("JSON_QUERY(", sql);
    }

    [Fact]
    public void JsonArrayLength_SqlServer_UsesOPENJSON()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.SqlServer);
        var sql = reg.GetFunction(SqlFn.JsonArrayLength, "payload", "'$'");
        Assert.Contains("OPENJSON", sql);
        Assert.Contains("COUNT(*)", sql);
    }

    [Fact]
    public void JsonExtract_SqlServer_AddsLaxPrefix()
    {
        var reg = new SqlFunctionRegistry(DatabaseProvider.SqlServer);
        // Path without lax prefix should be auto-prefixed
        var sql = reg.GetFunction(SqlFn.JsonExtract, "payload", "'$.name'");
        Assert.Contains("lax", sql);
    }

    // ── FunctionCallExpr integration ──────────────────────────────────────────

    [Fact]
    public void JsonExtract_ViaFunctionCallExpr_Postgres()
    {
        var expr = new FunctionCallExpr(
            SqlFn.JsonExtract,
            new ISqlExpression[]
            {
                NodeFixtures.EventPayload,
                new StringLiteralExpr("$.user.name")
            },
            PinDataType.Text);

        var sql = expr.Emit(NodeFixtures.Postgres);
        // "events"."payload"->'user'->>'name'
        Assert.Contains("->'user'", sql);
        Assert.Contains("->>'name'", sql);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE GRAPH COMPILER
// ═════════════════════════════════════════════════════════════════════════════

public class NodeGraphCompilerTests
{
    private static NodeGraph BuildUpperEmailGraph()
    {
        var tableNode = new NodeInstance(
            Id: "tbl1", Type: NodeType.TableSource,
            PinLiterals: new Dictionary<string, string>(),
            Parameters:  new Dictionary<string, string>(),
            TableFullName: "public.users");

        var upperNode = new NodeInstance(
            Id: "upper1", Type: NodeType.Upper,
            PinLiterals: new Dictionary<string, string>(),
            Parameters:  new Dictionary<string, string>(),
            Alias: "EmailUpper");

        var graph = new NodeGraph
        {
            Nodes = new[] { tableNode, upperNode },
            Connections = new[]
            {
                new Connection("tbl1", "email", "upper1", "text")
            },
            SelectOutputs = new[]
            {
                new SelectBinding("upper1", "result", "EmailUpper")
            }
        };

        return graph;
    }

    [Fact]
    public void Compile_UpperOnColumn_EmitsUpperFunction()
    {
        var graph    = BuildUpperEmailGraph();
        var compiler = new NodeGraphCompiler(graph, NodeFixtures.Postgres);
        var result   = compiler.Compile();

        Assert.Single(result.SelectExprs);
        var sql = result.SelectExprs[0].Expr.Emit(NodeFixtures.Postgres);
        Assert.Contains("UPPER", sql);
        Assert.Contains("email", sql);
    }

    [Fact]
    public void Compile_BetweenFilter_EmitsBetweenClause()
    {
        var tableNode = new NodeInstance("tbl", NodeType.TableSource,
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            TableFullName: "orders");

        var betweenNode = new NodeInstance("bt", NodeType.Between,
            new Dictionary<string, string>
            {
                ["low"]  = "100",
                ["high"] = "999"
            },
            new Dictionary<string, string>());

        var graph = new NodeGraph
        {
            Nodes = new[] { tableNode, betweenNode },
            Connections = new[]
            {
                new Connection("tbl", "total", "bt", "value")
            },
            WhereConditions = new[] { new WhereBinding("bt", "result") }
        };

        var compiler = new NodeGraphCompiler(graph, NodeFixtures.Postgres);
        var compiled = compiler.Compile();

        Assert.Single(compiled.WhereExprs);
        var sql = compiled.WhereExprs[0].Emit(NodeFixtures.Postgres);
        Assert.Contains("BETWEEN", sql);
        Assert.Contains("100", sql);
        Assert.Contains("999", sql);
    }

    [Fact]
    public void Compile_AndGate_CombinesTwoConditions()
    {
        var tbl  = new NodeInstance("tbl",  NodeType.TableSource,
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            TableFullName: "orders");

        var gt   = new NodeInstance("gt",   NodeType.GreaterThan,
            new Dictionary<string, string> { ["right"] = "0" },
            new Dictionary<string, string>());

        var isnn = new NodeInstance("isnn", NodeType.IsNotNull,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        var and  = new NodeInstance("and",  NodeType.And,
            new Dictionary<string, string>(), new Dictionary<string, string>());

        var graph = new NodeGraph
        {
            Nodes = new[] { tbl, gt, isnn, and },
            Connections = new[]
            {
                new Connection("tbl",  "total",  "gt",   "left"),
                new Connection("tbl",  "status", "isnn", "value"),
                new Connection("gt",   "result", "and",  "conditions"),
                new Connection("isnn", "result", "and",  "conditions"),
            },
            WhereConditions = new[] { new WhereBinding("and", "result") }
        };

        var compiled = new NodeGraphCompiler(graph, NodeFixtures.Postgres).Compile();
        var sql = compiled.WhereExprs[0].Emit(NodeFixtures.Postgres);
        Assert.Contains("AND", sql);
        Assert.Contains("IS NOT NULL", sql);
    }

    [Fact]
    public void Compile_JsonExtract_ProducesArrowOp_Postgres()
    {
        var tbl = new NodeInstance("tbl", NodeType.TableSource,
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            TableFullName: "events");

        var json = new NodeInstance("je", NodeType.JsonExtract,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["path"] = "$.city", ["outputType"] = "Text" },
            Alias: "City");

        var graph = new NodeGraph
        {
            Nodes = new[] { tbl, json },
            Connections = new[] { new Connection("tbl", "payload", "je", "json") },
            SelectOutputs = new[] { new SelectBinding("je", "value", "City") }
        };

        var compiled = new NodeGraphCompiler(graph, NodeFixtures.Postgres).Compile();
        var sql = compiled.SelectExprs[0].Expr.Emit(NodeFixtures.Postgres);

        // Postgres JSON_EXTRACT → ->> operator
        Assert.Contains("->>", sql);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// QUERY GENERATOR SERVICE (end-to-end)
// ═════════════════════════════════════════════════════════════════════════════

public class QueryGeneratorServiceTests
{
    [Fact]
    public void Generate_EmptyGraph_ProducesSelectStar()
    {
        var svc   = QueryGeneratorService.Create(DatabaseProvider.Postgres);
        var graph = new NodeGraph();
        var result = svc.Generate("users", graph);
        Assert.Contains("SELECT", result.Sql.ToUpper());
        Assert.Contains("users", result.Sql);
    }

    [Fact]
    public void Generate_WithCast_ProducesCorrectSQL()
    {
        var tbl = new NodeInstance("tbl", NodeType.TableSource,
            new Dictionary<string, string>(), new Dictionary<string, string>(),
            TableFullName: "orders");

        var cast = new NodeInstance("cast1", NodeType.Cast,
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["targetType"] = "Text" },
            Alias: "TotalText");

        var graph = new NodeGraph
        {
            Nodes = new[] { tbl, cast },
            Connections = new[] { new Connection("tbl", "total", "cast1", "value") },
            SelectOutputs = new[] { new SelectBinding("cast1", "result", "TotalText") }
        };

        var svc    = QueryGeneratorService.Create(DatabaseProvider.Postgres);
        var result = svc.Generate("orders", graph);

        Assert.Contains("CAST", result.Sql);
        Assert.Contains("TEXT", result.Sql);
    }

    [Fact]
    public void Generate_DebugTree_ContainsSelectSection()
    {
        var svc    = QueryGeneratorService.Create(DatabaseProvider.Postgres);
        var result = svc.Generate("users", new NodeGraph());
        Assert.Contains("SELECT", result.DebugTree);
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE GRAPH TOPOLOGICAL SORT
// ═════════════════════════════════════════════════════════════════════════════

public class NodeGraphTopologicalSortTests
{
    [Fact]
    public void TopologicalOrder_Simple_SourceBeforeSink()
    {
        var src  = new NodeInstance("src",  NodeType.TableSource,
            new Dictionary<string, string>(), new Dictionary<string, string>());
        var sink = new NodeInstance("sink", NodeType.Upper,
            new Dictionary<string, string>(), new Dictionary<string, string>());

        var graph = new NodeGraph
        {
            Nodes = new[] { sink, src },  // intentionally reversed in list
            Connections = new[] { new Connection("src", "col", "sink", "text") }
        };

        var order = graph.TopologicalOrder();
        var srcIdx  = order.IndexOf(src);
        var sinkIdx = order.IndexOf(sink);

        Assert.True(srcIdx < sinkIdx, "Source must come before sink in topological order");
    }

    [Fact]
    public void TopologicalOrder_Cycle_ThrowsInvalidOperation()
    {
        var a = new NodeInstance("a", NodeType.Upper,
            new Dictionary<string, string>(), new Dictionary<string, string>());
        var b = new NodeInstance("b", NodeType.Lower,
            new Dictionary<string, string>(), new Dictionary<string, string>());

        var graph = new NodeGraph
        {
            Nodes = new[] { a, b },
            Connections = new[]
            {
                new Connection("a", "result", "b", "text"),
                new Connection("b", "result", "a", "text")   // cycle!
            }
        };

        Assert.Throws<InvalidOperationException>(() => graph.TopologicalOrder());
    }
}

// ═════════════════════════════════════════════════════════════════════════════
// NODE DEFINITION REGISTRY
// ═════════════════════════════════════════════════════════════════════════════

public class NodeDefinitionRegistryTests
{
    [Fact]
    public void Get_KnownType_ReturnsDefinition()
    {
        var def = NodeDefinitionRegistry.Get(NodeType.Upper);
        Assert.Equal("UPPER", def.DisplayName);
        Assert.Equal(NodeCategory.StringTransform, def.Category);
    }

    [Fact]
    public void All_ContainsAllCategories()
    {
        var categories = NodeDefinitionRegistry.All
            .Select(d => d.Category)
            .Distinct()
            .ToHashSet();

        Assert.Contains(NodeCategory.StringTransform, categories);
        Assert.Contains(NodeCategory.MathTransform,   categories);
        Assert.Contains(NodeCategory.Comparison,      categories);
        Assert.Contains(NodeCategory.LogicGate,       categories);
        Assert.Contains(NodeCategory.Json,            categories);
    }

    [Fact]
    public void And_Node_HasMultiInputPin()
    {
        var def = NodeDefinitionRegistry.Get(NodeType.And);
        var condPin = def.InputPins.First(p => p.Name == "conditions");
        Assert.True(condPin.AllowMultiple);
    }

    [Fact]
    public void JsonExtract_Node_HasPathParameter()
    {
        var def = NodeDefinitionRegistry.Get(NodeType.JsonExtract);
        Assert.Contains(def.Parameters, p => p.Name == "path");
    }
}
