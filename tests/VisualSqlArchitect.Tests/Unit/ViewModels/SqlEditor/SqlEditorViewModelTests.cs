using System.Data;
using System.Diagnostics;
using System.IO;
using DBWeaver;
using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.Services.SqlEditor;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.SqlEditor;

public sealed class SqlEditorViewModelTests
{
    [Fact]
    public void Constructor_InitializesTabsWithConfiguredDefaults()
    {
        var sut = new SqlEditorViewModel(DatabaseProvider.MySql, "profile-default");

        Assert.Single(sut.Tabs.Tabs);
        Assert.Equal(DatabaseProvider.MySql, sut.ActiveTab.Provider);
        Assert.Equal("profile-default", sut.ActiveTab.ConnectionProfileId);
        Assert.NotNull(sut.ActiveTab.CloseCommand);
    }

    [Fact]
    public void NotifyConnectionContextChanged_RefreshesSchemaTablesFromMetadataResolver()
    {
        DbMetadata metadata = BuildMetadata("main", "employees");
        var sut = new SqlEditorViewModel(metadataResolver: () => metadata);

        Assert.Single(sut.SchemaTables);

        metadata = BuildMetadata("archive", "employees_archive");

        sut.NotifyConnectionContextChanged();

        Assert.Single(sut.SchemaTables);
        Assert.Equal("archive.employees_archive", sut.SchemaTables[0].FullName);
    }

    [Fact]
    public void ReceiveFromCanvas_DelegatesToTabManagerAndKeepsActiveTabInSync()
    {
        var sut = new SqlEditorViewModel();

        sut.ReceiveFromCanvas("SELECT now();", DatabaseProvider.Postgres);

        Assert.Equal("SELECT now();", sut.ActiveTab.SqlText);
        Assert.Equal(DatabaseProvider.Postgres, sut.ActiveTab.Provider);
        Assert.False(sut.ActiveTab.IsDirty);
    }

    [Fact]
    public void RecordCompletionLatency_TracksSampleCountLastAndP95()
    {
        var sut = new SqlEditorViewModel();

        for (int ms = 1; ms <= 20; ms++)
            sut.RecordCompletionLatency(TimeSpan.FromMilliseconds(ms));

        Assert.Equal(20, sut.CompletionTelemetry.SampleCount);
        Assert.Equal(20, sut.CompletionTelemetry.LastDurationMs);
        Assert.Equal(19, sut.CompletionTelemetry.P95DurationMs);
        Assert.True(sut.CompletionTelemetry.IsWithinBudget);
        Assert.Contains("Completion p95:", sut.CompletionTelemetryText, StringComparison.Ordinal);
        Assert.Contains("Amostras: 20", sut.CompletionTelemetryText, StringComparison.Ordinal);
    }

