using Microsoft.Extensions.Options;

namespace DBWeaver.Core;

public sealed class PreviewExecutionOptions
{
    public const int UseConfiguredDefault = -1;
    public const int BuiltInDefaultMaxRows = 200;
    public const string MaxRowsEnvironmentVariable = "VSA_PREVIEW_MAX_ROWS";

    public int DefaultMaxRows { get; set; } = BuiltInDefaultMaxRows;

    public static int ResolveDefaultMaxRows(IOptions<PreviewExecutionOptions>? options)
    {
        if (options is not null && options.Value.DefaultMaxRows > 0)
            return options.Value.DefaultMaxRows;

        string? fromEnv = Environment.GetEnvironmentVariable(MaxRowsEnvironmentVariable);
        if (int.TryParse(fromEnv, out int parsed) && parsed > 0)
            return parsed;

        int configuredFallback = options?.Value.DefaultMaxRows ?? BuiltInDefaultMaxRows;
        if (configuredFallback > 0)
            return configuredFallback;

        return BuiltInDefaultMaxRows;
    }
}
