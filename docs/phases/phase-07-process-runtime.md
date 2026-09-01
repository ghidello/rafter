# Phase 7: .NET 10 process runtime

## Objective

Implement one argument-safe, deadlock-safe, cancellation-safe child-process runtime on .NET 10. It must support
streamed execution and the bounded capture foundation required by phase 8 without leaking BCL process types into
Rafter's public API.

## Questions to resolve before implementation

- [ ] What ownership guarantee does Rafter make for descendants after the direct child exits, especially when a
      descendant retains stdout or stderr and prevents EOF?
- [ ] Can the required guarantee be implemented proactively on .NET 10 with Windows job objects, Unix process
      groups/sessions, or another supervised-launch mechanism on every supported platform?
- [ ] If the direct child has already exited and the remaining descendants can no longer be identified reliably,
      should Rafter close/cancel its drains and report incomplete output, fail the operation, or require stronger
      process-group ownership from launch time?
- [ ] Does `Kill(entireProcessTree: true)` satisfy only best-effort teardown, and how will Rafter verify descendant
      termination rather than treating direct-process `WaitForExit` as proof?
- [ ] Record the ownership model, platform implementation, retained-pipe outcome, and survivor guarantee before
      building the process runtime.

## Fixed invariants

- No shell is involved.
- Every argument is a distinct `ProcessStartInfo.ArgumentList` token.
- stdout and stderr are drained concurrently whenever redirected.
- Waiting for exit never begins as a sequential substitute for draining either stream.
- Capture limits bound retained data, not draining; a full pipe must never be the child's reason for hanging.
- Cancellation eventually uses `Kill(entireProcessTree: true)` after any bounded graceful period.
- .NET 11 Process APIs are out of scope until .NET 11 is final.

## Implementation checklist

### Public and internal model

- [ ] Implement the generic process fluent surface demonstrated by `processes.cs`, `environment.cs`,
      `working-directory.cs`, `redaction.cs`, and `process-cancellation.cs`.
- [ ] Normalize executable, argument tokens, valid exits, environment edits, working directory, stream mode,
      capture limit, public timeout, and cancellation policy into an immutable specification.
- [ ] Make every fluent modifier return a new `ProcessBuilder` without mutating its source; keep generic process
      builders reusable rather than consuming them at a terminal operation.
- [ ] Make every `Run()` and `Capture()` invocation launch an independent process with no cached completion or
      output, and support concurrent terminal calls on the same builder safely.
- [ ] Compile and execute the base-builder reuse and defaulted `Option<TimeSpan>` timeout syntax in `processes.cs`.
- [ ] Tie execution authority to the creating invocation and reject a terminal call made after that invocation has
      settled with a clear Rafter-owned failure.
- [ ] Allow one working directory, timeout, capture limit, valid-exit declaration, and environment block per process
      specification; preserve first values and accumulate duplicate-setting diagnostics.
- [ ] Validate all accumulated specification diagnostics at terminal `Run()` or `Capture()` and launch nothing on
      failure; keep argument, flag, and option token appenders repeatable.
- [ ] Default valid exit codes to `{ 0 }`; make `.ValidExitCodes(params int[] codes)` replace the complete set, require
      at least one code, and normalize duplicates.
- [ ] Implement `.Timeout(TimeSpan)` on the generic builder and reject non-positive or otherwise unsupported values
      before attempting launch.
- [ ] Apply no execution timeout by default; distinguish authored execution timeouts from bounded internal drain,
      graceful-termination, forced-kill, and retained-handle deadlines.
- [ ] Resolve option handles from the invocation snapshot exactly once; never retain unresolved handles in the
      runtime specification.
- [ ] Validate environment edit keys before launch with the fallback-name rules: reject empty or whitespace-only
      text, NUL, and `=`, preserve authored spelling, and rely on the host operating system's case semantics.
- [ ] Validate empty executable names, capture limits, and working directories before attempting launch.
- [ ] Implement `.CaptureLimitBytes(long)` as a positive per-stream retained-byte limit measured before decoding.
- [ ] Default capture retention to 1 MiB independently for stdout and stderr; do not apply that retention policy to
      streaming `Run()`.
