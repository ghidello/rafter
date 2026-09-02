# Phase 3 completion evidence

## Status

Phase 3 is implemented. Rafter freezes the Phase 2 command model, recognizes side-effect-free help, parses the
bounded command grammar, selects every source before conversion, and binds each reached value exactly once into an
immutable invocation snapshot. Successful binding deliberately stops at the Phase 3 execution barrier; graph
planning and callback execution remain owned by Phase 5.

## Grammar

| Input shape | Result |
| --- | --- |
| `--name value` | One long-option occurrence when `value` is not option-shaped |
| `--name=value` | One long-option occurrence; split at the first `=` and preserve an empty value |
| `--flag` | Boolean `true` when followed by another option or the end of input |
| `--flag value` | Consume `value` and apply the exact Boolean grammar |
| `-n value` | One short-alias occurrence |
| `-f` | Boolean alias with value `true` |
| `-n=value`, `-nvalue`, `-abc` | One unsupported-short-syntax diagnostic; never decomposed |
| repeated option occurrences | Preserve every value-bearing occurrence in order |
| repeated scalar option | Diagnose every occurrence after the first; bind the first value-bearing occurrence |
| missing value before another option | Diagnose the missing value without consuming the next option |
| hyphen-leading value | Require the attached long form, for example `--number=-1` |
| unknown `--name=payload` | Report only the option name; never retain or display `payload` |
| positional input | Diagnose its position without displaying its text |
| `--` | Diagnose unsupported syntax and continue with the ordinary grammar |
| exact `--help` or `-h` | Return successful help before authored-option parsing or binding |
| exact `--plain` | Select plain rendering; diagnose a second occurrence when help is absent |
| attached common-option forms | Diagnose malformed syntax without activating the common option |

Names and aliases are ordinal and case-sensitive. Values are preserved exactly. The parser makes progress on every
iteration, retains stable diagnostic kinds and positions internally, and has a fixed-seed bounded fuzz test.

## Source and binding precedence

| Order | Source | Presence rule |
| --- | --- | --- |
| 1 | Command line | First value-bearing scalar occurrence, or every repeated occurrence |
| 2 | Environment | Read once when declared and no command-line value was selected |
| 3 | Authored default | Use the frozen value when no earlier source is present |
| 4 | Absence | Optional becomes `null`; repeated becomes an empty immutable array; required is diagnosed |

An explicit or environment-provided empty string is present. Source selection and all required environment reads
finish before the first converter runs. Strings pass through, Boolean and enum values use dedicated bounded
grammars, nullable value types use their underlying converter, and other supported types call cached typed
`IParsable<T>.TryParse` delegates with `InvariantCulture` once per attempted value. Validators run once in authored
order after successful conversion. Ordinary failures continue independent options; thrown application code aborts
later conversion and validation while preserving the original exception internally.

Every public `context.Value(...)` overload reads the same option-identity-keyed snapshot. Repeated values are
`ImmutableArray<T>` instances exposed as `IReadOnlyList<T>`, and caller-owned mutations cannot affect the snapshot.
Ownership is checked before lookup.

## Invocation outcomes

| Condition | Public result | Internal status | Graph barrier |
| --- | --- | --- | --- |
| Invalid frozen model or foreign entry | Exit `2` | `InvalidModel` | No binding or callbacks |
| Exact help after a valid freeze | Exit `0` | `Help` | No parsing, fallback, conversion, validation, or callbacks |
| Syntax, conversion, requiredness, or validator rejection | Exit `2` | `InputFailure` | No contexts or callbacks |
| Environment lookup failure | Exit `1` | `InfrastructureFailure` | Abort later binding work |
| Converter or validator exception | Exit `1` | `AuthorFailure` | Preserve exception; abort later binding work |
| Redaction or presentation failure | Exit `1` | `InfrastructureFailure` | Write nothing unsafe |
| Successful binding | Temporary `NotSupportedException` | `SuccessfulBindingStub` | Snapshot exists; no callbacks |

The Phase 2 overlap guard remains active until each terminal path completes and is always released. A frozen command
supports sequential invocations, including reuse after a presentation failure.

## Presentation routing

| Report | Stream | Help included | Rich/plain selection |
| --- | --- | --- | --- |
| Successful help | stdout | Yes | Exact `--plain`, otherwise stdout capability |
| Invalid model | stderr | No | stderr capability |
| Ordinary input failure | stderr | Exactly once | Parsed `--plain`, otherwise stderr capability |
| Author/infrastructure binding failure | stderr | No | Parsed `--plain`, otherwise stderr capability |

