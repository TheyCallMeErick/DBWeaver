using System.Data;
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
        Assert.Equal("Tab close requires confirmation.", sut.ExecutionStatusText);
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
        Assert.Equal("Tab closed.", sut.ExecutionStatusText);
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
        Assert.Equal("Tab close canceled.", sut.ExecutionStatusText);
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
        Assert.Equal("Save canceled.", sut.ExecutionStatusText);
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
        Assert.Equal("Open failed.", sut.ExecutionStatusText);
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
        Assert.Equal("Ready.", sut.ExecutionStatusText);
        Assert.Null(sut.ExecutionDetailText);
        Assert.Null(sut.ResultRowsView);
        Assert.Empty(sut.ResultTabs);
        Assert.Equal(-1, sut.SelectedResultTabIndex);
        Assert.Empty(sut.ExecutionHistory);
        Assert.Equal("No execution telemetry yet.", sut.ExecutionTelemetryText);
        Assert.Equal("No aggregated errors.", sut.ExecutionTelemetryErrorsText);
        Assert.Equal("Execute a statement to see messages.", sut.LastExecutionMessage);
        Assert.Equal("Rows: -    Time: -", sut.ResultSummaryText);
        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.Empty(sut.PendingMutationIssues);
        Assert.Null(sut.PendingMutationCountQuery);
        Assert.Null(sut.PendingMutationEstimatedRows);
        Assert.Equal("No mutation estimate available.", sut.PendingMutationEstimateText);
        Assert.False(sut.HasPendingMutationDiff);
        Assert.Equal("No transactional diff preview available.", sut.PendingMutationDiffText);
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
        Assert.Equal("Execution succeeded.", configured.ExecutionStatusText);
        Assert.Equal("1 row(s) in 3 ms.", configured.ExecutionDetailText);
        Assert.NotNull(configured.ResultRowsView);
        Assert.Single(configured.ResultTabs);
        Assert.Equal(0, configured.SelectedResultTabIndex);
        Assert.Equal("Execution completed successfully.", configured.LastExecutionMessage);
        Assert.Equal("Rows: 1    Time: 3 ms", configured.ResultSummaryText);
        Assert.Single(configured.ExecutionHistory);
        Assert.Equal(1, configured.ExecutionTelemetry.StatementCount);
        Assert.Equal(1, configured.ExecutionTelemetry.SuccessCount);
        Assert.Equal(0, configured.ExecutionTelemetry.FailureCount);
        Assert.Equal(3, configured.ExecutionTelemetry.TotalDurationMs);
        Assert.Equal("No aggregated errors.", configured.ExecutionTelemetryErrorsText);
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
        Assert.Equal("No SQL statement selected for execution.", result.ErrorMessage);
        Assert.Same(result, sut.ActiveTab.LastResult);
        Assert.Single(sut.ActiveTab.ExecutionHistory);
        Assert.False(sut.ActiveTab.ExecutionHistory[0].Success);
        Assert.False(sut.IsExecuting);
        Assert.True(sut.HasExecutionError);
        Assert.Equal("Execution failed.", sut.ExecutionStatusText);
        Assert.Equal("No SQL statement selected for execution.", sut.ExecutionDetailText);
        Assert.Null(sut.ResultRowsView);
        Assert.Equal("No SQL statement selected for execution.", sut.LastExecutionMessage);
        Assert.Equal("Rows: -    Time: 0 ms", sut.ResultSummaryText);
        Assert.Equal(1, sut.ExecutionTelemetry.StatementCount);
        Assert.Equal(0, sut.ExecutionTelemetry.SuccessCount);
        Assert.Equal(1, sut.ExecutionTelemetry.FailureCount);
        Assert.Contains("No SQL statement selected for execution.", sut.ExecutionTelemetryErrorsText);
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
    public async Task ExecuteSelectionOrCurrent_KeepsHistoryCappedAtFiftyItems()
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

        for (int i = 0; i < 60; i++)
            _ = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.Equal(50, sut.ActiveTab.ExecutionHistory.Count);
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
        Assert.Equal("SQL execution was canceled.", result.ErrorMessage);
        Assert.False(sut.IsExecuting);
        Assert.False(sut.HasExecutionError);
        Assert.Equal("Execution canceled.", sut.ExecutionStatusText);
        Assert.Equal("SQL execution was canceled.", sut.ExecutionDetailText);
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
        Assert.Equal("Mutation confirmation required.", result.ErrorMessage);
        Assert.True(sut.HasPendingMutationConfirmation);
        Assert.Contains(sut.PendingMutationIssues, i => i.Code == "NO_WHERE");
        Assert.Equal("SELECT COUNT(*) FROM orders", sut.PendingMutationCountQuery);
        Assert.Equal(2, factory.ExecuteCount);
        Assert.Equal(1, sut.PendingMutationEstimatedRows);
        Assert.Equal("Estimated affected rows: 1", sut.PendingMutationEstimateText);
        Assert.True(sut.HasPendingMutationDiff);
        Assert.Contains("ROLLBACK guaranteed", sut.PendingMutationDiffText);
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
        Assert.Equal("Execution succeeded.", sut.ExecutionStatusText);
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
        Assert.Equal("Mutation execution canceled.", sut.ExecutionStatusText);
    }

    [Fact]
    public async Task ExecuteSelectionOrCurrent_WhenNoConnection_LeavesMutationEstimateUnavailable()
    {
        var sut = new SqlEditorViewModel(connectionConfigResolver: () => null);
        sut.ActiveTab.SqlText = "DELETE FROM orders;";

        SqlEditorResultSet result = await sut.ExecuteSelectionOrCurrentAsync(0, 0, 0);

        Assert.False(result.Success);
        Assert.Equal("Mutation confirmation required.", result.ErrorMessage);
        Assert.True(sut.HasPendingMutationConfirmation);
        Assert.Null(sut.PendingMutationEstimatedRows);
        Assert.Equal("Could not estimate affected rows automatically.", sut.PendingMutationEstimateText);
        Assert.False(sut.HasPendingMutationDiff);
        Assert.Contains("unavailable", sut.PendingMutationDiffText, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("Result 1", sut.ResultTabs[0].Title);
        Assert.Equal("Result 2", sut.ResultTabs[1].Title);
        Assert.Equal(1, sut.SelectedResultTabIndex);
        Assert.Equal(2, sut.ExecutionHistory.Count);
        Assert.False(sut.HasPendingMutationConfirmation);
        Assert.Equal(2, sut.ExecutionTelemetry.StatementCount);
        Assert.Equal(2, sut.ExecutionTelemetry.SuccessCount);
        Assert.Equal(0, sut.ExecutionTelemetry.FailureCount);
        Assert.Equal(4, sut.ExecutionTelemetry.TotalDurationMs);
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
        Assert.Equal("Rows: -    Time: 5 ms", sut.ResultSummaryText);

        sut.SelectedResultTabIndex = 0;

        Assert.Equal("Execution completed successfully.", sut.LastExecutionMessage);
        Assert.Equal("Rows: 1    Time: 1 ms", sut.ResultSummaryText);
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
        Assert.Contains("Mutation confirmation required.", sut.ExecutionTelemetryErrorsText);
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
}
