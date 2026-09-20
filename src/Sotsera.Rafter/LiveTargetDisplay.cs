using System.Collections.Immutable;
using static Sotsera.Rafter.CommandPresentation;
using static Sotsera.Rafter.ExecutionRuntime;

namespace Sotsera.Rafter;

internal sealed class LiveTargetDisplay
{
    private readonly OutputCapabilities _capabilities;
    private readonly TextRedactor _redactor;
    private readonly TargetNotification[] _targets;

    internal LiveTargetDisplay(GraphPlanner.GraphPlan plan, OutputCapabilities capabilities, TextRedactor redactor)
    {
        _capabilities = capabilities;
        _redactor = redactor;
        _targets = plan.Targets.Select((target, index) => new TargetNotification(target.Id, target.Name, index,
            TargetLifecycle.Pending, null, null, [])).ToArray();
    }

    internal string Update(TargetNotification notification)
    {
        _targets[notification.PlanIndex] = notification;
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        lines.Add(new ReportLine("Targets", LineRole.Heading));
        foreach (TargetNotification target in _targets)
        {
            string state = State(target);
            string symbol = _capabilities.SupportsUnicode ? Symbol(target) + " " : string.Empty;
            string blockers = target.DirectBlockers.IsEmpty ? string.Empty
                : "; blocked by: " + string.Join(", ", target.DirectBlockers.Select(id =>
                    _targets.Single(candidate => candidate.TargetId == id).TargetName));
            string text = $"  {symbol}[{target.TargetName}] {state}{blockers}";
            if (!_redactor.TryRedact(text, out string safe))
            {
                throw new InvalidOperationException("Live target state could not be redacted safely.");
            }

            lines.Add(new ReportLine(safe, Role(target)));
        }

        string frame = RenderRich(new Report(lines.ToImmutable()), _capabilities);
        if (_redactor.ContainsPattern(frame))
        {
            throw new InvalidOperationException("Live target state failed redaction verification.");
        }

        return frame;
    }

    private static string State(TargetNotification target)
        => target.Lifecycle switch
        {
            TargetLifecycle.Pending or TargetLifecycle.Ready => "Waiting",
            TargetLifecycle.Running => "Running",
            TargetLifecycle.CleaningUp => "Cleaning up",
            _ => target.Outcome == TargetOutcome.Succeeded ? target.Shape switch
            {
                SuccessfulShape.Aggregate => "Aggregate",
                SuccessfulShape.NoWork => "No work",
                _ => "Succeeded",
            } : target.Outcome!.ToString()!,
        };

    private static string Symbol(TargetNotification target)
        => target.Lifecycle switch
        {
            TargetLifecycle.Pending or TargetLifecycle.Ready => "·",
            TargetLifecycle.Running => "›",
            TargetLifecycle.CleaningUp => "↳",
            _ => target.Outcome switch
            {
                TargetOutcome.Succeeded => "✓",
                TargetOutcome.Skipped => "−",
                TargetOutcome.Failed => "✗",
                _ => "!",
            },
        };

    private static LineRole Role(TargetNotification target)
        => target.Lifecycle switch
        {
            TargetLifecycle.Pending or TargetLifecycle.Ready => LineRole.Muted,
            TargetLifecycle.Running or TargetLifecycle.CleaningUp => LineRole.Text,
            _ => target.Outcome switch
            {
                TargetOutcome.Succeeded => LineRole.Item,
                TargetOutcome.Skipped => LineRole.Muted,
                _ => LineRole.Error,
            },
        };
}
