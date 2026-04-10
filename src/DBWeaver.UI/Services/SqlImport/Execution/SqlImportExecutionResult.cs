namespace DBWeaver.UI.Services.SqlImport.Execution;

public readonly record struct SqlImportTiming(
    TimeSpan Parse,
    TimeSpan Map,
    TimeSpan Build,
    TimeSpan Total
);

public readonly record struct SqlImportExecutionResult(
    int Imported,
    int Partial,
    int Skipped,
    SqlImportTiming Timing
);
