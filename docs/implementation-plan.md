# Rafter implementation plan

This plan turns the syntax portfolio into the initial Rafter implementation. The examples are the public contract:
implementation choices may evolve without making the authored experience less clear or less safe.

The detailed checklists and completion gates are indexed in the [phase plans](phases/README.md).

## Product boundary

The first release is a library for .NET 10 file-based applications.

- Rafter has no external CLI, MCP server, `rafter.json`, recipe system, or command discovery.
- `Rafter.Command(...)` receives only the root policy; descriptions and other behavior are configured fluently.
- Every command, target, and option requires a non-whitespace fluent description; Rafter does not invent one from
  an identifier.
- A command runs exactly one entry target. Targets are internal graph nodes, not command-line subcommands.
- Rafter owns its bounded command-line grammar and uses Spectre.Console for presentation.
- Source execution is the initial supported experience.
- Published, self-contained, and Native AOT execution remain deferred experiments.
- .NET 11 is reconsidered only after its final release. Public contracts must not expose implementation details that
  would prevent adopting finalized .NET 11 process APIs later.

## [Phase 1: repository and package foundation](phases/phase-01-foundation.md)

Create the product solution, library, analyzers, tests, deterministic fixtures, and packaging infrastructure.

- Pin a stable .NET 10 SDK at the repository root.
- Keep root `global.json`, `Directory.Build.props`, `Directory.Packages.props`, NuGet configuration, and solution files
  specific to the product build.
- Establish nullable analysis, warnings, deterministic builds, package metadata, and platform-aware test lanes.
- Establish direct-source example validation with project mode by default and an opt-in centrally versioned package
  mode, without requiring an example to compile before its owning phase.

Completion requires a clean restore, build, test, and package from the repository root.

## [Phase 2: immutable command model](phases/phase-02-command-model.md)

Implement command, target, option, condition, cleanup, dependency, concurrency, root, and working-directory
definitions.

- Definitions become immutable before binding and execution.
- Duplicate names, duplicate single-valued settings, and invalid structural combinations accumulate deterministic
  authored-order diagnostics and fail together at model freeze before parsing or execution.
- Command concurrency defaults to `1`; `.Concurrency(...)` accepts only positive values and explicitly opts into
  parallel target execution.
- A target holds no permit while waiting, then holds one permit across its ready-state condition, execution, and
  target cleanup; callback-free aggregate and no-op settlement consume none.
- Declare each target's complete dependency set through one `.DependsOn(params Target[])` call; report a second call
  or duplicate dependency at model freeze rather than accumulating or silently deduplicating.
- Callback overloads cover context-free and context-aware synchronous and asynchronous delegates.
- Boolean, `Func<bool>`, context-aware synchronous, and context-aware asynchronous conditions share one normalized
  internal representation. There is no context-free asynchronous condition overload.
- Multiple target conditions compose as an authored-order, at-most-once, short-circuit AND chain; false skips and a
  thrown condition fails before later conditions or execution.
- Target execution supports context-free synchronous and asynchronous callbacks as well as context-aware forms.
- Each target owns at most one execution callback; a second `.Run(...)` registration records a model diagnostic and
  preserves the first.
- Target and command cleanup support the same context-free and context-aware callback shapes.
- Target `.Finally(...)` requires a target execution callback; aggregate and no-op targets cannot own cleanup, while
  invocation-wide cleanup belongs to `command.Finally(...)`.
- Each target and command owns at most one cleanup callback; a second `.Finally(...)` registration records a model
  diagnostic and preserves the first.
- Duplicate cleanup registration has no initial analyzer rule because model-freeze validation is complete and occurs
  before invocation behavior, while static detection would be partial.
- Single-valued model settings preserve their first value and accumulate duplicate-setting diagnostics for formatted
  reporting at model freeze; fluent methods do not return `Result<T>` and duplicates never use last-write-wins.
