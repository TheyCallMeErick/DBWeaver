namespace DBWeaver.UI.Services.SqlImport.Execution.Parsing;

public readonly record struct SqlImportParseResult(
    SqlImportParsedQuery? Query,
    int Imported,
    int Partial,
    int Skipped,
    bool ShouldStop
);
