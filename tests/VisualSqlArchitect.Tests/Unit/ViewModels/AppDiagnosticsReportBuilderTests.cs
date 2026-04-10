using DBWeaver.UI.Services.Benchmark;
using System.ComponentModel;
using DBWeaver.UI.Services.Localization;

using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels;

public class AppDiagnosticsReportBuilderTests
{
    [Fact]
    public void BuildReport_ContainsOverallAndCategoryEntries()
    {
        var localization = new FakeLocalizationService();
        var builder = new AppDiagnosticsReportBuilder(localization);
        var category = new AppDiagnosticCategoryViewModel { Title = "Canvas Integrity" };
        category.ReplaceItems(
        [
            new AppDiagnosticEntry
            {
                Name = "Validation",
                Details = "No issues",
                Recommendation = "Keep going",
                Status = DiagnosticStatus.Ok,
                LastCheckAt = new DateTime(2026, 4, 3, 10, 30, 0)
            }
        ]);

        string report = builder.BuildReport("All systems OK", [category]);

        Assert.Contains("DBWeaver - Diagnostic Report", report, StringComparison.Ordinal);
        Assert.Contains("Overall: All systems OK", report, StringComparison.Ordinal);
        Assert.Contains("[Canvas Integrity]", report, StringComparison.Ordinal);
        Assert.Contains("- Validation [Ok]", report, StringComparison.Ordinal);
        Assert.Contains("Details: No issues", report, StringComparison.Ordinal);
        Assert.Contains("Recommendation: Keep going", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildReport_SkipsEmptyCategories()
    {
        var localization = new FakeLocalizationService();
        var builder = new AppDiagnosticsReportBuilder(localization);
        var empty = new AppDiagnosticCategoryViewModel { Title = "Empty" };

        string report = builder.BuildReport("All systems OK", [empty]);

        Assert.DoesNotContain("[Empty]", report, StringComparison.Ordinal);
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public event PropertyChangedEventHandler? PropertyChanged
        {
            add { }
            remove { }
        }

        public string CurrentCulture => "en-US";

        public string CurrentLanguageLabel => "English";

        public string this[string key] => key;

        public bool ToggleCulture() => false;

        public bool SetCulture(string culture) => false;
    }
}

