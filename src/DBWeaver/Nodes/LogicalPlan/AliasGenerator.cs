namespace DBWeaver.Nodes.LogicalPlan;

public sealed class AliasGenerator
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);

    public string GenerateFor(string suggestion)
    {
        string seed = string.IsNullOrWhiteSpace(suggestion)
            ? "ds"
            : suggestion.Trim();

        if (_used.Add(seed))
            return seed;

        for (int index = 1; index < 1000; index++)
        {
            string candidate = $"{seed}_{index}";
            if (_used.Add(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"Cannot generate unique alias for '{seed}'.");
    }
}
