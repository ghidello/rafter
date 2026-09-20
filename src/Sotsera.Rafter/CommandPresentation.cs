using System.Collections.Immutable;
using Spectre.Console;
using static Sotsera.Rafter.BindingEngine;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.ExecutionRuntime;
using static Sotsera.Rafter.GraphPlanner;

namespace Sotsera.Rafter;

internal static class CommandPresentation
{
    internal static Report CreateModelFailure(ImmutableArray<ModelDiagnostic> diagnostics)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        lines.Add(new ReportLine("Command model is invalid", LineRole.ErrorHeading));
        foreach (ModelDiagnostic diagnostic in diagnostics)
        {
            lines.Add(new ReportLine($"error: {diagnostic.Message}", LineRole.Error));
        }

        return new Report(lines.ToImmutable());
    }

    internal static Report CreateHelp(CommandDefinition model, Guid entryTargetId, string invocationName)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        AddCommandHeader(lines, model, invocationName);
        AddOptions(lines, model);
        AddCommonOptions(lines);
        AddTargets(lines, model, entryTargetId);
        return new Report(lines.ToImmutable());
    }

    internal static Report CreateInputFailure(
        CommandDefinition model,
        Guid entryTargetId,
        string invocationName,
        ImmutableArray<InputDiagnostic> diagnostics)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        lines.Add(new ReportLine("Input errors", LineRole.ErrorHeading));
        foreach (InputDiagnostic diagnostic in diagnostics.Take(20))
        {
            lines.Add(new ReportLine($"error: {diagnostic.Message}", LineRole.Error));
        }

        if (diagnostics.Length > 20)
        {
            lines.Add(new ReportLine($"... and {diagnostics.Length - 20} more errors.", LineRole.Error));
        }

        lines.Add(ReportLine.Blank);
        AddCommandHeader(lines, model, invocationName);
        AddOptions(lines, model);
        AddCommonOptions(lines);
        AddTargets(lines, model, entryTargetId);
        return new Report(lines.ToImmutable());
    }

    internal static Report CreateBindingFailure(BindingResult result)
    {
        BindingFailure failure = result.Failure!;
        string message = $"Command failed during {failure.Stage} for '--{failure.OptionName}' "
            + $"({failure.ExceptionType.Name}).";
        return new Report([
            new ReportLine("Command failed", LineRole.ErrorHeading),
            new ReportLine($"error: {message}", LineRole.Error),
        ]);
    }

    internal static Report CreatePathFailure(string message)
        => new([
            new ReportLine("Command paths are invalid", LineRole.ErrorHeading),
            new ReportLine($"error: {ValueFormatter.FormatInlineText(message)}", LineRole.Error),
        ]);

    internal static Report CreateGraphFailure(ImmutableArray<GraphDiagnostic> diagnostics)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        lines.Add(new ReportLine("Target graph is invalid", LineRole.ErrorHeading));
        foreach (GraphDiagnostic diagnostic in diagnostics)
        {
            lines.Add(new ReportLine($"error: {diagnostic.Message}", LineRole.Error));
        }

        return new Report(lines.ToImmutable());
    }

    internal static Report CreateCancellation()
        => new([new ReportLine("Command cancelled", LineRole.ErrorHeading)]);

    internal static Report CreateExecutionSummary(ExecutionOutcome outcome, OutputCapabilities capabilities)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>();
        lines.Add(ReportLine.Blank);
        string status = outcome.ExitCode switch { 0 => "succeeded", 130 => "cancelled", _ => "failed" };
        lines.Add(new ReportLine($"Command {status}",
            outcome.ExitCode == 0 ? LineRole.SuccessHeading : LineRole.ErrorHeading));
        Dictionary<Guid, string> targetNames = outcome.Plan.Targets.ToDictionary(
            static target => target.Id, static target => target.Name);
        foreach (TargetResult target in outcome.Targets.OrderBy(static target => target.PlanIndex))
        {
            string state = target.Outcome == TargetOutcome.Succeeded
                ? target.Shape switch
                {
                    SuccessfulShape.Aggregate => "Aggregate",
                    SuccessfulShape.NoWork => "No work",
                    _ => "Succeeded",
                }
                : target.Outcome.ToString();
            string symbol = capabilities.IsRich && capabilities.SupportsUnicode
                ? target.Outcome switch
                {
                    TargetOutcome.Succeeded => "✓ ",
                    TargetOutcome.Skipped => "− ",
                    TargetOutcome.Failed => "✗ ",
                    _ => "! ",
                }
                : string.Empty;
            string blockers = target.DirectBlockers.IsEmpty
                ? string.Empty
                : $"; blocked by: {string.Join(", ", target.DirectBlockers.Select(id => targetNames[id]))}";
            LineRole role = target.Outcome switch
            {
                TargetOutcome.Succeeded => LineRole.Item,
                TargetOutcome.Skipped => LineRole.Muted,
                _ => LineRole.Error,
            };
            lines.Add(new ReportLine($"  {symbol}[{target.Target.Name}] {state}{blockers}", role));
        }

        if (outcome.ExitCode == 130)
        {
            AddSecondaryCleanupFailures(lines, outcome, includeCleanupOnlyFailures: true);
        }
        else if (outcome.ExitCode != 0)
        {
            lines.Add(ReportLine.Blank);
            AddExecutionFailures(lines, outcome);
        }

        return new Report(lines.ToImmutable());
    }

    internal static Report CreateInfrastructureFailure(string message)
        => new([
            new ReportLine("Command failed", LineRole.ErrorHeading),
            new ReportLine($"error: {message}", LineRole.Error),
        ]);

    internal static Task<bool> WriteAsync(
        Report report,
        TextWriter writer,
        OutputCapabilities capabilities,
        TextRedactor redactor)
    {
        try
        {
            if (!redactor.IsUsable || !TryRedact(report, redactor, out Report? safeReport))
            {
                return Task.FromResult(false);
            }

            string rendered = capabilities.IsRich
                ? RenderRich(safeReport!, capabilities)
                : RenderPlain(safeReport!, capabilities.NewLine);
            if (redactor.ContainsPattern(rendered))
            {
                return Task.FromResult(false);
            }

            TerminalPublication.WriteReport(writer, rendered);
            return Task.FromResult(true);
        }
        catch (Exception exception)
        {
            return Task.FromException<bool>(exception);
        }
    }

    internal static string RenderRich(Report report, OutputCapabilities capabilities)
    {
        StringWriter writer = new() { NewLine = "\n" };
        IAnsiConsole console = OutputPresentation.CreateConsole(writer, capabilities);
        foreach (ReportLine line in report.Lines)
        {
            WriteRichLine(console, line, capabilities.SupportsColor);
        }

        return writer.ToString().ReplaceLineEndings(capabilities.NewLine);
    }

    private static void AddExecutionFailures(ImmutableArray<ReportLine>.Builder lines, ExecutionOutcome outcome)
    {
        if (outcome.InfrastructureException is not null)
        {
            lines.Add(new ReportLine(
                "error: Command execution failed because scheduler state was inconsistent.",
                LineRole.Error));
        }

        foreach (TargetResult target in outcome.Targets.Where(static target => target.Outcome == TargetOutcome.Failed)
            .OrderBy(static target => target.PlanIndex))
        {
            string phase = target.FailurePhase?.ToString().ToLowerInvariant() ?? "execution";
            string exception = target.PrimaryException?.GetType().Name ?? "unknown failure";
            lines.Add(new ReportLine(
                $"error: Target '{target.Target.Name}' failed during {phase} ({exception}).",
                LineRole.Error));
        }

        bool hasPrimaryTargetFailure = outcome.Targets.Any(static target => target.Outcome == TargetOutcome.Failed);
        if (outcome.CommandCleanupException is not null
            && !hasPrimaryTargetFailure
            && outcome.InfrastructureException is null)
        {
            lines.Add(new ReportLine(
                $"error: Command cleanup failed ({outcome.CommandCleanupException.GetType().Name}).",
                LineRole.Error));
        }

        AddSecondaryCleanupFailures(lines, outcome, includeCleanupOnlyFailures: false);
    }

    private static void AddCommandHeader(
        ImmutableArray<ReportLine>.Builder lines,
        CommandDefinition model,
        string invocationName)
    {
        lines.Add(new ReportLine(model.Description, LineRole.Text));
        lines.Add(ReportLine.Blank);
        lines.Add(new ReportLine("Usage", LineRole.Heading));
        lines.Add(new ReportLine($"  {ValueFormatter.FormatInlineText(invocationName)} [options]", LineRole.Text));
    }

    private static void AddSecondaryCleanupFailures(
        ImmutableArray<ReportLine>.Builder lines,
        ExecutionOutcome? outcome,
        bool includeCleanupOnlyFailures)
    {
        if (outcome is null)
        {
            return;
        }

        ImmutableArray<TargetResult> secondaryTargetFailures = outcome.Targets
            .Where(target =>
                target.CleanupException is not null
                && (includeCleanupOnlyFailures || target.FailurePhase != FailurePhase.Cleanup))
            .OrderBy(static target => target.PlanIndex)
            .ToImmutableArray();
        bool commandCleanupIsSecondary = outcome.CommandCleanupException is not null
            && (outcome.InvocationCancellationRequested
                || outcome.InfrastructureException is not null
                || outcome.Targets.Any(static target => target.Outcome == TargetOutcome.Failed));
        if (secondaryTargetFailures.IsEmpty && !commandCleanupIsSecondary)
        {
            return;
        }

        lines.Add(ReportLine.Blank);
        lines.Add(new ReportLine("Cleanup also failed", LineRole.ErrorHeading));
        foreach (TargetResult target in secondaryTargetFailures)
        {
            lines.Add(new ReportLine(
                $"error: Target '{target.Target.Name}' cleanup failed ({target.CleanupException!.GetType().Name}).",
                LineRole.Error));
        }

        if (commandCleanupIsSecondary)
        {
            lines.Add(new ReportLine(
                $"error: Command cleanup failed ({outcome.CommandCleanupException!.GetType().Name}).",
                LineRole.Error));
        }
    }

    private static void AddOptions(ImmutableArray<ReportLine>.Builder lines, CommandDefinition model)
    {
        lines.Add(ReportLine.Blank);
        lines.Add(new ReportLine("Command options", LineRole.Heading));
        if (model.Options.IsEmpty)
        {
            lines.Add(new ReportLine("  (none)", LineRole.Muted));
            return;
        }

        foreach (OptionDefinition option in model.Options)
        {
            string alias = option.Alias is null ? string.Empty : $"-{option.Alias}, ";
            lines.Add(new ReportLine($"  {alias}--{option.Name}{GetValueSuffix(option)}", LineRole.Item));
            lines.Add(new ReportLine($"      {option.Description}", LineRole.Text));
            string metadata = GetOptionMetadata(option);
            if (metadata.Length > 0)
            {
                lines.Add(new ReportLine($"      {metadata}", LineRole.Muted));
            }
        }
    }

    private static void AddCommonOptions(ImmutableArray<ReportLine>.Builder lines)
    {
        lines.Add(ReportLine.Blank);
        lines.Add(new ReportLine("Common options", LineRole.Heading));
        lines.Add(new ReportLine("  --plain", LineRole.Item));
        lines.Add(new ReportLine("      Use deterministic plain-text output.", LineRole.Text));
        lines.Add(new ReportLine("  -h, --help", LineRole.Item));
        lines.Add(new ReportLine("      Show command help.", LineRole.Text));
    }

    private static void AddTargets(
        ImmutableArray<ReportLine>.Builder lines,
        CommandDefinition model,
        Guid entryTargetId)
    {
        lines.Add(ReportLine.Blank);
        lines.Add(new ReportLine("Targets", LineRole.Heading));
        lines.Add(new ReportLine(
            "  Targets describe the contained execution graph; they are not command-line selections.",
            LineRole.Muted));
        foreach (TargetDefinition target in model.Targets)
        {
            List<string> markers = [];
            if (target.Id == entryTargetId)
            {
                markers.Add("entry");
            }

            if (!target.Conditions.IsEmpty)
            {
                markers.Add("conditional");
            }

            markers.Add(target.Execution is null
                ? target.Dependencies.IsEmpty ? "no work" : "aggregate"
                : "executable");
            lines.Add(new ReportLine($"  {target.Name} [{string.Join(", ", markers)}]", LineRole.Item));
            lines.Add(new ReportLine($"      {target.Description}", LineRole.Text));
            if (!target.Dependencies.IsEmpty)
            {
                IEnumerable<string> names = target.Dependencies.Select(id =>
                    model.Targets.Single(candidate => candidate.Id == id).Name);
                lines.Add(new ReportLine($"      depends on: {string.Join(", ", names)}", LineRole.Muted));
            }
        }
    }

    private static string GetValueSuffix(OptionDefinition option)
    {
        string valueName = GetValueName(option);
        if ((Nullable.GetUnderlyingType(option.ValueType) ?? option.ValueType) == typeof(bool))
        {
            return $" [{valueName}]";
        }

        return option.IsRepeated ? $" <{valueName}>..." : $" <{valueName}>";
    }

    private static string GetValueName(OptionDefinition option)
    {
        Type type = option.ValueType;
        Type effectiveType = Nullable.GetUnderlyingType(type) ?? type;
        if (effectiveType == typeof(string))
        {
            return "TEXT";
        }

        if (effectiveType == typeof(bool))
        {
            return "BOOL";
        }

        if (effectiveType.IsEnum)
        {
            return string.Join('|', option.BindingStrategy.PermittedValues).ToUpperInvariant();
        }

        if (effectiveType == typeof(char))
        {
            return "CHAR";
        }

        if (effectiveType == typeof(Guid))
        {
            return "GUID";
        }

        if (effectiveType == typeof(DateOnly))
        {
            return "DATE";
        }

        if (effectiveType == typeof(TimeOnly))
        {
            return "TIME";
        }

        if (effectiveType == typeof(DateTime) || effectiveType == typeof(DateTimeOffset))
        {
            return "DATETIME";
        }

        if (effectiveType == typeof(TimeSpan))
        {
            return "DURATION";
        }

        return IsNumeric(effectiveType) ? "NUMBER" : effectiveType.Name.ToUpperInvariant();
    }

    private static bool IsNumeric(Type type)
        => type == typeof(sbyte)
            || type == typeof(byte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(Int128)
            || type == typeof(UInt128)
            || type == typeof(nint)
            || type == typeof(nuint)
            || type == typeof(Half)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);

    private static string GetOptionMetadata(OptionDefinition option)
    {
        List<string> metadata = [];
        metadata.Add(option.IsRequired ? "required" : "optional");
        if (option.EnvironmentName is not null)
        {
            metadata.Add($"environment: {ValueFormatter.FormatInlineText(option.EnvironmentName)}");
        }

        if (option.IsSensitive)
        {
            metadata.Add("sensitive");
        }
        else if (option.HasDefault)
        {
            metadata.Add($"default: {option.DefaultDisplay ?? "configured"}");
        }

        return string.Join("; ", metadata);
    }

    private static bool TryRedact(Report report, TextRedactor redactor, out Report? safeReport)
    {
        ImmutableArray<ReportLine>.Builder lines = ImmutableArray.CreateBuilder<ReportLine>(report.Lines.Length);
        foreach (ReportLine line in report.Lines)
        {
            if (!redactor.TryRedact(line.Text, out string safeText))
            {
                safeReport = null;
                return false;
            }

            lines.Add(line with { Text = safeText });
        }

        safeReport = new Report(lines.MoveToImmutable());
        return true;
    }

    private static string RenderPlain(Report report, string newLine)
    {
        StringWriter writer = new() { NewLine = newLine };
        foreach (ReportLine line in report.Lines)
        {
            writer.WriteLine(line.Text);
        }

        return writer.ToString();
    }

    private static void WriteRichLine(IAnsiConsole console, ReportLine line, bool color)
    {
        if (!color)
        {
            console.WriteLine(line.Text);
            return;
        }

        string text = Markup.Escape(line.Text);
        switch (line.Role)
        {
            case LineRole.SuccessHeading:
                console.MarkupLine($"[bold green]{text}[/]");
                break;
            case LineRole.Heading:
                console.MarkupLine($"[bold navy]{text}[/]");
                break;
            case LineRole.ErrorHeading:
                console.MarkupLine($"[bold maroon]{text}[/]");
                break;
            case LineRole.Error:
                console.MarkupLine($"[maroon]{text}[/]");
                break;
            case LineRole.Item:
                console.MarkupLine($"[green]{text}[/]");
                break;
            case LineRole.Muted:
                console.MarkupLine($"[grey]{text}[/]");
                break;
            default:
                console.WriteLine(line.Text);
                break;
        }
    }

    internal sealed record Report(ImmutableArray<ReportLine> Lines);

    internal sealed record ReportLine(string Text, LineRole Role)
    {
        internal static ReportLine Blank { get; } = new(string.Empty, LineRole.Text);
    }

    internal enum LineRole
    {
        Text,
        Heading,
        SuccessHeading,
        ErrorHeading,
        Error,
        Item,
        Muted,
    }
}
