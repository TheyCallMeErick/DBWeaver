using DBWeaver.Core;
using DBWeaver.Metadata;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.SidebarLeft;

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
        Assert.True(vm.ShowNoTablesState);
        Assert.False(vm.ShowFilterEmptyState);
    }

    [Fact]
    public void Metadata_WithTablesAndViews_PopulatesExpectedCategories()
    {
        var vm = new SchemaViewModel();
        vm.Metadata = BuildSampleMetadata();

        Assert.True(vm.HasConnection);
        Assert.Equal("sample_db", vm.DatabaseName);
        Assert.Equal(2, vm.Categories.Count);
        Assert.Equal("Tables", vm.Categories[0].Name);
        Assert.Equal("Views", vm.Categories[1].Name);

        Assert.Single(vm.Categories[0].Items);
        Assert.Single(vm.Categories[1].Items);

        SchemaObjectViewModel orders = vm.Categories[0].Items[0];
        Assert.Equal("orders", orders.Name);
        Assert.True(orders.IsExpandable);
        Assert.Equal(2, orders.Children.Count);
        Assert.Equal("KeyPlus", orders.Children[0].Icon);
        Assert.Equal("#FCD34D", orders.Children[0].BadgeColor);
        Assert.Equal("CircleOutline", orders.Children[1].Icon);
        Assert.Equal("#9CA3AF", orders.Children[1].BadgeColor);
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

        Assert.Empty(vm.Categories);
        Assert.True(vm.ShowFilterEmptyState);
        Assert.False(vm.ShowNoTablesState);
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
