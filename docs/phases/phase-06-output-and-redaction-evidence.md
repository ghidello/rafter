# Phase 6 implementation evidence

## Status

Partial implementation, with capability profiles and lifecycle notifications added on 2026-09-20.
The [closeout audit](phase-05-07-closeout.md) records the
repository baseline and explicitly identifies unfinished presentation and verification work. This is not a phase
completion certificate.

## Routing and redaction coverage

| Input | Managed destination and protection | Executable evidence |
| --- | --- | --- |
| Line, success, property | stdout; invocation-wide redaction before rendering | `PhaseSixOutputTests.RoutesAndFormatsSemanticOutputWithoutChangingSuccess`, `RedactsSensitiveValuesBeforeSemanticOutputReachesTheSink` |
| String property in a file-app host | JSON escaping without reflection-based serialization | `FoundationIntegrationTests.PropertyOutputWorksWhenJsonReflectionIsDisabled`, canonical `presentation.cs` |
| Warning, error, recovery | stderr; presentation alone does not fail a target | `RoutesAndFormatsSemanticOutputWithoutChangingSuccess` |
| Managed console | Target identity flows through await and Task.Run; host writers restored | `AttributesPartialConsoleWritesAcrossAsyncFlowsAndRestoresTheHostWriters`, canonical `console.cs` |
| Console during binding | Quarantined per stream; released only after successful complete binding | `BindingOutputTests.QuarantinesValidationOutputUntilTheCompleteRedactorIsKnown` |
| Binding overflow | At 1,048,577 characters, discard both quarantines and skip execution | `EnforcesThePerStreamQuarantineLimitBeforeTargetExecution`, including below/at-limit rows |
| Split/multiline/long console text | Incremental redaction before bounded presentation | `RedactsASecretSplitAcrossConsoleWrites`, `RedactsMultilineAndSegmentBoundarySecretsIncrementally` |
| Process-local CRLF secret | Normalized streaming match across one-byte writes; no process-local registration outside that launch | `ProcessOutputBoundaryTests.RedactsProcessLocalMultilineSecretsAcrossByteChunks` |
| Raw capture re-entry | Capture preserves original newlines and secret; managed semantic/console output redacts registered invocation secrets | `CapturePreservesRawNewlinesAndIsRedactedOnlyWhenPresented` |
| Failed output sink | Infrastructure failure; callback continues and both child pipes still drain | `ConvertsSinkFailureIntoInfrastructureFailureWithoutThrowingThroughTheCallback`, `SinkFailureStillDrainsBothChildPipesAndSettlesTheTarget` |
| Concurrent commands | Separate injected sinks and shared console ownership | `KeepsOverlappingCommandsInTheirOwnConsoleSinks` |

## Existing approved plain snapshots

`RoutesAndFormatsSemanticOutputWithoutChangingSuccess` compares complete stdout and stderr strings, including:

```text
[present] Starting.
[present] missing=null
[present] paths=["src","tests"]
[present] success: Finished.
```

Warnings, errors, and recovery lines are asserted separately on stderr. The redaction tests assert `<redacted>` and
the absence of the disposable input. `console.cs` is checked for each authored target prefix on its expected stream.
These snapshots cover existing semantic output. Successful output now ends with the final target summary shown in
the accepted presentation fixtures. Rich properties and live transitions still require implementation.

The [accepted presentation fixtures](phase-06-presentation-fixtures/README.md) add 99 authored stdout/stderr pairs
across nine profiles and a separate live lifecycle frame matrix. They cover canonical presentation, all terminal
shapes, cleanup failures, concurrent attribution, continuations and narrow collections. The user approved their
grammar on 2026-09-20. Approval is separate from executable conformance; implemented coverage is recorded below.

## Capability profiles

`OutputCapabilities` replaces the two ANSI flags with immutable per-stream redirection, static-layout, ANSI,
color, Unicode, cursor and width values. Invocation preparation selects plain output before any report when an
exact `--plain` token is present, or independently for a redirected/incapable stream or width outside 20–4,096.
Malformed and duplicate common-option validation remains unchanged.

`NO_COLOR` is read once per invocation, including early reports, and its captured result is reused if an option
also binds that variable. A non-empty value, including whitespace, disables only color. A failed lookup disables
color; if the command binds that variable, binding still observes the original exception. Rendering uses explicit
Spectre settings, disables default profile enrichment, and takes width and Unicode from the injected profile.
Production ANSI detection uses the pinned Spectre implementation with explicit color policy, avoiding automatic
`NO_COLOR`/`FORCE_COLOR` color detection. Capability-probe failures select plain output for only that stream;
failure to capture the writers remains an infrastructure failure.

