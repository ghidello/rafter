using System.Globalization;
using Spectre.Console;

namespace Sotsera.Rafter.Internal;

internal sealed class InvocationOutput
{
    private readonly RendererDestination _output;
    private readonly RendererDestination _error;
    private readonly InvocationArguments _arguments;

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

    public void Help(string description, OptionSnapshot[] options, CombinedRule[] rules)
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
        _output.Framework("  Run the supplied command body.", Style.Plain);
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
        Control("--help", "Show help without executing the command body.");
        Control("--quiet", "Hide routine lifecycle output; keep results and failures.");
        Control("--verbose", "Show additional execution context.");
        Control("--plain", "Disable styling and presentation prefixes.");
    }

    public void Start(string description)
    {
        if (!_arguments.Quiet)
        {
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
        _error.Framework($"  {scope} failed", new Style(Color.Red));
        _error.Literal(exception.ToString() + Environment.NewLine, "  ");
    }

    public void Summary(int code, TimeSpan elapsed)
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
        _error.Framework("", Style.Plain);
        var milliseconds = elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture);
        _error.Framework($"{label} | {milliseconds} ms", new Style(color));
    }

    public Task CompleteOutputAsync() => _output.CompleteAsync();

    public Task CompleteErrorAsync() => _error.CompleteAsync();

    private void Section(string text) => _output.Framework(text, new Style(Color.Teal, decoration: Decoration.Bold));

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
