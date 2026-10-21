using System.CommandLine;
using System.CommandLine.Parsing;

namespace Rafter.Evaluation;

// A deliberately fixed evaluation schema, not the Rafter runtime or its declaration API.
internal sealed class CandidateAdapter
{
    private readonly RootCommand _command = new("Rafter parser evaluation");
    private readonly Option<string> _name = new("--name", "-n", "--label", "-name", "--n")
    {
        Arity = ArgumentArity.ZeroOrOne
    };
    private readonly Option<int> _count = new("--count", "-c") { Arity = ArgumentArity.ZeroOrOne };
    private readonly Option<string> _flag = new("--flag", "-f") { Arity = ArgumentArity.ZeroOrOne };
    private readonly Option<bool> _help = new("--help") { Arity = ArgumentArity.Zero };
    private readonly Option<bool> _quiet = new("--quiet") { Arity = ArgumentArity.Zero };
    private readonly Option<bool> _verbose = new("--verbose") { Arity = ArgumentArity.Zero };
    private readonly Dictionary<string, Option> _spellings = new(StringComparer.Ordinal);

    public bool RequireName { get; init; }
    public bool RequireCount { get; init; }
    public bool RequireFlag { get; init; }
    public int? CountDefault { get; init; }
    public bool? FlagDefault { get; init; }
    public Func<string, bool>? NameValidator { get; init; }

    public CandidateAdapter()
    {
        _command.Options.Clear();
        _command.Directives.Clear();

        Option[] options = [_name, _count, _flag, _help, _quiet, _verbose];
        foreach (var option in options)
        {
            _command.Options.Add(option);
            _spellings.Add(option.Name, option);
            foreach (var alias in option.Aliases)
            {
                _spellings.Add(alias, option);
            }
        }
    }

    public CandidateOutcome Evaluate(params string[] arguments)
    {
        var diagnostics = new List<CandidateDiagnostic>();
        if ((RequireCount && CountDefault.HasValue) || (RequireFlag && FlagDefault.HasValue))
        {
            diagnostics.Add(new("InvalidDeclaration", null));
            return new(2, false, null, null, null, false, false, false, 0, diagnostics);
        }

        var normalized = Normalize(arguments, diagnostics);
        var result = _command.Parse(normalized, new ParserConfiguration
        {
            EnablePosixBundling = false,
            ResponseFileTokenReplacer = null
        });

        foreach (var option in _command.Options)
        {
            var optionResult = result.GetResult(option);
            if (optionResult?.IdentifierTokenCount > 1)
            {
                diagnostics.Add(new("RepeatedOption", option.Name));
            }

            if (IsPresent(optionResult) && optionResult!.Tokens.Count == 0 && (option == _name || option == _count))
            {
                diagnostics.Add(new("MissingValue", option.Name));
            }
        }

        // Only structured symbol identity is used. Raw parser messages can contain rejected values.
        foreach (var error in result.Errors)
        {
            var symbol = error.SymbolResult;
            while (symbol is not null && symbol is not OptionResult)
            {
                symbol = symbol.Parent;
            }

            diagnostics.Add(symbol is OptionResult optionResult
                ? new("InvalidValue", optionResult.Option.Name)
                : new("UnexpectedInput", null));
        }

        if (result.Tokens.Any(token => token.Type == TokenType.Directive))
        {
            diagnostics.Add(new("UnsupportedDirective", null));
        }

        var nameResult = result.GetResult(_name);
        var countResult = result.GetResult(_count);
        var flagResult = result.GetResult(_flag);
        var namePresent = IsPresent(nameResult);
        var countPresent = IsPresent(countResult);
        var flagPresent = IsPresent(flagResult);
        var help = IsPresent(result.GetResult(_help));
        bool? flag = FlagDefault;

        if (flagPresent)
        {
            if (flagResult!.Tokens.Count == 0)
            {
                flag = true;
            }
            else if (bool.TryParse(flagResult.Tokens[0].Value, out var parsedFlag))
            {
                flag = parsedFlag;
            }
            else
            {
                diagnostics.Add(new("InvalidValue", _flag.Name));
            }
        }

        if (IsPresent(result.GetResult(_quiet)) && IsPresent(result.GetResult(_verbose)))
        {
            diagnostics.Add(new("ConflictingControls", "--quiet, --verbose"));
        }

        if (!help)
        {
            if (RequireName && !namePresent)
            {
                diagnostics.Add(new("MissingRequired", _name.Name));
            }

            if (RequireCount && !countPresent)
            {
                diagnostics.Add(new("MissingRequired", _count.Name));
            }

            if (RequireFlag && !flagPresent)
            {
                diagnostics.Add(new("MissingRequired", _flag.Name));
            }
        }

        // No values are read through throwing conversion APIs until all parsing diagnostics are checked.
        if (diagnostics.Count > 0)
        {
            return new(2, false, null, null, null, namePresent, countPresent, flagPresent, 0,
                diagnostics.Distinct().ToArray());
        }

        var name = namePresent ? result.GetValue(_name) : null;
        var count = countPresent ? result.GetValue(_count) : CountDefault;
        var validatorCalls = 0;
        if (!help && namePresent && NameValidator is not null)
        {
            validatorCalls++;
            if (!NameValidator(name!))
            {
                diagnostics.Add(new("ValidationFailed", _name.Name));
            }
        }

        return new(diagnostics.Count == 0 ? 0 : 2, help, name, count, flag,
            namePresent, countPresent, flagPresent, validatorCalls, diagnostics);
    }

    private List<string> Normalize(string[] arguments, List<CandidateDiagnostic> diagnostics)
    {
        // Supply the synthetic command name ourselves so a user's first token cannot be stripped as the executable.
        var normalized = new List<string>(arguments.Length + 1) { _command.Name };
        foreach (var argument in arguments)
        {
            if (argument == "--")
            {
                // This evaluation schema has no positional arguments. Do not silently enable another syntax form.
                diagnostics.Add(new("UnexpectedInput", null));
            }

            // Preserve array boundaries and nonempty attached values, including option-like string values.
            // ZeroOrOne arity makes declared options boundaries even when the preceding value is missing.
            var separator = argument.AsSpan().IndexOfAny('=', ':');
            if (separator > 0 && _spellings.TryGetValue(argument[..separator], out var option))
            {
                if (argument[separator] == ':')
                {
                    diagnostics.Add(new("UnsupportedSeparator", option.Name));
                }
                else if (separator == argument.Length - 1)
                {
                    normalized.Add(argument[..separator]);
                    normalized.Add("");
                    continue;
                }
            }

            normalized.Add(argument);
        }

        return normalized;
    }

    private static bool IsPresent(OptionResult? result) => result is { Implicit: false };
}

internal sealed record CandidateDiagnostic(string Category, string? Option);

internal sealed record CandidateOutcome(
    int ExitCode,
    bool Help,
    string? Name,
    int? Count,
    bool? Flag,
    bool NamePresent,
    bool CountPresent,
    bool FlagPresent,
    int ValidatorCalls,
    IReadOnlyList<CandidateDiagnostic> Diagnostics);
