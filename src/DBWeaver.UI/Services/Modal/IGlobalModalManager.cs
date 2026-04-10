namespace DBWeaver.UI.Services.Modal;

public enum GlobalModalKind
{
    ConnectionManager,
    Settings,
}

public readonly record struct GlobalModalRequest(
    GlobalModalKind Kind,
    bool BeginNewProfile = false,
    bool KeepStartVisible = false
);

public interface IGlobalModalManager
{
    event Action<GlobalModalRequest>? ModalRequested;

    bool Request(GlobalModalRequest request);

    bool RequestConnectionManager(bool beginNewProfile = false, bool keepStartVisible = false);

    bool RequestSettings(bool keepStartVisible = false);
}
