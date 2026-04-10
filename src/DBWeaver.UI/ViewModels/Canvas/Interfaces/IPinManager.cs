namespace DBWeaver.UI.ViewModels.Canvas;

public interface IPinManager
{
    void ConnectPins(PinViewModel from, PinViewModel to);

    void DeleteConnection(ConnectionViewModel conn);
}
