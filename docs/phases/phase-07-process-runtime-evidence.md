# Phase 7 implementation evidence

## Status

Generic process execution, failure/race matrices, capture allocation measurements and concurrent-resource checks
pass locally in the 709-test solution suite. Supported-OS CI predates these changes. The phase remains open for that
final validation and the unresolved earlier macOS runner stall. See the [closeout audit](phase-05-07-closeout.md).

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
`RejectsCapturePolicyInStreamingModeBeforeLaunch` checks mode-policy rejection. The completion matrices below cover
all 13 diagnostic codes, pre-cancellation precedence, inactive omissions, foreign handles, executable forms and
timeout boundaries. The frozen tables remain authoritative; this pass makes no public API additions or removals.

## Capture memory and segment boundaries

`ProcessCaptureBuffer` now owns the runtime's 16 KiB capture segments and releases their references after successful
materialization, decode failure or overflow. Its character-counting pass uses stateful `Decoder.Convert` across
segments: the previous `GetCharCount` pass lost split-rune state and rejected valid UTF-8. Two buffer regressions
reproduced that defect. The real-process UTF-8 test now captures 54,000 bytes on each pipe with chunks of 1, 2, 3, 4
and 997 bytes, crossing internal segment boundaries as well as input-read boundaries.

Eleven buffer tests measure allocations, one-segment slack, overflow release and successful/failed materialization.
These synchronous measurements use thread-local allocated-byte counters around the actual buffer component after
warming it, excluding test inputs/assertions. They measure managed allocations, not whole-process RSS or native
process resources. For equal ASCII streams of N bytes and S segments each, the tested upper bound is
`6*N + 2*16,384 + 256*S + 16,384`: byte segments plus both UTF-16 results, segment slack, metadata and fixed overhead.
The runtime's two pooled 16 KiB read buffers and fixed decoder state are additional constant overhead.

| Bytes per stream | Combined segment capacity | Allocated bytes | Tested bound |
| --- | --- | --- | --- |
| 16,383 | 32,768 | 99,248 | 147,706 |
| 16,384 | 32,768 | 99,248 | 147,712 |
| 16,385 | 65,536 | 132,128 | 147,974 |
| 1,048,576 | 2,097,152 | 6,301,568 | 6,356,992 |
| 8,388,608 | 16,777,216 | 50,414,560 | 50,511,872 |

Two real-process stress cases concurrently launch 8 and 24 children, each draining and capturing 256 KiB on both
pipes. They verify exact text, independent process-handle exit confirmation, exactly-once adapter disposal and an
empty reaper. Every observed child is independently cleaned in a finally block, including after assertion failure.
The complete 537-test suite passes locally; supported-OS CI remains deferred.

## Cross-platform limitations

The capture component now has measured local allocation evidence; current three-OS evidence predates this change.
The new failure combinations, repeated races, disposal and concurrent captures have the focused local checks recorded
here. Their supported-OS results remain pending. The macOS diagnostic split has passed on an older revision, but the
earlier runner disconnect remains unexplained. The Phase 8 extension
and typed-tool examples remain explicitly deferred.

## Completion pass: resource failure classification and retention

`ProcessResourceFailureTests` adds seven cases. Adapter construction now uses the safe `ProcessStartException`
classification while retaining the original failure as its inner exception. Combined kill, stream-close and disposal
failures retain cancellation or timeout as primary and preserve the secondary failures in lifecycle order in both
streaming and capture modes. Late-operation failure history now keeps at most 32 exception samples and a total count;
a 200-resource test observes 400 operation/disposal failures, exactly one disposal per resource, and an empty reaper.
The full local suite after this pass contains 586 passing tests. Cross-platform validation of these changes is pending.

## Completion pass: specifications, startup, scopes and deadlines

| Matrix | Cases and observed result |
| --- | --- |
| `ProcessSpecificationMatrixTests` | 34 malformed-builder/pre-cancellation rows cover RAFTER1501–1513 and duplicate policies without adapter creation; five timeout rows include the supported upper bound |
| `ProcessHandleAndPathTests` | Sixteen foreign-handle operations rejected, eight bare/relative/parent-relative/absolute path cases with target/process directory selection, and inactive string/flag omissions without consuming a policy |
| `ProcessStartRaceTests` | 12 combinations repeated ten times: start true/false/throw, cancellation/timeout during synchronous start, capture/streaming; fixed primary classification, exactly-once disposal and no reaper entries |
| `ProcessCallbackScopeTests` | Independent condition/execution/target-cleanup/command-cleanup authority, expired builder rejection, concurrent independent launches, completed discarded tasks, and four active-discard/false-condition/callback-failure rows |
| `ProcessDeadlineTests` | Two tracking-clock rows prove authored timeout stops at execution settlement and the unused drain deadline timer is disposed before operation return |
| Existing resource/observer matrices | Construction, PID/stream/observer initialization, asynchronous exit observation, cancellation plus observation failure, kill/close/dispose combinations, retained pipes and late tracked ownership |

The deadline tests exposed two timer lifetime defects: a fast drain left its losing delay timer alive, and natural
exit left the authored execution timer armed throughout pipe drainage. Both now release their timer at the owning
boundary. Synthetic timer tests cover resource lifetime without waiting for wall-clock timeouts.

The complete local suite has 709 passing tests, an analyzer-clean build, 24 compiled examples and 14 deterministic
example scenarios. Package and symbol checks at `7586f38` pass for 63 source documents and both fresh-cache consumers.
R3, R6, R7 and R9 have local evidence recorded here. R2, R4, R5, R8, R10 and R11 retain their supported-OS requirements;
the historical passing matrix does not certify this revision. `dotnet`, `extensibility`, `git`, `node` and `repository`
remain the explicitly deferred Phase 8/9 examples.
