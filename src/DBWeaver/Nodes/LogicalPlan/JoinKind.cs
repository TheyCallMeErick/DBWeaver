namespace DBWeaver.Nodes.LogicalPlan;

public enum JoinKind
{
    Inner = 0,
    Left = 1,
    Right = 2,
    Full = 3,
    Cross = 4,
}