The cached environment name follows platform semantics: case-insensitive on Windows and case-sensitive on Unix.
The review regressions reproduce duplicate Windows lookups for `no_color`/`No_Color` before the fix and cover both
successful values and cached failures. Unix keeps differently cased variables distinct.

`OutputCapabilityTests` supplies 29 cases covering mixed stdout/stderr redirection, color policy, width boundaries,
failed cosmetic probes, early model/graph/cancellation/input reports, malformed `--plain`, shared environment
lookup, per-invocation refresh, and writer-capture failure. All tests inject complete profiles rather than use
host-terminal detection. The Release build and 465-test solution suite pass locally with formatting unchanged.
CI for the three added review cases is deferred until the planned final verification run.
Live cursor coordination, status glyphs and rich property/final-summary layouts remain separate unfinished work.

## Execution lifecycle notifications

The runtime now publishes immutable target identity, plan index and lifecycle notifications immediately after each
transition. Every initial `Pending` notification is delivered in plan order before admission. Outcome, successful
shape and authored-order direct blockers appear only after settlement; previously delivered values remain unchanged.

Each invocation creates a serialized observer guard. Its first callback exception disables further delivery and
records output infrastructure failure while execution, target cleanup and command cleanup continue. The underlying
execution outcome retains its original cancellation, callback and cleanup failures. A later invocation gets a fresh
guard. The seam is internal and introduces no public API or transient presentation output.

`ExecutionObserverTests` supplies 15 cases covering live execution/cleanup timing, initial ordering, immutable
terminal data, failure at each lifecycle, concurrent delivery, cancellation, original callback/cleanup failures,
per-invocation isolation, and the absence of transient plain/static output. All 465 solution tests pass locally; the Release build,
formatting and diff checks pass. CI is deferred to the planned final verification run. Live rendering, the
`Pending`/`Ready` to `Waiting` presentation mapping remain unfinished.

## Final target summaries

Execution completion now replaces the temporary Phase 5 report with one summary after buffered output and both
cleanup scopes settle. Success uses stdout; failure and cancellation use stderr and that stream's own capabilities.
Rows follow plan indices, distinguish executed/aggregate/no-work success and skipped/failed/cancelled/blocked states,
and preserve authored direct-blocker order. Primary failure details and secondary cleanup failures retain their
original classifications. No exception messages or durations are introduced.

Rich Unicode profiles add the accepted state symbols; ASCII and forced-plain profiles retain complete labels.
Colorless profiles retain Unicode without styling escapes. Target and blocker names cross the existing redaction
boundary. Summary rendering, redaction and writer failures record output infrastructure failure without changing
the retained execution outcome or cleanup counts.

`ExecutionSummaryTests` adds 46 cases: 36 direct invocation/profile snapshots for cleanup and cancellation, a
terminal-shape test across all nine profiles with deliberately shuffled result storage, capability overrides,
redacted identities, sink failures, fail-closed styling and settlement ordering. The real `presentation.cs` success/failure plain streams
are also compared against the accepted documents by `verify-implemented-examples.ps1`. Live-profile snapshot rows
here prove their final static summary only; they do not establish live cursor repainting.

Interactive Windows UTF-8 terminal runs of the unchanged example also displayed `✓ [present] Succeeded` and
`✗ [present] Failed` with exit codes 0 and 1 respectively. These smoke checks confirm the production capability
path, separately from the deterministic injected-profile tests.

## Shared report publication

Review after the summary implementation found that help, diagnostics and summaries bypassed the semantic-output
publication lock. Concurrent report and semantic writes overlapped the same sink, and report sinks calling Console
could have their own writes recaptured as target output. New regressions reproduced both defects before repair.

`TerminalPublication` now gives reports and semantic events one synchronous physical-write boundary and one
publication guard. Report APIs return completed or faulted tasks after synchronous publication; no thread blocks on
a task or holds the lock across an await. The guard is restored after exceptions. Managed sink failures are recorded
before releasing the boundary, so a concurrent later call cannot observe that invocation as healthy.

Reports flush pending managed console fragments to the same captured writer before publishing their document.
This also covers help from another command that has not registered for interception. Writer identity keeps
independently injected sinks separate. The same-sink ordering regression failed before this repair on stdout;
the final tests cover stdout and stderr, both shared and distinct sinks.

