# Rafter implementation plan

This plan turns the syntax portfolio into the initial Rafter implementation. The examples are the public contract:
implementation choices may evolve without making the authored experience less clear or less safe.

The detailed checklists and completion gates are indexed in the [phase plans](phases/README.md).

## Product boundary

The first release is a library for .NET 10 file-based applications.

- Rafter has no external CLI, MCP server, `rafter.json`, recipe system, or command discovery.
- `Rafter.Command(...)` receives only the root policy; descriptions and other behavior are configured fluently.
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
- Establish generated-copy example validation without requiring an example to compile before its owning phase.

Completion requires a clean restore, build, test, and package from the repository root.

## [Phase 2: immutable command model](phases/phase-02-command-model.md)

Implement command, target, option, condition, cleanup, dependency, concurrency, root, and working-directory
definitions.

- Definitions become immutable before binding and execution.
- Duplicate names and invalid structural combinations fail deterministically.
- Callback overloads cover context-free and context-aware synchronous and asynchronous delegates.
- Boolean, `Func<bool>`, context-aware, and asynchronous conditions share one normalized internal representation.
- The semantics of `.ExpectsFailure<TException>()` are a blocking phase question and must be recorded before its
  model and execution behavior are implemented.
- Required and defaulted option handles can flow directly into context-owned APIs where their resolved type is valid.

Completion requires the construction portions of the syntax portfolio to compile and focused model tests to pass.

## [Phase 3: bounded parsing and exactly-once binding](phases/phase-03-parsing-and-binding.md)

Implement only the command grammar demonstrated by the examples.

- Support required, optional, defaulted, repeated, Boolean, enum, and common scalar option values.
- Apply command-line input, declared environment fallback, defaults, conversion, validation, and sensitive-value
  registration before graph execution.
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
- Aggregate concurrent failures deterministically.
- Apply the expected-failure outcome table approved in phase 2.
- Propagate cancellation and run target and command cleanup in the documented order.
- Preserve the distinction between executable, aggregate, and intentional no-op targets.

Completion requires dependency, diamond, cycle, concurrency, failure, cancellation, and cleanup-order tests.

## [Phase 6: output, console attribution, and redaction](phases/phase-06-output-and-redaction.md)

Implement semantic output and its Spectre.Console presentation.

- Support line, success, warning, error with recovery text, and named property output.
- Select appropriate rich or plain presentation for interactive and redirected environments.
- Attribute managed `Console.Out` and `Console.Error` writes to the active target across ordinary awaits,
  `Task.Run`, and `ConfigureAwait(false)` without corrupting concurrent target output.
- Redact registered sensitive values from semantic output, managed console output, child-process output, diagnostics,
  exception presentation, and command rendering.
- Keep the synthetic redaction example as a contract fixture; never use real credentials in tests.

Completion requires deterministic presentation snapshots plus concurrent console and cross-channel redaction tests.

## [Phase 7: .NET 10 process runtime](phases/phase-07-process-runtime.md)

Implement all generic and typed processes through one Rafter-owned runtime. Do not depend on the .NET 11 process
helpers until .NET 11 is final and separately evaluated.

### Construction

- Launch directly with `System.Diagnostics.Process`; do not invoke a shell.
- Add every token through `ProcessStartInfo.ArgumentList`.
- Compose executable, arguments, accepted exit codes, environment changes, working directory, output mode, capture
  limit, and cancellation policy into an immutable process specification.
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
- Preserve the difference between an ordinary nonzero exit, an accepted exit, cancellation, timeout, startup
  failure, capture-limit failure, and infrastructure failure in Rafter-owned results and diagnostics.

### Process verification matrix

Use deterministic fixture executables rather than platform shell commands. Cover:

- stdout only, stderr only, and simultaneous stdout/stderr;
- enough output on either or both streams to exceed operating-system pipe capacity;
- output below, at, and above each capture limit while proving the stream continues to drain;
- very long lines, partial writes, and an unterminated final line;
- stream ordering guarantees only where the runtime can honestly provide them;
- zero, nonzero, and explicitly accepted exit codes;
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

- Implement `Run`, bounded `Capture`, and `CaptureJson`.
- Require caller-supplied `JsonTypeInfo<T>` for JSON capture so the contract remains trimming- and AOT-friendly.
- Implement DotNet, Git, npm, and pnpm builders as argument-safe specification builders only.
- Give every typed process the same per-process working-directory override as the generic process builder.
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
