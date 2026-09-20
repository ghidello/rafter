using System.Diagnostics;
using Spectre.Console;

namespace Sotsera.Rafter;

internal static class OutputPresentation
{
    internal static string Render(OutputEvent outputEvent, OutputCapabilities capabilities)
    {
        string prefix = $"[{outputEvent.Scope}] ";
        string text = outputEvent.Kind switch
        {
            OutputKind.Line => outputEvent.Text,
            OutputKind.Success => $"success: {outputEvent.Text}",
            OutputKind.Warning => $"warning: {outputEvent.Text}",
            OutputKind.Error => $"error: {outputEvent.Text}",
            OutputKind.Property => outputEvent.Text,
            OutputKind.Console or OutputKind.ConsoleError => outputEvent.Text,
            _ => throw new UnreachableException(),
        };
        string recovery = outputEvent.Recovery is null
            ? string.Empty
            : $"{prefix}recovery: {outputEvent.Recovery}\n";
        if (!capabilities.IsRich)
        {
            return $"{prefix}{text}\n{recovery}";
        }

        StringWriter writer = new() { NewLine = "\n" };
        IAnsiConsole console = CreateConsole(writer, capabilities);
        string color = outputEvent.Kind switch
        {
            OutputKind.Success => "green",
            OutputKind.Warning => "yellow",
            OutputKind.Error => "red",
            OutputKind.Property => "cyan",
            _ => "default",
        };
        string safeText = Markup.Escape($"{prefix}{text}");
        if (string.Equals(color, "default", StringComparison.Ordinal))
        {
            console.WriteLine($"{prefix}{text}");
        }
        else
        {
            console.MarkupLine($"[{color}]{safeText}[/]");
        }

        if (outputEvent.Recovery is not null)
        {
            console.MarkupLine($"[yellow]{Markup.Escape($"{prefix}recovery: {outputEvent.Recovery}")}[/]");
        }

        return writer.ToString();
    }

    internal static IAnsiConsole CreateConsole(TextWriter writer, OutputCapabilities capabilities)
    {
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = capabilities.SupportsAnsi ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = capabilities.SupportsColor ? ColorSystemSupport.Standard : ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = capabilities.Width!.Value;
        console.Profile.Capabilities.Unicode = capabilities.SupportsUnicode;
        return console;
    }
}
