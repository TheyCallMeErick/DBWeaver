using VisualSqlArchitect.Core;

namespace VisualSqlArchitect.Registry;

// ─── Canonical function names the canvas nodes know ──────────────────────────

public static class SqlFn
{
    // String
    public const string Regex        = "REGEX";
    public const string Contains     = "CONTAINS";
    public const string StartsWith   = "STARTS_WITH";
    public const string EndsWith     = "ENDS_WITH";
    public const string Coalesce     = "COALESCE";
    public const string NullIf       = "NULLIF";
    public const string Concat       = "CONCAT";
    public const string Length       = "LENGTH";
    public const string Upper        = "UPPER";
    public const string Lower        = "LOWER";
    public const string Trim         = "TRIM";

    // Date / Time
    public const string DateDiff     = "DATE_DIFF";
    public const string DateTrunc    = "DATE_TRUNC";
    public const string CurrentDate  = "CURRENT_DATE";
    public const string Year         = "YEAR";
    public const string Month        = "MONTH";
    public const string Day          = "DAY";

    // Aggregate
    public const string StringAgg    = "STRING_AGG";

    // Conditional
    public const string IfNull       = "IFNULL";
    public const string Greatest     = "GREATEST";
    public const string Least        = "LEAST";

    // ── JSON ─────────────────────────────────────────────────────────────────
    // args[0] = json column expression
    // args[1] = quoted path, e.g. '$.address.city'

    /// <summary>
    /// Extracts a scalar value from JSON as text.
    /// Postgres: col->>'key'         MySQL: JSON_UNQUOTE(JSON_EXTRACT(col, '$.key'))
    /// SQL Server: JSON_VALUE(col, '$.key')
    /// </summary>
    public const string JsonExtract     = "JSON_EXTRACT";

    /// <summary>
    /// Extracts a JSON sub-document (keeps JSON type, not unquoted text).
    /// Postgres: col->'key'          MySQL: JSON_EXTRACT(col, '$.key')
    /// SQL Server: JSON_QUERY(col, '$.key')
    /// </summary>
    public const string JsonQuery       = "JSON_QUERY";

    /// <summary>Number of elements in a JSON array.</summary>
    public const string JsonArrayLength = "JSON_ARRAY_LENGTH";

    /// <summary>Tests whether a JSON path exists / is not null.</summary>
    public const string JsonExists      = "JSON_EXISTS";
}

// ─── Contract ─────────────────────────────────────────────────────────────────

public interface ISqlFunctionRegistry
{
    string GetFunction(string functionName, params string[] args);
    bool IsSupported(string functionName);
}

// ─── Registry implementation ──────────────────────────────────────────────────

public sealed class SqlFunctionRegistry : ISqlFunctionRegistry
{
    private readonly DatabaseProvider _provider;
    private delegate string FnRenderer(string[] args);
    private readonly Dictionary<string, FnRenderer> _map;

    public SqlFunctionRegistry(DatabaseProvider provider)
    {
        _provider = provider;
        _map = BuildMap(provider);
    }

    public string GetFunction(string functionName, params string[] args)
    {
        if (_map.TryGetValue(functionName.ToUpperInvariant(), out var renderer))
            return renderer(args);
        throw new NotSupportedException(
            $"Function '{functionName}' is not mapped for provider '{_provider}'.");
    }

    public bool IsSupported(string functionName) =>
        _map.ContainsKey(functionName.ToUpperInvariant());

    private static Dictionary<string, FnRenderer> BuildMap(DatabaseProvider p) => p switch
    {
        DatabaseProvider.Postgres  => PostgresMap(),
        DatabaseProvider.MySql     => MySqlMap(),
        DatabaseProvider.SqlServer => SqlServerMap(),
        _ => throw new NotSupportedException($"Provider {p} is not supported.")
    };

