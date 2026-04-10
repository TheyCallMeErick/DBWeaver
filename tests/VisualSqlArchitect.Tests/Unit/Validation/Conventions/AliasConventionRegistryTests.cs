using DBWeaver.UI.ViewModels.Validation.Conventions;
using DBWeaver.UI.ViewModels.Validation.Conventions.Implementations;

namespace DBWeaver.Tests.Unit.Validation.Conventions;

public class AliasConventionRegistryTests
{
    [Fact]
    public void CreateDefault_ContainsExpectedConventions()
    {
        AliasConventionRegistry registry = AliasConventionRegistry.CreateDefault();
        Assert.Equal(4, registry.All.Count);
    }

    [Theory]
    [InlineData("snake_case", typeof(SnakeCaseConvention))]
    [InlineData("camelCase", typeof(CamelCaseConvention))]
    [InlineData("PascalCase", typeof(PascalCaseConvention))]
    [InlineData("SCREAMING_SNAKE_CASE", typeof(ScreamingSnakeCaseConvention))]
    public void Resolve_KnownConvention_ReturnsExpectedType(string name, Type expectedType)
    {
        AliasConventionRegistry registry = AliasConventionRegistry.CreateDefault();
        IAliasConvention resolved = registry.Resolve(name);
        Assert.IsType(expectedType, resolved);
    }

    [Fact]
    public void TryResolve_UnknownConvention_ReturnsNull()
    {
        AliasConventionRegistry registry = AliasConventionRegistry.CreateDefault();
        Assert.Null(registry.TryResolve("unknown"));
    }

    [Fact]
    public void Resolve_UnknownConvention_Throws()
    {
        AliasConventionRegistry registry = AliasConventionRegistry.CreateDefault();
        Assert.Throws<InvalidOperationException>(() => registry.Resolve("unknown"));
    }

    [Fact]
    public void Constructor_NullCollection_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AliasConventionRegistry(null!));
    }
}