- Expected exceptions remain application-owned control flow inside callbacks; Rafter has no target-level
  expected-exception policy.
- Required and defaulted option handles can flow directly into context-owned APIs where their resolved type is valid.
- Optional reference values resolve as nullable references, and optional value types require nullable type arguments;
  reject a non-nullable value-type option that is neither required nor defaulted at model freeze.
- Options support fluent binding-time validation with an authored failure message.
- Multiple validators compose in authored order and stop at the first failure for that option.
- Skip validators for complete optional absence; explicit empty values are present and validate, while required and
  defaulted options always validate.
- Treat `RequiredOption<string>` as a presence requirement only: an explicitly supplied empty command-line or
  environment value satisfies presence and must be rejected with `.Validate(...)` when the domain requires text.
- Validators are synchronous value predicates; asynchronous environmental checks belong in conditions or targets.
- Collect all ordinary input, conversion, and validation failures in deterministic token/declaration order while
  preserving the atomic binding barrier.
- A validator exception is an authoring failure with its original exception preserved, not an ordinary validation
  rejection; neither outcome crosses the binding barrier.
- A validator exception aborts binding immediately, retains previously collected safe input diagnostics, and does
  not invoke later converters or validators.
- `.Default(...)` accepts an already-computed value; deferred default factories are outside the initial API.

Completion requires the construction portions of the syntax portfolio to compile and focused model tests to pass.

## [Phase 3: bounded parsing and exactly-once binding](phases/phase-03-parsing-and-binding.md)

Implement only the command grammar demonstrated by the examples.

- Support required, optional, defaulted, Boolean, enum, and common scalar options plus explicit
  `RepeatedOption<T>` values.
- Convert `string` without transformation, handle Boolean and enum values with their dedicated grammars, unwrap
  nullable value types, and convert every other supported scalar through invariant-culture `IParsable<T>`.
- Parse enum values as one declared member name using ordinal case-insensitive matching; reject numeric values and
  implicit comma-separated `[Flags]` combinations. A flags enum can expose an explicitly declared composite member.
- Reject an enum option at model freeze when two declared member names collide under ordinal case-insensitive
  comparison; do not make exact casing select among an otherwise ambiguous folded match.
- Reject an option type that does not satisfy that conversion contract at model freeze; custom converters are
  outside v1, so applications can bind text and convert it inside a target when necessary.
- Parse a bare Boolean option as `true`, accept explicit `true` and `false` values, and do not synthesize `--no-*`
  aliases.
- Accept long-option values as either `--name value` or `--name=value`, splitting only on the first equals sign.
- Accept short aliases only as `-c value`, plus a bare Boolean alias for `true`; reject equals, attached-value, and
  bundled short forms.
- Match option names and aliases ordinally and case-sensitively on every platform; reject exact duplicates during
  command construction.
- Require authored option names without `--` in lowercase kebab-case, with no leading, trailing, or repeated hyphen.
- Require target names to use the same lowercase kebab-case format and ordinal case-sensitive identity; reject
  duplicates during command construction.
- Restrict `.Alias(char)` to lowercase ASCII letters and reject duplicate aliases or collisions with another
  option's one-character long name.
- Allow at most one short alias per option; a second `.Alias(...)` call records a model diagnostic and preserves the
  first.
- Bind one unsplit value per `RepeatedOption<T>` occurrence in occurrence order; reject repeated scalar options.
- Resolve absent `RepeatedOption<T>` as an empty immutable `IReadOnlyList<T>`; do not use `Option<T[]>` as the
  repeated-option contract.
- Reject the `--` end-of-options marker in v1 because there is no positional or pass-through tail; hyphen-leading
  values use `--name=value`.
- Report unknown options exactly without fuzzy suggestions or autocorrection in v1, never echo an attached value,
  and follow parse errors with the ordinary safe help/usage output. Revisit spelling suggestions post-v1.
