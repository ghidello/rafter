# Rafter syntax portfolio

These file-based applications define the proposed public experience for the library-only Rafter rebuild. They are design artifacts until the
corresponding package surface is implemented.

The portfolio deliberately assumes:

- one file-based application defines one command;
- `Rafter.Command(...)` receives only the root policy;
- command, target, and option descriptions are authored fluently and are required;
- exactly one target is passed to `RunAsync` as the command entry point;
- targets are internal graph nodes rather than command-line subcommands;
- Rafter parses the bounded command grammar and uses Spectre.Console internally;
- no external Rafter CLI, MCP server, `rafter.json`, recipe system, or command discovery is required;
- source execution is the initial experience, while published and Native AOT execution remain subject to later spikes.

## Portfolio

### Getting started

- `minimal.cs`: the smallest useful command.
- `options.cs`: required, defaulted, Boolean, environment-backed, and sensitive inputs.
- `dependencies.cs`: a branching target graph with bounded concurrency.
- `repository.cs`: a realistic repository verification graph that composes the principal APIs.

### Feature recipes

- `option-types.cs` and `user-secrets.cs`: additional input types, binding-time validation, and application-owned
  configuration.
- `root-invocation.cs`, `root-source.cs`, `root-explicit.cs`, and `working-directory.cs`: roots and scoped working
  directories.
- `filesystem.cs`: common filesystem preparation operations.
- `processes.cs` and `environment.cs`: reusable immutable generic process specifications and argument-safe child
  processes; `processes.cs` also demonstrates a defaulted `TimeSpan` timeout option.
- `dotnet.cs`, `git.cs`, and `node.cs`: typed tool integrations.
- `extensibility.cs`: an application-owned `CaptureJson` process-builder extension.

### Behavioral contract fixtures

These examples deliberately exercise failure paths, races, cancellation, or presentation boundaries. They define
product behavior but are not the suggested starting point for ordinary scripts.

- `no-op.cs`: an implicit no-op reported as completed with no work.
- `conditions.cs` and `cleanup.cs`: condition and cleanup callback forms.
- `failures.cs`, `concurrent-failures.cs`, and `validation-failures.cs`: deterministic failure classification and
  ordering.
- `callback-cancellation.cs` and `process-cancellation.cs`: callback and process cancellation.
- `diagnostics.cs`, `console.cs`, `redaction.cs`, and `presentation.cs`: semantic output, managed-console attribution,
  redaction, and human/machine presentation.

Every checked-in example references `../src/Sotsera.Rafter/Sotsera.Rafter.csproj` directly. This is the default
development mode and keeps the canonical sources buildable against the implementation without generated copies.

## Development and package dependency modes

The example-scoped build files support two dependency modes without changing an example:

- `Project` is the default and uses the checked-in `#:project` directive.
- `Package` removes that project reference and adds a centrally versioned `Sotsera.Rafter` package reference.

`examples/Directory.Packages.props` is the single source of truth for the development package version. Update it
when producing a package version that the portfolio must validate. Package mode adds the configuration-specific
local package output directory as an additional restore source, so public sources from the repository NuGet
configuration remain available.

Once an example's API surface is implemented, build it against the current project with:

```powershell
dotnet build ./examples/minimal.cs --configuration Release
```

After packing the matching version, build the unchanged example against the package with:

```powershell
dotnet build ./examples/minimal.cs --configuration Release -p:RafterDependency=Package
```

A standalone published script replaces the repository project directive with an explicit package directive such as
`#:package Sotsera.Rafter@1.0.0`. Compilation of the complete portfolio in both dependency modes is deferred until
the corresponding APIs are implemented.

## Resolving authored values

Authored code resolves an option explicitly when it needs the value for conditions, calculations, branching, or output:

```csharp
if (context.Value(publish))
{
    // Application-owned behavior.
}
```

Context-owned APIs accept required and defaulted option handles directly when the resolved type satisfies the operation:

```csharp
await context.Process(tool).Argument("verify").Option("--output", output).Run();
await context.DotNet.Build(solution).Configuration(configuration).Run();
context.FileSystem.EnsureEmptyDirectory(output);
```

