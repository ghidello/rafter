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

    public void Help(string description)
    {
        _output.Framework(description, new Style(decoration: Decoration.Bold));
        _output.Framework("", Style.Plain);
        Section("Command options");
        _output.Framework("  None.", Style.Plain);
        _output.Framework("", Style.Plain);
        Section("Execution");
        _output.Framework("  Run the supplied command body.", Style.Plain);
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
}
