using System.Collections.Immutable;
using System.Runtime.InteropServices;
using static Sotsera.Rafter.CommandModel;

namespace Sotsera.Rafter;

internal static class GraphPlanner
{
    internal sealed record GraphDiagnostic(string Code, string Message, int DeclarationIndex);

    internal sealed record GraphPlan(
        ImmutableArray<TargetDefinition> Targets,
        ImmutableDictionary<Guid, int> Indices)
    {
        internal int GetIndex(Guid targetId)
            => Indices.TryGetValue(targetId, out int index)
                ? index
                : throw new InvalidOperationException("The target is missing from the execution plan.");
    }

    internal sealed record GraphPlanningResult(
        GraphPlan? Plan,
        ImmutableArray<GraphDiagnostic> Diagnostics)
    {
        internal bool IsSuccess => Plan is not null;
    }

    internal static GraphPlanningResult Plan(CommandDefinition model, Guid entryTargetId)
    {
        Dictionary<Guid, TargetDefinition> targets = model.Targets.ToDictionary(static target => target.Id);
        if (!targets.ContainsKey(entryTargetId))
        {
            throw new InvalidOperationException("The entry target is missing from the frozen command model.");
        }

        HashSet<Guid> reachable = FindReachableTargets(entryTargetId, targets);
        ImmutableArray<GraphDiagnostic> diagnostics = FindCycleDiagnostics(model, targets, reachable);
        if (!diagnostics.IsEmpty)
        {
            return new GraphPlanningResult(null, diagnostics);
        }

        ImmutableArray<TargetDefinition> ordered = CreateDependencyFirstOrder(entryTargetId, targets);
        ImmutableDictionary<Guid, int>.Builder indices = ImmutableDictionary.CreateBuilder<Guid, int>();
        for (int index = 0; index < ordered.Length; index++)
        {
            indices.Add(ordered[index].Id, index);
        }

        return new GraphPlanningResult(new GraphPlan(ordered, indices.ToImmutable()), []);
    }

    private static HashSet<Guid> FindReachableTargets(
        Guid entryTargetId,
        Dictionary<Guid, TargetDefinition> targets)
    {
        HashSet<Guid> reachable = [];
        Stack<Guid> pending = new();
        pending.Push(entryTargetId);
        while (pending.TryPop(out Guid targetId))
        {
            if (!reachable.Add(targetId))
            {
                continue;
            }

            TargetDefinition target = GetTarget(targets, targetId);
            for (int index = target.Dependencies.Length - 1; index >= 0; index--)
            {
                Guid dependencyId = target.Dependencies[index];
                _ = GetTarget(targets, dependencyId);
                pending.Push(dependencyId);
            }
        }

        return reachable;
    }

