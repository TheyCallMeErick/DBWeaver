using DBWeaver.UI.Services.Benchmark;
using DBWeaver.UI.Services.CommandPalette;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class CommandPaletteFilterServiceTests
{
    [Fact]
    public void FilterAndSort_EmptyQuery_ReturnsAllCommandsSortedByName()
    {
        var service = new CommandPaletteFilterService();
        var commands = new[]
        {
            new PaletteCommandItem { Name = "Zeta", Description = "z", Tags = "z", Execute = () => { } },
            new PaletteCommandItem { Name = "Alpha", Description = "a", Tags = "a", Execute = () => { } },
        };

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "");

        Assert.Equal(2, result.Count);
        Assert.Equal("Alpha", result[0].Name);
        Assert.Equal("Zeta", result[1].Name);
    }

    [Fact]
    public void FilterAndSort_QueryPrefersNamePrefixMatch()
    {
        var service = new CommandPaletteFilterService();
        var commands = new[]
        {
            new PaletteCommandItem { Name = "Export", Description = "Save data", Tags = "output", Execute = () => { } },
            new PaletteCommandItem { Name = "Open settings", Description = "Configure app", Tags = "settings", Execute = () => { } },
        };

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "exp");

        Assert.Single(result);
        Assert.Equal("Export", result[0].Name);
    }

    [Fact]
    public void FilterAndSort_TrimsQueryBeforeScoring()
    {
        var service = new CommandPaletteFilterService();
        var commands = new[]
        {
            new PaletteCommandItem { Name = "Export", Description = "Save data", Tags = "output", Execute = () => { } }
        };

        IReadOnlyList<PaletteCommandItem> result = service.FilterAndSort(commands, "  exp  ");

        Assert.Single(result);
        Assert.Equal("Export", result[0].Name);
    }
}