Optional options do not participate in this convenience because their value may be absent. Authors resolve and handle them explicitly.
Optional reference-type handles resolve as nullable references. Optional value types are authored with nullable type arguments such as
`Option<int?>`; a non-nullable value-type option must become required or defaulted before model freeze so absence is never confused with
`default(T)`.

Rafter binds every authored option exactly once per invocation, before graph execution begins. Binding parses command-line input, reads declared
scalar environment fallbacks, applies scalar defaults, converts values, validates constraints, registers sensitive values for redaction, and
snapshots repeated values. A binding failure prevents every condition, target, cleanup callback, and child process from running.

An unset environment fallback means absence and continues to an authored default or optional absence. A set-but-empty environment value, like
an explicit `--name=`, is present data and goes through ordinary conversion and validation rather than silently falling through. Empty strings
are never registered as redaction patterns.
Requiredness checks presence, not string content. Consequently, an explicit empty command-line or environment value
satisfies `RequiredOption<string>`; authors use `.Validate(...)` when empty or whitespace-only text is invalid for
their domain.
Each scalar option may declare at most one `.FromEnvironment(...)` fallback. A second declaration is a duplicate
single-valued model setting and is reported with the other model-freeze diagnostics.
Fallback names and child-process environment edit keys require non-whitespace text and cannot contain NUL or `=`.
Rafter preserves their authored spelling and follows the host operating system's normal lookup case semantics; it
does not impose uppercase naming or emulate case sensitivity across platforms.
Scalar `.Default(...)` and every option's `.Sensitive()` are likewise single-valued. Repetition records a model
error; fluent type-state prevents duplicate scalar defaults where possible, while model-freeze validation remains
the complete guard.
Authored defaults must also be snapshot-safe: `string` or a value type containing no managed references. A custom
reference-type `IParsable<T>` can still bind external input, but cannot be used as an authored default because the
frozen command would otherwise retain caller-owned mutable state.

For a sensitive scalar option, Rafter registers every non-empty raw command-line occurrence associated with that
known option before the first environment lookup, including duplicate or rejected occurrences. It registers a
selected sensitive environment value or approved authored default immediately, before another fallible operation,
and any distinct stable converted representation before validation. A throwing environment lookup is a fail-closed
infrastructure failure whose arbitrary exception details are not rendered. Conversion failures and throwing
validators therefore pass through the same redaction boundary as later managed output.
`.Sensitive()` is available for every supported scalar and repeated option type, not only `string`. Repeated options
register each command-line occurrence independently, and empty representations are never added as redaction patterns.
Raw bound values, the redaction registry, and preserved original application exceptions remain internal and are not
claimed to be sanitized. Rafter never renders arbitrary exception details for a sensitive option.
Rafter redacts complete managed text with exact ordinal matching. It merges overlapping secret matches before
replacement, uses a marker that contains no registered pattern, verifies the result, and suppresses the complete
buffered renderer output before writing it. It suppresses the report rather than emitting text when redaction cannot
be proven safe. Unicode normalization and application-created secret transformations are caller-owned. Phase 6
applies the same algorithm across streaming boundaries.

`.Sensitive()` is redaction metadata, not secure input transport. It prevents registered values from being emitted
through Rafter-managed output, but it cannot remove a command-line value from shell history or process inspection.
Environment variables can also leak through process inheritance, diagnostics, or host tooling, so
`.FromEnvironment(...)` is a configuration fallback rather than a secret-store recommendation. Real credentials
should normally come from application-owned secret stores, credential files, standard input, or another secure
channel appropriate to the environment. Values obtained outside Rafter option binding are not registered
automatically and must not be emitted through output channels. V1 has no manual sensitive-value registration API;
adding one requires a command-level, pre-invocation contract and a syntax example rather than target-time mutation.

