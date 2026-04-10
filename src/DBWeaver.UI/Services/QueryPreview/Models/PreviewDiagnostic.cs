namespace DBWeaver.UI.Services.QueryPreview.Models;

public sealed class PreviewDiagnostic
{
    public PreviewDiagnostic(
        PreviewDiagnosticSeverity severity,
        PreviewDiagnosticCategory category,
        string code,
        string message,
        string? nodeId = null,
        string? pinName = null)
    {
        Severity = severity;
        Category = category;
        Code = code;
        Message = message;
        NodeId = nodeId;
        PinName = pinName;
    }

    public PreviewDiagnosticSeverity Severity { get; }

    public PreviewDiagnosticCategory Category { get; }

    public string Code { get; }

    public string Message { get; }

    public string? NodeId { get; }

    public string? PinName { get; }
}
