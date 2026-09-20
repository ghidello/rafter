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
| Post-start observer failure | `ProcessException` preserves the cause; child termination, partial-reader closure and disposal remain owned | Ten capture/streaming rows in `ProcessInitializationTests.TerminatesAnOwnedChildWhenInitializationFails` |
| Late partial drain after observer failure | Bounded teardown failure, tracked reaper ownership and eventual disposal in capture and streaming | Two rows in `ProcessInitializationTests.RetainsOwnershipOfAPartialDrainThatSettlesAfterInitializationFailure` |
| Cancellation wins while exit observation fails | Cancellation remains primary; failed or cancelled observation cannot release ownership before child exit | Two rows in `ProcessInitializationTests.CancellationStillVerifiesExitWhenTheExitObserverFails` |
| Per-stream overflow | `ProcessOutputException`, `CaptureLimitExceeded`, affected stream(s), safe limit | `ReportsCaptureOverflowAfterTheChildExits`, three `CaptureOverflowIdentifiesEachAffectedStream` rows |
| UTF-8 policy | Strict malformed-input failure; split valid runes preserve exact text | `RejectsMalformedUtf8WithoutReplacementText`, four `CapturesUtf8SplitAcrossEveryPossibleRuneBoundary` rows |
| Authored timeout | `ProcessTimeoutException`; terminate direct child and observed tree | `AuthoredTimeoutTerminatesTheDirectChild`, `AuthoredTimeoutTerminatesAReportedProcessTree` |
| Invocation cancellation | Standard `OperationCanceledException`, command exit 130 | `InvocationCancellationPreservesStandardCancellationClassification` |
| Retained stdout/stderr/both | Bounded `RetainedPipe` failure, no partial public result | Three `FailsWithinTheDrainDeadlineWhenADescendantRetainsPipes` rows |
| Late teardown | Tracked reaper ownership, eventual disposal | `TransfersNonSettlingTeardownToTheTrackedReaper` |
| Discarded active terminal task | Callback failure after owned settlement | `ADiscardedActiveProcessFailsAndIsSettledBeforeTheCallbackCloses` |
| Registration/attachment race | Scope closure waits for attachment and completion | `ProcessOperationScopeTests.ClosingWaitsForAnOperationWhoseTaskHasNotYetBeenAttached` |

## Lifecycle and deadlines

Disposal review added fourteen capture/streaming cases spanning success, false/throwing startup, invalid exit,
invalid UTF-8, cancellation and authored timeout. Every case initially exposed a raw disposal exception replacing
the selected result. The runtime now classifies invalid exits before disposal, retains complete raw capture,
preserves the selected exception type/token/timeout/output metadata, and appends disposal as a secondary cause.
Disposal alone produces a safe `ProcessException`; the adapter is disposed once. The 479-test suite and the
analyzer-clean Release build and formatting checks pass locally. The wider combined-failure matrix remains open.

The generic runtime prepares a complete immutable specification, registers callback ownership, arms its lifecycle
arbiter, starts the process synchronously, establishes independent stdout/stderr drains and exit observation, then
settles according to the winning natural-exit, external-cancellation, ownership-cancellation or timeout outcome.
All typed tools must later reuse this runtime.

If process-ID lookup, stream acquisition or exit-observer creation fails after launch, the runtime enters bounded
teardown with every reader already started. An asynchronous fault in the exit observer also enters teardown. When
the normal exit observer is unavailable or failed, a separate `HasExited` poll verifies direct-child termination;
unfinished verification or drainage remains tracked by the reaper. Synthetic tests cover ID/stdout/stderr/exit
initialization failures and asynchronous exit-observer faults in both modes, plus late readers in both modes.
Cancellation winning before the exit observer faults or cancels still requires independent direct-child
verification, with reaper ownership when it settles late. These tests do not establish the full simultaneous
cancellation/timeout/failure matrix.

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
Post-start observer failures now have focused regression coverage. Simultaneous failure ordering, exit-verification
failures, disposal exceptions, repeated races and many-process stress still require focused verification. The
macOS diagnostic split has passed, but the earlier runner disconnect remains unexplained. The Phase 8 extension
and typed-tool examples remain explicitly deferred.