Boolean options accept a bare `--name` shorthand for `true` and explicit `--name true` or `--name false` values. Absence continues through the
normal environment/default/optional precedence. Rafter does not synthesize `--no-name` aliases in v1.
Long option values may be separated by whitespace or the first equals sign, so `--configuration Release` and
`--configuration=Release` are equivalent; subsequent equals signs belong to the value.
Short aliases use only the separated `-c Release` form. A bare Boolean alias means `true`; `-c=Release`, attached
values such as `-cRelease`, and bundled flags such as `-abc` are rejected in v1.
Option names and aliases use ordinal case-sensitive matching on every platform. A spelling with different casing is
unknown rather than an alias, and exact duplicate names or aliases are reported with the other model-freeze
diagnostics.
Authored option names omit `--` and use lowercase kebab-case: they start with `a`–`z`, continue with lowercase
letters, digits, or single hyphens, and contain no trailing or repeated hyphen. One-character aliases are declared
separately with `.Alias(...)`.
Target names use the same lowercase kebab-case format and ordinal case-sensitive identity. Duplicate target names
are reported with the other model-freeze diagnostics.
`.Alias(char)` accepts one lowercase ASCII letter from `a` through `z`. Duplicate aliases and an alias colliding
with another option's one-character long name are reported at model freeze. Each option may declare at most one
alias; a second `.Alias(...)` call follows the same policy.
Every command, target, and option must have a non-empty, non-whitespace description before model freeze. Rafter
preserves authored wording and does not invent fallback descriptions from identifiers.
`RepeatedOption<T>` accepts one value per occurrence, preserves occurrence order, and resolves absence to an empty
immutable `IReadOnlyList<T>`. It never splits commas or semicolons. Repeating a scalar `Option<T>` is an error;
`Option<T[]>` is not the repeated-option API. Repeated options have no authored default or environment fallback in
v1. Their validators receive the complete immutable list and run even when it is empty, allowing an authored
validation message to require at least one occurrence when necessary.
Scalar conversion is invariant and deliberately extensible through the .NET type system: `string` passes through
unchanged, Boolean and enum values use Rafter's dedicated grammars, nullable value types delegate to their
underlying type, and other supported values receive one `IParsable<T>.TryParse` call per attempted value. This
includes framework types such as numeric values, `Guid`, `DateTime`, and `TimeSpan`, as well as application-defined
parsable types. A thrown converter or one that reports success with a null reference result is an author failure.
An unsupported option type is a model-freeze error. Rafter has no custom converter API in v1; bind a `string` and
convert it inside a target for more complex application-owned conversion.
Enum input accepts one declared member name using ordinal case-insensitive matching, so `Release` and `release` are
equivalent. Numeric values, undefined values, and implicit comma-separated `[Flags]` combinations are rejected. A
flags enum may still declare a named composite member and accept that member by name.
An enum option is rejected at model freeze if two declared member names collide under ordinal case-insensitive
comparison; exact casing does not select among an otherwise ambiguous type. Distinct names that share an underlying
numeric value remain valid because their textual inputs are still unambiguous.
The initial grammar has no `--` end-of-options marker because it has no positional or pass-through tail. A value
beginning with hyphens is supplied with the unambiguous `--name=value` form.
Unknown options are reported exactly without fuzzy suggestions or autocorrection in v1, followed by the ordinary
safe help/usage output. Diagnostics show only an unknown option's name and never an attached `=value` payload.
Unexpected positional diagnostics identify only the argument position and never display the token text because it
could be a value supplied after a mistyped sensitive option.
Rafter reserves `--plain`, `--help`, and `-h` as built-ins. Help succeeds without binding options, reading environment fallbacks,
running validators, resolving roots, or executing graph callbacks. Authors cannot
declare the `plain` or `help` name or the `h` alias. File-based commands receive no synthesized `--version` option.
Help shows each sensitive option's name, alias, description, required/optional state, scalar environment fallback
name, and sensitive marker, but never its default or environment value. Non-sensitive scalar defaults are formatted
with an approved invariant formatter; an application-defined default without one appears only as
`default: configured`. A sensitive authored default without a stable approved redaction representation is a
model-freeze error.
The approved default formats are invariant decimal for integral types, `R` for binary floating-point types, `G29`
for `decimal`, lowercase `D` for `Guid`, `O` for date/time types, `c` for `TimeSpan`, lowercase text for Boolean,
and the first declared canonical name for enums. String and character displays use the exact quoted escape grammar in
the Phase 3 plan; strings retain at most 64 Unicode elements and append `...` when truncated. Undefined enum
defaults and null authored defaults are model errors; null remains optional absence rather than a resolved default.
Rafter does not invoke application formatting code to produce help.
Help begins with the command description and a usage line whose invocation name is derived from .NET file-based
application host metadata, with deterministic executable, entry-assembly, launch-token, and `command` fallbacks;
v1 has no command-name API. Authored `Command options` are separate from Rafter-owned
`Common options`, which contain `--plain` and `-h, --help`. Friendly metavariables describe scalar and enum
values, Boolean values are optional because a bare flag means `true`, and only repeated options use `...`.

