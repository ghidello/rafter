# Implementation phases

Each phase document is an executable checklist and completion contract. Work proceeds in order unless an earlier
phase explicitly leaves an integration seam for a later one.

| Phase | Plan | Outcome |
| --- | --- | --- |
| 1 | [Foundation](phase-01-foundation.md) | Reproducible product solution and package skeleton |
| 2 | [Command model](phase-02-command-model.md) ([evidence](phase-02-command-model-evidence.md)) | Immutable authored definitions matching the examples |
| 3 | [Parsing and binding](phase-03-parsing-and-binding.md) | Bounded grammar and exactly-once values |
| 4 | [Roots and filesystem](phase-04-roots-and-filesystem.md) | Scoped working directories and guarded mutations |
| 5 | [Graph execution](phase-05-graph-execution.md) | Deterministic concurrent target lifecycle |
| 6 | [Output and redaction](phase-06-output-and-redaction.md) | Safe target-aware presentation |
| 7 | [Process runtime](phase-07-process-runtime.md) | Deadlock-safe .NET 10 child-process execution |
| 8 | [Capture and typed tools](phase-08-capture-and-tools.md) | Process extensibility and tool-specific builders |
| 9 | [Conformance](phase-09-conformance.md) | Analyzers, package consumption, and portfolio lock |

A phase is complete only when every completion gate in its document is checked and its evidence is committed with
the implementation. Passing some tests or compiling the next phase is not a substitute for satisfying a gate.

Some phases begin with blocking design questions. Those questions must be analyzed, answered, and recorded before
implementation of that phase starts. The resulting decision may refine that phase's checklist and gates, but it must
not silently weaken an earlier product invariant or change the syntax portfolio.
