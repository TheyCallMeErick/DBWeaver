using Microsoft.Extensions.Options;
using Xunit;

namespace DBWeaver.Tests.Unit.Core;

public sealed class PreviewExecutionOptionsTests
{
    [Fact]
    public void ResolveDefaultMaxRows_UsesOptionsValue_WhenPositive()
    {
        string? previous = Environment.GetEnvironmentVariable(
            PreviewExecutionOptions.MaxRowsEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(PreviewExecutionOptions.MaxRowsEnvironmentVariable, "999");
            IOptions<PreviewExecutionOptions> options = Options.Create(
                new PreviewExecutionOptions
                {
                    DefaultMaxRows = 321,
                }
            );

            int result = PreviewExecutionOptions.ResolveDefaultMaxRows(options);
            Assert.Equal(321, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PreviewExecutionOptions.MaxRowsEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDefaultMaxRows_UsesEnvironmentVariable_WhenOptionsMissing()
    {
        string? previous = Environment.GetEnvironmentVariable(
            PreviewExecutionOptions.MaxRowsEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(PreviewExecutionOptions.MaxRowsEnvironmentVariable, "654");

            int result = PreviewExecutionOptions.ResolveDefaultMaxRows(options: null);
            Assert.Equal(654, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PreviewExecutionOptions.MaxRowsEnvironmentVariable,
                previous
            );
        }
    }

    [Fact]
    public void ResolveDefaultMaxRows_UsesBuiltInFallback_WhenInvalidInput()
    {
        string? previous = Environment.GetEnvironmentVariable(
            PreviewExecutionOptions.MaxRowsEnvironmentVariable
        );
        try
        {
            Environment.SetEnvironmentVariable(PreviewExecutionOptions.MaxRowsEnvironmentVariable, "0");
            IOptions<PreviewExecutionOptions> options = Options.Create(
                new PreviewExecutionOptions
                {
                    DefaultMaxRows = 0,
                }
            );

            int result = PreviewExecutionOptions.ResolveDefaultMaxRows(options);
            Assert.Equal(PreviewExecutionOptions.BuiltInDefaultMaxRows, result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                PreviewExecutionOptions.MaxRowsEnvironmentVariable,
                previous
            );
        }
    }
}