The `Targets` help section is a deterministic execution manifest for people and agents, not target-selection CLI.
In declaration order it shows every target's name and required description, authored-order dependencies, the entry
target, whether conditions exist, and callback-free `Aggregate` or `No work` shape. It omits callback, cleanup,
permit, and working-directory implementation details. Redirected plain help remains stable and agent-readable; v1
adds neither JSON help nor a target-selection command.

`--plain` forces both stdout and stderr into deterministic width-independent output with no ANSI, cursor/live
control, or Unicode-only status glyphs, making `--plain --help` an explicit stable manifest for agents. Without it,
stdout and stderr capabilities are evaluated independently and a redirected or incapable stream is always plain.
A non-empty `NO_COLOR` disables color without forcing plain layout; an empty value is treated as unset. Rafter does
not support `FORCE_COLOR` in v1, and redirected output cannot be forced rich.

The final target summary is ordered by target plan order, never completion timing. Executable targets finish as
`Succeeded`, `Skipped`, `Failed`, `Cancelled`, or `Blocked`; direct blockers are listed in authored dependency order.
Successful callback-free targets appear as `Aggregate` or `No work`. Rich mode may show transient `Waiting`,
`Running`, and `Cleaning up` states, while plain mode emits only the deterministic terminal summary. Successful
summaries use stdout; failed or cancelled summaries and their details use stderr. Durations are omitted in v1.

Rich presentation uses `○` waiting, `●` running, `◐` cleanup, `✓` succeeded, `◇` aggregate, `–` no
work, `↷` skipped, `■` cancelled, `⊘` blocked, and `✗` failed. Success shapes are green, active states
cyan, skipped/no-work/waiting grey, cancelled/blocked yellow, and only actual failure red. The symbols remain under
`NO_COLOR`; `--plain` replaces them with full ASCII labels. A cleanup-only failure makes its target failed, while a
cancelled target with an additional cleanup failure remains cancelled and reports cleanup separately.
An exact standalone `--help` or `-h` token takes precedence over every other token, renders help, and exits
successfully. Help-like text inside a value does not activate it, and malformed forms such as `--help=false` remain
errors.

Validation is declared fluently on an option and applies to the single resolved value regardless of whether that value came from the command
line, an environment fallback, or a default. A failed validator reports its authored message and stops the invocation at the binding barrier.
An option may register multiple validators; they run once each in authored order and stop at that option's first failure.
Validators do not run for complete optional absence. Explicitly empty command-line or environment values are present, so validators do run for
them. Required and defaulted options always resolve a value and therefore always validate.
Validators are synchronous and inspect only the resolved value. Asynchronous, process-based, or remote checks belong in conditions or targets,
where invocation cancellation and execution lifecycle semantics are available.
Rafter collects ordinary command-line, conversion, and validation errors across options so authors can correct them in one pass. Syntax errors
are ordered by token position, followed by conversion and validation errors in option declaration order; each option contributes at most its
first failed validation message.
Presentation groups those failures once, followed by one safe help block. Rich output uses a heading and bullets;
plain output uses one physical `error: ...` line per diagnostic. Known non-sensitive invalid values may appear only
as bounded escaped excerpts, sensitive values appear as `<redacted>`, and an unknown option's attached payload is
never shown. Rafter retains every collected diagnostic internally but displays at most 20 followed by an omitted
count, preventing argument floods without changing deterministic collection.
`validation-failures.cs` is a contract fixture that contrasts a validator returning `false` with a validator throwing. A thrown validator is an
authoring failure whose original exception is preserved; it is not reported using the ordinary invalid-value message. Neither outcome crosses
the binding barrier, and sensitive values remain redacted from both diagnostic paths. A thrown validator aborts binding immediately, retains
safe ordinary diagnostics already collected, and prevents later converters or validators from running.

`context.Value(option)` returns nullable optional values, declared required/defaulted values, and immutable repeated
lists from the same ownership-checked bound snapshot. Reading an option from multiple targets never repeats
conversion, validation, environment access, or default selection.

