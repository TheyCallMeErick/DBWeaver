using DBWeaver.Nodes.Pins;

namespace DBWeaver.Tests.Unit.Nodes.Pins;

public sealed class PinReasonCatalogTests
{
    [Theory]
    [InlineData(PinConnectionReasonCode.StructuralTypeMismatch, "STRUCTURAL_TYPE_MISMATCH")]
    [InlineData(PinConnectionReasonCode.ScalarTypeMismatch, "PIN_ScalarTypeMismatch")]
    [InlineData(PinConnectionReasonCode.DomainFamilyMismatch, "PIN_DomainFamilyMismatch")]
    public void ToIssueCode_ReturnsStableCode(PinConnectionReasonCode reasonCode, string expectedCode)
    {
        string code = PinConnectionReasonCatalog.ToIssueCode(reasonCode);

        Assert.Equal(expectedCode, code);
    }

    [Fact]
    public void ToDiagnosticMessage_IncludesSourceAndDestinationLabels_ForScalarMismatch()
    {
        string message = PinConnectionReasonCatalog.ToDiagnosticMessage(
            PinConnectionReasonCode.ScalarTypeMismatch,
            "Source.pin",
            "Destination.pin");

        Assert.Contains("Source.pin", message);
        Assert.Contains("Destination.pin", message);
    }

    [Fact]
    public void ToDiagnosticMessage_ReturnsNonEmptyText_ForAllReasonCodes()
    {
        foreach (PinConnectionReasonCode reasonCode in Enum.GetValues<PinConnectionReasonCode>())
        {
            string message = PinConnectionReasonCatalog.ToDiagnosticMessage(
                reasonCode,
                "Source.pin",
                "Destination.pin");

            Assert.False(string.IsNullOrWhiteSpace(message));
        }
    }

    [Fact]
    public void ToIssueCode_ReturnsNonEmptyCode_ForAllReasonCodes()
    {
        foreach (PinConnectionReasonCode reasonCode in Enum.GetValues<PinConnectionReasonCode>())
        {
            string code = PinConnectionReasonCatalog.ToIssueCode(reasonCode);
            Assert.False(string.IsNullOrWhiteSpace(code));
        }
    }
}
