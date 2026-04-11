namespace DBWeaver.UI.Services.SqlEditor;

public enum SqlCompletionContext
{
    Unknown,
    SelectList,
    FromClause,
    JoinClause,
    OnClause,
    WhereClause,
    HavingClause,
    OrderByClause,
    GroupByClause,
    InsertColumns,
    ValuesClause,
    UpdateSetClause,
}
