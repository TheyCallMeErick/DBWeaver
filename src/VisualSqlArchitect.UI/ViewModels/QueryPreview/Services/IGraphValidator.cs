namespace VisualSqlArchitect.UI.ViewModels.QueryPreview.Services;

/// <summary>
/// Contract for components that contribute validation errors during graph compilation.
/// </summary>
public interface IGraphValidator
{
    void Validate(List<string> errors);
}