- Present ordinary input failures in one section: token/syntax diagnostics in argument order followed by at most one
  conversion/validation diagnostic per option in declaration order, then render safe help once.
- Show bounded escaped invalid values only for non-sensitive known options, use `<redacted>` for sensitive values,
  and never show an unknown option's attached value. Display at most 20 diagnostics plus an omitted-count summary
  while retaining every collected diagnostic internally.
- Use rich headings/bullets or one physical `error: ...` line per diagnostic in plain mode; keep stable diagnostic
  kinds internal rather than displaying public codes in normal output.
- Reserve side-effect-free `--help` and `-h`, which succeed without binding or graph behavior; do not synthesize
  `--version` for file-based commands.
- Reserve the Rafter-owned long option names `plain` and `help` plus short alias `h`; reject authored collisions at
  model freeze.
- Render sensitive option metadata but never sensitive values in help; show invariant non-sensitive scalar defaults
  and do not enumerate mutable collection defaults.
- Lay help out as command description and usage followed by separate `Command options`, `Common options`, and
  `Targets` sections. Keep `--plain` and `-h, --help` in common options rather than mixing Rafter-owned syntax into
  the command-owned list.
- Treat the target section as a deterministic human/agent-readable execution manifest, not target-selection syntax:
  preserve declaration order and show each name, description, authored dependency order, entry marker, conditional
  marker, and callback-free `Aggregate` or `No work` shape.
- Derive the invocation name from the executable or file-based application, expose no command-name API in v1, and
  omit callback, cleanup, permit, and working-directory implementation details from help.
- Give an exact standalone `--help` or `-h` token successful precedence over all other tokens; embedded or malformed
  help-like text does not activate help.
- Apply command-line input, declared environment fallback, defaults, conversion, fluent validation, and sensitive-value
  registration before graph execution.
- Distinguish an absent environment variable from a set-but-empty value; empty command-line and environment strings
  are present data subject to conversion and validation, and are never registered as redaction patterns.
- Allow at most one `.FromEnvironment(...)` fallback per option; duplicate declarations follow the common
  model-freeze diagnostic policy.
- Validate fallback and child-process environment names with the same portable minimum: require non-whitespace text
  and reject NUL or `=`; preserve authored spelling and use the host operating system's lookup case semantics.
- Treat `.Default(...)` and `.Sensitive()` as single-valued option declarations; use fluent type-state to prevent
  duplicate defaults where practical and model-freeze diagnostics as the complete guard.
- Allow `.Sensitive()` on every scalar and repeated option type rather than restricting it to `string`.
- Register selected non-empty raw sensitive text before conversion and any distinct converted representation before
  validation so conversion and validator failures are redacted; apply this independently to every repeated value.
- Bind every option exactly once per invocation and store an immutable snapshot.
- Snapshot repeated or caller-owned mutable values.
- A binding failure prevents every condition, target, process, and cleanup callback from running.
- `context.Value(option)` and context-owned APIs read the same snapshot without repeating conversion, validation,
  environment reads, defaults, or caller collection access.

Completion requires parser diagnostics, help, binding precedence, sensitive binding, and exactly-once tests.

## [Phase 4: roots, working directories, and filesystem safety](phases/phase-04-roots-and-filesystem.md)

Implement invocation, source, and explicit roots plus target- and process-scoped working directories.

- Never change the process-wide current directory for an individual target.
- Resolve a relative target directory against the command root.
- Resolve a relative process override against the target working directory.
- Target cleanup uses its target directory; command cleanup uses the command root.
- Provide `EnsureDirectory` and `EnsureEmptyDirectory` through `context.FileSystem`.
- Normalize mutations and require containment beneath the command root.
- `EnsureEmptyDirectory` rejects filesystem roots, the command root, and the target working directory itself.
- Validate the complete path before deletion, reject traversal through links, junctions, or reparse points, and remove
  a link found inside the directory as an entry without traversing its destination.

Completion requires cross-platform path, containment, link, rejection-without-mutation, and working-directory tests.