    [Fact]
    public void RecordCompletionLatency_KeepsRollingWindowBounded()
    {
        var sut = new SqlEditorViewModel();

        for (int ms = 1; ms <= 250; ms++)
            sut.RecordCompletionLatency(TimeSpan.FromMilliseconds(ms));

        Assert.Equal(SqlEditorTabState.CompletionTelemetryWindowSize, sut.CompletionTelemetry.SampleCount);
        Assert.Equal(250, sut.CompletionTelemetry.LastDurationMs);
        Assert.Equal(240, sut.CompletionTelemetry.P95DurationMs);
        Assert.False(sut.CompletionTelemetry.IsWithinBudget);
        Assert.Contains("Budget<= 100 ms", sut.CompletionTelemetryText, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveTabProvider_WhenChanged_KeepsSelectedConnectionProfile()
    {
        var sut = new SqlEditorViewModel(
            connectionProfilesResolver: () =>
            [
                new SqlEditorConnectionProfileOption
                {
                    Id = "pg-profile",
                    DisplayName = "Postgres Main",
                    Provider = DatabaseProvider.Postgres,
                },
                new SqlEditorConnectionProfileOption
                {
                    Id = "my-profile",
                    DisplayName = "MySql Main",
                    Provider = DatabaseProvider.MySql,
                },
            ]);

        sut.ActiveTabProvider = DatabaseProvider.Postgres;
        sut.ActiveTabConnectionProfileId = "pg-profile";
        Assert.Equal("pg-profile", sut.ActiveTabConnectionProfileId);

        sut.ActiveTabProvider = DatabaseProvider.MySql;

        Assert.Equal("pg-profile", sut.ActiveTabConnectionProfileId);
    }

    [Fact]
    public void ActiveTabConnectionProfileId_AcceptsAnyKnownProfileAndSyncsDialect()
    {
        var sut = new SqlEditorViewModel(
            connectionProfilesResolver: () =>
            [
                new SqlEditorConnectionProfileOption
                {
                    Id = "pg-profile",
                    DisplayName = "Postgres Main",
                    Provider = DatabaseProvider.Postgres,
                },
                new SqlEditorConnectionProfileOption
                {
                    Id = "my-profile",
                    DisplayName = "MySql Main",
                    Provider = DatabaseProvider.MySql,
                },
            ]);

        sut.ActiveTabConnectionProfileId = "my-profile";

        Assert.Equal("my-profile", sut.ActiveTabConnectionProfileId);
        Assert.Equal(DatabaseProvider.MySql, sut.ActiveTabProvider);
        Assert.Equal(2, sut.AvailableConnectionProfiles.Count);
    }

    [Fact]
    public void TabCommands_NewActivateAndClose_WorkAsExpected()
    {
        var sut = new SqlEditorViewModel();
        string firstId = sut.ActiveTab.Id;

        sut.NewTabCommand.Execute(null);

        Assert.Equal(2, sut.EditorTabs.Count);
        Assert.Equal(1, sut.ActiveEditorTabIndex);

        sut.ActiveEditorTabIndex = 0;
        Assert.Equal(0, sut.ActiveEditorTabIndex);
        Assert.Equal(firstId, sut.ActiveTab.Id);

        string closeId = sut.EditorTabs[1].Id;
        sut.CloseTabCommand.Execute(closeId);

        Assert.Single(sut.EditorTabs);
        Assert.Equal(0, sut.ActiveEditorTabIndex);
        Assert.Equal(firstId, sut.ActiveTab.Id);
    }

    [Fact]
    public void CloseTabCommand_DoesNotCloseLastTab()
    {
        var sut = new SqlEditorViewModel();
        string onlyId = sut.ActiveTab.Id;

        sut.CloseTabCommand.Execute(onlyId);

        Assert.Single(sut.EditorTabs);
        Assert.Equal(onlyId, sut.ActiveTab.Id);
    }

    [Fact]
    public void ManyTabsWarning_WhenTabCountReachesFifteen_BecomesVisible()
    {
        var sut = new SqlEditorViewModel();

        for (int i = 0; i < 14; i++)
            sut.NewTabCommand.Execute(null);

        Assert.Equal(15, sut.EditorTabs.Count);
        Assert.True(sut.HasManyTabsWarning);
        Assert.Contains("15", sut.ManyTabsWarningText);
    }

    [Fact]
    public void CloseTabCommand_DirtyTab_RequestsConfirmationInsteadOfClosing()
    {
        var sut = new SqlEditorViewModel();
        sut.NewTabCommand.Execute(null);
        sut.ActiveEditorTabIndex = 1;
        sut.ActiveTab.IsDirty = true;
        string dirtyId = sut.ActiveTab.Id;

        sut.CloseTabCommand.Execute(dirtyId);

        Assert.True(sut.HasPendingCloseTabConfirmation);
        Assert.Equal(2, sut.EditorTabs.Count);
        Assert.Equal(dirtyId, sut.ActiveTab.Id);
        AssertLocalized(
            sut.ExecutionStatusText,
            "O fechamento da aba exige confirmacao.",
            "Tab close requires confirmation.");
    }

    [Fact]
    public void ConfirmPendingCloseTabCommand_ClosesDirtyTabAfterConfirmation()
    {
        var sut = new SqlEditorViewModel();
        string firstId = sut.ActiveTab.Id;
        sut.NewTabCommand.Execute(null);
        sut.ActiveEditorTabIndex = 1;
        sut.ActiveTab.IsDirty = true;
        string dirtyId = sut.ActiveTab.Id;
        sut.CloseTabCommand.Execute(dirtyId);

        sut.ConfirmPendingCloseTabCommand.Execute(null);

        Assert.False(sut.HasPendingCloseTabConfirmation);
        Assert.Single(sut.EditorTabs);
        Assert.Equal(firstId, sut.ActiveTab.Id);
        AssertLocalized(sut.ExecutionStatusText, "Aba fechada.", "Tab closed.");
    }

    [Fact]
    public void CancelPendingCloseTabCommand_KeepsDirtyTabOpen()
    {
        var sut = new SqlEditorViewModel();
        sut.NewTabCommand.Execute(null);
        sut.ActiveEditorTabIndex = 1;
        sut.ActiveTab.IsDirty = true;
        string dirtyId = sut.ActiveTab.Id;
        sut.CloseTabCommand.Execute(dirtyId);

        sut.CancelPendingCloseTabCommand.Execute(null);

        Assert.False(sut.HasPendingCloseTabConfirmation);
        Assert.Equal(2, sut.EditorTabs.Count);
        Assert.Equal(dirtyId, sut.ActiveTab.Id);
        AssertLocalized(sut.ExecutionStatusText, "Fechamento da aba cancelado.", "Tab close canceled.");
    }

    [Fact]
    public async Task SaveActiveTabAsync_WithPath_WritesFileAndClearsDirty()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql");
        try
        {
            var sut = new SqlEditorViewModel();
            sut.ActiveTab.SqlText = "SELECT 42;";
            sut.ActiveTab.IsDirty = true;

            bool saved = await sut.SaveActiveTabAsync(path);

            Assert.True(saved);
            Assert.True(File.Exists(path));
            Assert.Equal("SELECT 42;", await File.ReadAllTextAsync(path));
            Assert.False(sut.ActiveTab.IsDirty);
            Assert.Equal(path, sut.ActiveTab.FilePath);
            Assert.Equal(Path.GetFileName(path), sut.ActiveTab.FallbackTitle);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task SaveActiveTabAsync_WithoutPath_ReturnsFalse()
    {
        var sut = new SqlEditorViewModel();
        sut.ActiveTab.FilePath = null;

        bool saved = await sut.SaveActiveTabAsync();

        Assert.False(saved);
        AssertLocalized(
            sut.ExecutionStatusText,
            "Salvamento cancelado.",
            "Save canceled.");
    }

    [Fact]
    public async Task OpenSqlFileAsync_WithEmptyActiveTab_LoadsCurrentTab()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, "SELECT now();");
        try
        {
            var sut = new SqlEditorViewModel();
            string originalId = sut.ActiveTab.Id;

            bool opened = await sut.OpenSqlFileAsync(path);

            Assert.True(opened);
            Assert.Equal(originalId, sut.ActiveTab.Id);
            Assert.Equal("SELECT now();", sut.ActiveTab.SqlText);
            Assert.False(sut.ActiveTab.IsDirty);
            Assert.Equal(path, sut.ActiveTab.FilePath);
            Assert.Equal(Path.GetFileName(path), sut.ActiveTab.FallbackTitle);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenSqlFileAsync_WithFilledActiveTab_CreatesNewTab()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sql");
        await File.WriteAllTextAsync(path, "SELECT 1;");
        try
        {
            var sut = new SqlEditorViewModel();
            sut.ActiveTab.SqlText = "SELECT old;";
            sut.ActiveTab.IsDirty = true;

            bool opened = await sut.OpenSqlFileAsync(path);

            Assert.True(opened);
            Assert.Equal(2, sut.EditorTabs.Count);
            Assert.Equal(1, sut.ActiveEditorTabIndex);
            Assert.Equal("SELECT 1;", sut.ActiveTab.SqlText);
            Assert.Equal(path, sut.ActiveTab.FilePath);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task OpenSqlFileAsync_MissingPath_ReturnsFalse()
    {
        var sut = new SqlEditorViewModel();

        bool opened = await sut.OpenSqlFileAsync("/tmp/this-file-should-not-exist-123456.sql");

        Assert.False(opened);
        AssertLocalized(
            sut.ExecutionStatusText,
            "Falha ao abrir arquivo SQL.",
            "Open failed.");
    }

    [Fact]
    public void GetSqlForExecution_WithoutSelection_ReturnsCurrentStatement()
    {
        var sut = new SqlEditorViewModel();
        sut.ActiveTab.SqlText = "SELECT 1; SELECT 2;";
        int caretOffset = sut.ActiveTab.SqlText.IndexOf("SELECT 2", StringComparison.Ordinal);

        string? sql = sut.GetSqlForExecution(0, 0, caretOffset);

        Assert.Equal("SELECT 2", sql);
    }

    [Fact]
    public void GetSqlForExecution_WithInvalidRange_ThrowsArgumentOutOfRangeException()
    {
        var sut = new SqlEditorViewModel();
        sut.ActiveTab.SqlText = "SELECT 1";

        Assert.Throws<ArgumentOutOfRangeException>(() => sut.GetSqlForExecution(-1, 0, 0));
    }

    [Fact]
    public void Constructor_InitializesExecutionFeedbackAsReady()
    {
        var sut = new SqlEditorViewModel();

        Assert.False(sut.IsExecuting);
        Assert.False(sut.HasExecutionError);
        AssertLocalized(sut.ExecutionStatusText, "Pronto.", "Ready.");
        Assert.Null(sut.ExecutionDetailText);
        Assert.Null(sut.ResultRowsView);
        Assert.Empty(sut.ResultTabs);
        Assert.Equal(-1, sut.SelectedResultTabIndex);
        Assert.Empty(sut.ExecutionHistory);
        AssertLocalized(sut.ExecutionTelemetryText, "Sem telemetria de execucao ainda.", "No execution telemetry yet.");
        Assert.Equal("Completion: sem amostras ainda.", sut.CompletionTelemetryText);
        AssertLocalized(sut.ExecutionTelemetryErrorsText, "Sem erros agregados.", "No aggregated errors.");
        AssertLocalized(sut.LastExecutionMessage, "Execute uma instrucao para ver mensagens.", "Execute a statement to see messages.");
        AssertLocalized(sut.ResultSummaryText, "Linhas: -    Tempo: -", "Rows: -    Time: -");
        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.Empty(sut.PendingMutationIssues);
        Assert.Null(sut.PendingMutationCountQuery);
        Assert.Null(sut.PendingMutationEstimatedRows);
        AssertLocalized(sut.PendingMutationEstimateText, "Sem estimativa de mutacao disponivel.", "No mutation estimate available.");
        Assert.False(sut.HasPendingMutationDiff);
        AssertLocalized(sut.PendingMutationDiffText, "Sem diff transacional disponivel.", "No transactional diff preview available.");
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_StoresLastResultAndHistory()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");

        var configured = new SqlEditorViewModel(
            selectionExtractor: null,
            executionService: new SqlEditorExecutionService(new FakeOrchestratorFactory()),
            connectionConfigResolver: () => config);

        configured.ActiveTab.SqlText = "SELECT 1; SELECT 2;";
        int configuredCaret = configured.ActiveTab.SqlText.IndexOf("SELECT 2", StringComparison.Ordinal);

        SqlEditorResultSet result = await configured.ExecuteSelectionOrCurrentAsync(0, 0, configuredCaret);

        Assert.True(result.Success);
        Assert.Equal("SELECT 2", result.StatementSql);
        Assert.Same(result, configured.ActiveTab.LastResult);
        Assert.Single(configured.ActiveTab.ExecutionHistory);
        Assert.Equal("SELECT 2", configured.ActiveTab.ExecutionHistory[0].Sql);
        Assert.False(configured.IsExecuting);
        Assert.False(configured.HasExecutionError);
        AssertLocalized(configured.ExecutionStatusText, "Execucao concluida com sucesso.", "Execution succeeded.");
        AssertLocalized(configured.ExecutionDetailText, "1 linha(s) em 3 ms.", "1 row(s) in 3 ms.");
        Assert.NotNull(configured.ResultRowsView);
        Assert.Single(configured.ResultTabs);
        Assert.Equal(0, configured.SelectedResultTabIndex);
        AssertLocalized(configured.LastExecutionMessage, "Execucao concluida com sucesso.", "Execution completed successfully.");
        AssertLocalized(configured.ResultSummaryText, "Linhas: 1    Tempo: 3 ms", "Rows: 1    Time: 3 ms");
        Assert.Single(configured.ExecutionHistory);
        Assert.Equal(1, configured.ExecutionTelemetry.StatementCount);
        Assert.Equal(1, configured.ExecutionTelemetry.SuccessCount);
        Assert.Equal(0, configured.ExecutionTelemetry.FailureCount);
        Assert.Equal(3, configured.ExecutionTelemetry.TotalDurationMs);
        AssertLocalized(configured.ExecutionTelemetryErrorsText, "Sem erros agregados.", "No aggregated errors.");
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_UsesConnectionConfigFromSelectedProfile()
    {
        var factory = new ConfigCapturingOrchestratorFactory();
        ConnectionConfig defaultConfig = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        ConnectionConfig profileConfig = new(
            DatabaseProvider.MySql,
            "my-host",
            3306,
            "db2",
            "user2",
            "pass2");

        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => defaultConfig,
            connectionConfigByProfileIdResolver: profileId =>
                string.Equals(profileId, "my-profile", StringComparison.Ordinal) ? profileConfig : null,
            connectionProfilesResolver: () =>
            [
                new SqlEditorConnectionProfileOption
                {
                    Id = "my-profile",
                    DisplayName = "MySql Main",
                    Provider = DatabaseProvider.MySql,
                },
            ]);

        sut.ActiveTabProvider = DatabaseProvider.MySql;
        sut.ActiveTabConnectionProfileId = "my-profile";
        sut.ActiveTab.SqlText = "SELECT 1;";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.True(result.Success);
        Assert.Equal(DatabaseProvider.MySql, factory.LastProvider);
        Assert.Equal("my-host", factory.LastHost);
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_WhenNoStatementFound_StoresFailure()
    {
        var sut = new SqlEditorViewModel();
        sut.ActiveTab.SqlText = "   ";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.False(result.Success);
        AssertLocalized(
            result.ErrorMessage,
            "Nenhuma instrucao SQL selecionada para execucao.",
            "No SQL statement selected for execution.");
        Assert.Same(result, sut.ActiveTab.LastResult);
        Assert.Single(sut.ActiveTab.ExecutionHistory);
        Assert.False(sut.ActiveTab.ExecutionHistory[0].Success);
        Assert.False(sut.IsExecuting);
        Assert.True(sut.HasExecutionError);
        AssertLocalized(sut.ExecutionStatusText, "Falha na execucao.", "Execution failed.");
        AssertLocalized(
            sut.ExecutionDetailText,
            "Nenhuma instrucao SQL selecionada para execucao.",
            "No SQL statement selected for execution.");
        Assert.Null(sut.ResultRowsView);
        AssertLocalized(
            sut.LastExecutionMessage,
            "Nenhuma instrucao SQL selecionada para execucao.",
            "No SQL statement selected for execution.");
        AssertLocalized(sut.ResultSummaryText, "Linhas: -    Tempo: 0 ms", "Rows: -    Time: 0 ms");
        Assert.Equal(1, sut.ExecutionTelemetry.StatementCount);
        Assert.Equal(0, sut.ExecutionTelemetry.SuccessCount);
        Assert.Equal(1, sut.ExecutionTelemetry.FailureCount);
        Assert.True(
            sut.ExecutionTelemetryErrorsText.Contains("Nenhuma instrucao SQL selecionada para execucao.", StringComparison.Ordinal)
            || sut.ExecutionTelemetryErrorsText.Contains("No SQL statement selected for execution.", StringComparison.Ordinal));
    }


    [Fact]
    public async Task ExecuteSelectionOrCurrent_WhenResultHasNoRows_DoesNotShowResultsSheet()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");

        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(new EmptyResultOrchestratorFactory()),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1 WHERE 1 = 0;";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.True(result.Success);
        Assert.NotNull(sut.ResultRowsView);
        Assert.True(sut.HasResultRows);
        Assert.False(sut.ShouldShowResultsSheet);
        Assert.True(sut.CanExportReport);
    }
    [Fact]
    public async Task ExecuteSelectionOrCurrent_KeepsHistoryCappedAtFiveHundredItems()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var sut = new SqlEditorViewModel(connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1;";

        for (int i = 0; i < 600; i++)
            _ = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.Equal(500, sut.ActiveTab.ExecutionHistory.Count);
    }

    [Fact]
    public async Task CancelExecution_WhenRunInProgress_UpdatesCanceledFeedback()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");

        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(new CancelAwareOrchestratorFactory()),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1;";

        Task<SqlEditorResultSet> run = sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);
        await Task.Delay(30);
        sut.CancelExecution();

        SqlEditorResultSet result = await run;

        Assert.False(result.Success);
        AssertLocalized(result.ErrorMessage, "A execucao SQL foi cancelada.", "SQL execution was canceled.");
        Assert.False(sut.IsExecuting);
        Assert.False(sut.HasExecutionError);
        AssertLocalized(sut.ExecutionStatusText, "Execucao cancelada.", "Execution canceled.");
        AssertLocalized(sut.ExecutionDetailText, "A execucao SQL foi cancelada.", "SQL execution was canceled.");
    }

    [Fact]
    public async Task CancelExecution_TransitionsPrimaryActionToStoppingState()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");

        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(new CancelAwareOrchestratorFactory()),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1;";

        Task<SqlEditorResultSet> run = sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);
        Assert.True(await WaitUntilAsync(() => sut.IsExecuting, 1000));

        sut.CancelExecution();

        Assert.True(sut.IsCancellationPending);
        Assert.False(sut.CanExecuteOrCancel);
        Assert.Equal("Parando...", sut.ExecuteOrCancelButtonText);

        _ = await run;

        Assert.False(sut.IsCancellationPending);
        Assert.True(sut.CanExecuteOrCancel);
        Assert.Equal("Executar", sut.ExecuteOrCancelButtonText);
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_WhenMutationNeedsConfirmation_DoesNotExecuteImmediately()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new CountingOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "DELETE FROM orders;";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.False(result.Success);
        AssertLocalized(result.ErrorMessage, "Confirmacao de mutacao necessaria.", "Mutation confirmation required.");
        Assert.True(sut.HasPendingMutationConfirmation);
        Assert.Contains(sut.PendingMutationIssues, i => i.Code == "NO_WHERE");
        Assert.Equal("SELECT COUNT(*) FROM orders", sut.PendingMutationCountQuery);
        Assert.Equal(2, factory.ExecuteCount);
        Assert.Equal(1, sut.PendingMutationEstimatedRows);
        AssertLocalized(sut.PendingMutationEstimateText, "Linhas afetadas estimadas: 1", "Estimated affected rows: 1");
        Assert.True(sut.HasPendingMutationDiff);
        Assert.True(
            sut.PendingMutationDiffText.Contains("ROLLBACK garantido", StringComparison.OrdinalIgnoreCase)
            || sut.PendingMutationDiffText.Contains("ROLLBACK guaranteed", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(sut.ExecutionHistory);
    }

    [Fact]
    public async Task ConfirmPendingMutationAsync_ExecutesAndStoresHistory()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new CountingOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "UPDATE orders SET status='x';";
        _ = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        SqlEditorResultSet? confirmed = await sut.ConfirmPendingMutationAsync();

        Assert.NotNull(confirmed);
        Assert.True(confirmed!.Success);
        Assert.Equal(3, factory.ExecuteCount);
        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.Single(sut.ExecutionHistory);
        AssertLocalized(sut.ExecutionStatusText, "Execucao concluida com sucesso.", "Execution succeeded.");
    }

    [Fact]
    public async Task CancelPendingMutation_ClearsPendingWithoutExecuting()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new CountingOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "TRUNCATE orders;";
        _ = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        sut.CancelPendingMutation();

        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.False(sut.HasPendingMutationDiff);
        Assert.Equal(0, factory.ExecuteCount);
        AssertLocalized(
            sut.ExecutionStatusText,
            "Execucao da mutacao cancelada.",
            "Mutation execution canceled.");
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_WhenNoConnection_LeavesMutationEstimateUnavailable()
    {
        var sut = new SqlEditorViewModel(connectionConfigResolver: () => null);
        sut.ActiveTab.SqlText = "DELETE FROM orders;";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.False(result.Success);
        AssertLocalized(result.ErrorMessage, "Confirmacao de mutacao necessaria.", "Mutation confirmation required.");
        Assert.True(sut.HasPendingMutationConfirmation);
        Assert.Null(sut.PendingMutationEstimatedRows);
        AssertLocalized(
            sut.PendingMutationEstimateText,
            "Nao foi possivel estimar as linhas afetadas automaticamente.",
            "Could not estimate affected rows automatically.");
        Assert.False(sut.HasPendingMutationDiff);
        Assert.Contains("indisponivel", sut.PendingMutationDiffText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAllAsync_ExecutesStatementsSequentially()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new CountingOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1; SELECT 2;";

        IReadOnlyList<SqlEditorResultSet> results = await sut.ExecuteAllAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(2, factory.ExecuteCount);
        Assert.Equal("SELECT 2", sut.ActiveTab.LastResult?.StatementSql);
        Assert.Equal(2, sut.ResultTabs.Count);
        AssertLocalized(sut.ResultTabs[0].Title, "Resultado 1", "Result 1");
        AssertLocalized(sut.ResultTabs[1].Title, "Resultado 2", "Result 2");
        Assert.Equal(1, sut.SelectedResultTabIndex);
        Assert.Equal(2, sut.ExecutionHistory.Count);
        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.Equal(2, sut.ExecutionTelemetry.StatementCount);
        Assert.Equal(2, sut.ExecutionTelemetry.SuccessCount);
        Assert.Equal(0, sut.ExecutionTelemetry.FailureCount);
        Assert.Equal(4, sut.ExecutionTelemetry.TotalDurationMs);
    }

    [Fact]
    public async Task ExecuteAllAsync_UpdatesStatusWithStatementCounterWhileRunning()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(new SlowSuccessOrchestratorFactory(TimeSpan.FromMilliseconds(220))),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1; SELECT 2;";

        Task<IReadOnlyList<SqlEditorResultSet>> run = sut.ExecuteAllAsync();

        bool observed = await WaitUntilAsync(
            () => sut.ExecutionStatusText.Contains("statement", StringComparison.OrdinalIgnoreCase)
                && sut.ExecutionStatusText.Contains("2", StringComparison.Ordinal),
            2500);

        IReadOnlyList<SqlEditorResultSet> results = await run;

        Assert.True(observed);
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void ActiveConnectionContextBadgeText_WhenProfileContainsProd_UsesProductionHeuristic()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "appdb",
            "user",
            "pass");
        var sut = new SqlEditorViewModel(
            connectionConfigResolver: () => config,
            connectionConfigByProfileIdResolver: profileId =>
                string.Equals(profileId, "prod-profile", StringComparison.Ordinal) ? config : null,
            connectionProfilesResolver: () =>
            [
                new SqlEditorConnectionProfileOption
                {
                    Id = "prod-profile",
                    DisplayName = "prod-main",
                    Provider = DatabaseProvider.Postgres,
                },
            ]);

        sut.ActiveTabConnectionProfileId = "prod-profile";

        Assert.True(sut.HasActiveConnection);
        Assert.True(sut.IsProductionConnectionContext);
        Assert.False(sut.IsStagingConnectionContext);
        Assert.False(sut.IsNeutralConnectionContext);
        Assert.Contains("[PostgreSQL]", sut.ActiveConnectionContextBadgeText, StringComparison.Ordinal);
        Assert.Contains("prod-main/default", sut.ActiveConnectionContextBadgeText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SelectedResultTabIndex_ChangesVisibleMessageAndSummary()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new ScriptAwareOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1; SELECT fail;";

        _ = await sut.ExecuteAllAsync();

        Assert.Equal(2, sut.ResultTabs.Count);
        Assert.Equal(1, sut.SelectedResultTabIndex);
        Assert.Equal("forced failure", sut.LastExecutionMessage);
        AssertLocalized(
            sut.ResultSummaryText,
            "Linhas: -    Tempo: 5 ms",
            "Rows: -    Time: 5 ms");

        sut.SelectedResultTabIndex = 0;

        AssertLocalized(sut.LastExecutionMessage, "Execucao concluida com sucesso.", "Execution completed successfully.");
        AssertLocalized(sut.ResultSummaryText, "Linhas: 1    Tempo: 1 ms", "Rows: 1    Time: 1 ms");
    }

    [Fact]
    public async Task ExecuteAllAsync_StopsWhenMutationConfirmationIsRequired()
    {
        ConnectionConfig config = new(
            DatabaseProvider.Postgres,
            "localhost",
            5432,
            "db",
            "user",
            "pass");
        var factory = new CountingOrchestratorFactory();
        var sut = new SqlEditorViewModel(
            executionService: new SqlEditorExecutionService(factory),
            connectionConfigResolver: () => config);
        sut.ActiveTab.SqlText = "SELECT 1; DELETE FROM orders; SELECT 2;";

        IReadOnlyList<SqlEditorResultSet> results = await sut.ExecuteAllAsync();

        Assert.Equal(2, results.Count);
        Assert.Equal(3, factory.ExecuteCount);
        Assert.True(sut.HasPendingMutationConfirmation);
        Assert.Contains(sut.PendingMutationIssues, i => i.Code == "NO_WHERE");
        Assert.Equal(1, sut.PendingMutationEstimatedRows);
        Assert.Single(sut.ExecutionHistory);
        Assert.Equal("SELECT 1", sut.ActiveTab.LastResult?.StatementSql);
        Assert.Equal(2, sut.ExecutionTelemetry.StatementCount);
        Assert.Equal(1, sut.ExecutionTelemetry.SuccessCount);
        Assert.Equal(1, sut.ExecutionTelemetry.FailureCount);
        Assert.True(
            sut.ExecutionTelemetryErrorsText.Contains("Confirmacao de mutacao necessaria.", StringComparison.Ordinal)
            || sut.ExecutionTelemetryErrorsText.Contains("Mutation confirmation required.", StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_WhenDraftsExist_RestoresTabsAndClearsDraftStore()
    {
        var draftStore = new FakeSqlEditorSessionDraftStore(
        [
            new SqlEditorSessionDraftEntry
            {
                TabId = "draft-1",
                FallbackTitle = "Script A",
                SqlText = "SELECT 1;",
                Provider = DatabaseProvider.Postgres,
                ConnectionProfileId = "profile-a",
                TabOrder = 0,
                IsActive = false,
                SavedAtUtc = DateTimeOffset.UtcNow,
            },
            new SqlEditorSessionDraftEntry
            {
                TabId = "draft-2",
                FallbackTitle = "Script B",
                SqlText = "SELECT 2;",
                Provider = DatabaseProvider.MySql,
                ConnectionProfileId = "profile-b",
                TabOrder = 1,
                IsActive = true,
                SavedAtUtc = DateTimeOffset.UtcNow,
            },
        ]);

        var sut = new SqlEditorViewModel(sessionDraftStore: draftStore);

        Assert.Equal(2, sut.EditorTabs.Count);
        Assert.Equal(1, sut.ActiveEditorTabIndex);
        Assert.Equal("SELECT 1;", sut.EditorTabs[0].SqlText);
        Assert.Equal("SELECT 2;", sut.EditorTabs[1].SqlText);
        Assert.True(sut.EditorTabs[0].IsDirty);
        Assert.True(sut.EditorTabs[1].IsDirty);
        Assert.Equal(1, draftStore.ClearDraftsCallCount);
    }

    private sealed class FakeOrchestratorFactory : IDbOrchestratorFactory
    {
        public IDbOrchestrator Create(ConnectionConfig config) => new FakeOrchestrator(config);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;
    }

    private sealed class FakeOrchestrator(ConnectionConfig config) : IDbOrchestrator
    {
        public DatabaseProvider Provider => config.Provider;
        public ConnectionConfig Config => config;

        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConnectionTestResult(true));

        public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
            Task.FromResult(new DatabaseSchema("db", config.Provider, []));

        public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
        {
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            return Task.FromResult(new PreviewResult(true, table, null, TimeSpan.FromMilliseconds(3), 1));
        }

        public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
            Task.FromResult(new DdlExecutionResult(true, []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CancelAwareOrchestratorFactory : IDbOrchestratorFactory
    {
        public IDbOrchestrator Create(ConnectionConfig config) => new CancelAwareOrchestrator(config);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;
    }

    private sealed class CancelAwareOrchestrator(ConnectionConfig config) : IDbOrchestrator
    {
        public DatabaseProvider Provider => config.Provider;
        public ConnectionConfig Config => config;

        public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
            Task.FromResult(new ConnectionTestResult(true));

        public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
            Task.FromResult(new DatabaseSchema("db", config.Provider, []));

        public async Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new PreviewResult(true, BuildTable(rows: 1), null, TimeSpan.FromMilliseconds(1), 1);
        }

        public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
            Task.FromResult(new DdlExecutionResult(true, []));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static DataTable BuildTable(int rows)
    {
        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        for (int i = 0; i < rows; i++)
            table.Rows.Add(i + 1);
        return table;
    }

    private static DbMetadata BuildMetadata(string schemaName, string tableName)
    {
        var table = new TableMetadata(
            schemaName,
            tableName,
            TableKind.Table,
            EstimatedRowCount: null,
            Columns: [],
            Indexes: [],
            OutboundForeignKeys: [],
            InboundForeignKeys: []);

        return new DbMetadata(
            DatabaseName: "db",
            Provider: DatabaseProvider.Postgres,
            ServerVersion: "16",
            CapturedAt: DateTimeOffset.UtcNow,
            Schemas: [new SchemaMetadata(schemaName, [table])],
            AllForeignKeys: []);
    }

    private static void AssertLocalized(string? actual, params string[] expectedValues)
    {
        Assert.NotNull(actual);
        Assert.Contains(actual!, expectedValues);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (condition())
                return true;

            await Task.Delay(20);
        }

        return condition();
    }

    private sealed class CountingOrchestratorFactory : IDbOrchestratorFactory
    {
        public int ExecuteCount { get; private set; }

        public IDbOrchestrator Create(ConnectionConfig config) => new CountingOrchestrator(config, this);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;

        private sealed class CountingOrchestrator(ConnectionConfig config, CountingOrchestratorFactory owner) : IDbOrchestrator
        {
            public DatabaseProvider Provider => config.Provider;
            public ConnectionConfig Config => config;

            public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
                Task.FromResult(new ConnectionTestResult(true));

            public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
                Task.FromResult(new DatabaseSchema("db", config.Provider, []));

            public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
            {
                owner.ExecuteCount++;
                return Task.FromResult(new PreviewResult(true, BuildTable(rows: 1), null, TimeSpan.FromMilliseconds(2), 1));
            }

            public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
                Task.FromResult(new DdlExecutionResult(true, []));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class SlowSuccessOrchestratorFactory(TimeSpan delay) : IDbOrchestratorFactory
    {
        public IDbOrchestrator Create(ConnectionConfig config) => new SlowSuccessOrchestrator(config, delay);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;

        private sealed class SlowSuccessOrchestrator(ConnectionConfig config, TimeSpan delay) : IDbOrchestrator
        {
            public DatabaseProvider Provider => config.Provider;
            public ConnectionConfig Config => config;

            public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
                Task.FromResult(new ConnectionTestResult(true));

            public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
                Task.FromResult(new DatabaseSchema("db", config.Provider, []));

            public async Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
            {
                await Task.Delay(delay, ct);
                DataTable table = BuildTable(rows: 1);
                return new PreviewResult(true, table, null, TimeSpan.FromMilliseconds(delay.TotalMilliseconds), RowsAffected: 1);
            }

            public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
                Task.FromResult(new DdlExecutionResult(true, []));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ScriptAwareOrchestratorFactory : IDbOrchestratorFactory
    {
        public IDbOrchestrator Create(ConnectionConfig config) => new ScriptAwareOrchestrator(config);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;

        private sealed class ScriptAwareOrchestrator(ConnectionConfig config) : IDbOrchestrator
        {
            public DatabaseProvider Provider => config.Provider;
            public ConnectionConfig Config => config;

            public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
                Task.FromResult(new ConnectionTestResult(true));

            public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
                Task.FromResult(new DatabaseSchema("db", config.Provider, []));

            public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
            {
                if (sql.Contains("fail", StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new PreviewResult(false, null, "forced failure", TimeSpan.FromMilliseconds(5), null));

                return Task.FromResult(new PreviewResult(true, BuildTable(rows: 1), null, TimeSpan.FromMilliseconds(1), 1));
            }

            public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
                Task.FromResult(new DdlExecutionResult(true, []));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ConfigCapturingOrchestratorFactory : IDbOrchestratorFactory
    {
        public DatabaseProvider? LastProvider { get; private set; }
        public string? LastHost { get; private set; }

        public IDbOrchestrator Create(ConnectionConfig config)
        {
            LastProvider = config.Provider;
            LastHost = config.Host;
            return new CapturingOrchestrator(config);
        }

        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;

        private sealed class CapturingOrchestrator(ConnectionConfig config) : IDbOrchestrator
        {
            public DatabaseProvider Provider => config.Provider;
            public ConnectionConfig Config => config;

            public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
                Task.FromResult(new ConnectionTestResult(true));

            public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
                Task.FromResult(new DatabaseSchema("db", config.Provider, []));

            public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default) =>
                Task.FromResult(new PreviewResult(true, BuildTable(rows: 1), null, TimeSpan.FromMilliseconds(1), 1));

            public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
                Task.FromResult(new DdlExecutionResult(true, []));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class EmptyResultOrchestratorFactory : IDbOrchestratorFactory
    {
        public IDbOrchestrator Create(ConnectionConfig config) => new EmptyResultOrchestrator(config);
        public Func<ConnectionConfig, IDbOrchestrator>? Register(DatabaseProvider provider, Func<ConnectionConfig, IDbOrchestrator> factory) => null;
        public bool IsRegistered(DatabaseProvider provider) => true;

        private sealed class EmptyResultOrchestrator(ConnectionConfig config) : IDbOrchestrator
        {
            public DatabaseProvider Provider => config.Provider;
            public ConnectionConfig Config => config;

            public Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) =>
                Task.FromResult(new ConnectionTestResult(true));

            public Task<DatabaseSchema> GetSchemaAsync(CancellationToken ct = default) =>
                Task.FromResult(new DatabaseSchema("db", config.Provider, []));

            public Task<PreviewResult> ExecutePreviewAsync(string sql, int maxRows = PreviewExecutionOptions.UseConfiguredDefault, CancellationToken ct = default)
            {
                var table = new DataTable();
                table.Columns.Add("id", typeof(int));
                return Task.FromResult(new PreviewResult(true, table, null, TimeSpan.FromMilliseconds(2), 0));
            }

            public Task<DdlExecutionResult> ExecuteDdlAsync(string sql, bool stopOnError = true, CancellationToken ct = default) =>
                Task.FromResult(new DdlExecutionResult(true, []));

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSqlEditorSessionDraftStore(
        IReadOnlyList<SqlEditorSessionDraftEntry>? drafts = null) : ISqlEditorSessionDraftStore
    {
        private List<SqlEditorSessionDraftEntry> _drafts = drafts?.ToList() ?? [];

        public int SaveDraftsCallCount { get; private set; }
        public int ClearDraftsCallCount { get; private set; }

        public IReadOnlyList<SqlEditorSessionDraftEntry> LoadDrafts() => _drafts;

        public void SaveDrafts(IReadOnlyList<SqlEditorSessionDraftEntry> draftsToSave)
        {
            SaveDraftsCallCount++;
            _drafts = draftsToSave.ToList();
        }

        public void ClearDrafts()
        {
            ClearDraftsCallCount++;
            _drafts = [];
        }
    }
}
