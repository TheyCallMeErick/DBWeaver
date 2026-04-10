namespace DBWeaver.UI.Services.QueryPreview;

/// <summary>
/// Contract for components that contribute validation errors during graph compilation.
/// </summary>
public interface IGraphValidator
{
    void Validate(List<string> errors);
}


