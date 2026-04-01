using VisualSqlArchitect.Core;
using VisualSqlArchitect.Metadata;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.SidebarLeft;

public class SchemaViewModelSidebarFlowTests
{
    [Fact]
    public void Metadata_SetWithoutSchemas_KeepsCategoriesEmpty()
    {
        var vm = new SchemaViewModel();

        vm.Metadata = new DbMetadata(
            "db",
            DatabaseProvider.Postgres,
            "16",
            DateTimeOffset.UtcNow,
            [],
            []
        );

        Assert.True(vm.HasConnection);
        Assert.Equal("db", vm.DatabaseName);
        Assert.Empty(vm.Categories);
    }

    [Fact]
    public void Metadata_WithTablesAndViews_PopulatesExpectedCategories()
    {
        var vm = new SchemaViewModel();
        vm.Metadata = BuildSampleMetadata();

        Assert.True(vm.HasConnection);
        Assert.Equal("sample_db", vm.DatabaseName);
        Assert.Equal(4, vm.Categories.Count);
        Assert.Equal("Tables", vm.Categories[0].Name);
        Assert.Equal("Views", vm.Categories[1].Name);
        Assert.Equal("Procedures", vm.Categories[2].Name);
        Assert.Equal("Triggers", vm.Categories[3].Name);

        Assert.Single(vm.Categories[0].Items);
        Assert.Single(vm.Categories[1].Items);
    }

    [Fact]
    public void FilterQuery_FiltersSchemaObjects_ByTableOrColumnName()
    {
        var vm = new SchemaViewModel();
        vm.Metadata = BuildSampleMetadata();

        vm.FilterQuery = "customer";

        Assert.NotEmpty(vm.Categories);
        var tables = vm.Categories.First(c => c.Name == "Tables");
        Assert.Single(tables.Items);
        Assert.Equal("orders", tables.Items[0].Name);

        vm.FilterQuery = "nonexistent_filter";

        Assert.Equal(2, vm.Categories.Count);
        Assert.Equal("Procedures", vm.Categories[0].Name);
        Assert.Equal("Triggers", vm.Categories[1].Name);
    }

    private static DbMetadata BuildSampleMetadata()
    {
        var orders = new TableMetadata(
            "public",
            "orders",
            TableKind.Table,
            10,
            [
                new ColumnMetadata("id", "int", "int", false, true, false, true, true, 1),
                new ColumnMetadata("customer_name", "varchar", "varchar", true, false, false, false, false, 2)
            ],
            [],
            [],
            []
        );

        var ordersView = new TableMetadata(
            "public",
            "orders_view",
            TableKind.View,
            null,
            [new ColumnMetadata("id", "int", "int", false, false, false, false, false, 1)],
            [],
            [],
            []
        );

        return new DbMetadata(
            "sample_db",
            DatabaseProvider.Postgres,
            "16",
            DateTimeOffset.UtcNow,
            [new SchemaMetadata("public", [orders, ordersView])],
            []
        );
    }
}