## [Phase 5: graph planning and execution](phases/phase-05-graph-execution.md)

Validate and execute the target graph with deterministic lifecycle behavior.

- Detect unknown dependencies and cycles before callbacks run.
- Schedule ready targets up to the configured concurrency.
- Run each target at most once, including shared dependencies.
- Block dependents after a prerequisite fails while allowing already-running and independent work to settle.
- Treat condition-skipped prerequisites as successful enough for their dependents to proceed; apply a condition to
  a branch aggregate or entry target when the whole branch should be suppressed.
- Aggregate concurrent failures deterministically.
- Propagate cancellation and run target and command cleanup in the documented order.
- Qualify target cleanup only after its execution callback starts; skipped targets and condition failures do not run
  target cleanup, while command cleanup remains invocation-wide.
- Qualify command cleanup only after parsing, binding, graph preflight, and invocation initialization succeed; once
  qualified, run it exactly once on every terminal execution path.
- Start qualified cleanup with a dedicated token that is not pre-cancelled by invocation cancellation, and await
  each managed cleanup callback to settlement.
- Keep managed cleanup cooperative: Rafter cannot safely terminate or detach an arbitrary callback, so authors own
  any operation-specific deadline. Reserve hard bounded teardown guarantees for Rafter-owned resources such as
  child processes.
- Model execution failure or cancellation separately from cleanup failures in one unsuccessful command outcome;
  cleanup never masks the primary state, and cleanup-only failure still makes an otherwise successful command fail.
- Order target-cleanup failures by deterministic target plan order, followed by command cleanup, preserve every
  original exception, and present secondary cleanup failures under a distinct `Cleanup also failed` section.
- Keep escaped callback and cleanup exceptions in Rafter's internal outcome for classification, presentation, and
  verification without adding a structured public command-result API in v1. Authors needing programmatic handling
  catch exceptions inside their callback; `RunAsync` remains the integer-returning script entry point.
- Return stable command exit codes: `0` for success/help/skipped/no-op/explicitly valid process exits, `1` for
  execution, process, infrastructure, or cleanup failure, `2` for model/input/binding/validation/planning errors,
  and `130` for invocation cancellation.
- Classify `OperationCanceledException` as invocation cancellation only when the invocation token was actually
  requested; otherwise treat it as an ordinary callback failure. Treat process timeout as failure, not cancellation.
- Preserve the distinction between executable, aggregate, and intentional no-op targets.
- Treat a target with neither a callback nor dependencies as an implicit successful no-op and present it as
  completed with no work; do not add a `.NoOp()` marker or analyzer warning.

Completion requires dependency, diamond, cycle, concurrency, failure, cancellation, and cleanup-order tests.

## [Phase 6: output, console attribution, and redaction](phases/phase-06-output-and-redaction.md)

Implement semantic output and its Spectre.Console presentation.

- Support line, success, warning, error with recovery text, and named property output.
- Expose `Output.Property(string name, object? value)` and snapshot values synchronously as distinct null, empty
  string, scalar, multiline string, or immutable one-dimensional ordered collection states; never treat `string` as
  an enumerable collection.
- Format scalars invariantly. In rich output render `<null>`, `""`, readable vertical/inline collections, and
  indented multiline values; in plain output emit one physical `name=value` record using JSON-style literals and
  escaping.
- Enumerate a supplied collection exactly once, reject unsupported nested structures, and preserve an enumeration or
  formatting exception as a callback failure rather than silently substituting output.
- Route line, success, property, ordinary lifecycle/progress, successful help, managed stdout, and process stdout to
  stdout. Route warning, error/recovery, command diagnostics, failure/cancellation summaries, error-triggered usage,
  managed stderr, and process stderr to stderr.
- Keep `Output.Error(...)` semantic rather than control flow: emitting it does not fail a target. Preserve ordering
  independently within each stream without promising cross-stream order after independent redirection.
