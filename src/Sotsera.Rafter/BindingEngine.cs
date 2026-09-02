using System.Collections.Immutable;
using static Sotsera.Rafter.CommandModel;
using static Sotsera.Rafter.OptionBindingStrategy;

namespace Sotsera.Rafter;

internal static class BindingEngine
{
    internal enum BindingStatus
    {
        Success,
        InputFailure,
        AuthorFailure,
        InfrastructureFailure,
    }

    internal enum InputDiagnosticKind
    {
        DuplicateCommonOption,
        UnsupportedEndOfOptions,
        MalformedCommonOption,
        UnknownOption,
        UnexpectedPositional,
        UnsupportedShortSyntax,
        DuplicateOption,
        MissingOptionValue,
        MissingRequiredOption,
        InvalidValue,
        ValidationRejected,
    }

    internal sealed record InputDiagnostic(InputDiagnosticKind Kind, string Message, int? ArgumentPosition);

    internal sealed record BindingFailure(string OptionName, string Stage, Type ExceptionType);

    internal sealed record BindingResult(
        BindingStatus Status,
        bool Plain,
        InvocationSnapshot? Snapshot,
        ImmutableArray<InputDiagnostic> Diagnostics,
        TextRedactor Redactor,
        Exception? Exception,
        BindingFailure? Failure);

    internal sealed class InvocationSnapshot(Guid commandId, ImmutableDictionary<Guid, object?> values)
    {
        internal Guid CommandId { get; } = commandId;

        internal T? GetOptional<T>(Guid optionId) => (T?)Get(optionId);

        internal T GetRequired<T>(Guid optionId) => (T)Get(optionId)!;

        internal IReadOnlyList<T> GetRepeated<T>(Guid optionId) => (IReadOnlyList<T>)Get(optionId)!;

        internal object? GetUntyped(Guid optionId) => Get(optionId);

        private object? Get(Guid optionId)
            => values.TryGetValue(optionId, out object? value)
                ? value
                : throw new InvalidOperationException("The option is missing from the invocation snapshot.");
    }

    internal static BindingResult Bind(CommandDefinition model, ImmutableArray<string> arguments, InvocationServices services)
    {
        ParsedInput parsed = CommandLineParser.Parse(model, arguments);
        TextRedactor.Registry registry = new();
        RegisterCommandLineValues(model, parsed, registry);

        List<InputDiagnostic> bindingDiagnostics = [];
        if (!TrySelectSources(model, parsed, services, registry, bindingDiagnostics, out List<SelectedOption> selections, out BindingResult? failure))
        {
            return failure!;
        }

        ImmutableDictionary<Guid, object?>.Builder values = ImmutableDictionary.CreateBuilder<Guid, object?>();
        foreach (SelectedOption selection in selections)
        {
            OptionDefinition option = selection.Option;
            try
            {
                if (!TryBindOption(selection, registry, bindingDiagnostics, out object? value))
                {
                    continue;
                }

                values.Add(option.Id, value);
            }
            catch (BindingAuthorException exception)
            {
                return new BindingResult(
                    BindingStatus.AuthorFailure,
                    parsed.Plain,
                    null,
                    [.. parsed.Diagnostics, .. bindingDiagnostics],
                    registry.Freeze(),
                    exception.OriginalException,
                    new BindingFailure(option.Name, exception.Stage, exception.OriginalException.GetType()));
            }
        }

        TextRedactor redactor = registry.Freeze();
        ImmutableArray<InputDiagnostic> diagnostics = [.. parsed.Diagnostics, .. bindingDiagnostics];
        if (!diagnostics.IsEmpty)
        {
            return new BindingResult(BindingStatus.InputFailure, parsed.Plain, null, diagnostics, redactor, null, null);
        }

        InvocationSnapshot snapshot = new(model.Id, values.ToImmutable());
        return new BindingResult(BindingStatus.Success, parsed.Plain, snapshot, [], redactor, null, null);
    }

