using DBWeaver.UI.Services.Theming;
﻿using DBWeaver.UI.Services.Localization;

namespace DBWeaver.UI.Services.ConnectionManager;

public sealed class ConnectionStatusPresenter(ILocalizationService localization) : IConnectionStatusPresenter
{
    private readonly ILocalizationService _localization = localization;

    public ConnectionStatusViewState Connecting() =>
        new(_localization["connection.status.connecting"], UiColorConstants.C_FBBF24);

    public ConnectionStatusViewState Testing() =>
        new(_localization["connection.status.testing"], UiColorConstants.C_FBBF24);

    public ConnectionStatusViewState Connected() =>
        new(_localization["connection.status.connected"], UiColorConstants.C_4ADE80);

    public ConnectionStatusViewState TestSuccess(TimeSpan? latency, double degradedLatencyThresholdMs)
    {
        double ms = latency?.TotalMilliseconds ?? 0;
        bool degraded = ms >= degradedLatencyThresholdMs;
        string lag = degraded
            ? $" -- {L("connection.status.highLatency", "High latency")} ({ms:0}ms)"
            : $" · {ms:0}ms";
        string message = $"{_localization["connection.status.connected"]}{lag}";
        return new ConnectionStatusViewState(message, degraded ? UiColorConstants.C_FBBF24 : UiColorConstants.C_4ADE80);
    }

    public ConnectionStatusViewState MetadataUnavailable() =>
        new(_localization["connection.status.metadataUnavailable"], UiColorConstants.C_EF4444);

    public ConnectionStatusViewState Cancelled() =>
        new(_localization["connection.status.cancelled"], UiColorConstants.C_9CA3AF);

    public ConnectionStatusViewState Failed(string message) =>
        new(message, UiColorConstants.C_EF4444);

    public ConnectionStatusViewState FailedWithPrefix(string reason) =>
        new($"{_localization["connection.status.failedPrefix"]}: {reason}", UiColorConstants.C_EF4444);

    private string L(string key, string fallback)
    {
        string value = _localization[key];
        return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
    }
}