- Select appropriate rich or plain presentation for interactive and redirected environments.
- Make `--plain` force deterministic width-independent plain output on both streams with no ANSI, cursor/live
  control, or Unicode-only status glyphs. Otherwise evaluate stdout and stderr capabilities independently and make
  every redirected or incapable stream plain.
- Treat a non-empty `NO_COLOR` as color suppression within an otherwise capable rich renderer, not as forced plain
  layout; treat an empty value as unset and do not support `FORCE_COLOR` in v1.
- Present a deterministic final target summary in plan order. Use terminal states `Succeeded`, `Skipped`, `Failed`,
  `Cancelled`, and `Blocked`, with successful callback-free shapes `Aggregate` and `No work`; list direct blockers in
  authored dependency order.
- Allow rich live-only `Waiting`, `Running`, and `Cleaning up` states, but do not simulate transient states in plain
  output. Emit a successful final summary to stdout and an unsuccessful/cancelled summary plus details to stderr;
  omit durations in v1.
- Use distinct rich symbols that remain meaningful without color: `○` waiting/dim grey, `●` running/cyan, `◐`
  cleanup/cyan, `✓` success/green, `◇` aggregate/green, `–` no-work/grey, `↷` skipped/grey, `■`
  cancelled/yellow, `⊘` blocked/yellow, and `✗` failed/red. Reserve red for actual failures and use stable ASCII
  labels under `--plain`.
- Attribute managed `Console.Out` and `Console.Error` writes to the active target across ordinary awaits,
  `Task.Run`, and `ConfigureAwait(false)` without corrupting concurrent target output.
- Scope managed console attribution to target conditions/execution/cleanup when active, otherwise to the command
  during invocation work and command cleanup; expired target scopes fall back to command attribution.
- Install interception only for `RunAsync` and restore the host writers exactly afterward. Require callbacks to await
  spawned work: output that outlives `RunAsync` is application-owned and has no Rafter attribution/redaction promise.
- Treat `Console.SetOut` or `Console.SetError` during interception as unsupported global-state mutation: detect loss
  of either coordinating writer at callback/presentation boundaries, record an infrastructure failure, never adopt
  the replacement, and restore both exact entry writers in `finally`.
- Report only the affected stream. Do not inspect replacement content, and document that an undetectable temporary
  replace-and-restore lies outside the supported attribution/redaction boundary.
- Redact registered sensitive values from semantic output, managed console output, streamed child-process output,
  captured data sent back through Rafter-managed output, diagnostics, exception presentation, and command rendering.
- Return exact raw program data from `Capture()` and through the complete capture attached to an invalid-exit
  exception. Treat it as application-owned, never present it automatically, and redact it whenever the application
  sends it back through a Rafter-managed output channel. Direct application use outside those channels remains the
  caller's responsibility.
- Do not retain a second redacted capture solely for presentation. Keep raw retention bounded by the process capture
  policy, release execution-only buffers when the terminal operation settles, and let the application own the
  lifetime of returned capture strings.
- Keep the synthetic redaction example as a contract fixture; never use real credentials in tests.

Completion requires deterministic presentation snapshots plus concurrent console and cross-channel redaction tests.

## [Phase 7: .NET 10 process runtime](phases/phase-07-process-runtime.md)

Implement all generic and typed processes through one Rafter-owned runtime. Do not depend on the .NET 11 process
helpers until .NET 11 is final and separately evaluated.

### Construction

- Launch directly with `System.Diagnostics.Process`; do not invoke a shell.
- Add every token through `ProcessStartInfo.ArgumentList`.
- Compose executable, arguments, valid exit codes, environment changes, working directory, output mode, capture
  limit, public timeout, and cancellation policy into an immutable process specification.
- Make `ProcessBuilder` an immutable, reusable invocation-scoped value: fluent calls derive new builders, and every
  terminal `Run()` or `Capture()` starts an independent uncached process, including concurrent executions.
