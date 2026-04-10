using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.CommandPalette;

public sealed class CommandPaletteService : ICommandPaletteService
{
    private readonly CommandPaletteFactory _factory;

    public CommandPaletteService(CommandPaletteFactory factory, CommandPaletteViewModel? viewModel = null)
    {
        _factory = factory;
        ViewModel = viewModel ?? new CommandPaletteViewModel();
    }

    public CommandPaletteViewModel ViewModel { get; }

    public void Refresh()
    {
        ViewModel.SetCommands(_factory.CreateAllCommands());
    }
}