Conditions support an authored Boolean, a deferred `Func<bool>`, a context-aware synchronous predicate, or a context-aware asynchronous
predicate. There is no context-free asynchronous overload: asynchronous conditions receive the target context so they can observe invocation
cancellation.
Targets may register multiple conditions. They form a short-circuit AND chain evaluated at most once each in authored order after the target
becomes ready and acquires its permit. The first `false` skips the target; the first thrown condition fails it; either outcome prevents later
conditions and target execution.

A condition returning `false` skips that target and satisfies its dependents; it does not fail or block the graph. Failed or cancelled
prerequisites do block dependents. To suppress a complete branch, authors put the condition on that branch's aggregate or selected entry target.
Target cleanup does not run when a condition returns `false` or throws because the execution callback never started. Once execution starts,
target cleanup runs after success, failure, or cancellation; command cleanup remains invocation-wide.

Target callbacks may omit the context when they do not use Rafter services. Both synchronous `Action` and asynchronous `Func<Task>` forms are
supported for execution and cleanup; context-aware callbacks remain the path to output, bound values, working directories, processes, and
cooperative cancellation.
Each target accepts at most one `.Run(...)` callback. A second registration records a model error and preserves the first; authors compose
sequential operations inside one callback and represent parallel work as separate dependency targets.

A target with neither an execution callback nor dependencies is a valid implicit no-op. It succeeds and is presented distinctly as completed
with no work. A target with dependencies but no callback is an aggregate, while a false condition produces a skipped target; no analyzer or
`.NoOp()` marker is required.

Commands execute at most one target callback at a time unless `.Concurrency(...)` explicitly opts into parallel graph execution. The configured
value must be positive. A target holds no permit while waiting for dependencies. Once ready, it holds one permit across deferred condition
evaluation, execution, and target cleanup. Callback-free aggregate and implicit no-op settlement consume no permit.

`.DependsOn(params Target[] dependencies)` declares a target's complete dependency set in one call. A second call or
the same dependency appearing more than once records a model error; dependencies are never silently deduplicated.

Each target and command accepts one `.Finally(...)` callback. Registering a second callback records a model error rather than replacing the first
or creating an implicitly ordered cleanup stack. Authors compose multiple cleanup operations explicitly inside that callback.
Duplicate single-valued model settings such as descriptions, concurrency, target working directories, execution, and cleanup preserve their
first value internally and accumulate authored-order diagnostics. `RunAsync` reports all such errors when it freezes the model, before parsing,
binding, or execution.
Target `.Finally(...)` requires that target to declare `.Run(...)`; aggregate and implicit no-op targets cannot own cleanup. Cleanup that belongs
to the invocation as a whole is declared with `command.Finally(...)` and runs after target settlement and qualified target cleanup.
Command cleanup begins only after parsing, binding, graph preflight, and invocation initialization succeed. From that point it runs exactly once
after every success, failure, cancellation, or all-skipped outcome. Earlier failures run no Rafter callback; resources acquired during ordinary
C# authoring remain the application's responsibility through `using` or language-level `try`/`finally`.
Cleanup receives a dedicated token that is not already cancelled merely because invocation cancellation caused the
cleanup path. Rafter awaits managed cleanup to settlement: it cannot safely kill or detach arbitrary callback code,
so cleanup authors keep callbacks finite and apply operation-specific timeouts themselves. Hard bounded teardown is
promised only for resources Rafter owns and can terminate, such as child processes.
Execution failure or cancellation remains the primary command outcome when cleanup also fails. Target-cleanup
failures are reported separately in stable target plan order, followed by command cleanup, under `Cleanup also
failed`; none replaces an earlier exception. If execution succeeded, any cleanup failure still makes the command
unsuccessful and becomes its sole failure category. Rafter retains all original exceptions in its internal outcome
for classification, presentation, and verification; none is wrapped or replaced. `RunAsync` exposes no
structured public outcome in v1. Authors who need programmatic handling catch an exception inside their execution or
cleanup callback before it escapes to Rafter; typed process exceptions remain catchable there.

`RunAsync` returns `0` for success, help, skipped/no-op completion, and explicitly valid nonzero child-process exits;
`1` for converter or validator author exceptions and execution, process, infrastructure, or cleanup failure; `2`
for command-model, syntax, failed-conversion, missing-required, validator-rejection, or graph-planning diagnostics;
and `130` for invocation cancellation. An `OperationCanceledException` counts as
invocation cancellation only when that invocation's token was actually requested; otherwise it is a callback
failure. A process timeout is likewise failure rather than cancellation. Concurrent and cleanup details affect the
diagnostic outcome, not the numeric mapping.

