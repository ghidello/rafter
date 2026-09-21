# Implementation phases

Each phase document is an executable checklist and completion contract. Work proceeds in order unless an earlier
phase explicitly leaves an integration seam for a later one.

The [closeout audit](phase-05-07-closeout.md) distinguishes local evidence from CI requirements.
Phases 1–6 are closed. [CI 19](https://github.com/ghidello/rafter/actions/runs/35646084394) passed the Phase 5–7
implementation on Windows, Ubuntu and macOS, including package integrity. The process-tree timeout stall recurred
in [CI 20](https://github.com/ghidello/rafter/actions/runs/35647010649), reopening Phase 7 gates R5, R8 and R11.
Phases 8–9 remain planned.

| Phase | Plan | Outcome |
| --- | --- | --- |
| 1 | [Foundation](phase-01-foundation.md) | Reproducible product solution and package skeleton |
| 2 | [Command model](phase-02-command-model.md) ([evidence](phase-02-command-model-evidence.md)) | Immutable authored definitions matching the examples |
| 3 | [Parsing and binding](phase-03-parsing-and-binding.md) | Bounded grammar and exactly-once values |
| 4 | [Roots and filesystem](phase-04-roots-and-filesystem.md) | Scoped working directories and guarded mutations |
| 5 | [Graph execution](phase-05-graph-execution.md) ([evidence](phase-05-graph-execution-evidence.md)) | Deterministic concurrent target lifecycle |
| 6 | [Output and redaction](phase-06-output-and-redaction.md) ([evidence](phase-06-output-and-redaction-evidence.md)) | Safe target-aware presentation |
| 7 | [Process runtime](phase-07-process-runtime.md) ([evidence](phase-07-process-runtime-evidence.md)) | Deadlock-safe .NET 10 child-process execution |
| 8 | [Typed tools and process extensibility](phase-08-capture-and-tools.md) | Process extensibility and tool-specific builders |
| 9 | [Conformance](phase-09-conformance.md) | Analyzers, package consumption, and portfolio lock |

A phase is complete only when every completion gate in its document is checked and its evidence is committed with
the implementation. Passing some tests or compiling the next phase is not a substitute for satisfying a gate.

Some phases begin with blocking design questions. Those questions must be analyzed, answered, and recorded before
implementation of that phase starts. The resulting decision may refine that phase's checklist and gates, but it must
not silently weaken an earlier product invariant or change the syntax portfolio.
