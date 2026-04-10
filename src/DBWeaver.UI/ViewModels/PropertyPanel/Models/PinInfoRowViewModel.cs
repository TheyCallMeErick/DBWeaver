namespace DBWeaver.UI.ViewModels;

public sealed class PinInfoRowViewModel(PinViewModel pin)
{
    public string Name => pin.Name;
    public string TypeLabel => pin.DataType.ToString();
    public string Direction => pin.Direction.ToString();
    public bool Connected => pin.IsConnected;
    public Avalonia.Media.Color Color => pin.PinColor;
    public Avalonia.Media.SolidColorBrush ColorBrush => pin.PinBrush;
}