    private static ImmutableArray<GraphDiagnostic> FindCycleDiagnostics(
        CommandDefinition model,
        Dictionary<Guid, TargetDefinition> targets,
        IReadOnlySet<Guid> reachable)
    {
        List<Guid> finishOrder = CreateFinishOrder(model, targets, reachable);
        Dictionary<Guid, List<Guid>> reverseEdges = CreateReverseEdges(model, reachable);
        Dictionary<Guid, int> declarationIndices = model.Targets
            .Select(static (target, index) => (target.Id, Index: index))
            .ToDictionary(static item => item.Id, static item => item.Index);
        HashSet<Guid> assigned = [];
        List<GraphDiagnostic> diagnostics = [];

        for (int index = finishOrder.Count - 1; index >= 0; index--)
        {
            Guid rootId = finishOrder[index];
            if (!assigned.Add(rootId))
            {
                continue;
            }

            List<Guid> component = [rootId];
            Stack<Guid> pending = new();
            pending.Push(rootId);
            while (pending.TryPop(out Guid targetId))
            {
                List<Guid> dependents = reverseEdges[targetId];
                for (int dependentIndex = dependents.Count - 1; dependentIndex >= 0; dependentIndex--)
                {
                    Guid dependentId = dependents[dependentIndex];
                    if (assigned.Add(dependentId))
                    {
                        component.Add(dependentId);
                        pending.Push(dependentId);
                    }
                }
            }

            bool cyclic = component.Count > 1 || targets[rootId].Dependencies.Contains(rootId);
            if (!cyclic)
            {
                continue;
            }

            component.Sort((left, right) => declarationIndices[left].CompareTo(declarationIndices[right]));
            string names = string.Join(", ", component.Select(id => $"'{targets[id].Name}'"));
            diagnostics.Add(new GraphDiagnostic(
                "RAFTER1401",
                $"The target graph contains a cycle involving {names}.",
                declarationIndices[component[0]]));
        }

        return diagnostics
            .OrderBy(static diagnostic => diagnostic.DeclarationIndex)
            .ThenBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    private static List<Guid> CreateFinishOrder(
        CommandDefinition model,
        Dictionary<Guid, TargetDefinition> targets,
        IReadOnlySet<Guid> reachable)
    {
        HashSet<Guid> visited = [];
        List<Guid> finishOrder = [];
        foreach (TargetDefinition target in model.Targets)
        {
            if (!reachable.Contains(target.Id) || !visited.Add(target.Id))
            {
                continue;
            }

            Stack<TraversalFrame> traversal = new();
            traversal.Push(new TraversalFrame(target.Id, 0));
            while (traversal.TryPop(out TraversalFrame frame))
            {
                TargetDefinition current = GetTarget(targets, frame.TargetId);
                if (frame.NextDependencyIndex >= current.Dependencies.Length)
                {
                    finishOrder.Add(frame.TargetId);
                    continue;
                }

                traversal.Push(frame with { NextDependencyIndex = frame.NextDependencyIndex + 1 });
                Guid dependencyId = current.Dependencies[frame.NextDependencyIndex];
                if (reachable.Contains(dependencyId) && visited.Add(dependencyId))
                {
                    traversal.Push(new TraversalFrame(dependencyId, 0));
                }
            }
        }

        return finishOrder;
    }

    private static Dictionary<Guid, List<Guid>> CreateReverseEdges(
        CommandDefinition model,
        IReadOnlySet<Guid> reachable)
    {
        Dictionary<Guid, List<Guid>> reverseEdges = reachable.ToDictionary(
            static id => id,
            static _ => new List<Guid>());
        foreach (TargetDefinition target in model.Targets)
        {
            if (!reachable.Contains(target.Id))
            {
                continue;
            }

            foreach (Guid dependencyId in target.Dependencies)
            {
                reverseEdges[dependencyId].Add(target.Id);
            }
        }

        return reverseEdges;
    }

    private static ImmutableArray<TargetDefinition> CreateDependencyFirstOrder(
        Guid entryTargetId,
        Dictionary<Guid, TargetDefinition> targets)
    {
        HashSet<Guid> visited = [];
        ImmutableArray<TargetDefinition>.Builder ordered = ImmutableArray.CreateBuilder<TargetDefinition>();
        Stack<ExpansionFrame> pending = new();
        pending.Push(new ExpansionFrame(entryTargetId, Expanded: false));
        while (pending.TryPop(out ExpansionFrame frame))
        {
            if (frame.Expanded)
            {
                ordered.Add(GetTarget(targets, frame.TargetId));
                continue;
            }

            if (!visited.Add(frame.TargetId))
            {
                continue;
            }

            TargetDefinition target = GetTarget(targets, frame.TargetId);
            pending.Push(frame with { Expanded = true });
            for (int index = target.Dependencies.Length - 1; index >= 0; index--)
            {
                pending.Push(new ExpansionFrame(target.Dependencies[index], Expanded: false));
            }
        }

        return ordered.ToImmutable();
    }

    private static TargetDefinition GetTarget(
        Dictionary<Guid, TargetDefinition> targets,
        Guid targetId)
        => targets.TryGetValue(targetId, out TargetDefinition? target)
            ? target
            : throw new InvalidOperationException("A dependency is missing from the frozen command model.");

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct TraversalFrame(Guid TargetId, int NextDependencyIndex);

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct ExpansionFrame(Guid TargetId, bool Expanded);
}