    private static bool TrySelectSources(
        CommandDefinition model,
        ParsedInput parsed,
        InvocationServices services,
        TextRedactor.Registry registry,
        List<InputDiagnostic> diagnostics,
        out List<SelectedOption> selections,
        out BindingResult? failure)
    {
        selections = [];
        foreach (OptionDefinition option in model.Options)
        {
            try
            {
                selections.Add(SelectSource(option, parsed, services, registry));
            }
            catch (Exception exception)
            {
                failure = new BindingResult(
                    BindingStatus.InfrastructureFailure,
                    parsed.Plain,
                    null,
                    [.. parsed.Diagnostics, .. diagnostics],
                    registry.Freeze(),
                    exception,
                    new BindingFailure(option.Name, "environment lookup", exception.GetType()));
                return false;
            }
        }

        failure = null;
        return true;
    }

    private static void RegisterCommandLineValues(
        CommandDefinition model,
        ParsedInput parsed,
        TextRedactor.Registry registry)
    {
        foreach (OptionDefinition option in model.Options.Where(static option => option.IsSensitive))
        {
            if (!parsed.Occurrences.TryGetValue(option.Id, out ImmutableArray<RawOccurrence> occurrences))
            {
                continue;
            }

            foreach (RawOccurrence occurrence in occurrences)
            {
                registry.Add(occurrence.Value);
            }
        }
    }

    private static SelectedOption SelectSource(
        OptionDefinition option,
        ParsedInput parsed,
        InvocationServices services,
        TextRedactor.Registry registry)
    {
        ImmutableArray<RawOccurrence> occurrences = parsed.Occurrences.TryGetValue(
            option.Id,
            out ImmutableArray<RawOccurrence> found)
            ? found
            : [];

        if (option.IsRepeated)
        {
            return new SelectedOption(option, occurrences.Where(static occurrence => occurrence.Value is not null)
                .Select(static occurrence => new SelectedValue(occurrence.Value!, occurrence.ArgumentPosition))
                .ToImmutableArray(), null, IsAbsent: false);
        }

        RawOccurrence? commandLine = occurrences.FirstOrDefault(static occurrence => occurrence.Value is not null);
        if (commandLine is not null)
        {
            return new SelectedOption(
                option,
                [new SelectedValue(commandLine.Value!, commandLine.ArgumentPosition)],
                null,
                IsAbsent: false);
        }

        if (option.EnvironmentName is not null)
        {
            string? environmentValue = services.ReadEnvironment(option.EnvironmentName);
            if (environmentValue is not null)
            {
                if (option.IsSensitive)
                {
                    registry.Add(environmentValue);
                }

                return new SelectedOption(option, [new SelectedValue(environmentValue, null)], null, IsAbsent: false);
            }
        }

        if (option.HasDefault)
        {
            if (option.IsSensitive)
            {
                registry.Add(option.DefaultStableRepresentation);
            }

            return new SelectedOption(option, [], option.DefaultValue, IsAbsent: false);
        }

        return new SelectedOption(option, [], null, IsAbsent: true);
    }