The first ten `TerminalPublicationTests` cases cover synchronous report delivery, overlapping reports/semantic writes,
reentrant sinks, guard restoration, failure suppression and pending-fragment ordering. All 465 solution tests pass.
This does not close the full ordering gate: process-monotonic event sequencing, atomic buffered-event publication,
host pass-through coordination, all writer overloads and live-display suspension still require their broader audit.
The user has deferred reconsidering the visual design; this work retains the existing rendering fixtures.

## Host failures and publication lifetime

Host pass-through write failures now both propagate the original exception to the host caller and mark active
invocations using that captured writer as output infrastructure failures. Newline getter/setter failures use the
same writer-identity filter; they no longer fail invocations with independent injected sinks. An earlier output
failure remains primary. Target outcomes and target/command cleanup counts remain unchanged.

`HostConsoleFailureTests` provides ten cases across stdout and stderr for write/getter/setter failures, independent
sinks, preserved prior failures and command completion. Eight cases failed before the fix. Writer identity refers
to the captured `TextWriter` instance; this does not discover arbitrary wrappers around a common underlying sink.

The publication guard now carries an explicitly closed scope rather than a copied depth value. A task started by
a sink can inherit the scope, but its permission to bypass managed interception ends when that physical write ends.
Nested active publications retain their own protection. Two more `TerminalPublicationTests` cases reproduced raw
secret exposure from deferred console writes after semantic/report publication; those writes now regain managed
attribution and redaction while the invocation remains active. This brings that class to twelve cases.

These repairs leave visible formatting unchanged. Full host/managed write serialization and process-wide event
sequencing remain open; the host failure tests establish classification and isolation, not those broader ordering gates.

## Console line overloads

The inherited `TextWriter` line overloads could route the payload and newline separately. Thirty-six cases in the
initial 112-case matrix reproduced two host sink calls for one line. The coordinating writer now constructs the
complete line before routing, including scalar, character, array, span, object and `StringBuilder` inputs. Async
overloads use the same synchronous path. `StringBuilder` writes snapshot all chunks before entering routing.

`ConsoleWriterOverloadTests` supplies 148 cases: 33 line variants on stdout/stderr through managed and host paths,
eight multi-chunk builder cases and eight pre-cancelled memory/builder cases. Changed newline strings and null
values match ordinary `StringWriter` behavior. Host lines arrive in one sink call, managed lines retain attribution,
async calls are complete before returning, and cancellation produces no output or invocation failure. Writers are
restored after each case. All 465 solution tests pass locally.

This repairs splitting within an individual line call. It does not establish serialization between independent
host and managed calls, atomic publication of multiple buffered events, or process-monotonic event sequencing.
The full overload gate still needs non-line scalar/formatting, validation and flush coverage. Output appearance and
the canonical examples remain unchanged.

Review added four `ConsoleWriterValidationTests` cases, each comparing six invalid slices through synchronous and
asynchronous write/line calls against `StringWriter`. All four initially exposed constructor parameter names leaking
through the console API. Array slices now preserve `TextWriter` exception types and parameter names for null,
negative and out-of-range arguments without publishing or changing invocation health. The validation repair passed
the 451-test suite, Release build and formatting checks. The wider formatting and flush matrix remains open.

## Output admission and sealing

Fourteen `OutputAdmissionTests` cases establish the existing facade's admission behavior without a runtime change.
Eight start sealing from inside scalar or collection-element formatting and verify that sealing remains incomplete
until publication or caller failure releases the admission. They repeat with a prior recorded output failure and
confirm that formatting still runs, its original exception escapes, and the earlier infrastructure failure is retained.

Five cases reject each facade method after sealing, before argument validation or property formatting. A concurrent
case starts 64 competing property calls while a known admitted formatter initiates sealing. Accepted calls match
formatting and publication counts; rejected calls do not format, and the sink stays unchanged after sealing completes.
The test uses asynchronous completion signals without blocking a task or holding a coordinator across an await.

All 465 solution tests, the Release build and formatting checks pass locally. These are focused facade/admission
checks; full command-cleanup/console/final-summary interleavings and the wider Phase 6 ordering matrix remain open.

## Remaining work

O1–O9 remain open except individually substantiated gates in the plan. The current suite is not the full phase matrix.
Missing cases include the remaining writer overloads, full console/semantic sealing interleavings, repeated cross-command
replacement, all renderer/observer failures, and process-wide ordering across semantic, console, and host writes.
The implementation gaps and repository/CI limits are enumerated in the closeout audit.
