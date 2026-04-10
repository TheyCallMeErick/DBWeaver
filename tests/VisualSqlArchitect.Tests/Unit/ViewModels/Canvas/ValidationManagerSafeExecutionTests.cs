using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using System.Reflection;
using DBWeaver.UI.ViewModels;
using Xunit;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

public class ValidationManagerSafeExecutionTests
{
    [Fact]
    public void ValidationManager_HasSafeWrapperForPostedValidation()
    {
        Type validationType = typeof(CanvasViewModel)
            .Assembly
            .GetType("DBWeaver.UI.ViewModels.Canvas.ValidationManager")!;

        Assert.NotNull(validationType.GetMethod("RunValidationSafely", BindingFlags.Instance | BindingFlags.NonPublic));
    }
}


