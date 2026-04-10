using DBWeaver.Nodes.Compilers;
using DBWeaver.Registry;
using NodeModel = DBWeaver.Nodes;

namespace DBWeaver.Tests.Unit.Nodes;

public sealed class NodeCompilerFactoryTests
{
    [Fact]
    public void Compile_WithInjectedCompiler_UsesInjectedImplementation()
    {
        var marker = new NodeModel.LiteralExpr("marker");
        var sut = new NodeCompilerFactory([new FakeCompiler(NodeModel.NodeType.Equals, marker)]);
        var node = new NodeModel.NodeInstance(
            "n1",
            NodeModel.NodeType.Equals,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        NodeModel.ISqlExpression emitted = sut.Compile(node, new FakeCompilationContext(), "result");

        Assert.Same(marker, emitted);
    }

    [Fact]
    public void Compile_WhenNoCompilerSupportsNodeType_ThrowsNotSupportedException()
    {
        var sut = new NodeCompilerFactory([new FakeCompiler(NodeModel.NodeType.TableSource, new NodeModel.LiteralExpr("x"))]);
        var node = new NodeModel.NodeInstance(
            "n1",
            NodeModel.NodeType.Equals,
            new Dictionary<string, string>(),
            new Dictionary<string, string>());

        Assert.Throws<NotSupportedException>(() => sut.Compile(node, new FakeCompilationContext(), "result"));
    }

    private sealed class FakeCompiler(NodeModel.NodeType supportedType, NodeModel.ISqlExpression expression) : INodeCompiler
    {
        public bool CanCompile(NodeModel.NodeType nodeType) => nodeType == supportedType;

        public NodeModel.ISqlExpression Compile(NodeModel.NodeInstance node, INodeCompilationContext context, string pinName = "result")
        {
            _ = node;
            _ = context;
            _ = pinName;
            return expression;
        }
    }

    private sealed class FakeCompilationContext : INodeCompilationContext
    {
        private static readonly NodeModel.EmitContext EmitCtx = new(
            DBWeaver.Core.DatabaseProvider.Postgres,
            new SqlFunctionRegistry(DBWeaver.Core.DatabaseProvider.Postgres));

        public NodeModel.NodeGraph Graph { get; } = new();
        public NodeModel.EmitContext EmitContext => EmitCtx;

        public NodeModel.ISqlExpression Resolve(string nodeId, string pinName = "result")
        {
            _ = nodeId;
            _ = pinName;
            return new NodeModel.LiteralExpr("0");
        }

        public NodeModel.ISqlExpression ResolveInput(
            string nodeId,
            string pinName,
            NodeModel.PinDataType expectedType = NodeModel.PinDataType.Expression
        )
        {
            _ = nodeId;
            _ = pinName;
            _ = expectedType;
            return new NodeModel.LiteralExpr("0");
        }

        public IReadOnlyList<NodeModel.ISqlExpression> ResolveInputs(string nodeId, string pinName)
        {
            _ = nodeId;
            _ = pinName;
            return [];
        }
    }
}