- Reject terminal execution clearly after the invocation that created the builder has settled.
- Preserve the `processes.cs` base-builder example as construction conformance for deriving independent streaming
  and capture specifications; use its defaulted `TimeSpan` option to cover invariant parsing and direct handle use.
- Treat process working directory, timeout, capture limit, valid-exit set, and environment block as single-valued;
  accumulate duplicate-setting diagnostics and fail terminal execution before launch while token appenders remain
  repeatable.
- Expose per-stream capture retention through `.CaptureLimitBytes(long)` and count bytes before decoding; exceeding
  the limit never stops either stream from being drained.
- Default capture retention to 1 MiB independently for stdout and stderr; streaming execution does not retain
  complete output.
- Treat either stream exceeding its retention limit as a distinct capture failure after safe process settlement;
  never return normally with silently truncated data or embed raw partial output in diagnostics.
- Keep `System.Diagnostics.Process` and its result types out of Rafter's public API.

### Deadlock-free output handling

The .NET 10 implementation owns the pipe-lifetime and draining rules explicitly.

1. Configure both stdout and stderr redirection before launch whenever Rafter must observe child output.
2. Start independent stdout and stderr drain operations immediately after a successful process start.
3. Never await process exit before both drains are active.
4. Await process exit and both drains before reporting ordinary completion.
5. Preserve stream identity, partial lines, and unterminated final lines.
6. Apply streaming redaction before presentation.
7. Enforce the default per-stream capture limit incrementally. After a capture limit is exceeded, continue draining
   and discarding that stream so the child cannot block on a full pipe.
8. Bound shutdown when a descendant inherits and retains a pipe handle after the tracked child exits. A retained
   handle must not make Rafter wait forever.

The implementation must settle every started read, wait, and termination operation and dispose all owned resources
on success, failure, cancellation, timeout, startup failure, and output-policy failure.

### Cancellation and termination

- Make cancellation-before-start, cancellation-during-start, natural exit, and cancellation-after-exit races
  deterministic.
- Attempt the documented graceful termination when the platform and child protocol support it.
- Resolve descendant ownership and retained-pipe behavior before implementation; do not treat direct-process exit as
  proof that descendants terminated.
- After a bounded grace period, apply the approved forced-termination strategy and verify only the survivor guarantee
  that strategy can actually provide.
- A retained pipe handle or uncooperative descendant must not prevent bounded cancellation.
- Preserve the difference between an ordinary nonzero exit, an explicitly valid nonzero exit, cancellation, timeout, startup
  failure, capture-limit failure, and infrastructure failure in Rafter-owned results and diagnostics.
- Keep the public thrown hierarchy small: `RafterException`, `ProcessException`, and dedicated
  `ProcessStartException`, `ProcessExitException`, `ProcessTimeoutException`, and `ProcessOutputException` types.
  Use `ProcessException` itself for otherwise unclassified process-infrastructure failures.
- Give `ProcessOutputException` a reason enum for capture-limit and strict-UTF-8 decoding failures plus only safe
  structured metadata; preserve underlying platform exceptions as `InnerException` where one exists.
- Represent cancellation with standard `OperationCanceledException`, and preserve application callback exception
  type and identity rather than wrapping it.
- Return `ProcessExit`, an allocation-free Rafter-owned value containing the actual valid exit code, from streaming
  `Run()` without retaining either output stream.
- Return immutable `ProcessCapture` from `Capture()` with `ExitCode`, exact `StandardOutput`, and exact
  `StandardError`; do not add an observed-order public line transcript in v1.
- When `Capture()` reaches a fully drained, bounded, successfully decoded but invalid exit, attach that complete
  `ProcessCapture` to the Rafter-owned exit failure. Do not expose partial capture for startup, cancellation,
  timeout, capture-limit, or decoding failures, and do not retain output solely to enrich streaming `Run()` failures.