Semantic reports do not depend on Spectre.Console. The rich and plain renderers receive captured streams and
capabilities through immutable invocation services. Help separates command and common options and includes a target
execution manifest that explicitly says targets are not command-line selections. The input report retains every
diagnostic internally, renders at most 20, and reports the omitted count.

Invocation names are derived from injected inputs in this order: file-app host metadata, non-`dotnet` executable,
entry assembly, launch token, then `command`. Only a leaf name is rendered. Integration tests also exercise real
stdout/stderr routing through the run fixture.

## Formatter policy

The frozen model retains canonical representations for the exact approved framework whitelist: strings, Boolean,
character, integral and floating-point numbers, decimal, GUID, date/time types, duration, and declared enum members.
Numeric and temporal formatting is invariant. Enum aliases use the first declared member as the canonical default.
Application-defined defaults are shown only as `configured`; sensitive ones are rejected because Rafter cannot
derive a stable redaction pattern without executing application formatting code.

Text presentation is quoted and escaped, counts Unicode scalar values without splitting supplementary characters,
represents isolated surrogates and non-printing characters with uppercase escapes, and truncates after 64 elements.
No arbitrary application `ToString()` or `IFormattable` implementation is invoked for presentation.

## Sensitivity boundary

Binding registers all non-empty raw known-sensitive command-line occurrences before any environment read. It then
registers selected sensitive environment/default representations during source selection and stable converted
representations before validation. Exact ordinal patterns are deduplicated; matches are discovered against the
original text and overlapping intervals are merged.

Reports are redacted semantically, fully buffered, serialized, scanned again, and written only when no registered
pattern remains. Marker collisions choose another safe marker; an unusable registry or failed final verification
suppresses the entire report and becomes an infrastructure failure. Sensitive failure reports omit application
messages. Raw snapshots, the registry, and preserved original exceptions intentionally remain internal and are not
claimed to be sanitized.

## Implementation map

| Responsibility | Implementation | Focused evidence |
| --- | --- | --- |
| Grammar and recovery | `BindingEngine.CommandLineParser` | `PhaseThreeGrammarTests` |
| Conversion | `OptionBindingStrategy` | grammar and binding tests |
| Source selection and snapshot | `BindingEngine` | `PhaseThreeBindingTests` |
| Typed lookup | `RafterContext` | binding and syntax-fixture tests |
| Canonical formatting | `ValueFormatter` | `PhaseThreeFormattingAndRedactionTests` |
| Secret registry | `TextRedactor` | formatting/redaction and presentation tests |
| Semantic reports and rendering | `CommandPresentation` | `PhaseThreePresentationTests` |
| Host seams and invocation names | `InvocationServices` | presentation tests |
| Lifecycle integration | `Command.RunAsync` | command-model, presentation, and integration tests |

## Verification

The completion commands are:

```powershell
dotnet restore Rafter.slnx --configfile nuget.config
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --no-restore
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build --no-restore
pwsh -NoProfile -File ./eng/verify-package.ps1 -Configuration Release
pwsh -NoProfile -File ./eng/validate-examples.ps1
git diff --check
```

Focused verification covers supported and rejected syntax, deterministic recovery and diagnostic order, every source
category, exactly-once readers/converters/validators, empty-value presence, immutable repeated absence, public lookup
shapes, help short-circuiting, callback non-entry, formatter boundaries, redaction overlap and collision behavior,
rich/plain routing, fail-closed presentation, invocation-name discovery, and real-process stream routing.

Observed locally on Windows x64 with .NET SDK `10.0.400`:

- Release build completed with zero warnings and zero errors with .NET and Meziantou analyzers enabled.
- All 74 tests passed across the unit, integration, and analyzer assemblies.
- Runtime and symbol packages passed content checks and restored successfully into isolated conventional-project and
  file-based consumers.
- All 29 canonical examples retained their project-mode references.
- Formatting and whitespace verification passed.

Cross-platform CI remains the branch integration gate after the user pushes the eventual commit.

## Deferred cross-phase evidence

- Phase 5 replaces the successful-binding stub and proves snapshot lookup from real conditions and targets, graph
  scheduling, and cleanup behavior.
- Phase 6 reuses the semantic reports, renderers, and redaction boundary for target-attributed and intercepted output.
- Phases 7 and 8 add generic and typed process execution without changing the Phase 3 option grammar.
- Phase 9 compiles and executes the complete canonical example portfolio once all referenced APIs exist.