- [ ] Implement public `RafterException` and `ProcessException`, with dedicated `ProcessStartException`,
      `ProcessExitException`, `ProcessTimeoutException`, and `ProcessOutputException` derived types; allow the process
      base to represent otherwise unclassified infrastructure failures without adding a type per internal stage.
- [ ] Give `ProcessOutputException` a stable reason enum covering capture-limit overflow and strict-UTF-8 decoding,
      together with safe stream/limit metadata where applicable.
- [ ] Preserve original platform failures as `InnerException`, avoid raw captured text in exception messages, and
      represent cancellation with standard `OperationCanceledException` carrying the relevant token.
- [ ] Expose the invalid exit code from `ProcessExitException` and, in capture mode only, the complete capture
      permitted by the phase-8 availability and phase-6 trust-boundary contracts.
- [ ] Keep failure output ownership mode-specific: streaming execution retains no output for exceptions, while
      capture may attach only a complete, bounded, successfully decoded result after an invalid exit.
- [ ] Preserve safe executable/argument diagnostics while redacting sensitive values.

### Launch and ownership

- [ ] Set `UseShellExecute = false` and populate `ArgumentList` token by token.
- [ ] Apply the normalized absolute working directory and environment changes without mutating parent state.
- [ ] Configure redirection consistently for stream and capture modes before start.
- [ ] Record ownership of the `Process`, readers/streams, cancellation registrations, timers, and drain tasks.
- [ ] Keep execution-owned state local to one terminal call so two launches from the same immutable builder cannot
      share process handles, buffers, timers, registrations, or completion state.
- [ ] Handle `Process.Start()` returning false or throwing without starting drains or leaking registrations.
- [ ] Obtain the process ID only after successful start and tolerate rapid natural exit.

### Concurrent stream draining

- [ ] Start independent stdout and stderr drains immediately after successful launch.
- [ ] Ensure both drain operations are created before any exit wait is awaited.
- [ ] Decode incrementally as strict UTF-8 without splitting multibyte characters incorrectly.
- [ ] Report invalid UTF-8 as a distinct decoding failure without emitting replacement characters or raw invalid
      bytes; expose no public encoding override in v1.
- [ ] Preserve stdout/stderr identity and unterminated final content.
- [ ] Route complete and partial lines through target-aware output without merging concurrent streams accidentally.
- [ ] Preserve bounded capture as exact raw application-owned data, while always redacting streaming presentation,
      diagnostics, exception rendering, and any capture text sent back through Rafter-managed output.
- [ ] Count captured bytes before decoding and enforce the configured limit separately for each stream.
- [ ] On first limit exceedance, record the policy failure and stop retaining additional content for that stream.
- [ ] Continue reading and discarding both streams until normal termination or bounded shutdown.
- [ ] After safe settlement, report a distinct capture-limit failure even when the process exits with a valid code;
      never return normally with silently truncated output.
- [ ] Identify the affected stream and configured byte limit without embedding raw partial output in diagnostics.
- [ ] Do not build an unbounded line buffer; define bounded handling for a single line larger than the capture limit.

### Exit and retained handles

- [ ] Await direct-process exit independently from EOF on redirected streams.
- [ ] On ordinary exit, allow a short documented drain-completion window for final buffered data.
- [ ] Detect when the direct process exited but EOF is withheld by a descendant retaining a pipe handle.
- [ ] After the bounded window, apply the recorded retained-handle decision and settle/cancel drain operations
      without relying on an already-exited direct process to prove descendant termination.
- [ ] Classify retained-handle termination distinctly enough for diagnostics without exposing implementation details.
- [ ] Never report success before output accepted by the contract is drained or a bounded policy decision is made.

### Cancellation and race handling

- [ ] Return cancellation without launch when the token is already cancelled.
- [ ] Serialize or atomically coordinate start, natural exit, cancellation, timer, and kill decisions.
- [ ] Treat expiration of the authored timeout as a distinct outcome while using the same bounded tree-termination,
      drain-settlement, and resource-cleanup machinery as external cancellation.
- [ ] Make exactly one path responsible for graceful termination and forced tree kill.
- [ ] Define the graceful protocol per supported platform/fixture; skip it when unavailable rather than pretending
      `CloseMainWindow` is portable.