- Preserve decoded stream newline sequences and unterminated final content in `ProcessCapture`; presentation
  normalization must not alter captured program data.
- Decode redirected streams as strict UTF-8, report invalid byte sequences as a distinct failure, and defer public
  encoding overrides until a concrete legacy-tool scenario exists.

### Process verification matrix

Use deterministic fixture executables rather than platform shell commands. Cover:

- stdout only, stderr only, and simultaneous stdout/stderr;
- enough output on either or both streams to exceed operating-system pipe capacity;
- output below, at, and above each capture limit while proving the stream continues to drain;
- very long lines, partial writes, and an unterminated final line;
- stream ordering guarantees only where the runtime can honestly provide them;
- zero, nonzero, and explicitly valid nonzero exit codes;
- working-directory and environment inheritance and overrides;
- cancellation before start, during execution, during heavy output, and immediately after natural exit;
- a child that acknowledges cancellation and one that ignores it;
- child and grandchild process trees;
- a descendant that retains stdout or stderr after the direct child exits;
- multiple concurrent process launches with redirected output;
- sensitive values in arguments, stdout, stderr, and failures;
- disposal and absence of surviving fixture processes after every test.

Completion requires this matrix to pass reliably on every supported operating system, with platform-specific
expectations stated explicitly rather than hidden by retries.

## [Phase 8: capture and typed tools](phases/phase-08-capture-and-tools.md)

Build convenience APIs on the generic process runtime.

- Implement `Run` and bounded `Capture` as the complete core process completion modes.
- Keep JSON deserialization application-owned and prove the public process surface is extensible with the portfolio's
  `CaptureJson` extension example.
- Use the ordinary public `ProcessBuilder` returned by `context.Process(...)` as the extension receiver; do not add a
  separate extensibility interface or privileged hook.
- Implement DotNet, Git, npm, and pnpm builders as argument-safe specification builders only.
- Give every typed process the same per-process working-directory override as the generic process builder.
- Give every typed process the same per-process timeout policy as the generic process builder.
- Apply no implicit execution timeout when `.Timeout(...)` is absent; internal drain and teardown deadlines remain
  separate runtime safety policies.
- Keep timeout on process builders only; targets expose cooperative invocation cancellation but no target timeout.
- Do not duplicate execution, cancellation, output, redaction, or failure policy in a typed integration.

Completion requires every process and typed-tool example to compile and exercise the shared runtime in tests.

## [Phase 9: analyzers and portfolio conformance](phases/phase-09-conformance.md)

Add analyzers only where they provide timely, unambiguous guidance that runtime validation cannot provide as well.

- Keep analyzer dependencies and runtime dependencies separate.
- Compile the complete example portfolio as contract fixtures.
- Assert public API shape so implementation refactoring cannot silently change the designed syntax.
- Run package-consumer tests against the produced package rather than only project references.

Completion requires the complete portfolio, analyzer tests, public API checks, package-consumer tests, and product
test suite to pass together.

## Deferred spikes

After the corresponding technology is stable enough to evaluate, investigate:

- cold, unchanged warm, and changed-source file-based execution;
- framework-dependent and self-contained publication;
- Native AOT compatibility, size, startup, and repeated execution;
- .NET 10 versus final .NET 11 behavior where the comparison remains useful;
- finalized .NET 11 Process APIs as an internal backend, without changing Rafter-owned public contracts.

No spike result becomes product policy until it is recorded in this plan or a subsequent accepted decision. Its
repository layout will be decided when we are ready to run it.

## Post-v1 API reviews

- Revisit whether real consumers need an explicit opt-in mode that returns clearly marked truncated capture results.
  The initial API fails on truncation and must not gain silent-success behavior.
- Revisit an observed-order captured transcript only when a real consumer justifies contracts for cross-pipe
  observation order, line endings, partial final lines, very long lines, and additional retained memory.
- Revisit output-encoding configuration only for a concrete supported tool that cannot emit UTF-8.
