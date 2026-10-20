namespace Sotsera.Rafter.Internal;

internal sealed record TargetSnapshot(Target Target, string Name, string Description, Target[] Dependencies,
    Func<RafterContext, Task>? Body, int BodyCalls, bool NullDependencies);

internal sealed record TargetPlan(TargetSnapshot[] Entries, TargetSnapshot[] Order,
    IReadOnlyDictionary<Target, TargetSnapshot> Declarations)
{
    public string EntryNames => string.Join(", ", Entries.Select(entry => entry.Name));

    public string DependencySuffix(TargetSnapshot target)
    {
        return target.Dependencies.Length == 0 ? ""
            : " <-- (" + string.Join(", ", target.Dependencies.Select(dependency => Declarations[dependency].Name)) + ")";
    }

    public static TargetPlan? Prepare(TargetSnapshot[] targets, Target[]? entries, List<string> diagnostics)
    {
        var declarations = targets.ToDictionary(target => target.Target);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in targets)
        {
            if (!DeclarationName.IsValid(target.Name))
            {
                diagnostics.Add("Target names must start with a lowercase letter and use lowercase letters/digits and single hyphens.");
            }
            else if (!names.Add(target.Name))
            {
                diagnostics.Add($"Target name '{target.Name}' is declared more than once.");
            }
            if (string.IsNullOrWhiteSpace(target.Description))
            {
                diagnostics.Add($"Target '{target.Name}' requires a nonblank description.");
            }
            if (target.BodyCalls > 1)
            {
                diagnostics.Add($"Target '{target.Name}' may declare its body only once.");
            }
            if (target.BodyCalls > 0 && target.Body is null)
            {
                diagnostics.Add($"Target '{target.Name}' has a null body.");
            }
            if (target.BodyCalls == 0 && target.Dependencies.Length == 0)
            {
                diagnostics.Add($"Target '{target.Name}' requires a body or dependencies.");
            }
            if (target.NullDependencies)
            {
                diagnostics.Add($"Target '{target.Name}' has a null dependency array.");
            }
            foreach (var dependency in target.Dependencies)
            {
                if (dependency is null || !declarations.ContainsKey(dependency))
                {
                    diagnostics.Add($"Target '{target.Name}' has a null dependency or a dependency from another command.");
                }
            }
        }
        if (entries is not null)
        {
            foreach (var entry in entries)
            {
                if (entry is null || !declarations.ContainsKey(entry))
                {
                    diagnostics.Add("Selected targets must be non-null and belong to this command.");
                }
            }
        }

        // Validate all declarations, including unreachable cycles. Use an explicit stack for deep graphs.
        Visit(targets, declarations, diagnostics);
        if (entries is null || diagnostics.Count > 0)
        {
            return null;
        }
        var selected = entries.Distinct().Select(entry => declarations[entry]).ToArray();
        var order = Visit(selected, declarations, diagnostics);
        return new(selected, order, declarations);
    }

    private static TargetSnapshot[] Visit(TargetSnapshot[] roots,
        Dictionary<Target, TargetSnapshot> declarations, List<string> diagnostics)
    {
        var completed = new HashSet<Target>();
        var active = new Dictionary<Target, int>();
        var path = new List<TargetSnapshot>();
        var order = new List<TargetSnapshot>();
        var stack = new Stack<(TargetSnapshot Target, int NextDependency)>();
        foreach (var root in roots)
        {
            if (completed.Contains(root.Target))
            {
                continue;
            }
            active.Add(root.Target, path.Count);
            path.Add(root);
            stack.Push((root, 0));
            while (stack.TryPop(out var frame))
            {
                var target = frame.Target;
                if (frame.NextDependency == target.Dependencies.Length)
                {
                    completed.Add(target.Target);
                    active.Remove(target.Target);
                    path.RemoveAt(path.Count - 1);
                    order.Add(target);
                    continue;
                }
                stack.Push((target, frame.NextDependency + 1));
                var dependency = target.Dependencies[frame.NextDependency];
                if (dependency is null || !declarations.TryGetValue(dependency, out var prerequisite)
                    || completed.Contains(dependency))
                {
                    continue;
                }
                if (active.TryGetValue(dependency, out var cycleStart))
                {
                    var cycle = path.Skip(cycleStart).Select(item => item.Name).Append(prerequisite.Name);
                    diagnostics.Add("Dependency cycle: " + string.Join(" -> ", cycle) + ".");
                    continue;
                }
                active.Add(dependency, path.Count);
                path.Add(prerequisite);
                stack.Push((prerequisite, 0));
            }
        }
        return [.. order];
    }
}
