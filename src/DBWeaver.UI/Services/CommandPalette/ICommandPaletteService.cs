using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.CommandPalette;

public interface ICommandPaletteService
{
    CommandPaletteViewModel ViewModel { get; }
    void Refresh();
}
