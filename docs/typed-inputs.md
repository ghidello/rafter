# Typed inputs and validation

Issue [#11](https://github.com/sotsera/rafter/issues/11) makes [options.cs](../examples/options.cs) and
[validation.cs](../examples/validation.cs) runnable against the new runtime. The initial types are `string`, `int`,
and `bool`. The parser and renderer versions remain unchanged; no new dependency is introduced.

## Typed handles and lookups

`command.Option<T>(name, description)` returns `Option<T>`. `.Required()` and `.Default(value)` return
`ValueOption<T>`, which exposes aliases and validation but neither policy method. Both execution and input-validation
contexts provide these lookup signatures:

```csharp
T GetValue<T>(ValueOption<T> option);
string? GetValue(Option<string> option);
T? GetValue<T>(Option<T> option) where T : struct;
```

The concrete string overload and constrained value-type overload preserve optional nullability for the initial types.
An unconstrained `T?` alone would not make optional integers return `Nullable<int>`. Additional reference types will
need an explicit API decision; this increment does not promise arbitrary `T` conversion.

Use non-nullable supplied types. Runtime validation rejects unsupported types, including nullable value types.
Nullable reference annotations are erased at runtime; the proposed analyzer remains necessary for `string?` feedback.
Keeping the original optional handle after finalization is allowed; its lookup retains the weaker nullable signature.

Each invocation captures declarations, aliases, rules, and policies before parsing or callbacks. It prepares values
once and stores them by declaration identity. Repeated `GetValue` calls only read that snapshot. Lookups of foreign
handles or handles added after capture throw within the invocation's exception boundary (exit 1). Later declaration
changes affect later invocations; overlapping runs have independent value dictionaries. Values have no lazy resolver.

## Validation and parsing

Fluent methods collect declarations without throwing validation errors. Initial `RunAsync` validation returns 2 for
invalid names/aliases, collisions, reserved control names, blank descriptions, null defaults or predicates, unsupported
types, and policy conflicts. This precedes help. **Repeated Required and repeated Default are both invalid**, even for
identical defaults. Retained handles and helper calls cannot bypass the check. Future analyzer work should report
repetitions/conflicts it can prove; no analyzer is shipped here.

Unprefixed single-character names receive `-`; longer names receive `--`. Explicit prefixes are preserved. Aliases
append in declaration order, ignoring duplicate spellings within an option. Matching is ordinal and case-sensitive.
The initial grammar follows the [agreed parsing rules](example-design-plan.md#boolean-syntax-and-repeated-scalar-options):
space/equal values, explicit empty strings, bare/explicit Boolean flags, and no repeated scalars. Required checks
presence, including empty strings, zero, and false. Declared options form value boundaries. Positional arguments,
standalone `--`, colon separators, bundling, response-file expansion, and directives remain unsupported.

Safe parsing diagnostics identify declared options without echoing rejected values or raw parser messages. This is
not general secret redaction: author-provided descriptions/defaults appear in help, and unexpected callback exceptions
retain their full details under the existing exception policy.

Help permits missing required inputs but still rejects malformed supplied arguments. It displays primary names then
aliases, type hints, presence/default policy, and individual/combined rules without running callbacks. Names are blue,
type hints lavender, and separators muted in capable terminals. Plain/redirected output contains no ANSI styling.

Individual rules run in declaration order and rule order, including defaults; omitted optional values are skipped.
False results aggregate with exit 2. Combined rules run only after all individual rules succeed, read the same prepared
values, and aggregate their own false results. A thrown rule stops later callbacks and returns 1, preserving earlier
rejection diagnostics. The body begins only after all validation succeeds. Cooperative cancellation is checked between
rules and before the body; synchronous user predicates cannot be forcibly interrupted.

## Verification

The existing Windows native-MTP harness includes:

- [Runtime tests](../tests/Rafter.Evaluation.Tests/TypedInputTests.cs) for declarations, repeated policies, aliases,
  presence/defaults, grammar and diagnostic privacy, rule ordering, exceptions, snapshots, ownership, cancellation,
  and help at 48/100 columns.
- [Consumer tests](../tests/Rafter.Evaluation.Tests/TypedInputConsumerTests.cs) building and running both actual
  examples in isolated workspaces, plus compiler failures for optional-to-nonnullable assignments, unavailable
  policy methods after finalization, and output facilities on the validation context.
- [Shared consumer scenarios](../fixtures/runtime/Scenario.cs) proving the typed API in file apps and ordinary
  projects, with explicit optional/nonnullable assignments in both contexts.

Local verification on 2026-10-21: Windows x64, SDK 10.0.402, managed net10.0. Release build completed with
**0 warnings and 0 errors**. The full native-MTP suite reports **Passed: 231, Failed: 0, Skipped: 0**, including
65 additional cases and the extended existing consumers. Both consumer forms treat warnings as errors.

Commands used from the repository root:

```powershell
dotnet build tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --no-restore -bl:artifacts/build.binlog
dotnet test --project tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --no-build --report-trx --results-directory artifacts/test-results
```

A real Windows pseudoterminal run of options.cs help confirmed blue names, lavender type hints, and muted commas.
With NO_COLOR set it retained bold emphasis without colors. Automated captures verify plain literal output and
48/100-column wrapping. GitHub CI evidence is recorded in the PR.

Targets and processes were outside this slice and are now implemented separately in the [target runtime](targets.md)
and [Windows process runtime](process-runtime.md). Environment inputs, async resolution, additional types,
analyzers, NuGet/global-usings integration, native AOT,
and Linux/macOS verification remain outside this slice.