- [ ] Bound the graceful period and forced-kill wait independently.
- [ ] Call `Kill(entireProcessTree: true)` defensively and tolerate already-exited races.
- [ ] Continue settling drains after cancellation so redirected pipes do not strand tasks.
- [ ] Preserve external cancellation as cancellation even if teardown produces secondary exceptions.
- [ ] Surface teardown failures when they materially mean the process tree may still be alive.

### Disposal and observability

- [ ] Dispose every owned resource exactly once on all terminal paths.
- [ ] Avoid `async void`, unobserved tasks, fire-and-forget drains, and blocking waits on async operations.
- [ ] Add internal lifecycle events or injectable observers sufficient for deterministic race tests.
- [ ] Keep user-visible timing and process IDs out of deterministic snapshots unless explicitly normalized.

## Deterministic process fixture checklist

- [ ] Emit configurable stdout and stderr bytes/lines independently and simultaneously.
- [ ] Emit more data than typical Windows and Unix pipe capacities.
- [ ] Emit partial writes, multibyte characters, very long lines, and no-final-newline output.
- [ ] Exit with a requested code and optional delay.
- [ ] Report working directory, arguments, and selected environment values as structured fixture data.
- [ ] Wait while acknowledging or ignoring the test cancellation protocol.
- [ ] Spawn a child/grandchild and report their PIDs through a safe fixture channel.
- [ ] Exit while a descendant deliberately retains stdout or stderr.
- [ ] Provide a reliable sentinel for verifying every spawned fixture process has terminated.

## Required verification

- [ ] Exercise stdout-only, stderr-only, and simultaneous high-volume output repeatedly.
- [ ] Prove the historical sequential-read deadlock pattern completes under the Rafter runtime.
- [ ] Test retained data below, exactly at, and above both stream limits.
- [ ] Prove limit exceedance still drains enough for the fixture to reach and record natural exit.
- [ ] Test zero, nonzero, explicitly valid nonzero, and rapidly exiting processes.
- [ ] Test executable and argument values containing spaces, quotes, empty strings, and shell metacharacters.
- [ ] Test environment add/replace/remove and target/process working-directory precedence.
- [ ] Test every cancellation race and retained-descendant scenario under strict test deadlines.
- [ ] Launch many redirected processes concurrently to expose thread-pool, pipe, and disposal issues.
- [ ] Scan output, diagnostics, and exceptions for disposable secrets.
- [ ] After every test, verify the survivor guarantee selected by the recorded ownership model; clean up fixture PIDs
      independently so a failed assertion cannot pollute later tests.

## Completion gates

- [ ] **R1 — Argument safety:** hostile-token fixtures receive the exact authored argument vector without shell
      interpretation.
- [ ] **R2 — Deadlock resistance:** simultaneous output beyond both pipe capacities completes on all supported OSes.
- [ ] **R3 — Bounded capture:** retained memory follows per-stream limits while fixtures prove pipes continue draining.
- [ ] **R4 — Retained-handle bound:** descendant-held pipes settle within the documented deadline.
- [ ] **R5 — Cancellation bound:** graceful and forced scenarios meet the recorded descendant-ownership and survivor
      guarantee.
- [ ] **R6 — Race determinism:** repeated start/exit/cancel races produce only documented outcomes and no unobserved
      exceptions.
- [ ] **R7 — Resource closure:** stress tests leave no fixture process, running drain, registration, or undisposed
      process resource detectable by the harness.
- [ ] **R8 — Cross-platform contract:** platform-specific differences are documented and CI-tested, not retry-hidden.
- [ ] **R9 — Evidence recorded:** process state machine, timeout values, fixture protocol, and matrix results are
      committed with the phase completion record.
- [ ] **R10 — Retained-handle decision:** the approved ownership strategy and incomplete-output behavior pass their
      platform-specific retained-descendant tests.

## Non-goals

No shell command strings, stdin API, arbitrary process pipelines, detached/fire-and-forget processes, terminal
emulation, pseudo-terminal support, raw handles, or public `System.Diagnostics.Process` escape hatch is included.
