using System.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Sotsera.Rafter.Internal;

internal sealed class TargetExecution(TargetPlan plan)
{
    public TargetPlan Plan { get; } = plan;
    public TargetSnapshot? Current { get; private set; }
    public int CompletedBodies { get; private set; }

    public async Task RunAsync(RafterContext context, InvocationOutput output)
    {
        foreach (var target in Plan.Order)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (target.Body is null)
            {
                continue;
            }
            Current = target;
            output.TargetStarted(target, Plan.DependencySuffix(target));
            var elapsed = Stopwatch.StartNew();
            var task = target.Body(context)
                ?? throw new InvalidOperationException($"Target '{target.Name}' returned a null task.");
            await task.ConfigureAwait(false);
            if (output.Failure is { } failure)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
            context.CancellationToken.ThrowIfCancellationRequested();
            CompletedBodies++;
            output.TargetCompleted(target.Name, elapsed.Elapsed);
            Current = null;
        }
    }
}
