using System.Buffers;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Sotsera.Rafter;

internal static class OutputPresentation
{
    internal static string Render(OutputEvent outputEvent, OutputCapabilities capabilities)
    {
        string prefix = $"[{EscapeText(outputEvent.Scope)}] ";
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
        IEnumerable<string> lines = capabilities.IsRich && outputEvent.Property is not null
            ? RenderProperty(outputEvent.Property, prefix.Length, capabilities.Width!.Value)
            : NormalizeLines(text);
        StringWriter writer = new() { NewLine = capabilities.NewLine };
        if (!capabilities.IsRich)
        {
            foreach (string line in lines)
            {
                writer.WriteLine(prefix + EscapeText(line));
            }
            if (outputEvent.Recovery is not null)
            {
                writer.WriteLine(prefix + "recovery: " + EscapeText(outputEvent.Recovery));
            }
            return writer.ToString();
        }

        IAnsiConsole console = CreateConsole(writer, capabilities);
        string color = outputEvent.Kind switch
        {
            OutputKind.Success => "green",
            OutputKind.Warning => "olive",
            OutputKind.Error => "maroon",
            OutputKind.Property => "teal",
            _ => "default",
        };
        foreach (string line in lines)
        {
            string attributed = prefix + EscapeText(line);
            if (string.Equals(color, "default", StringComparison.Ordinal))
            {
                console.WriteLine(attributed);
            }
            else
            {
                console.MarkupLine($"[{color}]{Markup.Escape(attributed)}[/]");
            }
        }

        if (outputEvent.Recovery is not null)
        {
            console.MarkupLine($"[olive]{Markup.Escape(prefix + "recovery: " + EscapeText(outputEvent.Recovery))}[/]");
        }

        return writer.ToString().ReplaceLineEndings(capabilities.NewLine);
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

    private static string[] NormalizeLines(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static IEnumerable<string> RenderProperty(OutputProperty property, int prefixLength, int width)
    {
        using JsonDocument document = JsonDocument.Parse(property.CanonicalValue);
        JsonElement value = document.RootElement;
        string? decoded = value.ValueKind == JsonValueKind.String ? OutputProperty.DecodeString(value) : null;
        if (decoded?.IndexOfAny(['\r', '\n']) >= 0)
        {
            return new[] { property.Name + ":" }.Concat(NormalizeLines(decoded).Select(line => "  " + line));
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            string[] items = value.EnumerateArray().Select(item => item.GetRawText()).ToArray();
            string inline = property.Name + ": [" + string.Join(", ", items) + "]";
            return prefixLength + inline.Length <= width || items.Length == 0
                ? [inline]
                : new[] { property.Name + ":" }.Concat(items.Select(item => "  - " + item));
        }
        return [property.Name + ": " + (value.ValueKind == JsonValueKind.Null ? "<null>" : property.CanonicalValue)];
    }

    private static string EscapeText(string text)
    {
        StringBuilder safe = new(text.Length);
        ReadOnlySpan<char> remaining = text.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed) != OperationStatus.Done)
            {
                safe.Append("\\u");
                safe.Append(((int)remaining[0]).ToString("x4", CultureInfo.InvariantCulture));
                remaining = remaining[1..];
                continue;
            }
            remaining = remaining[consumed..];
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
            {
                safe.Append(rune.Value <= char.MaxValue ? "\\u" : "\\U");
                safe.Append(rune.Value.ToString(rune.Value <= char.MaxValue ? "x4" : "x8", CultureInfo.InvariantCulture));
            }
            else
            {
                safe.Append(rune.ToString());
            }
        }
        return safe.ToString();
    }
}
