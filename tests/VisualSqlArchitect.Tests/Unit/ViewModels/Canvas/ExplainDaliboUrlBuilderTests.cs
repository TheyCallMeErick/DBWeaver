using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using System.Text;
using DBWeaver.UI.ViewModels.Canvas;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ExplainDaliboUrlBuilderTests
{
    [Fact]
    public void Build_ReturnsNull_WhenRawJsonMissing()
    {
        var sut = new ExplainDaliboUrlBuilder();

        Assert.Null(sut.Build(null));
        Assert.Null(sut.Build(""));
        Assert.Null(sut.Build("   "));
    }

    [Fact]
    public void Build_UsesBase64PlanQueryParam()
    {
        Environment.SetEnvironmentVariable(ExplainDaliboUrlBuilder.BaseUrlEnvKey, "https://explain.dalibo.com/new");
        try
        {
            var sut = new ExplainDaliboUrlBuilder();
            string rawJson = "[{\"Plan\":{\"Node Type\":\"Seq Scan\"}}]";

            string? url = sut.Build(rawJson);

            Assert.NotNull(url);
            Assert.StartsWith("https://explain.dalibo.com/new?plan=", url);
            string encoded = url![(url.IndexOf("plan=", StringComparison.Ordinal) + 5)..];
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Uri.UnescapeDataString(encoded)));
            Assert.Equal(rawJson, decoded);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExplainDaliboUrlBuilder.BaseUrlEnvKey, null);
        }
    }

    [Fact]
    public void Build_ReturnsNull_WhenConfiguredBaseUrlIsInvalid()
    {
        Environment.SetEnvironmentVariable(ExplainDaliboUrlBuilder.BaseUrlEnvKey, "not-a-url");
        try
        {
            var sut = new ExplainDaliboUrlBuilder();
            Assert.Null(sut.Build("{\"Plan\":{}}"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(ExplainDaliboUrlBuilder.BaseUrlEnvKey, null);
        }
    }
}


