namespace DBWeaver.UI.Services.SqlEditor;

public enum StatementKind
{
    Select,
    Insert,
    Update,
    Delete,
    CreateTable,
    AlterTable,
    CreateView,
    CreateIndex,
    DropTable,
    DropView,
    Other,
}