`Output.Property(string, object?)` keeps the calling syntax uniform for nulls, scalars, strings, and collections.
Rafter snapshots the supplied value synchronously, treats `string` as scalar, and enumerates another collection once
into an immutable one-dimensional sequence. Nested structured collections are rejected with guidance to format them
in application code; enumeration or formatting exceptions remain callback failures. Rich output distinguishes
`<null>`, `""`, multiline text, and readable collections. Plain output uses one physical `name=value` line with
JSON-style literals and escaping. `presentation.cs` exercises each shape.

Line, success, property, lifecycle/progress, successful help, managed `Console.Out`, and child stdout use stdout.
Warning, error/recovery, command diagnostics, failure/cancellation summaries, invalid-input usage, managed
`Console.Error`, and child stderr use stderr. `Output.Error(...)` reports a semantic event but does not itself fail
the target; callback control flow determines the outcome. Ordering is preserved within each stream, but stdout and
stderr have no relative-order guarantee after they are independently redirected.

Managed console writes during target conditions, execution, and target cleanup carry that target's attribution.
Command cleanup and other invocation work without an active target, including binding and validation, use command
attribution. An expired target scope falls back to command attribution if unawaited work writes before the invocation
settles. Rafter intercepts only while `RunAsync` is active and restores the host writers exactly; callbacks must await
spawned work because output that outlives `RunAsync` is application-owned and receives no Rafter attribution or
redaction guarantee.
Calling `Console.SetOut` or `Console.SetError` while `RunAsync` owns the console is unsupported. Rafter detects loss
of its coordinating writers at callback and presentation boundaries, records an infrastructure failure without
adopting or inspecting the replacement, and restores the exact stdout and stderr instances captured at entry.
Temporary replace-and-restore mutations may be undetectable and therefore sit outside the supported attribution and
redaction boundary; ordinary `Console.Write*` calls remain supported.

`redaction.cs` intentionally emits a synthetic sensitive value through every supported output path. It is a contract fixture, not a pattern for
writing real credentials to output. Its value must always be a disposable test value.

`extensibility.cs` intentionally keeps JSON deserialization outside Rafter's core API. It demonstrates that an application can build fluent,
strongly typed conveniences over the same public `ProcessBuilder` returned by `context.Process(...)` and its modifiers. The extension uses only
ordinary public completion and result APIs, with no separate extensibility interface or access to runtime internals.

## Process working directories

Targets expose `.WorkingDirectory(...)` as the default logical directory for their callbacks. Relative target directories resolve against the
command root. Every generic process, typed process, and filesystem operation created from that target's context uses the target directory by
default. Target cleanup uses its owner's directory; command cleanup uses the command root.

Generic processes and every typed process integration also expose `.WorkingDirectory(...)` as a per-process override. A relative process override
resolves against the target working directory and affects only that child-process specification. Both target and process modifiers accept a path
string or a required or defaulted string option handle.

Generic and typed process builders expose `.Timeout(...)` as a per-process policy. A timeout is owned by Rafter's runtime: it terminates the
process tree, settles redirected streams and cleanup, and then reports a distinct timeout failure. Timing out only the returned task is not a
supported substitute because that could leave child processes running. When `.Timeout(...)` is absent, healthy process execution has no implicit
deadline and continues until the process exits or the invocation is cancelled; bounded internal teardown periods are separate runtime safeguards.
Targets do not expose `.Timeout(...)`: arbitrary managed callbacks can only be cancelled cooperatively, so Rafter does not imply that it can
forcibly stop them.

Process `.WorkingDirectory(...)`, `.Timeout(...)`, `.CaptureLimitBytes(...)`, `.ValidExitCodes(...)`, and
`.Environment(...)` are single-valued. Duplicate calls preserve the first value and accumulate specification
diagnostics reported by terminal `.Run()` or `.Capture()` before launch. One `.Environment(...)` block contains the
complete ordered mutation sequence. Token appenders such as `.Argument(...)`, `.Flag(...)`, and `.Option(...)` remain
repeatable.

