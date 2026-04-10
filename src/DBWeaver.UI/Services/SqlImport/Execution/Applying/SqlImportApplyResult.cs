namespace DBWeaver.UI.Services.SqlImport.Execution.Applying;

public readonly record struct SqlImportApplyResult(int Imported, int Partial, int Skipped);
