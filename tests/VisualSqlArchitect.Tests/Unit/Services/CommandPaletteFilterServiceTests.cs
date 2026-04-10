using DBWeaver.UI.Services.CommandPalette;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.Services;

public sealed class CommandPaletteFilterServiceTests
{
    [Fact]
    public void FilterAndSort_QueryByShortcut_FindsMatchingCommand()
    {
        var service = new CommandPaletteFilterService();
        IReadOnlyList<PaletteCommandItem> commands =
        [
            new PaletteCommandItem { Name = "Open File", Description = "Open canvas file", Shortcut = "Ctrl+O", Tags = "file open" },
            new PaletteCommandItem { Name = "Toggle Preview", Description = "Toggle output preview", Shortcut = "F3", Tags = "preview data" },
        ];

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "f3");

        PaletteCommandItem item = Assert.Single(result);
        Assert.Equal("Toggle Preview", item.Name);
    }

    [Fact]
    public void FilterAndSort_QueryByName_StillWorks()
    {
        var service = new CommandPaletteFilterService();
        IReadOnlyList<PaletteCommandItem> commands =
        [
            new PaletteCommandItem { Name = "Open File", Description = "Open canvas file", Shortcut = "Ctrl+O", Tags = "file open" },
            new PaletteCommandItem { Name = "Toggle Preview", Description = "Toggle output preview", Shortcut = "F3", Tags = "preview data" },
        ];

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "toggle");

        PaletteCommandItem item = Assert.Single(result);
        Assert.Equal("Toggle Preview", item.Name);
    }

    [Fact]
    public void FilterAndSort_MixedNameAndShortcut_TokenizesAndMatches()
    {
        var service = new CommandPaletteFilterService();
        IReadOnlyList<PaletteCommandItem> commands =
        [
            new PaletteCommandItem { Name = "Open File", Description = "Open canvas file", Shortcut = "Ctrl+O", Tags = "file open" },
            new PaletteCommandItem { Name = "Toggle Preview", Description = "Toggle output preview", Shortcut = "F3", Tags = "preview data" },
        ];

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "preview f3");

        PaletteCommandItem item = Assert.Single(result);
        Assert.Equal("Toggle Preview", item.Name);
    }
}
