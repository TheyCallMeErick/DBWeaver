using DBWeaver.Core;
using DBWeaver.Nodes;

namespace DBWeaver.Ddl;

public sealed partial class DdlGraphCompiler
{
    private CreateEnumTypeExpr CompileEnumTypeDefinition(NodeInstance typeNode, DdlIdempotentMode idempotentMode)
    {
        if (_provider != DatabaseProvider.Postgres)
            throw new InvalidOperationException("CreateTypeOutput é suportado apenas para PostgreSQL.");

        EnumTypeSpec spec = BuildEnumTypeSpec(typeNode);
        return new CreateEnumTypeExpr(spec.SchemaName, spec.TypeName, spec.Values, idempotentMode);
    }

    private CreateSequenceExpr CompileSequenceDefinition(NodeInstance sequenceNode, DdlIdempotentMode idempotentMode)
    {
        SequenceSpec spec = BuildSequenceSpec(sequenceNode);
        return new CreateSequenceExpr(
            spec.Schema,
            spec.SequenceName,
            spec.StartValue,
            spec.Increment,
            spec.MinValue,
            spec.MaxValue,
            spec.Cycle,
            spec.Cache,
            idempotentMode
        );
    }

    private EnumTypeSpec BuildEnumTypeSpec(NodeInstance typeNode)
    {
        string typeName = ReadParam(typeNode, "TypeName", "");
        if (string.IsNullOrWhiteSpace(typeName))
            throw new InvalidOperationException("EnumTypeDefinition requires TypeName.");

        string schemaName = ReadParam(typeNode, "SchemaName", "public");
        string rawValues = ReadParam(typeNode, "EnumValues", "");
        IReadOnlyList<string> values = ParseEnumValues(rawValues);

        if (values.Count == 0)
            throw new InvalidOperationException("EnumTypeDefinition requires at least one value.");

        return new EnumTypeSpec(schemaName, typeName, values);
    }

    private SequenceSpec BuildSequenceSpec(NodeInstance sequenceNode)
    {
        string sequenceName = ReadParam(sequenceNode, "SequenceName", "");
        if (string.IsNullOrWhiteSpace(sequenceName))
            throw new InvalidOperationException("SequenceDefinition requires SequenceName.");

        string defaultSchema = _provider == DatabaseProvider.SqlServer ? "dbo" : "public";
        string schema = ReadParam(sequenceNode, "Schema", defaultSchema);

        return new SequenceSpec(
            schema,
            sequenceName,
            ReadSequenceLongInputOrParam(sequenceNode, "start_value", "StartValue"),
            ReadSequenceLongInputOrParam(sequenceNode, "increment", "Increment"),
            ReadSequenceLongInputOrParam(sequenceNode, "min_value", "MinValue"),
            ReadSequenceLongInputOrParam(sequenceNode, "max_value", "MaxValue"),
            ReadSequenceBoolInputOrParam(sequenceNode, "cycle", "Cycle", false),
            ReadSequenceIntInputOrParam(sequenceNode, "cache", "Cache")
        );
    }

    private long? ReadSequenceLongInputOrParam(NodeInstance sequenceNode, string inputPin, string parameterName)
    {
        Connection? wire = _graph.GetSingleInputConnection(sequenceNode.Id, inputPin);
        if (wire is null || !_graph.NodeMap.TryGetValue(wire.FromNodeId, out NodeInstance? sourceNode))
            return ReadLongParam(sequenceNode, parameterName);

        string? raw = sourceNode.Parameters.TryGetValue("value", out string? valueRaw)
            ? valueRaw
            : null;

        if (long.TryParse(raw, out long parsedLong))
            return parsedLong;

        if (double.TryParse(raw, out double parsedDouble))
            return Convert.ToInt64(Math.Truncate(parsedDouble));

        return ReadLongParam(sequenceNode, parameterName);
    }

    private int? ReadSequenceIntInputOrParam(NodeInstance sequenceNode, string inputPin, string parameterName)
    {
        Connection? wire = _graph.GetSingleInputConnection(sequenceNode.Id, inputPin);
        if (wire is null || !_graph.NodeMap.TryGetValue(wire.FromNodeId, out NodeInstance? sourceNode))
            return ReadIntParam(sequenceNode, parameterName);

        string? raw = sourceNode.Parameters.TryGetValue("value", out string? valueRaw)
            ? valueRaw
            : null;

        if (int.TryParse(raw, out int parsed))
            return parsed;

        return ReadIntParam(sequenceNode, parameterName);
    }

