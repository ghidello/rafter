# Rafter first working examples

Proposed implementation milestone, following a consistency review of the seven current example drafts.

The examples form a coherent first implementation scope: declare a command and inputs, validate them, execute a body
or a target graph, and run external tools with explicit failure handling. The public syntax fits together without
requiring another broad redesign. Some observable behavior still needs decisions before its implementation begins.

The [example design plan](example-design-plan.md) remains the source of agreed API behavior. Recommendations below
are proposals, not additional accepted requirements. The [minimal runtime](minimal-runtime.md) implements body-only
invocations; typed inputs, targets, processes, analyzers, and packaging remain pending.
The [dependency review](dependency-decisions.md) records the initial shortlist and compatibility checks to complete
before adopting packages. Dependency selection is part of readiness, not an incidental scaffolding choice.

## Review of the current drafts

| Draft | Purpose and consistency finding |
| --- | --- |
| [minimal.cs](../examples/minimal.cs) | Runnable with a project reference to the new runtime: required description, direct output, and a body supplied to `RunAsync`. |
| [options.cs](../examples/options.cs) | Required and defaulted inputs read through `GetValue`. No extra target or stored command body is needed. |
| [validation.cs](../examples/validation.cs) | Individual rules, cross-input rules, and an omitted optional input fit in one example. Ordinary rejection and thrown-validator behavior are specified. |
| [processes.cs](../examples/processes.cs) | An awaited process whose result can be ignored. The completion message demonstrates default stopping on failure. |
| [process-failure.cs](../examples/process-failure.cs) | `Fail` records failure but returns the result, allowing explicit inspection and return. It does not imply successful command completion. |
| [process-working-directory.cs](../examples/process-working-directory.cs) | Relative child working directory and child-only environment overrides match the root policy. |
| [dependencies.cs](../examples/dependencies.cs) | A required entry target, shared build dependency, and body-free group fit the target overload. There is no extra command body after the graph. |

No obsolete command `.Run(...)`, `context.Output`, or `context.Value(...)` calls remain in these drafts. Explicit
namespace imports remain intentionally: automatic imports are still a packaging proposal. Package/project reference
directives are present in minimal.cs; the other six examples remain syntax drafts until their runtime slices arrive.

Important boundaries to preserve during implementation:

- Outer command `RunAsync` contains recoverable exceptions; process `RunAsync` can throw inside its callback.
- `Fail` continues the current body and records command failure; `Throw` unwinds the body; `Continue` handles that
  unsuccessful exit without clearing any other recorded failure.
- Validation checks every declared target, but execution runs only the selected reachable graph.
- Description and graph validation happen before help; help skips work and user validation callbacks.
- The command root is captured once per invocation. Child settings do not mutate the host process or shell.
- Input values are prepared before execution; `GetValue` is a lookup, not a computation trigger.

The target example's `dotnet build` and `dotnet test --no-build` commands assume an appropriate project or solution
in the root. Their exact arguments must be checked against the selected SDK, test runner, and fixture layout during
scaffolding. A generic syntax example does not establish that those commands work for this repository yet.

## Proposed milestone scope

Make the seven drafts run as real file-based C# applications and demonstrate the same public API in a regular C#
consumer project. Use isolated fixtures for build/test examples. Produce a local package for consumer and analyzer
verification; publication is outside the milestone.

Include:

- The three agreed command overloads, description validation, default invocation root, help, and exit codes `0`, `1`, `2`, `130`.
- String, integer, and Boolean inputs, with required/defaulted/optional handles and synchronous `GetValue`.
- Fluent option aliases with inferred single/double-dash prefixes and preservation of explicit prefixes.
- Individual and cross-input validation, aggregated ordinary rejections, and contained callback exceptions.
- Sequential targets, grouping, ordering, deduplication, graph validation, and stopping later scheduling on failure.
- Direct executable invocation with argument tokens, working directory, inherited environment plus overrides,
  accepted exit codes, returned results, and `Continue`/`Fail`/`Throw` handling.
- A small, deliberate output design that remains readable both in terminals and when redirected.
- Initial analyzer diagnostics and package integration with concrete, bounded coverage.
- The minimum cancellation and owned-process lifetime behavior selected before implementing processes.

