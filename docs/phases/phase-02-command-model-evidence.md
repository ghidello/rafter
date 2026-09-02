# Phase 2 completion evidence

## Status

Phase 2 is implemented. The public API baseline contains only the command-model syntax represented by the portfolio,
and the execution terminal intentionally remains a `NotSupportedException` stub after atomic freeze. Parsing,
binding, graph planning, output, filesystem behavior, and process execution remain owned by later phases.

## Mutable-to-frozen model

```text
Command
└─ AuthoredCommand (mutable, command ID, authored sequence)
   ├─ root policy (root ID)
   ├─ single-valued command settings
   ├─ AuthoredOption[] (option IDs)
   │  └─ validator[] (validator IDs and authored order)
   └─ AuthoredTarget[] (target IDs)
      ├─ dependency snapshots (target IDs)
      ├─ condition[] (condition IDs and authored order)
      ├─ execution/cleanup callbacks (callback IDs)
      └─ working-directory declaration (working-directory ID)

first RunAsync / internal freeze
                 │
                 ▼
ModelFreezeResult (cached by reference)
├─ ordered ModelDiagnostic[]
└─ CommandDefinition, only when diagnostics are empty
   ├─ RootDefinition
   ├─ immutable OptionDefinition[]
   └─ immutable TargetDefinition[]
```

Every public handle points to one command-owned authored identity. `DefaultedOption<T>` is therefore a different
static view of the same option rather than another registration. Freeze closes all builders before validation,
copies every collection into `ImmutableArray<T>`, and caches either the immutable definition or the complete ordered
failure. A failed freeze is just as final and idempotent as a successful freeze.
Authored defaults enter the definition only when the generic declaration proves that `T` is `string` or contains no
managed references; unsafe defaults fail freeze before their caller-owned object can enter a command definition.

## Ownership-check matrix

| Attachment | Identity checked | Checking phase | Failure category |
|---|---|---|---|
| `Target.DependsOn(...)` | Every dependency target and owning command | Phase 2 freeze | Model diagnostic on the dependency argument |
| Target `.WorkingDirectory(required/defaulted)` | Option and owning command | Phase 2 freeze | Model diagnostic on the fluent call |
| `RunAsync(entryTarget, args)` | Entry target and invoked command | Every invocation | Invocation diagnostic; never cached in the model |
| `context.Value(option)` | Option and active command | Phase 3 | Binding/context diagnostic |
| Filesystem/resolved-directory option use | Option and active command | Phase 4 | Context diagnostic |
| Generic-process option handles | Option and active invocation | Phase 7 | Process-specification diagnostic |
| Typed-tool option handles | Option and active invocation | Phase 8 | Typed-builder diagnostic |

Null handles, arrays, elements, delegates, names, and metadata remain programming-contract failures and throw before
mutation. Non-null ownership mistakes are semantic diagnostics and follow the common authored ordering contract.

## Syntax traceability

| Phase 2 syntax | Canonical examples | Compile/behavior fixture |
|---|---|---|
| `Rafter.Command(root)`, fluent descriptions, concurrency | `minimal.cs`, `dependencies.cs`, root examples | `PhaseTwoSyntaxFixtureTests` |
| Optional, required, defaulted, repeated handles | `options.cs`, `option-types.cs` | `CommandModelTests.MetadataMethodsPreserveOptionTypeState`, `CommandModelContractTests.PublicSurfaceEnforcesTheFrozenTypeState` |
| Option metadata and validators | `options.cs`, `option-types.cs`, `validation-failures.cs` | `CommandModelTests.FreezesACompleteCommandIntoImmutableDefinitions`, `CommandModelContractTests.RepeatedValidationHasTheImmutableListShapeAndRetainsItsMessage` |
| Executable, aggregate, and implicit no-work targets | `minimal.cs`, `dependencies.cs`, `no-op.cs` | `PhaseTwoSyntaxFixtureTests.EveryPhaseTwoCallbackAndTargetShapeCompilesAgainstThePublicApi` |
| Dependency declaration and snapshots | `dependencies.cs`, `repository.cs` | `CommandModelTests.DependenciesAndInvocationArgumentsAreSnapshotted` |
| Four execution callback forms | `console.cs`, `cleanup.cs` and context-using examples | `PhaseTwoSyntaxFixtureTests.EveryPhaseTwoCallbackAndTargetShapeCompilesAgainstThePublicApi` |
| Four condition forms and composition | `conditions.cs`, `user-secrets.cs` | `CommandModelContractTests.ConditionsPreserveAuthoredOrderAndDeferredEvaluation` |
| Four target and command cleanup forms | `cleanup.cs` | `PhaseTwoSyntaxFixtureTests.EveryCleanupCallbackFormCompilesAgainstThePublicApi` |
| Literal/required/defaulted target working directory | `working-directory.cs`, `node.cs` | `PhaseTwoSyntaxFixtureTests.EveryPhaseTwoCallbackAndTargetShapeCompilesAgainstThePublicApi` |
| `RunAsync(entryTarget, args)` and lifecycle | every runnable example | `CommandModelTests.RejectsOverlappingInvocationsAndAllowsSequentialReuse`, `CommandModelTests.ForeignEntryIsInvocationSpecificAndDoesNotPoisonTheFrozenModel` |

## Analyzer exceptions

All analyzers remain enabled. Two narrow file-scoped `.editorconfig` policies document public names fixed by the
approved syntax: CA1716 for `Option<T>` and MA0049 for `Rafter.Command(...)`. Each file contains only the named public
type; no project-wide rule or analyzer is disabled.

## Verification

The following commands passed locally on Windows x64 with .NET SDK `10.0.400`:

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

Observed results:

- Release build: 0 warnings and 0 errors with .NET and Meziantou analyzers enabled.
- Tests: 23 passed across unit, integration, and analyzer assemblies; the Phase 2 command-model suites contribute 20.
- Runtime and symbol packages retained the exact Phase 1 layouts.
- Isolated conventional-project and file-based package consumers restored and ran successfully.
- All 29 canonical examples retained their direct project-mode reference.
- Formatting and whitespace verification passed.

Cross-platform CI remains the branch integration gate after the user pushes the commit.