    // ─────────────────────────────────────────────────────────────────────────
    // POSTGRESQL
    // ─────────────────────────────────────────────────────────────────────────
    private static Dictionary<string, FnRenderer> PostgresMap() => new()
    {
        [SqlFn.Regex]       = a => $"{a[0]} ~ {a[1]}",
        [SqlFn.Contains]    = a => $"{a[0]} ILIKE '%' || {a[1]} || '%'",
        [SqlFn.StartsWith]  = a => $"{a[0]} ILIKE {a[1]} || '%'",
        [SqlFn.EndsWith]    = a => $"{a[0]} ILIKE '%' || {a[1]}",
        [SqlFn.Concat]      = a => $"CONCAT({Join(a)})",
        [SqlFn.Length]      = a => $"LENGTH({a[0]})",
        [SqlFn.Upper]       = a => $"UPPER({a[0]})",
        [SqlFn.Lower]       = a => $"LOWER({a[0]})",
        [SqlFn.Trim]        = a => $"TRIM({a[0]})",
        [SqlFn.Coalesce]    = a => $"COALESCE({Join(a)})",
        [SqlFn.NullIf]      = a => $"NULLIF({a[0]}, {a[1]})",
        [SqlFn.DateDiff]    = a => $"EXTRACT(DAY FROM ({a[1]}::timestamp - {a[0]}::timestamp))",
        [SqlFn.DateTrunc]   = a => $"DATE_TRUNC({a[0]}, {a[1]})",
        [SqlFn.CurrentDate] = _ => "CURRENT_DATE",
        [SqlFn.Year]        = a => $"EXTRACT(YEAR FROM {a[0]})",
        [SqlFn.Month]       = a => $"EXTRACT(MONTH FROM {a[0]})",
        [SqlFn.Day]         = a => $"EXTRACT(DAY FROM {a[0]})",
        [SqlFn.StringAgg]   = a => $"STRING_AGG({a[0]}, {a[1]})",
        [SqlFn.IfNull]      = a => $"COALESCE({a[0]}, {a[1]})",
        [SqlFn.Greatest]    = a => $"GREATEST({Join(a)})",
        [SqlFn.Least]       = a => $"LEAST({Join(a)})",

        // ── JSON — Postgres uses arrow operators ->> (text) and -> (jsonb) ───
        // Path arg is a quoted SQL string '$.a.b'; we convert to arrow chain.
        [SqlFn.JsonExtract]     = a => BuildPgJsonPath(a[0], StripQuotes(a[1]), asText: true),
        [SqlFn.JsonQuery]       = a => BuildPgJsonPath(a[0], StripQuotes(a[1]), asText: false),
        [SqlFn.JsonArrayLength] = a =>
        {
            var path   = StripQuotes(a[1]);
            var target = (path is "$" or "")
                ? a[0]
                : BuildPgJsonPath(a[0], path, asText: false);
            return $"jsonb_array_length(({target})::jsonb)";
        },
        [SqlFn.JsonExists] = a =>
        {
            var path   = StripQuotes(a[1]);
            var target = BuildPgJsonPath(a[0], path, asText: false);
            return $"({target}) IS NOT NULL";
        },
    };

    // ─────────────────────────────────────────────────────────────────────────
    // MYSQL / MARIADB
    // ─────────────────────────────────────────────────────────────────────────
    private static Dictionary<string, FnRenderer> MySqlMap() => new()
    {
        [SqlFn.Regex]       = a => $"{a[0]} REGEXP {a[1]}",
        [SqlFn.Contains]    = a => $"{a[0]} LIKE CONCAT('%', {a[1]}, '%')",
        [SqlFn.StartsWith]  = a => $"{a[0]} LIKE CONCAT({a[1]}, '%')",
        [SqlFn.EndsWith]    = a => $"{a[0]} LIKE CONCAT('%', {a[1]})",
        [SqlFn.Concat]      = a => $"CONCAT({Join(a)})",
        [SqlFn.Length]      = a => $"CHAR_LENGTH({a[0]})",
        [SqlFn.Upper]       = a => $"UPPER({a[0]})",
        [SqlFn.Lower]       = a => $"LOWER({a[0]})",
        [SqlFn.Trim]        = a => $"TRIM({a[0]})",
        [SqlFn.Coalesce]    = a => $"COALESCE({Join(a)})",
        [SqlFn.NullIf]      = a => $"NULLIF({a[0]}, {a[1]})",
        [SqlFn.DateDiff]    = a => $"DATEDIFF({a[1]}, {a[0]})",
        [SqlFn.DateTrunc]   = a => $"DATE_FORMAT({a[1]}, {TruncFormatMySQL(a[0])})",
        [SqlFn.CurrentDate] = _ => "CURDATE()",
        [SqlFn.Year]        = a => $"YEAR({a[0]})",
        [SqlFn.Month]       = a => $"MONTH({a[0]})",
        [SqlFn.Day]         = a => $"DAY({a[0]})",
        [SqlFn.StringAgg]   = a => $"GROUP_CONCAT({a[0]} SEPARATOR {a[1]})",
        [SqlFn.IfNull]      = a => $"IFNULL({a[0]}, {a[1]})",
        [SqlFn.Greatest]    = a => $"GREATEST({Join(a)})",
        [SqlFn.Least]       = a => $"LEAST({Join(a)})",

        // ── JSON — MySQL uses JSON_EXTRACT / JSON_UNQUOTE ────────────────────
        // MySQL path syntax IS the JSONPath standard ($.key.nested[0])
        // JSON_EXTRACT returns JSON; JSON_UNQUOTE strips the outer quotes for scalars.
        [SqlFn.JsonExtract]     = a => $"JSON_UNQUOTE(JSON_EXTRACT({a[0]}, {NormalizeMySqlPath(a[1])}))",
        [SqlFn.JsonQuery]       = a => $"JSON_EXTRACT({a[0]}, {NormalizeMySqlPath(a[1])})",
        [SqlFn.JsonArrayLength] = a =>
        {
            var path   = NormalizeMySqlPath(a[1]);
            var target = (StripQuotes(a[1]) is "$" or "")
                ? a[0]
                : $"JSON_EXTRACT({a[0]}, {path})";
            return $"JSON_LENGTH({target})";
        },
        [SqlFn.JsonExists] = a =>
            $"JSON_CONTAINS_PATH({a[0]}, 'one', {NormalizeMySqlPath(a[1])}) = 1",
    };

