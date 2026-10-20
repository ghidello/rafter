using System.CommandLine;
using System.CommandLine.Parsing;
using CliOption = System.CommandLine.Option;

namespace Sotsera.Rafter.Internal;

internal sealed record InvocationArguments(bool Help, bool Quiet, bool Verbose, bool Plain, string[] Diagnostics)
{
    public InputValues Values { get; init; } = new(new Dictionary<OptionDeclaration, object?>());

    public static InvocationArguments Parse(string[]? args, OptionSnapshot[] declarations)
    {
        if (args is null || args.Any(argument => argument is null))
        {
            return new(false, false, false, false, ["Arguments must be a non-null array of non-null strings."]);
        }

        var command = new RootCommand();
        command.Options.Clear();
        command.Directives.Clear();
        var help = new System.CommandLine.Option<bool>("--help") { Arity = ArgumentArity.Zero };
        var quiet = new System.CommandLine.Option<bool>("--quiet") { Arity = ArgumentArity.Zero };
        var verbose = new System.CommandLine.Option<bool>("--verbose") { Arity = ArgumentArity.Zero };
        var plain = new System.CommandLine.Option<bool>("--plain") { Arity = ArgumentArity.Zero };
        CliOption[] controls = [help, quiet, verbose, plain];
        var inputs = new Dictionary<CliOption, OptionSnapshot>();
        foreach (var declaration in declarations)
        {
            var name = declaration.Spellings[0];
            var aliases = declaration.Spellings[1..];
            CliOption option = declaration.ValueType == typeof(int)
                ? new System.CommandLine.Option<int>(name, aliases) { Arity = ArgumentArity.ZeroOrOne }
                : new System.CommandLine.Option<string>(name, aliases) { Arity = ArgumentArity.ZeroOrOne };
            inputs.Add(option, declaration);
        }
        var spellings = new Dictionary<string, CliOption>(StringComparer.Ordinal);
        foreach (var option in controls.Concat(inputs.Keys))
        {
            command.Options.Add(option);
            spellings.Add(option.Name, option);
            foreach (var alias in option.Aliases)
            {
                spellings.Add(alias, option);
            }
        }

        var diagnostics = new List<string>();
        var normalized = Normalize(args, command.Name, spellings, controls, diagnostics);
        var result = command.Parse(normalized, new ParserConfiguration
        {
            EnablePosixBundling = false,
            ResponseFileTokenReplacer = null
        });
        var isHelp = IsPresent(result.GetResult(help));
        var isQuiet = IsPresent(result.GetResult(quiet));
        var isVerbose = IsPresent(result.GetResult(verbose));
        var isPlain = IsPresent(result.GetResult(plain));
        foreach (var option in command.Options)
        {
            var parsed = result.GetResult(option);
            if (parsed?.IdentifierTokenCount > 1)
            {
                diagnostics.Add($"{option.Name} may only be supplied once.");
            }
            if (inputs.TryGetValue(option, out var declaration))
            {
                if (IsPresent(parsed) && parsed!.Tokens.Count == 0 && declaration.ValueType != typeof(bool))
                {
                    diagnostics.Add($"{option.Name} requires a value.");
                }
                if (!isHelp && declaration.Required && !IsPresent(parsed))
                {
                    diagnostics.Add($"{option.Name} is required.");
                }
            }
        }

        // Raw parser messages and rejected values can contain private input. Use only symbol identity.
        foreach (var error in result.Errors)
        {
            var symbol = error.SymbolResult;
            while (symbol is not null && symbol is not OptionResult)
            {
                symbol = symbol.Parent;
            }
            diagnostics.Add(symbol is OptionResult optionResult
                ? $"{optionResult.Option.Name} has an invalid value."
                : "Unexpected input. Only declared options and Rafter controls are accepted.");
        }
        if (result.Tokens.Any(token => token.Type == TokenType.Directive))
        {
            diagnostics.Add("Directives are not supported.");
        }
        if (isQuiet && isVerbose)
        {
            diagnostics.Add("--quiet and --verbose cannot be combined.");
        }

        var values = new Dictionary<OptionDeclaration, object?>();
        foreach (var (option, declaration) in inputs)
        {
            var parsed = result.GetResult(option);
            var value = declaration.HasDefault ? declaration.DefaultValue : null;
            if (IsPresent(parsed) && declaration.ValueType == typeof(bool))
            {
                if (parsed!.Tokens.Count == 0)
                {
                    value = true;
                }
                else if (bool.TryParse(parsed.Tokens[0].Value, out var boolean))
                {
                    value = boolean;
                }
                else
                {
                    diagnostics.Add($"{option.Name} requires true or false.");
                }
            }
            else if (IsPresent(parsed) && diagnostics.Count == 0)
            {
                value = option is System.CommandLine.Option<int> integer
                    ? result.GetValue(integer)
                    : result.GetValue((System.CommandLine.Option<string>)option);
            }
            values.Add(declaration.Declaration, value);
        }
        return new(isHelp, isQuiet, isVerbose, isPlain, diagnostics.Distinct().ToArray())
        {
            Values = new(values)
        };
    }

    private static List<string> Normalize(string[] args, string commandName,
        Dictionary<string, CliOption> spellings, CliOption[] controls, List<string> diagnostics)
    {
        // Prevent the parser from stripping a user token that happens to match the executable name.
        var normalized = new List<string>(args.Length + 1) { commandName };
        foreach (var argument in args)
        {
            if (argument == "--")
            {
                diagnostics.Add("The end-of-options separator is not supported.");
            }
            var separator = argument.AsSpan().IndexOfAny('=', ':');
            if (separator > 0 && spellings.TryGetValue(argument[..separator], out var option))
            {
                if (argument[separator] == ':')
                {
                    diagnostics.Add($"{option.Name} does not support colon separators.");
                }
                if (controls.Contains(option))
                {
                    diagnostics.Add($"{option.Name} does not accept a value.");
                }
                else if (argument[separator] == '=' && separator == argument.Length - 1)
                {
                    // Preserve explicit empty values, which the parser otherwise treats as absent.
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
