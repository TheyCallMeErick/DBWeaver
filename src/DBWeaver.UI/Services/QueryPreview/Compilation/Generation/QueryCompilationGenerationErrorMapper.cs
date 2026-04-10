namespace DBWeaver.UI.Services.QueryPreview;

internal sealed class QueryCompilationGenerationErrorMapper
{
    public IEnumerable<string> Map(Exception ex)
    {
        if (TryResolvePlanningException(ex, out DBWeaver.Nodes.LogicalPlan.PlanningException? planningException))
        {
            DBWeaver.Nodes.LogicalPlan.PlanningException resolved = planningException!;
            yield return resolved.Message;

            switch (resolved.Kind)
            {
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.JoinWithoutCondition:
                    yield return "Join node requires an explicit condition. Connect a condition pin or configure an explicit ON expression.";
                    yield break;
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.OutputSourceAmbiguous:
                    yield return "Use exactly one top-level Result Output and ensure it references a single resolvable source path.";
                    yield break;
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.DatasetNotReachableFromOutput:
                    yield return "Output is not connected to a reachable dataset source. Connect table/cte/subquery sources to output columns.";
                    yield break;
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.DuplicateAlias:
                    yield return "Duplicate dataset alias detected. Set unique aliases for each source node.";
                    yield break;
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.CteNotReferencedInPlan:
                    yield return "CTE definida sem uso no plano final. Conecte um CTE Source ao fluxo que chega no Result Output.";
                    yield break;
                case DBWeaver.Nodes.LogicalPlan.PlannerErrorKind.CyclicDependency:
                    if (resolved.Message.Contains("not marked recursive", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return "CTE self-reference requires the 'recursive' flag enabled on the CTE Definition node.";
                        yield break;
                    }

                    if (resolved.Message.Contains("cycle detected between cte definitions", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return "CTE cycle detected. Remove circular CTE dependencies or refactor with a base CTE plus recursive CTE.";
                        yield break;
                    }

                    yield return "Dependência cíclica detectada entre CTEs. Remova o ciclo ou marque apenas auto-referência válida como recursiva.";
                    yield break;
            }
        }

        if (ex is InvalidOperationException && ex.Message.Contains("Cycle detected between CTE definitions", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE cycle detected. Remove circular CTE dependencies or refactor with a base CTE plus recursive CTE.";
            yield break;
        }

        if (ex is InvalidOperationException && ex.Message.Contains("references itself but is not marked recursive", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "CTE self-reference requires the 'recursive' flag enabled on the CTE Definition node.";
            yield break;
        }

        if (ex is NotSupportedException && ex.Message.Contains("requires 'value' input", StringComparison.OrdinalIgnoreCase))
        {
            yield return ex.Message;
            yield return "Window function is missing required 'value' input. Connect a value pin for this function type.";
            yield break;
        }

        yield return ex.Message;
    }

    private static bool TryResolvePlanningException(
        Exception exception,
        out DBWeaver.Nodes.LogicalPlan.PlanningException? planningException)
    {
        if (exception is DBWeaver.Nodes.LogicalPlan.PlanningException direct)
        {
            planningException = direct;
            return true;
        }

        if (exception.InnerException is DBWeaver.Nodes.LogicalPlan.PlanningException inner)
        {
            planningException = inner;
            return true;
        }

        planningException = null;
        return false;
    }
}