    // ─────────────────────────────────────────────────────────────────────────
    // SQL SERVER
    // ─────────────────────────────────────────────────────────────────────────
    private static Dictionary<string, FnRenderer> SqlServerMap() => new()
    {
        [SqlFn.Regex]       = a => $"PATINDEX({a[1]}, {a[0]}) > 0",
        [SqlFn.Contains]    = a => $"{a[0]} LIKE '%' + {a[1]} + '%'",
        [SqlFn.StartsWith]  = a => $"{a[0]} LIKE {a[1]} + '%'",
        [SqlFn.EndsWith]    = a => $"{a[0]} LIKE '%' + {a[1]}",
        [SqlFn.Concat]      = a => $"CONCAT({Join(a)})",
        [SqlFn.Length]      = a => $"LEN({a[0]})",
        [SqlFn.Upper]       = a => $"UPPER({a[0]})",
        [SqlFn.Lower]       = a => $"LOWER({a[0]})",
        [SqlFn.Trim]        = a => $"TRIM({a[0]})",
        [SqlFn.Coalesce]    = a => $"COALESCE({Join(a)})",
        [SqlFn.NullIf]      = a => $"NULLIF({a[0]}, {a[1]})",
        [SqlFn.DateDiff]    = a => $"DATEDIFF(DAY, {a[0]}, {a[1]})",
        [SqlFn.DateTrunc]   = a => $"DATETRUNC({a[0]}, {a[1]})",
        [SqlFn.CurrentDate] = _ => "CAST(GETDATE() AS DATE)",
        [SqlFn.Year]        = a => $"YEAR({a[0]})",
        [SqlFn.Month]       = a => $"MONTH({a[0]})",
        [SqlFn.Day]         = a => $"DAY({a[0]})",
        [SqlFn.StringAgg]   = a => $"STRING_AGG({a[0]}, {a[1]})",
        [SqlFn.IfNull]      = a => $"ISNULL({a[0]}, {a[1]})",
        [SqlFn.Greatest]    = a => $"(SELECT MAX(v) FROM (VALUES {ValuesRows(a)}) AS t(v))",
        [SqlFn.Least]       = a => $"(SELECT MIN(v) FROM (VALUES {ValuesRows(a)}) AS t(v))",

        // ── JSON — SQL Server uses JSON_VALUE (scalar) / JSON_QUERY (object/array) ──
        // JSON_VALUE:  returns NVARCHAR(4000) scalar
        // JSON_QUERY:  returns a JSON fragment (object or array) — cannot return scalars
        // Path syntax: lax $.key.nested[0]  (lax suppresses errors on missing paths)
        [SqlFn.JsonExtract]     = a => $"JSON_VALUE({a[0]}, {NormalizeSsPath(a[1])})",
        [SqlFn.JsonQuery]       = a => $"JSON_QUERY({a[0]}, {NormalizeSsPath(a[1])})",
        [SqlFn.JsonArrayLength] = a =>
        {
            // SQL Server has no direct JSON_ARRAY_LENGTH; emulate with OPENJSON
            var path = NormalizeSsPath(a[1]);
            var src  = (StripQuotes(a[1]) is "$" or "")
                ? a[0]
                : $"JSON_QUERY({a[0]}, {path})";
            return $"(SELECT COUNT(*) FROM OPENJSON({src}))";
        },
        [SqlFn.JsonExists] = a =>
            $"(ISJSON({a[0]}) = 1 AND JSON_VALUE({a[0]}, {NormalizeSsPath(a[1])}) IS NOT NULL)",
    };

