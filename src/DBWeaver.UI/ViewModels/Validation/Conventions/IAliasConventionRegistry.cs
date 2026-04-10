namespace DBWeaver.UI.ViewModels.Validation.Conventions;

public interface IAliasConventionRegistry
{
    IReadOnlyCollection<IAliasConvention> All { get; }
    IAliasConvention? TryResolve(string conventionName);
    IAliasConvention Resolve(string conventionName);
}

