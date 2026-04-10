namespace DBWeaver.Nodes.Pins;

public static class PinConnectionReasonCatalog
{
    public static string ToIssueCode(PinConnectionReasonCode reasonCode) =>
        reasonCode switch
        {
            PinConnectionReasonCode.None => "PIN_NONE",
            PinConnectionReasonCode.StructuralTypeMismatch => "STRUCTURAL_TYPE_MISMATCH",
            _ => $"PIN_{reasonCode}",
        };

    public static string ToDiagnosticMessage(
        PinConnectionReasonCode reasonCode,
        string sourceLabel,
        string destinationLabel) =>
        reasonCode switch
        {
            PinConnectionReasonCode.SelfConnectionForbidden =>
                "A pin cannot be connected to itself.",
            PinConnectionReasonCode.SameNodeConnectionForbidden =>
                "Pins from the same node cannot be connected.",
            PinConnectionReasonCode.SameDirectionForbidden =>
                "Only output-to-input connections are allowed.",
            PinConnectionReasonCode.DomainFamilyMismatch =>
                "Query and DDL pin families are incompatible.",
            PinConnectionReasonCode.ScalarTypeMismatch =>
                $"Cannot connect {sourceLabel} to {destinationLabel} because scalar types are incompatible.",
            PinConnectionReasonCode.StructuralTypeMismatch =>
                $"Cannot connect {sourceLabel} to {destinationLabel} because structural pin families are incompatible.",
            PinConnectionReasonCode.MultiplicityExceeded =>
                "The destination pin does not accept additional connections.",
            PinConnectionReasonCode.WildcardProjectionOnly =>
                "Wildcard projection is only allowed for supported projection targets.",
            PinConnectionReasonCode.ComparisonTypeMismatch =>
                "Comparison inputs must share a compatible scalar type.",
            _ =>
                $"Cannot connect {sourceLabel} to {destinationLabel}.",
        };
}
