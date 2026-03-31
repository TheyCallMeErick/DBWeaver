using System.Reflection;
using VisualSqlArchitect.UI.ViewModels;
using Xunit;

namespace VisualSqlArchitect.Tests.Unit.ViewModels.Canvas;

public class ValidationManagerSafeExecutionTests
{
    [Fact]
    public void ValidationManager_HasSafeWrapperForPostedValidation()
    {
        Type validationType = typeof(CanvasViewModel)
            .Assembly
            .GetType("VisualSqlArchitect.UI.ViewModels.Canvas.ValidationManager")!;

        Assert.NotNull(validationType.GetMethod("RunValidationSafely", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}