The cancellation and owned-process behavior is now agreed in the design plan. Its implementation mechanisms and
platform verification remain pending; the process slice is not complete until those obligations are demonstrated.

## Decisions needed before the affected implementation

These groups consolidate the remaining choices. They do not require reopening the accepted syntax.

### Output and help

The [output and help design](output-design.md) now records an agreed visual direction: compact sections, consistent
indentation, descriptions, timings, one final outcome, and terminal-only content prefixes with clean redirected results.
The selected flatter layout uses unindented target descriptions and one two-space content gutter. Cancellation-request
messages use yellow in styled output, retain their wording in plain output, and remain hidden in quiet mode.
Stream separation, grouped input diagnostics, and separate help sections are also agreed for now. Normal, mutually
exclusive quiet/verbose modes, and independent plain presentation are agreed; redirected streams automatically use plain.

Review the [new comparison](output-preview.html), built around current examples using the preserved
[old previews](../reference/output/README.md) as inspiration. Unexpected callback exceptions retain full details in every mode; invalid inputs/declarations and rejected
process exits use the concise diagnostics specified in the output design. Multiple-entry headers and summaries preserve
requested order, sections follow dependency execution order, and each executed body is counted once. The first version
shows the executable and the working directory in verbose output or relevant failure diagnostics, but never
automatically prints process arguments or environment values, including in verbose mode. An API for selected-argument
disclosure/redaction is deferred. Refine remaining help/failure transcripts and further verbose content before the
affected renderer work. Keep animation and machine-readable events for a later topic.

The [Spectre.Console review](renderer-review.md) and [executable evaluation](renderer-evaluation.md) now recommend
0.57.2 with explicit literal/plain paths and independent stream policy. Windows terminal checks and startup/allocation
measurements support the choice; child-ANSI policy and other platforms remain separate work.

### Input and declaration edge cases

