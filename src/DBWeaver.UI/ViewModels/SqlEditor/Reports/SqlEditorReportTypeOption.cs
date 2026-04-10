namespace DBWeaver.UI.ViewModels;

public sealed record SqlEditorReportTypeOption(
    SqlEditorReportType Type,
    string Label,
    string Description,
    string DefaultExtension
)
{
    public override string ToString() => Label;
}
