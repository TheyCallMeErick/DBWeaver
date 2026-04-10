using DBWeaver.UI.ViewModels;

namespace DBWeaver.UI.Services.SqlEditor;

public interface ISqlEditorViewModelFactory
{
    SqlEditorViewModel Create(SqlEditorViewModelFactoryContext context);
}

