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
            await context.RunBodyAsync(target.Body, $"Target '{target.Name}'").ConfigureAwait(false);
            if (context.ProcessFailure)
            {
                return;
            }
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
