using Avalonia.Input;
using DBWeaver.UI.Services.Input.ShortcutRegistry;

namespace DBWeaver.Tests.Unit.Services.Input.ShortcutRegistry;

public sealed class ShortcutConflictDetectorTests
{
    private readonly ShortcutConflictDetector _detector = new();

    [Fact]
    public void DetectConflicts_SameGestureAndContext_ReportsConflict()
    {
        ShortcutDefinition left = Def("a", ShortcutContext.Canvas, "Ctrl+P");
        ShortcutDefinition right = Def("b", ShortcutContext.Canvas, "Ctrl+P");

        IReadOnlyList<ShortcutValidationIssue> issues = _detector.DetectConflicts([left, right]);

        Assert.NotEmpty(issues);
        Assert.Contains(issues, i => i.Code == ShortcutValidationCodes.DuplicateGesture);
    }

    [Fact]
    public void DetectConflicts_GlobalAndScoped_ReportsConflict()
    {
        ShortcutDefinition global = Def("global", ShortcutContext.Global, "F5");
        ShortcutDefinition scoped = Def("scoped", ShortcutContext.SqlEditor, "F5");

        IReadOnlyList<ShortcutValidationIssue> issues = _detector.DetectConflicts([global, scoped]);

        Assert.NotEmpty(issues);
    }

    [Fact]
    public void DetectConflicts_DifferentScopedContexts_NoConflict()
    {
        ShortcutDefinition canvas = Def("canvas", ShortcutContext.Canvas, "F5");
        ShortcutDefinition sql = Def("sql", ShortcutContext.SqlEditor, "F5");

        IReadOnlyList<ShortcutValidationIssue> issues = _detector.DetectConflicts([canvas, sql]);

        Assert.Empty(issues);
    }

    private static ShortcutDefinition Def(string id, ShortcutContext context, string normalizedGesture)
    {
        Key key = normalizedGesture.Contains("F5", StringComparison.OrdinalIgnoreCase) ? Key.F5 : Key.P;
        KeyModifiers modifiers = normalizedGesture.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)
            ? KeyModifiers.Control
            : KeyModifiers.None;
        var gesture = new ShortcutGesture(key, modifiers, normalizedGesture, normalizedGesture);
        return new ShortcutDefinition(
            new ShortcutActionId(id),
            id,
            id,
            "section",
            ["test"],
            gesture,
            gesture,
            context,
            AllowCustomization: true,
            Execute: static () => { });
    }
}