`ProcessBuilder` is an immutable, reusable process specification. Every fluent call derives a new builder without
changing its source, and every terminal `Run()` or `Capture()` starts a new independent process; terminal results are
never cached. The same builder may be launched concurrently within its owning target. It remains bound to the
invocation context that created it, and a terminal call after that invocation has settled fails clearly.
`processes.cs` makes that behavior visible by deriving its streaming and captured executions from one base builder;
it also passes a defaulted `Option<TimeSpan>` directly to `.Timeout(...)`.

Processes treat exit code `0` as successful by default. `.ValidExitCodes(...)` replaces that complete set, so authors include `0` explicitly
when it should remain successful alongside another tool-specific code. The method requires at least one code and normalizes duplicates.
Streaming `.Run()` returns a small Rafter-owned result containing the actual valid exit code without retaining stdout or stderr. Callers can
therefore distinguish valid nonzero outcomes without switching to capture mode. That result is the value type `ProcessExit`, whose only initial
property is `ExitCode`.

`.CaptureLimitBytes(long)` applies the same retained-data limit independently to stdout and stderr; it is not a combined budget. For example, a
2 MiB setting permits up to 2 MiB from each stream, or 4 MiB of retained child-process bytes in total. The byte count is measured before text
decoding. `Capture()` defaults to 1 MiB per stream; streaming `Run()` does not retain complete output. Exceeding a limit stops retaining that
stream but never stops draining it, so a full pipe cannot deadlock the child process. After the process settles, `Capture()` reports a distinct
capture-limit failure rather than returning normally with incomplete data; diagnostics identify the affected stream and limit without embedding
raw partial output.

Successful `Capture()` returns an immutable `ProcessCapture` with `ExitCode`, `StandardOutput`, and `StandardError`. It does not expose an
observed-order line transcript in the initial API: exact per-stream data is the programmatic capture contract, while streaming `Run()` already
presents lines in Rafter's observation order.
If capture completes within its bounds and decodes successfully but the exit code is invalid, the Rafter-owned exit
failure exposes that complete `ProcessCapture`. Startup, cancellation, timeout, capture-limit, and decoding failures
do not expose partial public capture, and streaming `Run()` never retains output merely to enrich an exception.
Exposed capture is exact raw application-owned program data so structured formats are not corrupted. Rafter never
presents it automatically; if the application sends it through `context.Output`, intercepted console output, or
another Rafter-managed channel, registered sensitive values are redacted there. Direct use outside those channels is
the application's responsibility.

Public process failures use a compact hierarchy rooted at `RafterException` and `ProcessException`, with dedicated
start, invalid-exit, timeout, and output exception types. Output failures distinguish capture-limit and strict-UTF-8
decoding reasons through an enum rather than additional exception classes. Unexpected process infrastructure uses
the process base type, underlying platform failures remain available as `InnerException`, and cancellation remains
standard `OperationCanceledException`. Rafter records application callback exceptions without changing their type
or identity; model, parsing, binding, and graph problems remain command diagnostics handled by `RunAsync`.

Redirected streams use strict UTF-8 decoding in the initial API. Invalid byte sequences produce a distinct decoding failure rather than silent
replacement, and no public encoding override is exposed until a concrete legacy-tool scenario justifies it.

Rafter never changes the process-wide current directory for an individual target because targets may execute concurrently. Direct `System.IO`
calls therefore do not inherit a target directory automatically; authors use `context.WorkingDirectory` or the context-owned filesystem facade
when they need that behavior.

## Filesystem safety

The initial context-owned filesystem facade mutates only paths contained beneath the command root. It normalizes each path before mutation and
rejects absolute or relative input that resolves outside that boundary. Authors that intentionally need another location select an appropriate
command root rather than bypassing the boundary.

`EnsureEmptyDirectory` additionally rejects a filesystem root, the command root, and the target working directory itself. It never follows a
symbolic link, junction, or other reparse point while cleaning, and rejects the target when an existing path component needed to reach it crosses
one. A link encountered inside the directory is removed as an entry without traversing its destination. These checks happen before any contents
are removed so a rejected request leaves the filesystem unchanged.

An API for explicitly opting into mutations outside the command root is deferred until a concrete automation scenario justifies its syntax and
safety contract.
