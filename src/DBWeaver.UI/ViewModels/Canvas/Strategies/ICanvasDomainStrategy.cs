using System.Collections.ObjectModel;
using Avalonia;
using DBWeaver.Metadata;
using DBWeaver.Nodes;
using DBWeaver.UI.Serialization;

namespace DBWeaver.UI.ViewModels.Canvas.Strategies;

public sealed record CanvasSnapshot(string JsonPayload);

public sealed record NodeSuggestion(NodeType NodeType, string Reason);

/// <summary>
/// Encapsulates domain-specific canvas behavior for Query and DDL domains.
/// Allows CanvasViewModel to operate without direct knowledge of domain-specific rules.
/// </summary>
public interface ICanvasDomainStrategy
{
    /// <summary>Gets the name of this domain (e.g., "Query" or "DDL").</summary>
    string DomainName { get; }

    /// <summary>Returns true if the node type supports a sub-editor (e.g., CTE, View).</summary>
    bool CanEnterSubEditor(NodeViewModel node);

    /// <summary>
    /// Asynchronously retrieves the canvas seed (initial state snapshot) for opening a sub-editor
    /// on the given node. Returns null if the node does not support a sub-editor.
    /// </summary>
    Task<CanvasSnapshot?> GetSubEditorSeedAsync(NodeViewModel node);

    /// <summary>
    /// Called after a new connection is established in the canvas.
    /// Used for domain-specific synchronization (e.g., DDL column syncing).
    /// </summary>
    void OnConnectionEstablished(
        ConnectionViewModel connection,
        IEnumerable<ConnectionViewModel> allConnections,
        IEnumerable<NodeViewModel> allNodes
    );

    /// <summary>
    /// Called after a connection is removed from the canvas.
    /// Used for domain-specific cleanup.
    /// </summary>
    void OnConnectionRemoved(
        ConnectionViewModel connection,
        IEnumerable<ConnectionViewModel> allConnections,
        IEnumerable<NodeViewModel> allNodes
    );

    /// <summary>
    /// Called after a new node is added to the canvas.
    /// Used for domain-specific initialization or validation.
    /// </summary>
    void OnNodeAdded(NodeViewModel node, IEnumerable<ConnectionViewModel> allConnections);

    /// <summary>
    /// Returns the list of nodes that act as "output" nodes in this domain
    /// (e.g., ResultOutput in Query, CreateTableOutput in DDL).
    /// Used by compilation and validation logic.
    /// </summary>
    IReadOnlyList<NodeViewModel> GetOutputNodes(IEnumerable<NodeViewModel> nodes);

    /// <summary>
    /// Returns suggestions for auto-completing a connection from the given source pin.
    /// Returns an empty list if the domain does not support connection suggestions.
    /// </summary>
    IReadOnlyList<NodeSuggestion> GetConnectionSuggestions(
        PinViewModel sourcePinViewModel,
        IEnumerable<NodeViewModel> canvasNodes
    );

    /// <summary>
    /// Called after a node parameter is changed (via undo/redo or property panel).
    /// Allows the domain to re-synchronize dependent nodes (e.g., table preview when
    /// a connected ScalarType's TypeKind changes).
    /// Default implementation is a no-op so existing strategies need not implement it.
    /// </summary>
    void OnParameterChanged(
        NodeViewModel node,
        string paramName,
        IEnumerable<ConnectionViewModel> allConnections,
        IEnumerable<NodeViewModel> allNodes
    ) { }

    /// <summary>
    /// Handles domain-specific behavior when a table from the database schema is inserted into the canvas.
    /// Returns true if the domain handled the insertion; otherwise false.
    /// </summary>
    bool TryHandleSchemaTableInsert(
        TableMetadata table,
        Point position,
        Func<bool>? isDdlModeActiveResolver,
        Action<TableMetadata, Point>? importDdlTableAction,
        Action spawnQueryTableNode
    );
}

/// <summary>
/// Extended domain strategy interface with helper methods for CTE/View editor operations.
/// Implemented by strategies that support sub-editor functionality.
/// </summary>
public interface ICanvasDomainStrategyExt : ICanvasDomainStrategy
{
    /// <summary>
    /// Extracts the subgraph of nodes and connections that form a CTE or View definition
    /// for editing in a sub-canvas.
    /// </summary>
    (List<SavedNode> Nodes, List<SavedConnection> Connections) ExtractCteEditableSubgraph(
        NodeViewModel cteNode,
        IEnumerable<NodeViewModel> allNodes,
        IEnumerable<ConnectionViewModel> allConnections
    );

    /// <summary>
    /// Removes the existing CTE/View query subgraph from the canvas during exit.
    /// </summary>
    void RemoveExistingCteQuerySubgraph(
        NodeViewModel cteNode,
        ObservableCollection<NodeViewModel> allNodes,
        ObservableCollection<ConnectionViewModel> allConnections
    );

    /// <summary>
    /// Resolves the ID of the primary output node from a list of saved nodes,
    /// if one exists and is applicable to this domain.
    /// </summary>
    string? ResolvePrimaryOutputNodeId(IEnumerable<SavedNode> nodes);
}