Boolean syntax and repeated scalar options are now agreed in the [typed-input design](example-design-plan.md#boolean-syntax-and-repeated-scalar-options):
a bare Boolean flag supplies `true`; explicit `true`/`false` work with space or equals separators. Omission follows the
declared policy, and explicit false counts as supplied. Repeated scalar options are invalid even when values match.

Naming is agreed in [Names and aliases](example-design-plan.md#names-and-aliases): lowercase names with hyphen-separated
segments, case-sensitive name matching, distinct option/target namespaces, reserved Rafter option names, and case-insensitive
Boolean literals. Fluent aliases and prefix rules are included in the options draft. Repeated alias calls append;
duplicate spellings on one option are harmless; collisions across options after prefix expansion and malformed aliases
are declaration errors during initial validation. Repeated CLI occurrences through different aliases remain invalid.

Proposed type scope remains the three types used by the drafts. Descriptions are already required.

The [invalid-value and callback configuration rules](example-design-plan.md#invalid-values-and-repeated-callback-configuration)
are agreed: reject null defaults/callbacks, repeated target `.Run(...)`, and repeated process `.OnFailure(...)` calls.
Aliases and validation rules continue to append. Command/input/target errors are checked during initial command
validation with exit `2`; process configuration created inside a body is checked when process `RunAsync` begins,
before launching the child. Invalid process configuration follows the agreed exception boundary below.

Choose the CLI parser and its configuration after these policies are selected. The old parser dependency and version
are references, not new-project decisions. Invalid input, invalid declarations, and exceptions during validation must
remain distinct outcomes.
The [System.CommandLine review](parser-review.md) and [executable evaluation](parser-evaluation.md) now recommend
2.0.13 with a bounded internal adapter. Empty attached values, safe diagnostics, and initial parsing/help rules have
Windows execution evidence; this does not yet implement the Rafter runtime or establish other platforms.

### Process completion and failure boundaries

The rejected-exit callback and exception boundary are agreed. Launch errors, invalid process configuration, and output
failures throw inside the body. If unhandled, the outer invocation reports failure with exit `1`; do not call the
exit-code handler or fabricate a completed child result. Deliberately catching a process exception handles that failure
and permits recovery, provided no other failure was recorded. An already recorded `Fail` remains a failure.

The [owned-scope completion policy](example-design-plan.md#completion-of-owned-process-scopes) is also agreed:
normally exiting tools do not leave owned helpers behind. Settle descendants and output before returning the root
result or applying its rejected-exit policy. Use **Termination unknown** when shutdown cannot be confirmed, and
**Output incomplete** when only output completion is unconfirmed. Persistent helpers are outside the initial scope.

Outcome precedence is agreed: clean cancellation uses `130`; unhandled or recorded execution failures, termination
unknown, and output incomplete use `1` even during cancellation. Preserve the original failure and identify cleanup
problems explicitly. Previously handled failures stay handled, and expected cancellation errors do not automatically
become independent failures. These rules do not waive ownership or output-settlement obligations.

Also settle:

- Bare executable search and relative executable paths, including whether lookup uses the effective child environment.
- Validation and replacement behavior for accepted-code lists and environment overrides.
- Remaining output-failure presentation. The compact diagnostic layout and action timing are agreed: Continue emits
  its yellow warning; Fail records invocation failure and emits its red diagnostic before returning the result;
  a thrown process-failure diagnostic is deferred until it escapes the body. Final summaries do not repeat diagnostics.
- Child shutdown and descendant ownership: the [cancellation baseline](example-design-plan.md#cancellation-and-owned-child-processes)
  is agreed (Ctrl+C, context token, no later targets, settlement before exit `130`, and no recovery into success by catching
  cancellation). The shared five-second grace, scoped termination, and separate shared five-second verification/output
  drain budget are accepted initial defaults. A second interrupt skips the grace period but preserves verification.
  These are maximum phase waits, not a guarantee that callbacks or Rafter's own cleanup operations have settled within
  ten seconds. Investigate platform ownership mechanisms using the [Aspire review](aspire-process-shutdown-review.md),
  without inheriting its CLI forced-exit or output-drain compromises.

The [process-awaiting policy](example-design-plan.md#awaiting-process-operations) is agreed: an analyzer error catches
clearly discarded process tasks, including explicit discards. Runtime tracking detects outstanding process operations
when their body or target callback finishes, reports an execution error, prevents later targets, and performs the agreed
cleanup before returning exit `1`, preserving any uncertainty diagnostics. Verify this with real unfinished-process
fixtures, including output/cleanup still pending after the main process exits. A completed task does not prove it was awaited.

These are process-slice decisions. They do not block implementing the minimal command and typed-input slices first.

### Analyzer and package behavior

An error for clearly discarded process tasks is agreed. Verify ignored calls and explicit discards, alongside valid
awaited calls with ignored results, returned tasks, delayed awaits, and awaited `Task.WhenAll`. Avoid speculative
diagnostics for tasks passed through helpers; broader data-flow coverage remains to be designed.

Nullable option type arguments already require warnings. Proposed additional initial diagnostics: errors for provably
blank descriptions and warnings for definitely empty targets at invocation. Exact severity and scope need confirmation.
Check descriptions consistently across commands, options, targets, and validation rules. Limit analysis to cases that
can be established reliably; valid configuration split across statements or helpers must not be diagnosed speculatively.

Keep the agreed compiler constraints: missing work fails overload resolution, and required/defaulted handles do not
expose either policy method again. Verify actual compiler behavior for synchronous/asynchronous callbacks and nullable
value/reference lookups; do not approximate those guarantees with tests of runtime values alone.

Decide whether to adopt the proposed automatic namespace import, enabled for direct C# package consumers with an
opt-out. If adopted, verify the package in file-based apps and regular projects and provide equivalent local development
imports. Until then, the drafts' explicit `using` directives remain valid.

### Delivery baseline

Use the [dependency decision review](dependency-decisions.md) to distinguish shipping runtime dependencies, analyzer
tooling, and test/build dependencies. Evaluate parser and renderer compatibility before adoption; select process
packages only against the agreed ownership and settlement requirements. No candidate or version is adopted merely
by appearing in that review.

The [SDK and platform review](sdk-and-platform-baseline.md) recommends SDK 10.0.402, C# 14, and net10.0, starting
verification on Windows x64. Linux/macOS and native AOT publishing need separate evidence before support claims.
This is a proposed verification order, not a permanent Windows-only restriction. Verify compatible test/runtime
packages and apply the documented SDK selection policy when scaffolding; do not copy old pins or acceptance claims.

The [GitHub workflow](github-workflow.md) records the verified canonical repository, fork, acting account, integration
branch, squash-merge workflow, and actual enforcement. The **First working examples** milestone and evaluation
issues #1–#3 are published; use #1 to establish the harness before the parser/renderer evaluations.

## Implementation work that does not need another product discussion

Handle class names, internal storage, helper decomposition, and test-fixture organization can be chosen during
implementation, provided they preserve accepted behavior. The compiler design for typed `GetValue` overloads should
be proven early, including optional value types, rather than postponing a potential API constraint until packaging.

Keep values and failure state invocation-local. Choose and document how declarations are captured for a run; do not
promise concurrent declaration mutation. Handle invalid lookup ownership and null delegate results within the agreed
invocation exception boundary. Surface any necessary public behavior change for discussion instead of silently changing
the examples.

## Proposed GitHub issue breakdown

The initial [evaluation issues](github-workflow.md#published-initial-work) are complete. Minimal invocation is scoped in
[#9](https://github.com/sotsera/rafter/issues/9); the remaining rows are outlines. Detailed acceptance checklists live in
the scoped issues, with links here and to design notes rather than parallel copies of the same checklist.

| Work item | Concrete completion evidence |
| --- | --- |
| [Minimal invocation (#9)](https://github.com/sotsera/rafter/issues/9) | [Implemented body-only runtime](minimal-runtime.md), separate runtime/test boundaries, real minimal file app and regular consumer, sync/awaited async bodies, root capture, help, description errors, contained exceptions, and managed cancellation. Analyzer project and packaging remain in their later slice. |
| Add typed inputs and validation | Options and combined validation examples run; compile checks prove nullability and fluent exclusivity; missing/empty/false/zero inputs, defaults, rule aggregation, and thrown validators follow the selected policies. |
| Execute target graphs | Dependency example runs; reversed selections and shared dependencies run each target once; invalid unused declarations, cycles, ownership, and failure scheduling are verified. |
| Execute and own external processes | Real child fixtures verify tokens, cwd, environment isolation, accepted/rejected exits, all failure actions, launch/output errors, and selected interruption/settlement behavior. |
| Deliver authoring diagnostics and local package | Analyzer positives and false-positive checks, fresh package-only file-app/project consumers, selected namespace-import behavior, and editor feedback are verified. |

The sequence is a proposal. Target scheduling can be verified with simple callback fixtures before the process slice;
the combined build/test example is an integration acceptance case after both are available.

## Milestone completion checklist

- [ ] Resolve the decision groups needed by the chosen scope and link their accepted rules from the design plan.
- [ ] Select the SDK/target baseline and needed dependencies, record compatibility evidence, and pin adopted versions.
- [x] Establish GitHub identity/workflow and create the scoped evaluation issues before coding begins.
- [ ] Run all seven examples through the actual file-app host, with suitable isolated fixtures and documented commands.
- [ ] Verify public compile-time guarantees and the selected analyzers, including legitimate split configuration.
- [ ] Verify success, rejection, exceptions, and process lifetime cases with appropriate integration checks.
- [ ] Review actual terminal and redirected output against the selected human and agent presentation requirements.
- [ ] Verify a local package from fresh file-app and project consumers, including editor behavior for the chosen setup.
- [ ] Record source, platform, commands, and observed outcomes; leave unexecuted checks pending.

## Later work

Keep these outside the proposed first milestone unless a concrete example demonstrates that one is necessary:
conditional targets, cleanup callbacks, reusable process configurations, capture APIs, configurable timeouts, environment
fallback inputs, child environment removal/clean mode, additional input types, explicit/source roots,
sensitive-value features, asynchronous input resolution, parallel targets, and machine-readable output formats.

Basic interruption and owned-process settlement are agreed requirements for the first process slice; they are distinct from
the deferred configurable timeout and cleanup APIs. Implementation and platform verification remain pending.
Cross-platform validation beyond the selected baseline and public
publication are separate gates, not implied by local milestone completion.