    private bool ReadSequenceBoolInputOrParam(
        NodeInstance sequenceNode,
        string inputPin,
        string parameterName,
        bool fallback)
    {
        Connection? wire = _graph.GetSingleInputConnection(sequenceNode.Id, inputPin);
        if (wire is null || !_graph.NodeMap.TryGetValue(wire.FromNodeId, out NodeInstance? sourceNode))
            return ReadBoolParam(sequenceNode, parameterName, fallback);

        string? raw = sourceNode.Parameters.TryGetValue("value", out string? valueRaw)
            ? valueRaw
            : null;

        return bool.TryParse(raw, out bool parsed)
            ? parsed
            : ReadBoolParam(sequenceNode, parameterName, fallback);
    }

    private ScalarTypeSpec BuildScalarTypeSpec(NodeInstance scalarTypeNode)
    {
        string typeKind = ReadParam(scalarTypeNode, "TypeKind", "VARCHAR").Trim().ToUpperInvariant();
        int? length = ReadIntParam(scalarTypeNode, "Length");
        int? precision = ReadIntParam(scalarTypeNode, "Precision");
        int? scale = ReadIntParam(scalarTypeNode, "Scale");
        return new ScalarTypeSpec(typeKind, length, precision, scale);
    }

    private string ResolveSequenceDefaultExpression(SequenceSpec spec)
    {
        return _provider switch
        {
            DatabaseProvider.Postgres => $"nextval('{spec.Schema}.{spec.SequenceName}')",
            DatabaseProvider.SqlServer => $"NEXT VALUE FOR [{spec.Schema}].[{spec.SequenceName}]",
            DatabaseProvider.MySql or DatabaseProvider.SQLite => string.Empty,
            _ => string.Empty,
        };
    }

    private string ResolveEnumColumnDataType(EnumTypeSpec spec)
    {
        return _provider switch
        {
            DatabaseProvider.MySql => $"ENUM({string.Join(", ", spec.Values.Select(SqlStringUtility.QuoteLiteral))})",
            DatabaseProvider.Postgres => $"\"{spec.SchemaName.Replace("\"", "\"\"")}\".\"{spec.TypeName.Replace("\"", "\"\"")}\"",
            DatabaseProvider.SqlServer or DatabaseProvider.SQLite => "ENUM",
            _ => "ENUM",
        };
    }

    private string ResolveScalarColumnDataType(ScalarTypeSpec spec)
    {
        return spec.TypeKind switch
        {
            "VARCHAR" => $"VARCHAR({Math.Max(1, spec.Length ?? 255)})",
            "TEXT" => "TEXT",
            "INT" => "INT",
            "BIGINT" => "BIGINT",
            "DECIMAL" => $"DECIMAL({Math.Max(1, spec.Precision ?? 18)},{Math.Max(0, spec.Scale ?? 2)})",
            "BOOLEAN" => _provider == DatabaseProvider.SqlServer ? "BIT" : "BOOLEAN",
            "DATE" => "DATE",
            "DATETIME" => _provider == DatabaseProvider.Postgres ? "TIMESTAMP" : "DATETIME",
            "JSON" => _provider == DatabaseProvider.SqlServer ? "NVARCHAR(MAX)" : "JSON",
            "UUID" => _provider switch
            {
                DatabaseProvider.Postgres => "UUID",
                DatabaseProvider.SqlServer => "UNIQUEIDENTIFIER",
                _ => "CHAR(36)",
            },
            _ => spec.TypeKind,
        };
    }

    private static IReadOnlyList<string> ParseEnumValues(string rawValues)
    {
        if (string.IsNullOrWhiteSpace(rawValues))
            return [];

        return
        [
            .. rawValues
                .Split([',', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim('\'', '"')),
        ];
    }

    private sealed record EnumTypeSpec(string SchemaName, string TypeName, IReadOnlyList<string> Values);
    private sealed record ScalarTypeSpec(string TypeKind, int? Length, int? Precision, int? Scale);
    private sealed record SequenceSpec(
        string Schema,
        string SequenceName,
        long? StartValue,
        long? Increment,
        long? MinValue,
        long? MaxValue,
        bool Cycle,
        int? Cache
    );
}
