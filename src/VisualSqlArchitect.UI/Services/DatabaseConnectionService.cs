using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.Nodes;
using VisualSqlArchitect.UI.ViewModels;
using VisualSqlArchitect.UI.ViewModels.Canvas;

namespace VisualSqlArchitect.UI.Services;

/// <summary>
/// Orchestrates the database connection workflow:
/// 1. Establishes connection to the database
/// 2. Fetches and introspects database tables
/// 3. Loads tables into the search menu for quick access
/// 4. Initializes metadata service for auto-join suggestions
///
/// This service ensures that after a successful database connection,
/// the normal flow of operations is triggered automatically.
/// </summary>
public sealed class DatabaseConnectionService
{
    private readonly ILogger<DatabaseConnectionService> _logger;
    private MetadataService? _metadataService;
    private CancellationTokenSource? _operationCts;
    private DbMetadata? _loadedMetadata;

    /// <summary>
    /// The most recently loaded database metadata.
    /// Null if no database has been connected or loading failed.
    /// </summary>
    public DbMetadata? LoadedMetadata => _loadedMetadata;

    public DatabaseConnectionService(ILogger<DatabaseConnectionService>? logger = null)
    {
        _logger = logger ?? NullLogger<DatabaseConnectionService>.Instance;
    }

    /// <summary>
    /// Executes the complete database connection workflow:
    /// - Tests connection
    /// - Loads schema metadata
    /// - Populates search menu with tables
    /// - Initializes metadata service for auto-join detection
    /// </summary>
    public async Task ConnectAndLoadAsync(
        ConnectionConfig config,
        SearchMenuViewModel searchMenu,
        CancellationToken ct = default
    )
    {
        // Cancel any previous operation
        _operationCts?.Cancel();
        _operationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CancellationToken linked = _operationCts.Token;

        try
        {
            _logger.LogInformation(
                "[DatabaseConnectionService] Starting connection workflow for {Provider} @ {Host}:{Port}/{Database}",
                config.Provider,
                config.Host,
                config.Port,
                config.Database
            );

            // Step 1: Initialize the metadata service
            _metadataService = MetadataService.Create(config, cacheTtl: TimeSpan.FromMinutes(5));

            // Step 2: Fetch complete database schema
            _logger.LogInformation("[DatabaseConnectionService] Fetching database schema...");
            var metadata = await _metadataService.GetMetadataAsync(forceRefresh: true, ct: linked);

            // Store the loaded metadata for later access
            _loadedMetadata = metadata;

            _logger.LogInformation(
                "[DatabaseConnectionService] Schema loaded: {Tables} tables, {Views} views, {FKs} foreign keys",
                metadata.TotalTables,
                metadata.TotalViews,
                metadata.TotalForeignKeys
            );

            // Step 3: Convert metadata to SearchMenu format and load
            var tables = ConvertMetadataToTableList(metadata);
            _logger.LogInformation(
                "[DatabaseConnectionService] Loading {Count} tables into search menu",
                tables.Count
            );

            searchMenu.LoadTables(tables);

            _logger.LogInformation(
                "[DatabaseConnectionService] Database connection workflow completed successfully"
            );
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[DatabaseConnectionService] Connection workflow cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "[DatabaseConnectionService] Error during connection workflow: {Message}",
                ex.Message
            );
            throw;
        }
    }

    /// <summary>
    /// Converts DbMetadata to the tuple format expected by SearchMenuViewModel.LoadTables().
    /// Flattens all schemas and returns (FullName, Columns) pairs.
    /// </summary>
    private static List<(string FullName, IReadOnlyList<(string Name, PinDataType Type)> Cols)>
        ConvertMetadataToTableList(DbMetadata metadata)
    {
        var result = new List<(string, IReadOnlyList<(string, PinDataType)>)>();

        foreach (var schema in metadata.Schemas)
        {
            foreach (var table in schema.Tables)
            {
                // Build full name: schema.tablename
                string fullName = $"{table.Schema}.{table.Name}";

                // Convert columns to (Name, PinDataType) tuples
                var columns = table.Columns
                    .OrderBy(c => c.OrdinalPosition)
                    .Select(c => (
                        Name: c.Name,
                        Type: MapDataTypeToPinDataType(c)
                    ))
                    .ToList();

                result.Add((fullName, columns.AsReadOnly()));
            }
        }

        return result;
    }

    /// <summary>
    /// Maps database column semantics to UI pin data types.
    /// Used for proper icon and color rendering in the canvas.
    /// </summary>
    private static PinDataType MapDataTypeToPinDataType(ColumnMetadata column)
    {
        return column.SemanticType switch
        {
            ColumnSemanticType.Numeric => PinDataType.Number,
            ColumnSemanticType.Text => PinDataType.Text,
            ColumnSemanticType.DateTime => PinDataType.DateTime,
            ColumnSemanticType.Boolean => PinDataType.Boolean,
            ColumnSemanticType.Guid => PinDataType.Text, // GUIDs rendered as text in pins
            ColumnSemanticType.Document => PinDataType.Json,
            ColumnSemanticType.Binary => PinDataType.Text, // Binary data rendered as text
            ColumnSemanticType.Spatial => PinDataType.Json, // Spatial types as JSON
            ColumnSemanticType.Other => PinDataType.Text,
            _ => PinDataType.Text,
        };
    }

    /// <summary>
    /// Returns the active metadata service if connected, or null if not.
    /// Used for features like auto-join detection that require database schema.
    /// </summary>
    public MetadataService? GetActiveMetadataService() => _metadataService;

    /// <summary>
    /// Cancels any in-progress connection operations.
    /// </summary>
    public void Cancel()
    {
        _operationCts?.Cancel();
    }

    /// <summary>
    /// Cleans up resources. Call when switching connections or closing the app.
    /// </summary>
    public void Dispose()
    {
        _operationCts?.Dispose();
        _metadataService = null;
    }
}
