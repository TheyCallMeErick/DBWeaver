namespace DBWeaver.UI.Services.Modal;

public sealed class GlobalModalManager : IGlobalModalManager
{
    public static GlobalModalManager Instance { get; } = new();

    private GlobalModalManager()
    {
    }

    public event Action<GlobalModalRequest>? ModalRequested;

    public bool Request(GlobalModalRequest request)
    {
        Action<GlobalModalRequest>? handlers = ModalRequested;
        if (handlers is null)
            return false;

        handlers.Invoke(request);
        return true;
    }

    public bool RequestConnectionManager(bool beginNewProfile = false, bool keepStartVisible = false) =>
        Request(new GlobalModalRequest(
            Kind: GlobalModalKind.ConnectionManager,
            BeginNewProfile: beginNewProfile,
            KeepStartVisible: keepStartVisible
        ));

    public bool RequestSettings(bool keepStartVisible = false) =>
        Request(new GlobalModalRequest(
            Kind: GlobalModalKind.Settings,
            KeepStartVisible: keepStartVisible
        ));
}
