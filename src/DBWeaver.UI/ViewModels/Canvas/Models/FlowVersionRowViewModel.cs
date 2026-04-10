using DBWeaver.UI.Serialization;

namespace DBWeaver.UI.ViewModels.Canvas;

public sealed class FlowVersionRowViewModel(FlowVersion version) : ViewModelBase
{
    public FlowVersion Version { get; } = version;

    public string Id => Version.Id;
    public string Label => Version.Label;
    public string NodeCount => $"{Version.NodeCount} nodes, {Version.ConnectionCount} wires";

    public string CreatedAtFormatted
    {
        get
        {
            if (DateTimeOffset.TryParse(Version.CreatedAt, out var dt))
            {
                var local = dt.ToLocalTime();
                return local.ToString("yyyy-MM-dd  HH:mm:ss");
            }

            return Version.CreatedAt;
        }
    }
}
