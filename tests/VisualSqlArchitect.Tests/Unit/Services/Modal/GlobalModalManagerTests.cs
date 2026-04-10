using DBWeaver.UI.Services.Modal;
using Xunit;

namespace DBWeaver.Tests.Unit.Services.Modal;

public class GlobalModalManagerTests
{
    [Fact]
    public void RequestSettings_PublishesExpectedRequest()
    {
        var manager = GlobalModalManager.Instance;
        GlobalModalRequest? captured = null;

        void Handler(GlobalModalRequest request) => captured = request;

        manager.ModalRequested += Handler;
        try
        {
            bool handled = manager.RequestSettings(keepStartVisible: true);

            Assert.True(handled);
            Assert.Equal(GlobalModalKind.Settings, captured?.Kind);
            Assert.True(captured?.KeepStartVisible);
        }
        finally
        {
            manager.ModalRequested -= Handler;
        }
    }

    [Fact]
    public void RequestConnectionManager_PublishesExpectedRequest()
    {
        var manager = GlobalModalManager.Instance;
        GlobalModalRequest? captured = null;

        void Handler(GlobalModalRequest request) => captured = request;

        manager.ModalRequested += Handler;
        try
        {
            bool handled = manager.RequestConnectionManager(beginNewProfile: true, keepStartVisible: true);

            Assert.True(handled);
            Assert.Equal(GlobalModalKind.ConnectionManager, captured?.Kind);
            Assert.True(captured?.BeginNewProfile);
            Assert.True(captured?.KeepStartVisible);
        }
        finally
        {
            manager.ModalRequested -= Handler;
        }
    }
}
