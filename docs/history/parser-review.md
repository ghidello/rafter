# Parser review: System.CommandLine

Reviewed and executed 2026-10-20. **Recommend adopting System.CommandLine 2.0.13 as the internal parser for the first
named-input slice**, using the adapter boundaries established in the [executable evaluation](parser-evaluation.md).
This supersedes the earlier source-only recommendation. The package is pinned in the evaluation harness; no Rafter
runtime or shipping package exists yet.

Rafter must own declarations, typed value handles, required/default rules, help, diagnostics, user validation,
invocation, cancellation, and exit codes. Do not expose the package's default behavior directly as Rafter's API.

## Baseline and provenance

- Package: [System.CommandLine 2.0.13](https://www.nuget.org/packages/System.CommandLine/2.0.13), MIT.
- Inspected source: tag `v2.0.13`, commit `158e67d46e869f4956e35cb54713c56120ae094f`.
- Package metadata records the dotnet/dotnet build-source commit `4099f83977f4d29604a9aeb971fb0a7bc1d5f699`.
  This is distinct from the command-line-api tag; executable findings use the restored NuGet package.
- On net10.0, restore selects the net8.0 assembly with no transitive package dependencies from this parser.
  The unused netstandard2.0 asset depends on System.Memory. The evaluation adapter is authored here; no upstream
  implementation source was copied or adapted.

## Source findings verified by execution

| Concern | Observed package behavior | Evaluated integration |
| --- | --- | --- |
| Parsing without invocation | Parsing APIs work independently of Invoke/InvokeAsync. | Never invoke the package pipeline. |
| Names, aliases, repeats | Ordinal matching; IdentifierTokenCount includes aliases. | Use normalized spellings and reject scalar repeats. Rafter retains alias display order. |
| Presence | Implicit Boolean false differs from an explicitly supplied false. | Resolve presence with OptionResult.Implicit; apply defaults/required rules outside the parser. |
| Built-in help | Clears malformed-input errors during Parse, even without Invoke. | Remove built-ins and add ordinary, action-free Rafter control options. |
| Missing scalar values | Default scalar arity can consume the following declared option as a value. | Use ZeroOrOne parser arity, then diagnose a missing scalar value in Rafter. |
| Boolean errors | Typed bool parsing leaves invalid literals unmatched at command level. | Parse the Boolean token as a string, then use bool.TryParse for safe option-specific diagnostics. |
| Empty attached values | `--name=` loses its empty value; `--flag=` becomes true. | Expand only recognized empty equals forms to an option token plus an empty token. |
| Colon and directives | Colon is accepted; unknown leading directives can disappear without errors. | Reject colon option separators and directive tokens explicitly. |
| Response files and bundles | Both are configurable. | Disable response-file replacement and POSIX bundling. |
| Executable name inference | A first input matching the executable name can be discarded. | Supply the synthetic command name before the original argument array. |
| Diagnostics | ParseError has text and symbol identity, but no error-kind enum. | Use known declaration identity and token presence; never print or match raw error messages. |

Sources: [parser configuration](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-configure-the-parser),
[tokenization](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/StringExtensions.cs),
[option result](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/OptionResult.cs),
[parse operation](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/ParseOperation.cs),
[option arity and greediness](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Option.cs),
[root defaults](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/RootCommand.cs),
[parse errors](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/ParseError.cs).

## Adoption boundary

The adapter does not reproduce general tokenization, shell quoting, or command traversal. Its normalization examines
only the first equals/colon separator of each original token against the known spelling table, preserving nonempty
attached values. Integer conversion, ordinal matching, alias lookup, option association, and repeat counting remain
in System.CommandLine. Boolean conversion and safe diagnostics are intentionally Rafter-owned.

The [evaluation record](parser-evaluation.md) describes the limits of error aggregation, the standalone `--` question,
and deferred platform/type coverage. Re-evaluate this boundary if positional arguments, collections, new input types,
or a package upgrade require broader token rewriting. Passing these tests does not implement all Rafter features.