    private static bool TryBindOption(
        SelectedOption selection,
        TextRedactor.Registry registry,
        List<InputDiagnostic> diagnostics,
        out object? value)
    {
        OptionDefinition option = selection.Option;
        if (option.IsRepeated)
        {
            List<object?> converted = [];
            foreach (SelectedValue selected in selection.RawValues)
            {
                if (!TryConvertSelected(option, selected, registry, diagnostics, out object? item))
                {
                    value = null;
                    return false;
                }

                converted.Add(item);
            }

            value = option.BindingStrategy.CreateImmutableList(converted);
            return Validate(option, value, null, diagnostics);
        }

        if (selection.IsAbsent)
        {
            value = null;
            if (option.IsRequired)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.MissingRequiredOption,
                    $"Required option '--{option.Name}' is missing.",
                    null));
                return false;
            }

            return true;
        }

        if (selection.RawValues.Length > 0)
        {
            SelectedValue selected = selection.RawValues[0];
            if (!TryConvertSelected(option, selected, registry, diagnostics, out value))
            {
                return false;
            }
        }
        else
        {
            value = selection.DefaultValue;
        }

        int? argumentPosition = selection.RawValues.IsEmpty ? null : selection.RawValues[0].ArgumentPosition;
        return Validate(option, value, argumentPosition, diagnostics);
    }

    private static bool TryConvertSelected(
        OptionDefinition option,
        SelectedValue selected,
        TextRedactor.Registry registry,
        List<InputDiagnostic> diagnostics,
        out object? value)
        => TryConvert(option, selected.Value, selected.ArgumentPosition, registry, diagnostics, out value);

    private static bool TryConvert(
        OptionDefinition option,
        string raw,
        int? argumentPosition,
        TextRedactor.Registry registry,
        List<InputDiagnostic> diagnostics,
        out object? value)
    {
        ConversionAttempt attempt;
        try
        {
            attempt = option.BindingStrategy.Convert(raw);
        }
        catch (Exception exception)
        {
            throw new BindingAuthorException("conversion", exception);
        }

        if (attempt.IsNullSuccess)
        {
            throw new BindingAuthorException(
                "conversion",
                new InvalidOperationException("A converter reported success with a null reference result."));
        }

        if (!attempt.IsSuccess)
        {
            string display = option.IsSensitive ? "<redacted>" : ValueFormatter.FormatText(raw);
            string permitted = option.BindingStrategy.PermittedValues.IsEmpty
                ? string.Empty
                : $" Expected one of: {string.Join(", ", option.BindingStrategy.PermittedValues)}.";
            diagnostics.Add(new InputDiagnostic(
                InputDiagnosticKind.InvalidValue,
                $"Invalid value {display} for '--{option.Name}'.{permitted}",
                argumentPosition));
            value = null;
            return false;
        }

        value = attempt.Value;
        if (option.IsSensitive
            && value is not null
            && ValueFormatter.TryFormatStable(value, option.ValueType, out string stable, out _))
        {
            registry.Add(stable);
        }

        return true;
    }

    private static bool Validate(
        OptionDefinition option,
        object? value,
        int? argumentPosition,
        List<InputDiagnostic> diagnostics)
    {
        foreach (ValidatorDefinition validator in option.Validators)
        {
            bool isValid;
            try
            {
                isValid = validator.Evaluate(value);
            }
            catch (Exception exception)
            {
                throw new BindingAuthorException("validation", exception);
            }

            if (!isValid)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.ValidationRejected,
                    $"--{option.Name}: {ValueFormatter.FormatInlineText(validator.Message)}",
                    argumentPosition));
                return false;
            }
        }

        return true;
    }

    private sealed record SelectedOption(
        OptionDefinition Option,
        ImmutableArray<SelectedValue> RawValues,
        object? DefaultValue,
        bool IsAbsent);

    private sealed record SelectedValue(string Value, int? ArgumentPosition);

    private sealed class BindingAuthorException(string stage, Exception innerException) : Exception(null, innerException)
    {
        internal string Stage { get; } = stage;

        internal Exception OriginalException { get; } = innerException;
    }

    internal sealed record ParsedInput(
        bool Plain,
        ImmutableArray<string> Arguments,
        ImmutableDictionary<Guid, ImmutableArray<RawOccurrence>> Occurrences,
        ImmutableArray<InputDiagnostic> Diagnostics);

    internal sealed record RawOccurrence(string? Value, int ArgumentPosition);

    internal static class CommandLineParser
    {
        internal static ParsedInput Parse(CommandDefinition model, ImmutableArray<string> arguments)
        {
            Dictionary<string, OptionDefinition> names = model.Options.ToDictionary(static option => option.Name, StringComparer.Ordinal);
            Dictionary<char, OptionDefinition> aliases = model.Options
                .Where(static option => option.Alias is not null)
                .ToDictionary(static option => option.Alias!.Value);
            Dictionary<Guid, List<RawOccurrence>> occurrences = [];
            List<InputDiagnostic> diagnostics = [];
            bool plain = false;
            int plainCount = 0;

            for (int index = 0; index < arguments.Length; index++)
            {
                string token = arguments[index];
                if (TryParsePlain(token, index, diagnostics, ref plain, ref plainCount))
                {
                    continue;
                }

                if (string.Equals(token, "--", StringComparison.Ordinal))
                {
                    diagnostics.Add(new InputDiagnostic(
                        InputDiagnosticKind.UnsupportedEndOfOptions,
                        "The '--' end-of-options marker is not supported.",
                        index));
                    continue;
                }

                if (token.StartsWith("--", StringComparison.Ordinal))
                {
                    ParseLong(arguments, names, occurrences, diagnostics, ref index);
                    continue;
                }

                if (token.StartsWith('-'))
                {
                    ParseShort(arguments, aliases, occurrences, diagnostics, ref index);
                    continue;
                }

                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.UnexpectedPositional,
                    $"Unexpected positional argument at position {index}.",
                    index));
            }

            return new ParsedInput(
                plain,
                arguments,
                occurrences.ToImmutableDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.ToImmutableArray()),
                [.. diagnostics]);
        }

        private static bool TryParsePlain(
            string token,
            int index,
            List<InputDiagnostic> diagnostics,
            ref bool plain,
            ref int plainCount)
        {
            if (!string.Equals(token, "--plain", StringComparison.Ordinal))
            {
                return false;
            }

            plain = true;
            plainCount++;
            if (plainCount > 1)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.DuplicateCommonOption,
                    "Common option '--plain' may occur only once.",
                    index));
            }

            return true;
        }

        private static void ParseLong(
            ImmutableArray<string> arguments,
            Dictionary<string, OptionDefinition> names,
            Dictionary<Guid, List<RawOccurrence>> occurrences,
            List<InputDiagnostic> diagnostics,
            ref int index)
        {
            int optionPosition = index;
            string token = arguments[index];
            int equalsIndex = token.IndexOf('=', 2);
            bool hasAttachedValue = equalsIndex >= 0;
            string name = hasAttachedValue ? token[2..equalsIndex] : token[2..];
            if (name is "help" or "plain")
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.MalformedCommonOption,
                    $"Malformed common option '--{name}'.",
                    index));
                return;
            }

            if (!names.TryGetValue(name, out OptionDefinition? option))
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.UnknownOption,
                    $"Unknown option \"{ValueFormatter.FormatInlineText($"--{name}")}\".",
                    index));
                return;
            }

            string? value = hasAttachedValue ? token[(equalsIndex + 1)..] : TakeSeparatedValue(option, arguments, ref index);
            AddOccurrence(option, value, optionPosition, occurrences, diagnostics);
        }

        private static void ParseShort(
            ImmutableArray<string> arguments,
            Dictionary<char, OptionDefinition> aliases,
            Dictionary<Guid, List<RawOccurrence>> occurrences,
            List<InputDiagnostic> diagnostics,
            ref int index)
        {
            int optionPosition = index;
            string token = arguments[index];
            if (token.StartsWith("-h", StringComparison.Ordinal))
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.MalformedCommonOption,
                    "Malformed common option '-h'.",
                    index));
                return;
            }

            if (token.Length != 2)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.UnsupportedShortSyntax,
                    "Unsupported short-option syntax.",
                    index));
                return;
            }

            char alias = token[1];
            if (!aliases.TryGetValue(alias, out OptionDefinition? option))
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.UnknownOption,
                    $"Unknown option \"{ValueFormatter.FormatInlineText(token)}\".",
                    index));
                return;
            }

            string? value = TakeSeparatedValue(option, arguments, ref index);
            AddOccurrence(option, value, optionPosition, occurrences, diagnostics);
        }

        private static string? TakeSeparatedValue(OptionDefinition option, ImmutableArray<string> arguments, ref int index)
        {
            bool hasNext = index + 1 < arguments.Length;
            bool nextIsOptionShaped = hasNext && arguments[index + 1].StartsWith('-');
            Type effectiveType = Nullable.GetUnderlyingType(option.ValueType) ?? option.ValueType;
            if (effectiveType == typeof(bool) && (!hasNext || nextIsOptionShaped))
            {
                return "true";
            }

            if (hasNext && !nextIsOptionShaped)
            {
                index++;
                return arguments[index];
            }

            return null;
        }

        private static void AddOccurrence(
            OptionDefinition option,
            string? value,
            int argumentPosition,
            Dictionary<Guid, List<RawOccurrence>> occurrences,
            List<InputDiagnostic> diagnostics)
        {
            if (!occurrences.TryGetValue(option.Id, out List<RawOccurrence>? values))
            {
                values = [];
                occurrences.Add(option.Id, values);
            }

            if (!option.IsRepeated && values.Count > 0)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.DuplicateOption,
                    $"Option '--{option.Name}' may occur only once.",
                    argumentPosition));
            }

            values.Add(new RawOccurrence(value, argumentPosition));
            if (value is null)
            {
                diagnostics.Add(new InputDiagnostic(
                    InputDiagnosticKind.MissingOptionValue,
                    $"Option '--{option.Name}' requires a value.",
                    argumentPosition));
            }
        }
    }
}
