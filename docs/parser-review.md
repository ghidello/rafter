# Parser review: System.CommandLine

Reviewed 2026-10-20. Recommendation: use `System.CommandLine` as the preferred internal parser, subject to an executable
compatibility check before final adoption. Keep Rafter's declarations, value handles, help, validation stages,
diagnostics, cancellation, and invocation behavior in Rafter. This is a source/documentation review, not a runtime
verification result. No package has been installed or pinned, and no implementation code has been added.

## Reviewed baseline

The package listing identifies **2.0.13** as the current stable release, updated 2026-10-13, with a newer 3.0 prerelease.
It is MIT-licensed. Its `net8.0` asset lists no package dependencies; its `netstandard2.0` asset depends on System.Memory.
This is a reasonable evaluation baseline, not Rafter's selected SDK/target or final package pin.
[NuGet package metadata](https://www.nuget.org/packages/System.CommandLine/2.0.13)

Source findings below use the upstream `v2.0.13` tag. Source was inspected, not copied into Rafter. Compatibility with
the eventual SDK and file-based host, performance, and actual resolved package graph still require verification.

## Fit and integration boundaries

| Requirement | Source finding | Proposed Rafter integration |
| --- | --- | --- |
| Parsing without owning execution | Parsing and invocation are separate. | Call parsing APIs; do not call the package's Invoke/InvokeAsync pipeline. |
| Case-sensitive names and aliases | Known-token lookup and aliases use ordinal comparison. | Supply Rafter's normalized spellings after declaration validation. Keep an ordered alias list for help instead of relying on the parser's set enumeration. |
| Repeated scalar options | OptionResult exposes IdentifierTokenCount; ParseOperation increments it for each occurrence of the same option. | Reject counts greater than one, including aliases and bare Boolean repeats. Verify real combinations. |
| Absent optional values | OptionResult exposes Implicit, distinct from a value lookup. | Resolve presence, defaults, Required, and nullable/nonnullable handles in Rafter; do not infer presence from zero/false/null alone. |
| Boolean values | The parse loop uses bool.TryParse to recognize following Boolean tokens. | Check bare flags, mixed-case literals, space/equals forms, invalid explicit values, and option boundaries. |
| Help with invalid explicit input | Built-in HelpAction clears parse errors; RootCommand adds help/version/suggestion behavior. | Replace built-ins with ordinary Rafter-owned control options and render help ourselves. A Parse-only integration must also remove actions that affect parsing. |
| No speculative user callback execution | Parser validators run as part of parsing/validation. | Keep user predicates outside parser validators so help cannot execute them. Apply Rafter's presence/default and individual/cross-input stages in the agreed order. |
| No option bundling | POSIX bundling is enabled by default and configurable. | Disable it. |
| No unrequested response-file feature | Response-file expansion is enabled by default and can read files. | Propose disabling it for the initial scope rather than silently adding a new feature. |
| Private, controlled diagnostics | ParseError exposes Message and SymbolResult, without a public structured error-kind enum. | Construct Rafter diagnostics from known input/result structure; do not echo raw messages or classify errors by matching localized message strings. |

Sources: [parser/invocation configuration](https://learn.microsoft.com/en-us/dotnet/standard/commandline/how-to-configure-the-parser),
[alias set](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/AliasSet.cs),
[tokenization](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/StringExtensions.cs),
[option result](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/OptionResult.cs),
[parse operation](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/ParseOperation.cs),
[root defaults](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/RootCommand.cs),
[help action](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Help/HelpAction.cs),
[parser defaults](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/ParserConfiguration.cs),
[parse errors](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/ParseError.cs).

## Two areas requiring executable checks

**Empty attached values and extra syntax.** The tagged tokenizer splits option tokens on colon or equals. For an empty
suffix, it changes the suffix to null and adds no value token. This suggests that `--name=` and an explicitly supplied
empty argument may differ unless Rafter normalizes the input. It also means we must deliberately decide whether to
expose colon syntax. Leading directive-shaped tokens have a separate parsing path, so removing built-in directives
alone is not proof that every unsupported directive will be rejected.
[Tokenizer source, including TrySplitIntoSubtokens](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/StringExtensions.cs)

Rafter already accepts an explicitly supplied empty string for presence validation. Recommend making `--name=` another
way to supply that empty string, while an empty explicit Boolean remains invalid; this detailed spelling policy is a
proposal to confirm. Prove any normalization preserves original token boundaries and does not reinterpret arbitrary
string values. Do not hide an increasingly complete second parser behind an adapter.

**Safe diagnostics and help ordering.** Prove that explicit malformed values still produce exit `2` with `--help`,
without invoking user predicates or printing rejected values. Missing required inputs alone must allow help. The
package's human-readable error text is not a stable structured diagnostic contract, so this mapping is an adoption
gate rather than a minor cosmetic task. [ParseError source](https://github.com/dotnet/command-line-api/blob/v2.0.13/src/System.CommandLine/Parsing/ParseError.cs)

## Focused compatibility cases

The detailed acceptance checklist now lives in
[#2: Verify System.CommandLine compatibility before adoption](https://github.com/sotsera/rafter/issues/2),
depending on the harness in #1. Use direct argument arrays and real file-app invocations to separate parser behavior
from shell quoting. Apply the declaration/help rules in the design plan, including conflicting quiet/verbose controls;
record validator invocation counts. No executable compatibility checks have run yet.

Run the issue's cases against the exact candidate package.
Adopt it if the adapter remains small and behavior is faithful. If empty-value handling or structured diagnostics
requires extensive re-tokenization, reconsider the parser choice before building Rafter around it.

The review supports proceeding with this candidate; it does not yet justify claiming that every Rafter parsing rule
is satisfied or that a particular package version is production-ready for this project.
