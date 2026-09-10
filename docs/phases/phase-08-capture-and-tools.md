# Phase 8: typed tools and process extensibility

## Objective

Build typed DotNet, Git, npm, and pnpm builders as thin specification layers over Phase 7's completed generic
`Run()` and bounded `Capture()` modes, while proving applications can add their own fluent conveniences.

## Architectural rule

Every typed builder must end in the same immutable generic process specification and runtime. A typed integration
may improve syntax and validation; it may not own another launch, output, cancellation, redaction, or failure path.

## Implementation checklist

### Generic completion-mode conformance

- [ ] Verify exact raw application-owned data from `Capture()` is not presented automatically and invocation-wide
      registered sensitive values are redacted when an extension or typed tool sends it through a Rafter-managed
      output channel; process-local literal sensitivity is not retroactive taint tracking.
- [ ] Verify typed tools and extensions preserve `Run()` live output and its allocation-free `ProcessExit` without
      retaining stdout or stderr.
- [ ] Verify typed tools and extensions preserve immutable `ProcessCapture`, exact stream data, the absence of an
      observed-order transcript, and the Phase 7 default or overridden per-stream capture limit.
- [ ] Verify typed tools and extensions preserve original decoded newlines and unterminated final content rather
      than normalizing captured program data for presentation.
- [ ] Verify an invalid exit after complete bounded capture exposes the full `ProcessCapture` through the shared
      Rafter-owned exit failure.
- [ ] Keep typed builders and application extensions on the same public process exception hierarchy; do not wrap a
      generic runtime failure in a tool-specific exception.
- [ ] Verify no typed tool or extension exposes partial capture for startup failure, cancellation, timeout,
      capture-limit overflow, retained-pipe termination, or decoding failure.
- [ ] Verify no typed tool or extension makes streaming `Run()` retain stdout or stderr solely to enrich a failure.
- [ ] Keep the complete capture attached to an invalid-exit failure raw and application-owned, while ensuring normal
      exception rendering exposes only safe metadata and no captured text.
- [ ] Keep valid-exit-code evaluation identical between run and capture modes.

### Public extensibility

- [ ] Keep the concrete `ProcessBuilder` returned by `context.Process(...)` and every fluent modifier, plus its
      capture result, public without exposing execution-engine internals.
- [ ] Preserve the immutable, reusable semantics of the generic builder through every typed builder and extension;
      deriving or executing one specification must not mutate or consume another.
- [ ] Use that ordinary concrete builder as the extension receiver; do not introduce a separate extensibility
      interface or privileged hook.
- [ ] Compile and execute the application-owned `CaptureJson` extension demonstrated by `extensibility.cs`.
- [ ] Ensure an extension can compose existing completion behavior without bypassing capture limits, valid exit
      codes, cancellation, working-directory handling, or process cleanup.
- [ ] Keep JSON parsing, serialization metadata, null handling, and JSON-specific failures outside Rafter's core API.

### Shared typed-builder foundation

- [ ] Create internal token-building primitives for flags, arguments, options, repeated values, and optional omission.
- [ ] Accept required/defaulted option handles only where context resolution is guaranteed and enforce the active
      invocation's phase-2 ownership identity before resolution.
- [ ] Preserve authored call order unless a tool's grammar requires a documented canonical order.
- [ ] Apply the phase-7 single-valued process-policy and repeatable-token rules identically to typed builders.
- [ ] Verify a typed builder can derive multiple variants and launch each variant independently, including
      concurrent terminal calls where the owning target permits them.
- [ ] Delegate working directory, environment, valid exits, timeout, capture mode, and execution to the generic
      builder.
- [ ] Test typed-builder output as exact executable plus argument-vector snapshots before launching fixtures.

### DotNet builder

- [ ] Implement only commands and modifiers demonstrated by `dotnet.cs` and `repository.cs`.
- [ ] Cover restore/build/test/pack syntax, solution/project arguments, configuration, output, and shown switches.
- [ ] Keep values tokenized; never concatenate an option and value into a shell-style string.
- [ ] Define executable resolution (`dotnet` by default) without probing during builder construction.

### Git builder

- [ ] Implement only operations demonstrated by `git.cs`.
- [ ] Preserve message/tag values as single tokens even with spaces or metacharacters.
- [ ] Support capture for revision lookup through the shared capture result.
- [ ] Avoid implicit repository discovery beyond the configured working directory.

### npm and pnpm builders

- [ ] Implement only operations demonstrated by `node.cs`.
- [ ] Preserve the separator rules required when forwarding script arguments.
- [ ] Apply target and per-process working directories and timeouts identically to generic and other typed builders.
- [ ] Do not infer package manager selection or install tools automatically.

## Required verification

- [ ] Run/Capture shared-policy tests cover exit classification, environment, working directory, timeout, and
      cancellation. Separate trust-boundary tests prove streaming output is redacted, capture remains raw, and raw
      capture containing an invocation-wide sensitive option is redacted if it re-enters a Rafter-managed output
      channel.
- [ ] Test capture at boundary sizes and with independently overflowing stdout and stderr.
- [ ] Test that the extensibility example consumes the ordinary public capture result and requires no privileged API.
- [ ] Snapshot every typed example's executable and exact argument vector.
- [ ] Execute typed builders against deterministic argument-echo fixtures where real tools are unnecessary.
- [ ] Add a small opt-in integration lane for installed real tools, excluded from correctness gates unless CI pins them.
- [ ] Prove all typed paths use the same phase-7 runtime observer and failure types.

## Completion gates

- [ ] **T1 — Mode contract:** Run and Capture share exit, cancellation, timeout, environment, and working-directory
      semantics while enforcing their distinct streaming-redaction and raw-capture responsibilities.
- [ ] **T2 — Capture bound:** both streams obey limits, continue draining after overflow, and expose no partial public
      result on output-policy failure.
- [ ] **T3 — Public extensibility:** the application-owned `CaptureJson` example compiles and runs using only supported
      public process APIs.
- [ ] **T4 — Exact vectors:** approved executable/argument snapshots cover every typed example.
- [ ] **T5 — One runtime:** architecture tests prove typed integrations cannot bypass the generic executor.
- [ ] **T6 — Portfolio execution:** process, environment, DotNet, Git, Node, repository, and working-directory examples
      compile and reach their intended runtime paths.
- [ ] **T7 — Evidence recorded:** capture semantics table, JSON failure table, and typed-vector snapshots are committed.

## Non-goals

Typed builders are not complete wrappers for their external tools. No tool installation, version management, shell
fallback, stdin, pipeline, or undocumented pass-through string API is added.

After v1 usage evidence exists, revisit whether an explicit opt-in truncated-result mode is justified. Silent
truncation remains outside the contract.

Also revisit an observed-order capture transcript only after a concrete consumer establishes the required ordering,
line-boundary, and memory semantics.
