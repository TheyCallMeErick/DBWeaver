using DBWeaver.Nodes;
using DBWeaver.UI.ViewModels;

namespace DBWeaver.Tests.Unit.ViewModels.PropertyPanel;

public class ParameterRowViewModelSuggestedValuesTests
{
    [Fact]
    public void SetSuggestedValues_WhenCurrentValueMissing_PreservesValueAndInsertsAtTop()
    {
        var row = new ParameterRowViewModel(
            new NodeParameter("table", ParameterKind.Text, null),
            "sales.orders");

        row.SetSuggestedValues(["public.customers", "public.invoices"]);

        Assert.Equal("sales.orders", row.Value);
        Assert.True(row.HasSuggestedValues);
        Assert.Equal("sales.orders", row.SuggestedValues.First());
    }

    [Fact]
    public void SetSuggestedValues_WhenCurrentValueAlreadyExists_DoesNotDuplicateCaseInsensitive()
    {
        var row = new ParameterRowViewModel(
            new NodeParameter("table", ParameterKind.Text, null),
            "Public.Customers");

        row.SetSuggestedValues(["public.customers", "PUBLIC.CUSTOMERS", "public.orders"]);

        Assert.Equal("Public.Customers", row.Value);
        Assert.Equal(2, row.SuggestedValues.Count);
        Assert.Equal("public.customers", row.SuggestedValues[0]);
        Assert.Equal("public.orders", row.SuggestedValues[1]);
    }

    [Fact]
    public void SetSuggestedValues_DoesNotMarkRowDirtyAfterSuggestionRefresh()
    {
        var row = new ParameterRowViewModel(
            new NodeParameter("table", ParameterKind.Text, null),
            "public.customers");

        row.SetSuggestedValues(["public.customers", "public.orders"]);

        Assert.False(row.IsDirty);
    }

    [Fact]
    public void SetSuggestedValues_RepeatedRefreshes_DoNotThrowAndKeepLatestSuggestions()
    {
        var row = new ParameterRowViewModel(
            new NodeParameter("table", ParameterKind.Text, null),
            "public.orders");

        Exception? error = Record.Exception(() =>
        {
            row.SetSuggestedValues(["public.customers", "public.orders", "public.items"]);
            row.SetSuggestedValues(["public.customers"]);
            row.SetSuggestedValues(["public.orders", "public.logs"]);
            row.SetSuggestedValues([]);
            row.SetSuggestedValues(["public.audit"]);
        });

        Assert.Null(error);
        Assert.Equal("public.orders", row.Value);
        Assert.True(row.HasSuggestedValues);
        Assert.Equal("public.orders", row.SuggestedValues[0]);
        Assert.Equal("public.audit", row.SuggestedValues[1]);
    }
}

