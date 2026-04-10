namespace DBWeaver.UI.Services.ConnectionManager;

public interface IConnectionStatusPresenter
{
    ConnectionStatusViewState Connecting();

    ConnectionStatusViewState Testing();

    ConnectionStatusViewState Connected();

    ConnectionStatusViewState TestSuccess(TimeSpan? latency, double degradedLatencyThresholdMs);

    ConnectionStatusViewState MetadataUnavailable();

    ConnectionStatusViewState Cancelled();

    ConnectionStatusViewState Failed(string message);

    ConnectionStatusViewState FailedWithPrefix(string reason);
}

