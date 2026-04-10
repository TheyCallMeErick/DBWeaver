using System.Collections.ObjectModel;
using System.Diagnostics;
using DBWeaver.UI.Services.SqlImport.Build;
using DBWeaver.UI.Services.SqlImport.Contracts;
using DBWeaver.UI.Services.SqlImport.Execution.Applying;
using DBWeaver.UI.Services.SqlImport.Execution.Parsing;
using DBWeaver.UI.Services.SqlImport.Rewriting;
using DBWeaver.UI.Services.SqlImport.Validation;
using DBWeaver.UI.ViewModels;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.SqlImport.Execution;

public sealed class SqlImportExecutionService(
    CanvasViewModel canvas,
    SqlImportSyntaxValidator syntaxValidator,
    SqlImportCteRewriteService cteRewriteService
) : ISqlImportExecutionService
{
    private readonly CanvasViewModel _canvas = canvas;
    private readonly SqlImportSyntaxValidator _syntaxValidator = syntaxValidator;
    private readonly SqlImportClauseParser _clauseParser = new(cteRewriteService);
    private readonly SqlImportClauseApplier _clauseApplier = new(canvas);

    public SqlImportExecutionResult Execute(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    )
    {
        (int imported, int partial, int skipped, SqlImportTiming timing) = BuildGraph(
            sql,
            report,
            cancellationToken
        );

        return new SqlImportExecutionResult(imported, partial, skipped, timing);
    }

    private (int imported, int partial, int skipped, SqlImportTiming timing) BuildGraph(
        string sql,
        ObservableCollection<ImportReportItem> report,
        CancellationToken cancellationToken
    )
    {
        int imported = 0;
        int partial = 0;
        int skipped = 0;

        var totalWatch = Stopwatch.StartNew();
        var parseWatch = Stopwatch.StartNew();

        cancellationToken.ThrowIfCancellationRequested();
        _syntaxValidator.ValidateBasicSyntax(sql);

        SqlImportParseResult parseResult = _clauseParser.Parse(sql, report, cancellationToken);
        imported += parseResult.Imported;
        partial += parseResult.Partial;
        skipped += parseResult.Skipped;

        parseWatch.Stop();

        if (parseResult.ShouldStop || parseResult.Query is null)
        {
            totalWatch.Stop();
            return (
                imported,
                partial,
                skipped,
                new SqlImportTiming(parseWatch.Elapsed, TimeSpan.Zero, TimeSpan.Zero, totalWatch.Elapsed)
            );
        }

        var mapWatch = Stopwatch.StartNew();

        SqlImportParsedQuery parsed = parseResult.Query;

        var coreBuilder = new ImportModelToCanvasBuilder(_canvas);
        var coreInput = new ImportBuildInput(
            parsed.FromParts.Select(p => new ImportFromPart(p.Table, p.Alias, p.JoinType, p.OnClause)).ToList(),
            parsed.SelectedColumns.Select(c => new ImportSelectTerm(c.Expr, c.Alias)).ToList(),
            parsed.IsStar,
            parsed.StarQualifier,
            SqlImportLayoutPolicy.Default
        );
        ImportBuildContext coreContext = coreBuilder.BuildCore(coreInput, report, cancellationToken);

        imported += coreContext.Imported;
        partial += coreContext.Partial;
        skipped += coreContext.Skipped;

        mapWatch.Stop();
        var buildWatch = Stopwatch.StartNew();

        SqlImportApplyResult applyResult = _clauseApplier.Apply(parsed, coreContext, report, cancellationToken);
        imported += applyResult.Imported;
        partial += applyResult.Partial;
        skipped += applyResult.Skipped;

        buildWatch.Stop();
        totalWatch.Stop();

        return (
            imported,
            partial,
            skipped,
            new SqlImportTiming(parseWatch.Elapsed, mapWatch.Elapsed, buildWatch.Elapsed, totalWatch.Elapsed)
        );
    }
}
