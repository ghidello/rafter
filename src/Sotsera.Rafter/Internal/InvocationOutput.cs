using System.Globalization;
using Spectre.Console;

namespace Sotsera.Rafter.Internal;

internal sealed class InvocationOutput
{
    private readonly RendererDestination _output;
    private readonly RendererDestination _error;
    private readonly InvocationArguments _arguments;
    private bool _targetStarted;

    public Exception? Failure => _output.Failure ?? _error.Failure;

    public InvocationOutput(InvocationHost host, InvocationArguments arguments)
    {
        _arguments = arguments;
        _output = new(host.Output,
            DestinationPolicy.Detect(host.Output, host.OutputRedirected, arguments.Plain, host.NoColor), host.Width);
        _error = new(host.Error,
            DestinationPolicy.Detect(host.Error, host.ErrorRedirected, arguments.Plain, host.NoColor), host.Width);
    }

    public void WriteLine(string? text) => _output.Literal((text ?? "") + Environment.NewLine, "  | ");

    public void Help(string description, OptionSnapshot[] options, CombinedRule[] rules, TargetPlan? plan = null)
    {
        _output.Framework(description, new Style(decoration: Decoration.Bold));
        _output.Framework("", Style.Plain);
        Section("Command options");
        if (options.Length == 0)
        {
            _output.Framework("  None.", Style.Plain);
        }
        for (var index = 0; index < options.Length; index++)
        {
            if (index > 0 && options[index - 1].Rules.Length > 0)
            {
                _output.Framework("", Style.Plain);
            }
            Input(options[index]);
        }
        _output.Framework("", Style.Plain);
        Section("Execution");
        if (plan is null)
        {
            _output.Framework("  Run the supplied command body.", Style.Plain);
        }
        else
        {
            TargetHelp(plan);
        }
        if (rules.Length > 0)
        {
            _output.Framework("  Combined input rules:", Style.Plain);
            foreach (var rule in rules)
            {
                _output.Layout(new Paragraph("- " + rule.Description) { Overflow = Overflow.Fold }, 4);
            }
        }
        _output.Framework("", Style.Plain);
        Section("Rafter options");
        Control("--help", plan is null ? "Show help without executing the command body."
            : "Show help without executing targets.");
        Control("--quiet", "Hide routine lifecycle output; keep results and failures.");
        Control("--verbose", "Show additional execution context.");
        Control("--plain", "Disable styling and presentation prefixes.");
    }

    public void Start(string description, TargetPlan? plan = null)
    {
        if (!_arguments.Quiet)
        {
            if (plan is not null)
            {
                var heading = new Paragraph("> " + plan.EntryNames, new Style(decoration: Decoration.Bold));
                if (plan.Entries.Length == 1)
                {
                    heading.Append(plan.DependencySuffix(plan.Entries[0]), new Style(Color.Grey));
                }
                _error.Layout(heading);
            }
            _error.Framework(description, new Style(decoration: Decoration.Bold));
            _error.Framework("", Style.Plain);
        }
    }

    public void Invalid(string heading, IEnumerable<string> diagnostics)
    {
        _error.Framework(heading, new Style(Color.Red, decoration: Decoration.Bold));
        foreach (var diagnostic in diagnostics)
        {
            _error.Framework("  " + diagnostic, Style.Plain);
        }
    }

