# Authoring diagnostics

Implemented for [issue #17](https://github.com/sotsera/rafter/issues/17). The local `Sotsera.Rafter` package includes a
separate C# analyzer assembly; it is also enabled for this repository's examples through their development targets.
Runtime validation and process tracking remain active. Diagnostics neither evaluate user code nor replace runtime checks.

| ID | Default severity | Initial coverage |
| --- | --- | --- |
| RAF001 | Error | Constant null, empty, or whitespace-only descriptions. |
| RAF002 | Warning | Nullable type arguments to `Command.Option<T>`. |
| RAF003 | Error | Clearly discarded `ProcessCommand.RunAsync()` tasks. |

These are the initial implementation defaults. Repeated/conflicting option policies, repeated process settings,
empty targets, code fixes, and broader task data-flow analysis remain follow-up work. Generated code is excluded.

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

## Configuration and host baseline

Diagnostics use normal Roslyn configuration, including `.editorconfig`, project `NoWarn`, and `#pragma warning disable`.
For example, `dotnet_diagnostic.RAF002.severity = error` promotes the nullable-type warning. Disabling an authoring
diagnostic does not disable runtime validation. Tests explicitly disable all three and verify invalid declarations still
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
The PR records the corresponding Windows CI evidence.

Upstream references: [Roslyn package and MIT license](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/4.14.0),
[NuGet analyzer layout](https://learn.microsoft.com/en-us/nuget/guides/analyzers-conventions).
