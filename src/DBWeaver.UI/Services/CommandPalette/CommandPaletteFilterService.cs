namespace DBWeaver.UI.Services.CommandPalette;

public sealed class CommandPaletteFilterService : ICommandPaletteFilterService
{
    public IReadOnlyList<PaletteCommandItem> FilterAndSort(
        IEnumerable<PaletteCommandItem> commands,
        string query)
    {
        string q = query.Trim();

        return commands
            .Select(c => (Command: c, Score: FuzzyScorer.Score(c, q)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Command.Name)
            .Select(x => x.Command)
            .ToList();
    }
}