    public void CancellationRequested()
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("Cancellation requested.", new Style(Color.Yellow));
        }
    }

    public void Exception(Exception exception, string scope)
    {
        if (exception is ProcessFailureException rejected)
        {
            ProcessRejected(rejected, ProcessFailureAction.Throw);
            return;
        }
        if (exception is ProcessLaunchException launch)
        {
            _error.Framework("  " + launch.Message, new Style(Color.Red));
            _error.Framework("  " + launch.InnerException!.Message, Style.Plain);
            _error.Framework("  Working directory: " + launch.Directory, Style.Plain);
            return;
        }
        if (exception is ProcessHandlerException handler)
        {
            _error.Framework("  " + handler.Message, new Style(Color.Red));
            ProcessExit(handler.Failure);
            _error.Literal(handler.InnerException + Environment.NewLine, "  ");
            return;
        }
        if (exception is ProcessCompletionException completion)
        {
            _error.Framework("  " + completion.Message, new Style(Color.Red));
            if (completion.ExitCode is { } code)
            {
                _error.Framework($"  Observed exit code: {code}", Style.Plain);
            }
            if (completion.InnerException is { } cause)
            {
                _error.Framework("  " + cause.Message, Style.Plain);
            }
            return;
        }
        _error.Framework($"  {scope} failed", new Style(Color.Red));
        _error.Literal(exception.ToString() + Environment.NewLine, "  ");
    }

    public void ProcessStarted(string executable, string directory)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework("  Process | " + executable, new Style(decoration: Decoration.Bold));
            if (_arguments.Verbose)
            {
                _error.Framework("  Working directory: " + directory, Style.Plain);
            }
        }
    }

    public void ProcessOutput(string text, bool error) => (error ? _error : _output).Literal(text);

    public void ProcessRejected(ProcessFailureException failure, ProcessFailureAction action)
    {
        var heading = action == ProcessFailureAction.Continue ? "Process failure handled" : "Process failed";
        _error.Framework($"  {heading} | {failure.Executable}",
            new Style(action == ProcessFailureAction.Continue ? Color.Yellow : Color.Red));
        ProcessExit(failure);
        if (action != ProcessFailureAction.Throw)
        {
            _error.Framework($"  Action: {action}", Style.Plain);
        }
    }

    private void ProcessExit(ProcessFailureException failure)
    {
        _error.Framework($"  Exit code: {failure.Result.ExitCode} | accepted: {string.Join(", ", failure.AcceptedExitCodes)}",
            Style.Plain);
    }

    public void Summary(int code, TimeSpan elapsed, TargetExecution? targets = null)
    {
        if (code == 0 && _arguments.Quiet)
        {
            return;
        }
        var (label, color) = code switch
        {
            0 => ("[OK] Completed", Color.Green),
            130 => ("[Cancelled] Cancelled", Color.Yellow),
            _ => ("[Failed] Failed | invocation", Color.Red)
        };
        if (targets is not null)
        {
            var status = code == 0 ? "OK" : code == 130 ? "Cancelled" : "Failed";
            var detail = code == 0 ? $"{targets.CompletedBodies} target bodies completed"
                : targets.Current is { } current ? $"stopped in {current.Name}" : "invocation";
            label = $"[{status}] {targets.Plan.EntryNames} | {detail}";
        }
        _error.Framework("", Style.Plain);
        _error.Framework($"{label} | {Duration(elapsed)}", new Style(color));
    }

    public void TargetStarted(TargetSnapshot target, string dependencySuffix)
    {
        if (_arguments.Quiet)
        {
            return;
        }
        if (_targetStarted)
        {
            _error.Framework("", Style.Plain);
        }
        _targetStarted = true;
        Heading(_error, target, dependencySuffix);
        _error.Framework(target.Description, Style.Plain);
        _error.Framework("", Style.Plain);
    }

    public void TargetCompleted(string name, TimeSpan elapsed)
    {
        if (!_arguments.Quiet)
        {
            _error.Framework($"[OK] {name} | {Duration(elapsed)}", new Style(Color.Green));
        }
    }

    public Task CompleteOutputAsync() => _output.CompleteAsync();

    public Task CompleteErrorAsync() => _error.CompleteAsync();

    private void Section(string text) => _output.Framework(text, new Style(Color.Teal, decoration: Decoration.Bold));

    private void TargetHelp(TargetPlan plan)
    {
        _output.Layout(new Paragraph($"The script selects {plan.EntryNames}. Each target body runs once.")
            { Overflow = Overflow.Fold }, 2);
        foreach (var target in plan.Entries.Where(target => target.Body is null).Concat(plan.Order)
            .DistinctBy(target => target.Target))
        {
            _output.Framework("", Style.Plain);
            Heading(_output, target, plan.DependencySuffix(target), 2);
            var description = target.Description + (target.Body is null ? " (Group; no body.)" : "");
            _output.Layout(new Paragraph(description) { Overflow = Overflow.Fold }, 4);
        }
        _output.Framework("", Style.Plain);
        var bodyOrder = string.Join(", ", plan.Order.Where(target => target.Body is not null).Select(target => target.Name));
        _output.Layout(new Paragraph($"Body order: {bodyOrder}.") { Overflow = Overflow.Fold }, 2);
    }

    private static void Heading(RendererDestination destination, TargetSnapshot target, string suffix, int indent = 0)
    {
        var name = target.Body is null ? "> " + target.Name : $"[{target.Name}]";
        var heading = new Paragraph(name, new Style(decoration: Decoration.Bold)) { Overflow = Overflow.Fold };
        heading.Append(suffix, new Style(Color.Grey));
        destination.Layout(heading, indent);
    }

    private static string Duration(TimeSpan elapsed) => elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";

    private void Control(string name, string description)
    {
        _output.Framework("  " + name, new Style(Color.LightSkyBlue1));
        _output.Layout(new Paragraph(description) { Overflow = Overflow.Fold }, 4);
    }

    private void Input(OptionSnapshot option)
    {
        var paragraph = new Paragraph { Overflow = Overflow.Fold };
        for (var index = 0; index < option.Spellings.Length; index++)
        {
            if (index > 0)
            {
                paragraph.Append(", ", new Style(Color.Grey));
            }
            paragraph.Append(option.Spellings[index], new Style(new Color(173, 214, 240)));
        }
        var typeHint = option.ValueType == typeof(bool) ? " [bool]"
            : option.ValueType == typeof(int) ? " <int>" : " <string>";
        paragraph.Append(typeHint, new Style(new Color(223, 196, 255)));
        _output.Layout(paragraph, 2);
        var policy = option.Required ? "Required." : option.HasDefault
            ? $"Default: {FormatDefault(option.DefaultValue)}." : "Optional; no default.";
        _output.Layout(new Paragraph($"{option.Description} {policy}") { Overflow = Overflow.Fold }, 4);
        if (option.ValueType == typeof(bool))
        {
            _output.Layout(new Paragraph("Bare flag means true; explicit true/false allowed.")
                { Overflow = Overflow.Fold }, 4);
        }
        if (option.Rules.Length > 0)
        {
            _output.Framework(option.Required || option.HasDefault ? "    Rules:" : "    Rules when supplied:",
                Style.Plain);
            foreach (var rule in option.Rules)
            {
                _output.Layout(new Paragraph("- " + rule.Description) { Overflow = Overflow.Fold }, 6);
            }
        }
    }

    private static string FormatDefault(object? value) => value switch
    {
        bool boolean => boolean ? "true" : "false",
        string text => "\"" + text + "\"",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
    };
}
