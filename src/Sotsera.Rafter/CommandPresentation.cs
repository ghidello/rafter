using System.Collections.Immutable;
using Spectre.Console;
using static Sotsera.Rafter.BindingEngine;
using static Sotsera.Rafter.CommandModel;

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
        string message = $"Command failed during {failure.Stage} for '--{failure.OptionName}' ({failure.ExceptionType.Name}).";
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

    internal static async Task<bool> WriteAsync(
        Report report,
        TextWriter writer,
        bool rich,
        TextRedactor redactor)
    {
        if (!redactor.IsUsable || !TryRedact(report, redactor, out Report? safeReport))
        {
            return false;
        }

        string rendered = rich ? RenderRich(safeReport!) : RenderPlain(safeReport!);
        if (redactor.ContainsPattern(rendered))
        {
            return false;
        }

        await writer.WriteAsync(rendered).ConfigureAwait(false);
        return true;
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
        lines.Add(new ReportLine("  Targets describe the contained execution graph; they are not command-line selections.", LineRole.Muted));
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
                IEnumerable<string> names = target.Dependencies.Select(id => model.Targets.Single(target => target.Id == id).Name);
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

    private static string RenderPlain(Report report)
    {
        StringWriter writer = new() { NewLine = "\n" };
        foreach (ReportLine line in report.Lines)
        {
            writer.WriteLine(line.Text);
        }

        return writer.ToString();
    }

    private static string RenderRich(Report report)
    {
        StringWriter writer = new() { NewLine = "\n" };
        AnsiConsoleSettings settings = new()
        {
            Ansi = AnsiSupport.Yes,
            ColorSystem = ColorSystemSupport.TrueColor,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(writer),
        };
        IAnsiConsole console = AnsiConsole.Create(settings);
        foreach (ReportLine line in report.Lines)
        {
            WriteRichLine(console, line);
        }

        return writer.ToString();
    }

    private static void WriteRichLine(IAnsiConsole console, ReportLine line)
    {
        string text = Markup.Escape(line.Text);
        switch (line.Role)
        {
            case LineRole.Heading:
                console.MarkupLine($"[bold blue]{text}[/]");
                break;
            case LineRole.ErrorHeading:
                console.MarkupLine($"[bold red]{text}[/]");
                break;
            case LineRole.Error:
                console.MarkupLine($"[red]{text}[/]");
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
        ErrorHeading,
        Error,
        Item,
        Muted,
    }
}
