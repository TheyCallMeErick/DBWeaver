using System.Text;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.UI.Services.Explain;

public interface IExplainDaliboUrlBuilder
{
    string? Build(string? rawJson);
}

public sealed class ExplainDaliboUrlBuilder : IExplainDaliboUrlBuilder
{
    public const string BaseUrlEnvKey = "VSA_EXPLAIN_DALIBO_BASE_URL";
    private const string DefaultBaseUrl = "https://explain.dalibo.com/new";

    public string? Build(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        string baseUrl = ResolveBaseUrl();
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? parsed))
            return null;

        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawJson));
        string separator = parsed.Query.Length == 0 ? "?" : "&";
        return $"{baseUrl}{separator}plan={Uri.EscapeDataString(base64)}";
    }

    private static string ResolveBaseUrl()
    {
        string? configured = Environment.GetEnvironmentVariable(BaseUrlEnvKey);
        return string.IsNullOrWhiteSpace(configured) ? DefaultBaseUrl : configured.Trim();
    }
}



