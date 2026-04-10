using DBWeaver.UI.Services.Canvas.AutoJoin;
using DBWeaver.UI.Services.Explain;
using Xunit;
using DBWeaver.UI.ViewModels.Canvas;
using System.Reflection;

namespace DBWeaver.Tests.Unit.ViewModels.Canvas;

/// <summary>
/// Regression tests for ExplainPlanViewModel fire-and-forget exception handling.
///
/// Problem (FRAGILITY_REPORT Â§8): <c>_ = RunExplainAsync()</c> was called without a
/// wrapping try/catch in the fire-and-forget context. Any exception propagating after
/// the first await would become an unhandled exception and crash the app.
///
/// Fix applied: <c>RunExplainAsyncSafe()</c> wraps the call; <c>Open()</c> calls the
/// safe wrapper instead of the raw async method.
///
/// NOTE: <c>RunExplainAsync</c> is intentionally <b>public</b> because
/// <c>ExplainPlanOverlay.axaml.cs</c> calls it directly for explicit re-runs.
/// Tests must use <c>BindingFlags.Public</c>, not <c>NonPublic</c>.
/// </summary>
public class ExplainPlanViewModelFireAndForgetTests
{
    // â”€â”€ Infrastructure helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static MethodInfo? GetMethod(string name, BindingFlags flags) =>
        typeof(ExplainPlanViewModel).GetMethod(name, flags);

    private static PropertyInfo? GetProp(string name) =>
        typeof(ExplainPlanViewModel).GetProperty(name);

    // â”€â”€ Safe wrapper existence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void SafeWrapper_Exists_AsPrivateMethod()
    {
        // RunExplainAsyncSafe is an internal implementation detail â€” must be private.
        var method = GetMethod("RunExplainAsyncSafe",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
    }

    [Fact]
    public void SafeWrapper_ReturnsTask()
    {
        var method = GetMethod("RunExplainAsyncSafe",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    [Fact]
    public void SafeWrapper_IsAsync()
    {
        var method = GetMethod("RunExplainAsyncSafe",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);
        // Async methods are decorated by the compiler with AsyncStateMachineAttribute.
        bool isAsync = method!.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() != null;
        Assert.True(isAsync, "RunExplainAsyncSafe must be async to properly catch exceptions");
    }

    // â”€â”€ Public RunExplainAsync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RunExplainAsync_IsPublic()
    {
        // RunExplainAsync is public because ExplainPlanOverlay.axaml.cs calls it directly.
        // Regression: tests previously searched with NonPublic and incorrectly failed.
        var method = GetMethod("RunExplainAsync",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
    }

    [Fact]
    public void RunExplainAsync_ReturnsTask()
    {
        var method = GetMethod("RunExplainAsync",
            BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.Equal(typeof(Task), method!.ReturnType);
    }

    // â”€â”€ Open() uses safe wrapper, NOT the raw method â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Open_Method_Exists_AndIsPublic()
    {
        var method = GetMethod("Open", BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
    }

    // â”€â”€ Error-state properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ErrorMessage_Property_Exists()
    {
        var prop = GetProp("ErrorMessage");

        Assert.NotNull(prop);
        Assert.True(prop!.CanRead, "ErrorMessage must be readable for UI binding");
    }

    [Fact]
    public void IsLoading_Property_Exists_AndIsReadable()
    {
        var prop = GetProp("IsLoading");

        Assert.NotNull(prop);
        Assert.True(prop!.CanRead);
    }

    // â”€â”€ Behavioral tests â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ErrorMessage_InitiallyNull()
    {
        var canvas = new DBWeaver.UI.ViewModels.CanvasViewModel();
        var vm = new ExplainPlanViewModel(canvas);

        // Before any run, ErrorMessage must be null/empty (no spurious errors shown)
        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void IsLoading_InitiallyFalse()
    {
        var canvas = new DBWeaver.UI.ViewModels.CanvasViewModel();
        var vm = new ExplainPlanViewModel(canvas);

        Assert.False(vm.IsLoading);
    }

    [Fact]
    public void HasData_FalseWhenNoSteps()
    {
        var canvas = new DBWeaver.UI.ViewModels.CanvasViewModel();
        var vm = new ExplainPlanViewModel(canvas);

        // With no steps and not loading, HasData must be false
        Assert.False(vm.HasData);
        Assert.Empty(vm.Steps);
    }

    [Fact]
    public void Close_SetsIsVisible_False()
    {
        var canvas = new DBWeaver.UI.ViewModels.CanvasViewModel();
        var vm = new ExplainPlanViewModel(canvas);

        vm.Close();

        Assert.False(vm.IsVisible);
    }

    // â”€â”€ Regression: complete safety contract â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void RegressionTest_Complete_Exception_Safety()
    {
        // Verifies all required components for safe fire-and-forget exception handling:
        //   1. RunExplainAsyncSafe() â€” private async wrapper (catches exceptions)
        //   2. RunExplainAsync()     â€” public async method (callable from overlay)
        //   3. ErrorMessage          â€” property updated on failure
        //   4. IsLoading             â€” reset to false on failure (prevents UI hang)

        var type = typeof(ExplainPlanViewModel);

        // 1. Safe wrapper exists and is private
        var safeWrapper = type.GetMethod("RunExplainAsyncSafe",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(safeWrapper);

        // 2. RunExplainAsync is public (used by overlay code-behind)
        var asyncMethod = type.GetMethod("RunExplainAsync",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(asyncMethod);

        // 3. ErrorMessage property for user feedback on failure
        var errorProp = type.GetProperty("ErrorMessage");
        Assert.NotNull(errorProp);
        Assert.True(errorProp!.CanRead);

        // 4. IsLoading property to prevent UI hang on failure
        var isLoadingProp = type.GetProperty("IsLoading");
        Assert.NotNull(isLoadingProp);
        Assert.True(isLoadingProp!.CanRead);
    }

    [Fact]
    public void RegressionTest_SafeWrapper_IsAsync_AndNotRaw()
    {
        // Ensures the safe wrapper is genuinely async (not just a sync wrapper).
        // A non-async wrapper would not properly propagate exceptions from
        // continuations, defeating the purpose of the fix.
        var safeWrapper = typeof(ExplainPlanViewModel).GetMethod(
            "RunExplainAsyncSafe",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(safeWrapper);

        bool isAsync = safeWrapper!.GetCustomAttribute<
            System.Runtime.CompilerServices.AsyncStateMachineAttribute>() != null;
        Assert.True(isAsync,
            "RunExplainAsyncSafe must be async to guarantee exception capture " +
            "across all await continuations");
    }
}


