using System.CommandLine;
using System.CommandLine.Parsing;

namespace Sotsera.Rafter.Internal;

internal sealed record InvocationArguments(bool Help, bool Quiet, bool Verbose, bool Plain, string[] Diagnostics)
{
    public static InvocationArguments Parse(string[]? args)
    {
        if (args is null || args.Any(argument => argument is null))
        {
            return new(false, false, false, false, ["Arguments must be a non-null array of non-null strings."]);
        }

        var command = new RootCommand();
        command.Options.Clear();
        command.Directives.Clear();
        var help = new Option<bool>("--help") { Arity = ArgumentArity.Zero };
        var quiet = new Option<bool>("--quiet") { Arity = ArgumentArity.Zero };
        var verbose = new Option<bool>("--verbose") { Arity = ArgumentArity.Zero };
        var plain = new Option<bool>("--plain") { Arity = ArgumentArity.Zero };
        Option[] options = [help, quiet, verbose, plain];
        foreach (var option in options)
        {
            command.Options.Add(option);
        }

        // Controls take no values. This also rejects separators, directives, response files, and positional input.
        // Never render raw parser errors or rejected tokens, which can contain private input.
        var diagnostics = new List<string>();
        if (args.Any(argument => !options.Any(option => option.Name == argument)))
        {
            diagnostics.Add("Unexpected input. Rafter controls do not accept values.");
        }

        var result = command.Parse([command.Name, .. args], new ParserConfiguration
        {
            EnablePosixBundling = false,
            ResponseFileTokenReplacer = null
        });
        foreach (var option in options)
        {
            if (result.GetResult(option)?.IdentifierTokenCount > 1)
            {
                diagnostics.Add($"{option.Name} may only be supplied once.");
            }
        }

        if (result.Errors.Count > 0)
        {
            diagnostics.Add("Unexpected input. Rafter controls do not accept values.");
        }
        var isQuiet = args.Contains("--quiet", StringComparer.Ordinal);
        var isVerbose = args.Contains("--verbose", StringComparer.Ordinal);
        if (isQuiet && isVerbose)
        {
            diagnostics.Add("--quiet and --verbose cannot be combined.");
        }

        return new(args.Contains("--help", StringComparer.Ordinal), isQuiet, isVerbose,
            args.Contains("--plain", StringComparer.Ordinal), diagnostics.Distinct().ToArray());
    }
}
