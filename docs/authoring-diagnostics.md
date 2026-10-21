# Authoring diagnostics

Initially implemented for [issue #17](https://github.com/sotsera/rafter/issues/17), extended with option-policy checks
in [issue #21](https://github.com/sotsera/rafter/issues/21). The local `Sotsera.Rafter` package includes a
separate C# analyzer assembly; it is also enabled for this repository's examples through their development targets.
Runtime validation and process tracking remain active. Diagnostics neither evaluate user code nor replace runtime checks.

| ID | Default severity | Initial coverage |
| --- | --- | --- |
| RAF001 | Error | Constant null, empty, or whitespace-only descriptions. |
| RAF002 | Warning | Nullable type arguments to `Command.Option<T>`. |
| RAF003 | Error | Clearly discarded `ProcessCommand.RunAsync()` tasks. |
| RAF004 | Error | Repeated or conflicting option policies through the same local in a supported straight-line segment. |

These are the implementation defaults. Broader option-policy analysis, repeated process settings, empty targets,
code fixes, and broader task data-flow analysis remain follow-up work. Generated code is excluded.

## RAF001

Descriptions on commands, options, targets, individual rules (optional and required/defaulted handles), and combined
rules must contain non-whitespace text. The analyzer uses the argument's compiler constant value, including named
arguments, constant expressions, `null!`, and `default(string)`. It reports at the description expression.

```csharp
var command = Rafter.Command("  "); // RAF001
var name = command.Option<string>("name", "Name to greet.");
name.Validate("", value => value.Length > 0); // RAF001
```

Values loaded from configuration or computed by helpers are left to initial runtime validation. No symbolic execution
or assumption about mutable locals is made. Unrelated APIs with a `description` parameter are not inspected.

## RAF002

Write `Option<string>`, `Option<int>`, or `Option<bool>` rather than their nullable forms. The optional handle already
provides a nullable lookup; `Required()` or `Default(...)` selects the present-value contract. The analyzer checks
semantic types, including `Nullable<int>`, and highlights the type argument.

```csharp
var name = command.Option<string?>("name", "Name."); // RAF002
var count = command.Option<int>("count", "Count.").Default(1);
```

This diagnostic does not change the runtime's supported type set or perform a code fix.

## RAF003

Do not discard the task returned by a process operation. Awaiting a task and ignoring its **result** is valid.

```csharp
context.Process("dotnet").Arguments("--version").RunAsync();     // RAF003
_ = context.Process("dotnet").Arguments("--version").RunAsync(); // RAF003
await context.Process("dotnet").Arguments("--version").RunAsync(); // Valid
```

The initial check recognizes expression statements, explicit discards, expression-bodied `Action` callbacks,
parentheses/conversions, conditional access, and discarded `ConfigureAwait(...)` calls on the returned task.
A real variable named `_` is distinguished from a discard. Awaited calls (including ignored results), returned tasks,
stored tasks, `Task.WhenAll`, and tasks passed to helpers are not diagnosed. Storing a task does **not** prove it will
eventually be observed; general alias/control-flow/helper analysis is deliberately outside this first rule.

See [process operation tracking](process-runtime.md) for outstanding operations detected at runtime. A task that has
already completed cannot be proven to have been awaited through runtime tracking alone.

## RAF004

Configure an option's presence policy once. Both repeated `Required()` and repeated `Default(...)` calls are invalid,
even with identical default values; mixing the two policies is also invalid. Fluent return types already prevent
chaining policies together. This diagnostic covers separate calls through a retained optional local handle:

```csharp
var name = command.Option<string>("name", "Name.");
var requiredName = name.Required();
name.Default("Ada"); // RAF004: name already has a Required call.
```

The diagnostic highlights the later invocation and supplies the first invocation as an additional location.
Coverage is deliberately limited to straight-line statements within one block, identifying the same local by its
Roslyn symbol. It includes standalone calls, calls stored in local declarations, explicit discards, named arguments,
parentheses, and built-in conversions. Constants and ordinary local/parameter reads are supported arguments. Simple
local declarations and direct Rafter command/option creation do not interrupt tracking. Distinct option locals are
tracked independently; aliases are not resolved to a shared option identity.

Reassignment, branches, loops, nested blocks, returns, arbitrary calls, and other uncertain operations end the current
segment. This includes intervening `Aliases`/`Validate` calls and computed default arguments: the first version does
not model those fluent chains or callbacks. Nested bodies are analyzed independently; state never flows between a
caller and a helper, lambda, or local function. Field/parameter receivers, ref locals, and conditional access are outside
coverage. No diagnostic at these boundaries proves that the configuration is valid; initial runtime validation still
rejects repetitions and conflicts before executing a body, with exit code `2`.

The default severity is an error because each reported pattern violates the existing runtime rule. No code fix chooses
which policy to keep. Repeated process settings and empty targets remain separate diagnostics to design and implement.

## Configuration and host baseline

Diagnostics use normal Roslyn configuration, including `.editorconfig`, project `NoWarn`, and `#pragma warning disable`.
For example, `dotnet_diagnostic.RAF002.severity = error` promotes the nullable-type warning. Disabling an authoring
diagnostic does not disable runtime validation. Tests explicitly disable all four and verify invalid declarations still
return exit code `2` without executing the body.

The analyzer targets `netstandard2.0` and uses the stable `Microsoft.CodeAnalysis.CSharp` 4.14.0 API baseline. This is a
deliberate older compiler API baseline, not a claim that 4.14.0 is the latest release. The shipping package includes only
the analyzer DLL under `analyzers/dotnet/cs`; Roslyn and its supporting packages are development/test dependencies.
CLI loading is verified against the repository's .NET SDK 10.0.402 (compiler 5.9.0-1.26505.111).
Older SDKs and other editor hosts are unverified.

Tests exercise semantic diagnostics through Roslyn and actual package-only compiler builds. The regular-project
design-time check resolves the package's analyzer and namespace import through MSBuild. These checks establish
compiler and design-time asset discovery. The subsequent [milestone review](milestone-review.md) verifies live VS Code
diagnostics and completion for both project and file-app consumers. Pixel-level styling and other editor hosts remain
unverified. No editor-specific plugin is required by the package.

Local verification on 2026-10-21: the full regression suite passed 370 tests, with 0 failures and 0 skips. This includes
30 semantic analyzer cases and the isolated package workflow. Local packing also succeeded without warnings.
The PR records the corresponding Windows CI evidence. This is historical evidence for the initial three rules;
issue #21 and its PR record the subsequent RAF004 regression, package-consumer, and editor checks.

RAF004 verification on 2026-10-21: the focused analyzer suite passed **59 cases** and the full local suite passed
**403 tests**, with no failures or skips. Fresh package-only project/file-app consumers report RAF004, and suppressing
the analyzers still produces runtime exit `2` for the policy conflict before body execution. Packing succeeded.
The isolated VS Code probe passed **12 live checks** across those two consumer forms, including RAF004 at error
severity, with VS Code 1.142.0, C# 2.160.4, and C# Dev Kit 3.40.215. See the
[review tooling](milestone-review.md#reproduce-and-evidence) for reproduction; the current probe includes all four rules.
Its evidence is under `artifacts/editor-review-62a55ef62c5c40a385fdadc761df9112/`. Windows CI results are linked from the PR.
Two upstream attempts exposed existing process-test failures. The [process-test isolation record](process-test-isolation.md)
preserves those attempts, the controlled inheritance reproduction, and the correction to the harness. Final suite
counts and CI results are recorded separately in the PR.

Upstream references: [Roslyn package and MIT license](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/4.14.0),
[NuGet analyzer layout](https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions).
