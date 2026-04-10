namespace DBWeaver.Nodes.Pins;

public enum PinConnectionReasonCode
{
    None = 0,
    SelfConnectionForbidden = 1,
    SameNodeConnectionForbidden = 2,
    SameDirectionForbidden = 3,
    DomainFamilyMismatch = 4,
    ScalarTypeMismatch = 5,
    StructuralTypeMismatch = 6,
    MultiplicityExceeded = 7,
    WildcardProjectionOnly = 8,
    ComparisonTypeMismatch = 9,
}
