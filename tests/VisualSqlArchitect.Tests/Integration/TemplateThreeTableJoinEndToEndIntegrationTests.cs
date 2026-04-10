using System.Text.Json;
using DBWeaver.Core;
using DBWeaver.Nodes;
using DBWeaver.QueryEngine;
using DBWeaver.Registry;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.Services.QueryPreview;
using Xunit;

namespace DBWeaver.Tests.Integration;

public class TemplateThreeTableJoinEndToEndIntegrationTests
{
    [Fact]
    public void ThreeTableJoinTemplate_EndToEnd_GeneratesSqlForAllProviders()
    {
        using var canvas = new CanvasViewModel();

        QueryTemplate template = QueryTemplateLibrary.All.First(t => t.Name == "JOIN de três tabelas");
        canvas.LoadTemplate(template);

        NodeGraph graph = BuildNodeGraphFromCanvas(canvas);

        string serialized = JsonSerializer.Serialize(graph);
        NodeGraph? roundTrip = JsonSerializer.Deserialize<NodeGraph>(serialized);
        Assert.NotNull(roundTrip);

        foreach (DatabaseProvider provider in new[]
                 {
                     DatabaseProvider.Postgres,
                     DatabaseProvider.MySql,
                     DatabaseProvider.SqlServer,
                     DatabaseProvider.SQLite,
                 })
        {
            var emitContext = new EmitContext(provider, new SqlFunctionRegistry(provider));
            CompiledNodeGraph compiled = new NodeGraphCompiler(roundTrip!, emitContext).Compile();

            List<NodeViewModel> tableNodes = canvas.Nodes.Where(n => n.Type == NodeType.TableSource).ToList();
            (List<JoinDefinition> joins, List<string> warnings) = new JoinResolver(canvas, provider).BuildJoins(tableNodes);
            Assert.True(joins.Count >= 2, $"Expected at least 2 JOIN definitions for provider {provider}. Warnings: {string.Join(" | ", warnings)}");

            GeneratedQuery generated = QueryGeneratorService.Create(provider)
                .Generate("public.orders", compiled, joins);

            Assert.False(string.IsNullOrWhiteSpace(generated.Sql));
            Assert.Contains("JOIN", generated.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("order_items", generated.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("products", generated.Sql, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static NodeGraph BuildNodeGraphFromCanvas(CanvasViewModel canvas)
    {
        NodeViewModel resultOutput = canvas.Nodes.First(n => n.Type == NodeType.ResultOutput || n.Type == NodeType.SelectOutput);

        List<NodeInstance> nodes = canvas.Nodes
            .Select(n => new NodeInstance(
                Id: n.Id,
                Type: n.Type,
                PinLiterals: new Dictionary<string, string>(n.PinLiterals, StringComparer.OrdinalIgnoreCase),
                Parameters: new Dictionary<string, string>(n.Parameters, StringComparer.OrdinalIgnoreCase),
                Alias: n.Alias,
                TableFullName: n.Type == NodeType.TableSource ? n.Subtitle : null,
                ColumnPins: n.Type == NodeType.TableSource
                    ? n.OutputPins.ToDictionary(p => p.Name, p => p.Name, StringComparer.OrdinalIgnoreCase)
                    : null,
                ColumnPinTypes: n.Type == NodeType.TableSource
                    ? n.OutputPins.ToDictionary(p => p.Name, p => p.DataType, StringComparer.OrdinalIgnoreCase)
                    : null
            ))
            .ToList();

        List<Connection> connections = canvas.Connections
            .Where(c => c.ToPin is not null)
            .Select(c => new Connection(c.FromPin.Owner.Id, c.FromPin.Name, c.ToPin!.Owner.Id, c.ToPin.Name))
            .ToList();

        List<SelectBinding> selectOutputs = canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutput && (c.ToPin.Name == "columns" || c.ToPin.Name == "column"))
            .Select(c => new SelectBinding(c.FromPin.Owner.Id, c.FromPin.Name, c.FromPin.Owner.Alias))
            .Distinct()
            .ToList();

        List<WhereBinding> whereConditions = canvas.Connections
            .Where(c => c.ToPin?.Owner == resultOutput && c.ToPin.Name == "where")
            .Select(c => new WhereBinding(c.FromPin.Owner.Id, c.FromPin.Name))
            .ToList();

        return new NodeGraph
        {
            Nodes = nodes,
            Connections = connections,
            SelectOutputs = selectOutputs,
            WhereConditions = whereConditions,
            Havings = [],
            Qualifies = [],
            OrderBys = [],
            GroupBys = [],
            Ctes = [],
        };
    }
}

