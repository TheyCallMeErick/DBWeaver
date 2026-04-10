namespace DBWeaver.UI.ViewModels.Validation.Conventions;

/// <summary>
/// Optional extension for conventions that support deterministic normalization.
/// </summary>
public interface IAutoFixableConvention : IAliasConvention
{
    string Normalize(string alias);
}

