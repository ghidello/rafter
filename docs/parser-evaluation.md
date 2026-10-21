# Executable parser evaluation

Issue [#2](https://github.com/sotsera/rafter/issues/2), evaluated 2026-10-20.
**Recommendation: adopt System.CommandLine 2.0.13 for the initial named-input parser**, with Rafter-owned adaptation
and validation. The executable evidence supports this choice without requiring a second general tokenizer.
This is a dependency recommendation for the first implementation slice, not a completed runtime.

## Decisions confirmed during evaluation

- Accept an explicit empty string through `--name=` or a separate empty argument. Reject empty Boolean/numeric values.
- Reject colon option separators, response-file expansion, and directives initially. Preserve punctuation inside values.
- Declared options form boundaries: `--name --help` is missing a name value; `--name=--help` is the literal string.

These decisions are recorded in the [input design](example-design-plan.md#boolean-syntax-and-repeated-scalar-options).
The fixed probe accepts undeclared option-like strings as values after `--name`; a declared spelling requires attached
equals syntax to be used as a value. A standalone `--` is rejected in this no-positional-argument probe. Its public
meaning must be decided if positional or pass-through input is introduced; it is not silently inherited here.

## Fixtures and skills

- [CandidateAdapter.cs](../tests/testassets/evaluations/parser/CandidateAdapter.cs) is the fixed evaluation adapter shared by the tests and
  [probe.cs](../tests/testassets/evaluations/parser/probe.cs). It is not a generic declaration API, command runner, or shipping library.
- [ParserCandidate.cs](../tests/testassets/evaluations/parser/ParserCandidate.cs) exposes an unadapted schema for baseline comparisons.
- [ParserTests.cs](../tests/Rafter.Tests/Evaluation/ParserTests.cs) checks accepted behavior and runs the real file app.
  [ParserBaselineTests.cs](../tests/Rafter.Tests/Evaluation/ParserBaselineTests.cs) records incompatible package defaults.
- The consumer launcher copies the parser asset and root `Directory.Packages.props` into a temporary workspace,
  preserving repository-relative paths. `#:package System.CommandLine` therefore resolves the same centrally pinned
  version outside the checkout. No source reference to the old Rafter repository is used.
- **csharp-scripts** guided the real managed file app, narrow includes, SDK verification, and isolation from parent
  build configuration. The checked-in fixture is retained as regression evidence.
- **run-tests** confirmed native MTP syntax, the existing test project, and TRX reporting. No new test project was needed,
  so the scaffolding skill was not applied.

The file app prints a JSON observation to stdout and safe diagnostic categories/options to stderr, returning 0 or 2.
This is test instrumentation, not Rafter's designed output format. Its name validator rejects empty names only during
normal validation, allowing help to demonstrate that predicates are skipped. Tests use ProcessStartInfo.ArgumentList,
not a joined command string: they verify actual host argument delivery without asserting PowerShell or other shell
quoting conventions.

## Results and coverage

Local Windows x64, SDK 10.0.402, net10.0, managed execution:
**Passed: 83, Failed: 0, Skipped: 0**. This includes the four existing harness cases, six raw-parser baseline cases,
and 73 adapter cases. One adapter case builds a real file app and launches it eight times.

| Area | Executed evidence |
| --- | --- |
| Names/aliases | Primary, short, long, explicit `-name` and `--n`; ordinal mismatch; no bundling; repeats across aliases, identical values, bare flags, and mixed bare/attached forms. |
| Values and presence | Bare and explicit Booleans with mixed case, omission, empty/invalid/overflow values, separate/attached empty strings, zero/false as required values, defaults independent of presence, negative integers. |
| Boundaries | Missing values before help; empty attached values before help; literal option-like attached values; equals/colon in strings; spaces, quotes, Unicode, and backslashes through a real app. |
| Help and validation | Missing required inputs permit help; malformed explicit inputs still return 2; declaration conflicts precede help; user predicates skip help, parse failures, and omitted values. A normal explicit value invokes its predicate. |
| Privacy and aggregation | Integer/Boolean failures, a missing string value, and conflicting quiet/verbose controls aggregate in one parse. Safe categories/option names remain identical under en-US, it-IT, and fr-FR UI cultures and omit a distinctive rejected-value marker. |
| Extra syntax | Colon separators and directive tokens rejected; version/bundled options absent; an actual existing response file is not expanded and remains a literal when supplied as a string value. |
| Package defaults | Baseline checks reproduce empty-value loss, help swallowing, lost Boolean error association, error-clearing built-in help, silent directives, and executable-name inference. |

Use the [harness commands](evaluation-harness.md#run-locally) for restore, Release build, and tests. TRX results live at
`artifacts/test-results/Rafter.Evaluation.Tests_net10.0_x64.trx`; CI retains them with the build log. CI results for this
change are recorded in its PR after execution.

## Why the adapter remains bounded

The adapter has roughly 220 lines including schema, validation sequencing, and observation records. The normalization
method is roughly 40 lines. It performs only three adjustments: supplies the synthetic root name, expands a recognized
empty attached value, and identifies unsupported colon/end-of-options syntax. It never splits nonempty attached values,
interprets quotes, reads response files, or scans a second grammar to associate arbitrary tokens with options.

ZeroOrOne parser arity deliberately prevents a declared option from being consumed as another option's missing value.
Rafter checks required scalar value presence after parsing. Boolean input uses a string option so malformed literals
stay associated with their declaration, then bool.TryParse performs conversion. Rafter's public Boolean handle need
not reveal that internal representation. Ordinary action-free controls replace built-in help/version behavior.

ParseError.SymbolResult can identify an option for conversion errors. The adapter walks its parent chain when needed
and emits InvalidValue with the declaration's canonical name. It determines MissingValue and RepeatedOption from
token/count metadata. Unmatched input receives the broader UnexpectedInput category without printing the token.
No localization-string matching, raw exception text, or raw parser-message rendering is used.

Independent failures on different options can aggregate. Repeated occurrences of one scalar share a single result;
the adapter guarantees a repeat diagnostic, not every conversion/missing-value error on every occurrence. Unknown
input has a safe command-level category rather than a guessed option association. Do not promise complete recovery
from all malformed syntax or reuse failed parse values during execution.

## Dependency graph and alternatives

The restored test graph adds exactly **System.CommandLine 2.0.13**, selecting `lib/net8.0/System.CommandLine.dll` and
localized resources. This asset has **no transitive package dependencies**. The existing xUnit/MTP/assertion graph is
unchanged. The test reference has PrivateAssets=all, the test project is not packable, and there is no shipping project.
The fixture also consumes the candidate directly; this is evaluation packaging, not the future Rafter package graph.

The package's MIT license and source revisions are recorded in the [parser review](parser-review.md). The resolved
graph was checked with `dotnet list ... package --include-transitive` and `obj/project.assets.json`; explicit central
versions remain a baseline, not a committed transitive lock file.

Using package defaults directly is rejected by the executable counterexamples. A custom parser would offer control
over the grammar but require maintaining matching, association, conversion, and recovery ourselves. Replacing the
candidate with another CLI framework would require a fresh compatibility evaluation, including its invocation/help
assumptions. Neither alternative is justified by the narrow adaptation demonstrated here.

Deferred: the actual Rafter declaration/handle API, all declaration analyzers, custom converters, collections,
positional inputs, target selection, thrown user predicates, runtime cancellation, Linux/macOS, Native AOT, startup
benchmarking, and shipping-package consumption. The tests establish the initial string/int/Boolean parsing boundary.
