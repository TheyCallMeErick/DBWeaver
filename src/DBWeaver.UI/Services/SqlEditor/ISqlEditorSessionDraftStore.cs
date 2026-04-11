namespace DBWeaver.UI.Services.SqlEditor;

public interface ISqlEditorSessionDraftStore
{
    IReadOnlyList<SqlEditorSessionDraftEntry> LoadDrafts();

    void SaveDrafts(IReadOnlyList<SqlEditorSessionDraftEntry> drafts);

    void ClearDrafts();
}
