# Phase 8: capture and typed tools

## Objective

Finish the process-facing user experience with bounded capture, source-generated JSON capture, and typed DotNet,
Git, npm, and pnpm builders that remain thin specification layers over phase 7.

## Architectural rule

Every typed builder must end in the same immutable generic process specification and runtime. A typed integration
may improve syntax and validation; it may not own another launch, output, cancellation, redaction, or failure path.

## Implementation checklist

### Generic completion modes

- [ ] Import and enforce the capture/redaction trust-boundary decision approved in phase 6.
- [ ] Implement `Run()` for live target-aware output and a Rafter-owned execution result where required.
- [ ] Implement `Capture()` returning stdout, stderr, and Rafter-owned exit information.
- [ ] Apply the phase-7 default per-stream capture limit and `.CaptureLimit(...)` override.
- [ ] Define whether captured text preserves original newline sequences or normalizes them, then test that contract.
- [ ] Preserve partial output on nonzero exit, cancellation, and capture-limit failure when safe and useful.
- [ ] Keep accepted exit-code evaluation identical between run and capture modes.

### JSON capture

- [ ] Implement `CaptureJson<T>(JsonTypeInfo<T>)` using captured stdout only.
- [ ] Require a non-null caller-supplied `JsonTypeInfo<T>`; do not add reflection-based convenience overloads.
- [ ] Define handling for empty output, leading/trailing whitespace, malformed JSON, `null`, trailing content, stderr,
      nonzero exits, and capture-limit failure.
- [ ] Wrap JSON failures with safe process context while preserving the original parsing exception.
- [ ] Ensure raw sensitive JSON cannot leak through exception messages or diagnostic excerpts.
- [ ] Confirm the path produces no trimming or Native AOT warnings attributable to reflection-based serialization.

### Shared typed-builder foundation

- [ ] Create internal token-building primitives for flags, arguments, options, repeated values, and optional omission.
- [ ] Accept required/defaulted option handles only where context resolution is guaranteed.
- [ ] Preserve authored call order unless a tool's grammar requires a documented canonical order.
- [ ] Delegate working directory, environment, accepted exits, capture mode, and execution to the generic builder.
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
- [ ] Apply target and per-process working directories identically to generic and other typed builders.
- [ ] Do not infer package manager selection or install tools automatically.

## Required verification

- [ ] Run/Capture parity tests cover exit classification, redaction, environment, working directory, and cancellation.
- [ ] Test capture at boundary sizes and with independently overflowing stdout and stderr.
- [ ] Test JSON success for source-generated metadata plus every defined malformed/empty/null/trailing case.
- [ ] Run trimming analysis and a focused publish check for the JSON path without claiming full AOT support.
- [ ] Snapshot every typed example's executable and exact argument vector.
- [ ] Execute typed builders against deterministic argument-echo fixtures where real tools are unnecessary.
- [ ] Add a small opt-in integration lane for installed real tools, excluded from correctness gates unless CI pins them.
- [ ] Prove all typed paths use the same phase-7 runtime observer and failure types.

## Completion gates

- [ ] **T1 — Mode parity:** Run, Capture, and CaptureJson share exit, cancellation, redaction, and working-directory
      semantics.
- [ ] **T2 — Capture bound:** both streams obey limits with documented partial-result behavior.
- [ ] **T3 — JSON contract:** source-generated success and complete failure matrix pass without reflection fallback.
- [ ] **T4 — Exact vectors:** approved executable/argument snapshots cover every typed example.
- [ ] **T5 — One runtime:** architecture tests prove typed integrations cannot bypass the generic executor.
- [ ] **T6 — Portfolio execution:** process, environment, DotNet, Git, Node, repository, and working-directory examples
      compile and reach their intended runtime paths.
- [ ] **T7 — Evidence recorded:** capture semantics table, JSON failure table, and typed-vector snapshots are committed.

## Non-goals

Typed builders are not complete wrappers for their external tools. No tool installation, version management, shell
fallback, stdin, pipeline, or undocumented pass-through string API is added.