    // ═════════════════════════════════════════════════════════════════════════
    // SHARED JSON PATH HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Strips surrounding single-quotes from a SQL string literal.
    /// '$.address.city'  →  $.address.city
    /// </summary>
    private static string StripQuotes(string sqlLiteral)
    {
        var s = sqlLiteral.Trim();
        if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2)
            return s[1..^1];
        return s;
    }

    /// <summary>
    /// Converts a JSONPath-style path ($.a.b[0].c) into a Postgres arrow chain.
    ///   asText=true  → col->>'a'->>'b'->0->>'c'   (final operator ->>)
    ///   asText=false → col->'a'->'b'->0->'c'        (all operators ->)
    ///
    /// Array indices become integer navigation: ->'items'->0
    /// </summary>
    private static string BuildPgJsonPath(string col, string path, bool asText)
    {
        // Remove leading '$' and split on dots / bracket notation
        var parts = ParseJsonPathSegments(path);
        if (parts.Count == 0) return col;

        var sb = new System.Text.StringBuilder(col);
        for (int i = 0; i < parts.Count; i++)
        {
            var isLast = i == parts.Count - 1;
            var part   = parts[i];

            if (int.TryParse(part, out var idx))
                sb.Append($"->{idx}");                             // numeric index: ->0
            else if (isLast && asText)
                sb.Append($"->>'{ part}'");                        // last key as text: ->>'key'
            else
                sb.Append($"->'{part}'");                          // intermediate key: ->'key'
        }
        return sb.ToString();
    }

    /// <summary>
    /// Normalises a path for MySQL. Ensures it starts with $. and is wrapped in single quotes.
    /// '$.a.b'   → '$.a.b'   (unchanged)
    /// 'a.b'     → '$.a.b'
    /// address   → '$.address'
    /// </summary>
    private static string NormalizeMySqlPath(string sqlLiteral)
    {
        var raw = StripQuotes(sqlLiteral);
        if (!raw.StartsWith("$")) raw = "$." + raw.TrimStart('.');
        return $"'{raw}'";
    }

    /// <summary>
    /// Normalises a path for SQL Server.
    /// SQL Server requires 'lax' mode prefix to avoid errors on missing paths.
    /// Strips outer quotes, adds 'lax ' prefix if not present, returns as SQL literal.
    /// '$.a.b'       → 'lax $.a.b'
    /// 'lax $.a.b'   → 'lax $.a.b'  (idempotent)
    /// </summary>
    private static string NormalizeSsPath(string sqlLiteral)
    {
        var raw = StripQuotes(sqlLiteral);
        if (!raw.StartsWith("lax ") && !raw.StartsWith("strict "))
            raw = "lax " + (raw.StartsWith("$") ? raw : "$." + raw.TrimStart('.'));
        return $"'{raw}'";
    }

    /// <summary>
    /// Parses a JSONPath expression into key/index segments.
    /// $.address.city[0].street  →  ["address","city","0","street"]
    /// </summary>
    private static IReadOnlyList<string> ParseJsonPathSegments(string path)
    {
        // Strip leading '$' and optional leading '.'
        var normalised = path.TrimStart('$').TrimStart('.');
        if (string.IsNullOrEmpty(normalised)) return Array.Empty<string>();

        var segments = new List<string>();
        // Replace [N] array notation with .N so we can split uniformly on '.'
        var flat = System.Text.RegularExpressions.Regex
            .Replace(normalised, @"\[(\d+)\]", ".$1");

        foreach (var part in flat.Split('.', StringSplitOptions.RemoveEmptyEntries))
            segments.Add(part);

        return segments;
    }

    // ── Shared scalar helpers ─────────────────────────────────────────────────

    private static string Join(string[] args) => string.Join(", ", args);

    private static string TruncFormatMySQL(string unit) => unit.Trim('\'').ToLower() switch
    {
        "year"   => "'%Y-01-01'",
        "month"  => "'%Y-%m-01'",
        "day"    => "'%Y-%m-%d'",
        "hour"   => "'%Y-%m-%d %H:00:00'",
        "minute" => "'%Y-%m-%d %H:%i:00'",
        _ => throw new NotSupportedException($"DATE_TRUNC unit '{unit}' is not supported in MySQL.")
    };

    private static string ValuesRows(string[] args) =>
        string.Join(", ", args.Select(a => $"({a})"));
}
