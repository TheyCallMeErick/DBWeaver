using DBWeaver.UI.Services.ConnectionManager;
using DBWeaver.UI.Services.Benchmark;
using System.ComponentModel;
using DBWeaver.UI.Services.Localization;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class ConnectionStatusPresenterTests
{
    [Fact]
    public void Connecting_UsesConnectingColor()
    {
        var presenter = new ConnectionStatusPresenter(new FakeLocalizationService());

        ConnectionStatusViewState state = presenter.Connecting();

        Assert.Equal("#FBBF24", state.Color);
        Assert.Equal("Connecting...", state.Message);
    }

    [Fact]
    public void Connected_UsesOnlineColor()
    {
        var presenter = new ConnectionStatusPresenter(new FakeLocalizationService());

        ConnectionStatusViewState state = presenter.Connected();

        Assert.Equal("#4ADE80", state.Color);
        Assert.Equal("Connected", state.Message);
    }

    [Fact]
    public void TestSuccess_DegradedLatency_UsesWarningColorAndHighLatencyText()
    {
        var presenter = new ConnectionStatusPresenter(new FakeLocalizationService());

        ConnectionStatusViewState state = presenter.TestSuccess(TimeSpan.FromMilliseconds(700), 500);

        Assert.Equal("#FBBF24", state.Color);
        Assert.Contains("High latency", state.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("700ms", state.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedWithPrefix_UsesErrorColorAndPrefix()
    {
        var presenter = new ConnectionStatusPresenter(new FakeLocalizationService());

        ConnectionStatusViewState state = presenter.FailedWithPrefix("boom");

        Assert.Equal("#EF4444", state.Color);
        Assert.Equal("Failed: boom", state.Message);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";
        public string CurrentLanguageLabel => "English";

        public string this[string key] => key switch
        {
            "connection.status.connecting" => "Connecting...",
            "connection.status.testing" => "Testing connection...",
            "connection.status.connected" => "Connected",
            "connection.status.highLatency" => "High latency",
            "connection.status.metadataUnavailable" => "Metadata unavailable",
            "connection.status.cancelled" => "Cancelled",
            "connection.status.failedPrefix" => "Failed",
            _ => key,
        };

        public bool ToggleCulture() => false;
        public bool SetCulture(string culture) => false;
    }
}


