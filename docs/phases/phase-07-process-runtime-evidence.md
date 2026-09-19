# Phase 7 implementation evidence

## Status

Generic process execution passes the current Windows, Ubuntu and macOS suite. The phase remains open for the complete
synthetic failure/race matrix, memory measurement, stress/resource evidence and the earlier macOS stall. See the
[closeout audit](phase-05-07-closeout.md) for repairs and the repository baseline.

## Completion and failure matrix

| Terminal path | Observed public contract | Tests |
| --- | --- | --- |
| Valid streaming exit | `ProcessExit`; no retained capture | `ReturnsAnExplicitlyValidNonzeroStreamingExit` |
| Valid capture | Exact per-stream text, original newlines, leading BOM, unterminated content | `CapturesExactArgumentTokensAndWorkingDirectory`, `PreservesALeadingUtf8BomAsProcessData`, `CapturePreservesRawNewlinesAndIsRedactedOnlyWhenPresented` |
| Invalid streaming exit | `ProcessExitException`, null capture | `ReportsAStreamingInvalidExitWithoutRetainingCapture` |
| Invalid capture exit | `ProcessExitException` with complete raw capture | `PreservesCompleteCaptureOnAnExplicitlyInvalidExit` |
| Start failure / pre-cancellation | `ProcessStartException` / standard cancellation without launch | `ReportsStartupFailureButHonorsPreCancellationBeforeLaunch` |
| Per-stream overflow | `ProcessOutputException`, `CaptureLimitExceeded`, affected stream(s), safe limit | `ReportsCaptureOverflowAfterTheChildExits`, three `CaptureOverflowIdentifiesEachAffectedStream` rows |
| UTF-8 policy | Strict malformed-input failure; split valid runes preserve exact text | `RejectsMalformedUtf8WithoutReplacementText`, four `CapturesUtf8SplitAcrossEveryPossibleRuneBoundary` rows |
| Authored timeout | `ProcessTimeoutException`; terminate direct child and observed tree | `AuthoredTimeoutTerminatesTheDirectChild`, `AuthoredTimeoutTerminatesAReportedProcessTree` |
| Invocation cancellation | Standard `OperationCanceledException`, command exit 130 | `InvocationCancellationPreservesStandardCancellationClassification` |
| Retained stdout/stderr/both | Bounded `RetainedPipe` failure, no partial public result | Three `FailsWithinTheDrainDeadlineWhenADescendantRetainsPipes` rows |
| Late teardown | Tracked reaper ownership, eventual disposal | `TransfersNonSettlingTeardownToTheTrackedReaper` |
| Discarded active terminal task | Callback failure after owned settlement | `ADiscardedActiveProcessFailsAndIsSettledBeforeTheCallbackCloses` |
| Registration/attachment race | Scope closure waits for attachment and completion | `ProcessOperationScopeTests.ClosingWaitsForAnOperationWhoseTaskHasNotYetBeenAttached` |

## Lifecycle and deadlines

The generic runtime prepares a complete immutable specification, registers callback ownership, arms its lifecycle
arbiter, starts the process synchronously, establishes independent stdout/stderr drains and exit observation, then
settles according to the winning natural-exit, external-cancellation, ownership-cancellation or timeout outcome.
All typed tools must later reuse this runtime.

| Policy | Production value | Current evidence |
| --- | --- | --- |
| Capture retention | 1 MiB per stream by default; authored override applies independently | Exact-limit, overflow and raw-capture tests |
| Drain after natural exit | 2 seconds | Real descendant-retained-pipe tests, each bounded to 6 seconds including launch/cleanup overhead |
| Synchronous tree-kill request | 2 seconds | Internal policy plus shortened synthetic late-teardown test |
| Direct-child verification after kill | 5 seconds | Policy plus real timeout/tree tests; complete adversarial matrix still open |
| Drain settlement after forced close | 2 seconds | Policy plus synthetic tracked-reaper test |

These are separate deadlines, not one combined process timeout. Synchronous `Process.Start()` is not preemptible.
Descendant termination remains best effort. Real fixture tests with descendants independently clean reported PIDs.

## Fixture and diagnostics

`Sotsera.Rafter.ProcessFixture` implements `inspect`, `environment`, `working-directory`, `json`, `emit`, `wait`,
`spawn-child`, and `retain-pipe`. `emit` supports independently encoded stdout/stderr payloads, repetition, byte-sized
chunks, delay and exit code. Descendant scenarios record control metadata outside the redirected output. The
fixture requires a verb and returns 64 for invalid arguments; the CI smoke command now uses `emit`.

`ReportsSpecificationDiagnosticsInAuthoredOrderWithoutLaunching` checks ordered safe diagnostics, and
`RejectsCapturePolicyInStreamingModeBeforeLaunch` checks mode-policy rejection. Exhaustive diagnostic ordering,
inactive omissions, foreign handles, executable forms and timeout boundaries remain open; the frozen tables in the
phase plan retain authority. No public API additions or removals were made by this closeout change.

## Memory and cross-platform limitations

The implementation retains capture in 16 KiB segments and creates UTF-16 strings only for successful complete
capture. The current tests verify data limits, not the specified measured managed-memory envelope. R3 therefore
remains open. The current three-OS CI establishes R11, while the broader R2 and R5–R9 matrices remain open.
Source/observer initialization failure
after successful launch, simultaneous failure ordering, disposal exceptions, repeated races and many-process stress
still require focused verification. The Phase 8 extension and typed-tool examples remain explicitly deferred.
