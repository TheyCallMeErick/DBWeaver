using System.Xml;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace DBWeaver.UI.Services;

public static class SqlEditorHighlightingService
{
    private static readonly Lazy<IHighlightingDefinition?> CachedDefinition = new(LoadDefinition);

    public static IHighlightingDefinition? GetSqlDefinition() => CachedDefinition.Value;

    private static IHighlightingDefinition? LoadDefinition()
    {
        try
        {
            var uri = new Uri("avares://DBWeaver.UI/Assets/Syntax/Sql.xshd");
            using var stream = AssetLoader.Open(uri);
            using var reader = XmlReader.Create(stream);
            return HighlightingLoader.Load(reader, HighlightingManager.Instance);
        }
        catch
        {
            return null;
        }
    }
}
