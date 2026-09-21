# Phase 5–7 closeout audit

## Status as of 2026-09-21

The audit started at `a9e39198a24855bfcb244c74f0c1230e89471638`. The local completion pass through Phase 7 is
implemented at `7586f38`, with subsequent output-boundary repairs at `479288b`. It includes the accepted rich/live
presentation, the user-approved fail-closed redaction boundaries, graph and process matrices, measured capture memory,
and concurrent real-process cleanup.
The solution has 722 passing local tests. All 24 implemented examples compile and render help, and all 14 deterministic
example scenarios pass without changing canonical example sources.

Phases 5–7 passed at `10cd98599048275c3f9f0dd631ae4bd5b455b608`, verified by
[CI 19](https://github.com/ghidello/rafter/actions/runs/35646084394) on its first attempt. Windows, Ubuntu, macOS and
package integrity all pass. The user authorized the final push and CI step on 2026-09-21. The older intermittent
macOS runner stall recurred in CI 20 on the documentation-only closeout commit, so Phase 7 gates R5, R8 and R11
are reopened. Older test counts below identify historical checkpoints, not the current baseline.

## Repairs made during verification

- Removed the explicit Source Link 8.0.0 override. .NET 10 already supplies Source Link; the override introduced
  `Microsoft.Build.Tasks.Git 8.0.0`, flagged by [GHSA-23fw-v26w-5fgq](https://github.com/advisories/GHSA-23fw-v26w-5fgq).
  The [Source Link documentation](https://github.com/dotnet/sourcelink#using-source-link-in-net-projects) explains
  SDK integration and package overrides. Auditing and warnings-as-errors remain enabled.
- Restricted build-time Git configuration to the repository. A global URL rewrite previously selected the local
  `github-ghidello` alias, and a warning-free build still produced no Source Link map. The corrected packaged PDB
  maps runtime sources to `raw.githubusercontent.com/ghidello/rafter/<revision>/*`.
- Strengthened package verification to check assembly/PDB identity and the actual packaged source map, use a fresh
  NuGet cache and an external path containing spaces, and execute public command APIs from conventional and
  file-based consumers. It does not claim remote-source/checksum verification of uncommitted edits.
- Fixed callback-scope closure racing terminal-task attachment. Registration now remains owned while synchronous
  process startup completes and the terminal task is attached; closure awaits the attached task before returning.
  `ProcessOperationScopeTests` reproduced the previous premature settlement before the fix.
- Fixed process-local multiline secret matching across CRLF byte chunks. Streaming input and sensitive patterns now
  use the same newline normalization; raw capture remains unchanged. The new regression failed before the fix.
- Installed console interception before binding and added independent 1,048,576-character stdout/stderr
  quarantines. Successful binding releases only redacted text. Rejection, binding failure, or overflow discards the
  quarantine; overflow prevents target execution and reports infrastructure failure.
- Replaced reflection-based JSON serialization of property strings with direct JSON encoding. The canonical
  `presentation.cs` failed under its reflection-disabled runtime configuration despite passing unit tests; the
  integration fixture now disables JSON reflection and asserts escaped property output.
- Added real compilation/help checks for the 24 currently implemented examples and deterministic runtime checks.
  Three narrowly scoped analyzer exceptions accommodate the approved file-app enums and collection demonstration.
  Example-scoped package metadata also reconciles the unchanged user-secrets directive with central package
  management; its direct version previously caused NU1008 during restore.
- Enabled project names in the shared artifacts paths for file apps. Their SDK default omitted the project name,
  so distinct examples shared intermediate compiler state and a later no-build invocation could execute another
  example's code. The harness builds every example before rerunning earlier ones, exercising this isolation.
- Corrected the CI fixture invocation: the process fixture requires a verb, so its old no-argument smoke check
  returned 64. CI now invokes `emit`, verifies formatting and runs the implemented-example harness on each OS.
- Added owned teardown when process-ID lookup, stream acquisition or exit-observer creation fails after successful
  launch, including asynchronous exit-observer faults. Partially initialized readers are retained for bounded
  cleanup; delayed settlement transfers ownership to the reaper. Fourteen synthetic regressions cover capture and
  streaming, original failure preservation, reader settlement and eventual disposal after late drainage. Review
  also exposed cancellation winning while the exit observer fails: teardown now verifies actual child exit before
  releasing ownership, including when the original observer faults or is cancelled.
- Replaced the ANSI flags with independent immutable stdout/stderr capability profiles, made exact `--plain`
  apply before model errors, and captured `NO_COLOR` once per invocation. The capability tests cover mixed streams,
  width limits, color-only suppression, failed probes, early reports and reuse of the lookup by option binding.
  Review fixed mixed-case `NO_COLOR` aliases causing duplicate lookups on Windows, while preserving Unix's
  case-sensitive environment names and cached lookup failures.
- Added immutable execution lifecycle notifications at actual transitions, including initial plan-ordered pending
  states and settled outcome data. A serialized guard disables observation after its first failure and records
  output infrastructure failure without changing scheduling, callback counts, cleanup or the execution outcome.
- Implemented the user-accepted final target summaries, Unicode/ASCII states, authored blocker order and secondary
  cleanup sections. Summary failures retain the execution outcome and record output infrastructure failure.
- Unified report and semantic-output publication after regressions reproduced overlapping writes and reentrant
  report-sink recapture. Reports now flush earlier managed fragments on the same writer, and managed sink failure
  is recorded before another publication can enter. Ten regressions cover the shared boundary and its limits.
- Scoped host write and newline-property failures to active users of the captured writer, preserving the host
  exception, prior invocation failure and cleanup behavior. Ten tests cover both streams and independent sinks.
- Replaced the copied publication-depth guard with a scope that closes in inherited execution contexts too. Two
  regressions reproduced deferred sink writes bypassing redaction after publication; both now remain managed.
- Made console line overloads route the complete payload and newline together, and snapshot `StringBuilder`
  chunks before routing. The 148 overload cases cover both streams, managed/host delivery, changed newlines,
  synchronous completion, null values and pre-cancelled writes; 36 initial cases reproduced split host writes.
- Restored `TextWriter` exception types and parameter names for invalid character-array slices. Four validation
  cases cover synchronous/asynchronous writes and lines on both streams without output or infrastructure failure.
- Added 14 output-admission cases covering sealing during scalar/collection-element formatting, preserved caller
  failures after recorded output failure, rejection before validation, and 64 concurrent facade calls racing the seal.
  The existing admission implementation passed these checks without a runtime change.
- Preserved process outcomes when adapter disposal also fails. Fourteen capture/streaming regressions exposed
  overwritten startup, output, invalid-exit, cancellation and timeout failures; cleanup now remains secondary.
- Implemented accepted rich properties and attributed multiline semantic output. Forty-four new cases cover the
  nine-profile fixtures, escaped controls and property secrets redacted before JSON encoding.
- Fixed UTF-8 decoding across internal capture segments and measured the actual capture buffer allocation envelope
  through 8 MiB per stream. Concurrent real-child stress now covers 8 and 24 processes with independent exit checks.
- Added live lifecycle surfaces, console continuation markers and synchronous flush forwarding under one terminal
  publication boundary. All 99 accepted documents and the four live-profile frame sequences now match.
- Applied the user's fail-closed decision at uncertain redaction barriers. The pending fragment is withheld and
  output fails without interrupting callback or cleanup settlement. Final sealing can settle an unmatched prefix.
- Fixed adjacent streaming secrets incorrectly sharing one marker. Exhaustive chunk splits and 500 deterministic
  generated inputs now agree with the non-streaming interval-union reference.
- Fixed overlapping signal subscriptions during final unsubscription and retired signal callbacks cancelling new
  invocations. Six terminal-path tests retain same-command overlap protection through presentation and teardown.
- Released unused process drain deadline timers and disarmed authored timeouts when execution has already settled.
  Two tracking-clock regressions verify that pipe drainage retains only its own deadline.
- Added all 13 process diagnostic codes with pre-cancellation precedence, 120 controlled synchronous-start races,
  callback-scope authority, foreign-handle/path/policy matrices and combined resource-failure classification.
- Captured each destination's newline in its immutable output profile. Nine cases cover LF, CRLF, CR, custom
  newlines, profile fallback and independent stdout/stderr capture; raw process capture remains exact.
- Preserved split CRLF state through repeated publication barriers, decoded unpaired UTF-16 surrogates before
  property redaction and visibly escaped them in semantic text. Eleven new text cases cover these boundaries and
  round-trip all UTF-16 code units. A mixed-stream live regression also prevents a separate writer's newline from
  resuming display inside an unfinished host line; retired writers cannot suppress later displays.

## Local verification

Environment: Windows x64, .NET SDK 10.0.401 selected through `global.json` patch roll-forward, PowerShell 7.

| Check | Result |
| --- | --- |
| Normal restore, with auditing and warnings-as-errors | Passed after removing the obsolete override |
| Release solution build | Passed, zero warnings and errors |
| Solution tests | 722 passed, zero failed or skipped |
| Formatting verification | Passed |
| Runtime and symbol package layout | Passed |
| Packaged PDB identity and canonical Source Link map | Passed locally for `5a5ba58`; 63 runtime documents mapped |
| Fresh-cache external project and file-app consumers | Passed; both bind and execute the public command API |
| Canonical reference check | All 29 examples retain their original project reference |
| Implemented-example compilation and plain help | All 24 classified examples passed |
| Deterministic example execution | All 14 scenarios passed; canonical `.cs` sources unchanged |

The example harness records compilation, help, and scenario results under `artifacts/example-verification`.
The full list and its limits are in the development guide. Artifacts are generated evidence and remain ignored.

## Gate reconciliation

| Phase | Established evidence | Completion record |
| --- | --- | --- |
| 5 | Reachable graph preflight, exactly-once scheduling, measured sync/async bounds at 1/2/8, 25 repeated outcome runs, all condition families, cleanup/cancellation boundaries, signal and invocation-overlap races | G0–G9 closed with local evidence and CI 19 |
| 6 | All 99 accepted documents and live frames; independent profiles/newlines; serialized managed, console, host and report publication; bounded property snapshots; fail-closed boundaries; chunk-reference redaction; six overlapping replacement cases; sink/observer/sealing failures | O0–O9 closed with local evidence and CI 19 |
| 7 | Argument/policy/handle/path matrices; capture and strict UTF-8; 120 start races; observer/kill/close/dispose failure combinations; tracked late teardown with bounded failure history; measured allocation envelope; 8/24 real-child stress and timer ownership | CI 19 passed; R5, R8 and R11 reopened after the CI 20 macOS recurrence |

The phase evidence documents map the local gates to named tests. Completion-gate checkboxes reflect that distinction;
the original detailed implementation lists remain the contract history and are not a substitute for the gate evidence.
Capture measurements cover managed allocations in the actual segmented buffer, not whole-process RSS. Descendant
termination remains the approved best-effort policy with independent fixture cleanup, not a new survivor guarantee.
Arbitrary application-owned sinks must settle their synchronous calls; these tests do not promise to break locks or
waits introduced by custom sink code. The public API and canonical syntax portfolio are unchanged by this pass.

## Cross-platform evidence

The first closeout run, [CI 10](https://github.com/ghidello/rafter/actions/runs/35470555329), tested
`70aae16ab251fa9e989b0491395f4d5240294abf`. Windows passed, while Ubuntu and macOS exposed fixture portability
failures; the dependent package job was skipped. The retained-pipe fixture now gives the unused Unix stream its own
pipe before managed-child startup. Windows retains explicit closure of the inherited standard handle. The
environment-clear test invokes an explicit .NET host; the unchanged environment example uses a generated Unix
fixture launcher for the same reason, so clearing `DOTNET_ROOT` cannot hide a custom SDK installation.

[CI 11](https://github.com/ghidello/rafter/actions/runs/35470905050) passed Windows and Ubuntu, but its macOS test
step stalled. The cause is not established. Fixture cleanup now has a five-second exit wait, and CI adds a two-minute
suite deadline, a five-minute outer step deadline, and long-running-test diagnostics. A later passing run does not
close the intermittent-hang investigation or the Phase 7 stress/race gates.

[CI 12](https://github.com/ghidello/rafter/actions/runs/35471171229) passed for implementation commit
`1bdd196b1c04fe6a4cb24f31c5ba25579f0087bb` on 2026-09-19:

| Job | Result |
| --- | --- |
| [Windows](https://github.com/ghidello/rafter/actions/runs/35471171229/job/105972283416) | Restore, analyzer-clean build, formatting, 173 tests, fixture smoke checks, 24 examples and 14 scenarios passed |
| [Ubuntu](https://github.com/ghidello/rafter/actions/runs/35471171229/job/105972283299) | Same checks passed; zero failed or skipped tests |
| [macOS](https://github.com/ghidello/rafter/actions/runs/35471171229/job/105972283521) | Same checks passed; zero failed or skipped tests; retained-pipe cases settled in 2.1–2.4 seconds |
| [Package integrity](https://github.com/ghidello/rafter/actions/runs/35471171229/job/105972804165) | Matching packaged DLL/PDB, Source Link for 58 documents, both fresh-cache external consumers, 29 canonical references and unchanged example sources passed |

This establishes the repository-quality baseline for the implementation commit above. It does not establish the
unimplemented contracts or exhaustive phase matrices. Historical Phase 4 results are not used as evidence for this
implementation.

CI 11 and [CI 13](https://github.com/ghidello/rafter/actions/runs/35471565195) ultimately reported that the macOS
hosted runner lost communication with GitHub. CI 13's second attempt remained stuck beyond both configured
deadlines and was force-cancelled on 2026-09-20. The disconnected jobs did not provide a retrievable log archive;
the available evidence does not identify a failing test or establish a runtime root cause.

[CI 14](https://github.com/ghidello/rafter/actions/runs/35493840813) passed with the 23 Phase 7 process-test methods
(28 cases) run in individual macOS steps, alongside the other 145 tests. This diagnostic split preserves every
test but does not reproduce the original shared-process execution. Its success is not proof that the underlying
runner-loss issue is fixed. [CI 15](https://github.com/ghidello/rafter/actions/runs/35494118283), for `d6e66f8`, also
passed all three OS jobs and package verification. It pins Linux jobs to the validated `ubuntu-24.04` image ahead
of the announced `ubuntu-latest` migration.

[CI 16](https://github.com/ghidello/rafter/actions/runs/35519505085), for `aec96c0`, passed all three OS jobs and
package verification with 187 tests, including the 14 observer-failure regressions.

[CI 17](https://github.com/ghidello/rafter/actions/runs/35520592866), for `d873080`, passed on its second attempt
with 213 tests, including the initial 26 capability cases, all examples and package verification for 59 source
documents. Its first macOS attempt stalled in `AuthoredTimeoutTerminatesAReportedProcessTree` beyond the test and
step deadlines and was force-cancelled. The fresh runner passed that test; the intermittent cause remains open.
That package result refers to `d873080`. The local table above records the later `5a5ba58` package.

[CI 18](https://github.com/ghidello/rafter/actions/runs/35645601955), for `5a5ba58`, passed the Windows job with
722 tests and all implemented examples. Ubuntu and macOS each failed the two new device-path diagnostic rows;
the package job was consequently skipped. The fixture incorrectly applied Windows device-namespace restrictions
to Unix filenames. Commit `10cd985` corrects the test to assert native normalization, cancellation and launch behavior
on each OS without skipping either row. This was a test portability error, not a recurrence of the runner stall.

## Completion verification

[CI 19](https://github.com/ghidello/rafter/actions/runs/35646084394) passed on 2026-09-21 for `10cd985` with no retries:

| Job | Verified result |
| --- | --- |
| [Windows](https://github.com/ghidello/rafter/actions/runs/35646084394/job/106486766424) | Audited restore, analyzer-clean build, formatting, 722 tests, 24 compiled/help examples and 14 execution scenarios |
| [Ubuntu 24.04](https://github.com/ghidello/rafter/actions/runs/35646084394/job/106486766235) | Same checks; 722 passed, zero failed or skipped |
| [macOS](https://github.com/ghidello/rafter/actions/runs/35646084394/job/106486766402) | Same checks; 693 tests in the main step plus 29 process cases in 23 isolated method steps; all passed |
| [Package integrity](https://github.com/ghidello/rafter/actions/runs/35646084394/job/106488179862) | Matching DLL/PDB, 63 Source Link documents for `10cd985`, both fresh-cache external consumers, canonical-reference and unchanged-example checks |

The macOS process-tree timeout and retained-pipe cases passed on this runner. The historical runner-loss cause is
still unknown; investigate any recurrence without treating retries as a repair. The diagnostic method split remains
in place and does not claim to reproduce the older all-in-one test-process conditions.

## Reopened macOS investigation

[CI 20](https://github.com/ghidello/rafter/actions/runs/35647010649), for documentation-only commit `f08459a`,
passed Windows and Ubuntu but stalled at macOS `AuthoredTimeoutTerminatesAReportedProcessTree`, starting at
19:49:37 UTC on 2026-09-21 and remaining in progress beyond the 45-second test and two-minute step deadlines.
The job had no downloadable log archive when inspected. The run was cancelled after confirming the stall.
This reproduces CI 17's last reported method; it does not identify which call inside the test stalled.

Review found that independent fixture cleanup called `Kill(entireProcessTree: true)` synchronously for each known
PID before entering its bounded exit wait. Cleanup now uses individual `Kill()` calls, since every fixture reports
its own PID. The actual Rafter tree-kill assertion remains unchanged. The workflow records `dotnet --info` to
preserve the selected SDK, runtime and architecture for future investigations.
The cleanup change passes all 29 real-process cases and formatting verification locally on Windows.

[dotnet/runtime#131944](https://github.com/dotnet/runtime/issues/131944) reports an upstream macOS arm64 tree-kill
hang in .NET 11. CI 19 installed .NET 10.0.12 on macOS 26 arm64; the upstream report is a lead, not a confirmed
explanation of this failure. The .NET 10.0.12 Unix implementation still stops and kills each node recursively,
whereas the linked issue concerns a later two-phase algorithm. No runtime workaround is justified by this evidence.

The agreed scope through Phase 7 remains open at R5, R8 and R11. Phase 8 typed tools and Phase 9 conformance remain future work.
Further appearance review remains deferred; the accepted presentation fixtures are still the contract.

[CI 21](https://github.com/ghidello/rafter/actions/runs/35648783274), for `392aa32`, passed Windows and Ubuntu,
but again stalled in the same macOS test beyond its deadlines. Individual-PID cleanup did not resolve the stall.
The run was force-cancelled. A standalone diagnostic now precedes the affected macOS test and exercises five
fixture trees with `System.Diagnostics.Process` only, using the same 750 ms delay, redirected streams and dedicated
kill thread. It logs tree-kill return, direct exit/drain completion and independent cleanup separately, without
referencing Rafter or xUnit. All five iterations pass locally on Windows.

Run it after building the solution with `dotnet run eng/diagnose-process-tree.cs --configuration Release --
artifacts/bin/Sotsera.Rafter.ProcessFixture/release/Sotsera.Rafter.ProcessFixture` (append `.exe` on Windows).
This is a diagnostic isolation step, not additional proof that the Phase 7 cancellation gate is closed.

[CI 22](https://github.com/ghidello/rafter/actions/runs/35650207429), for `a541640`, passed all three OS jobs and
package integrity. The macOS probe ran on .NET 10.0.12, macOS 26.6.2 arm64: its five tree-kill calls returned in
28–50 ms; every exit, drain and cleanup completed. The original tree-timeout test also passed (991 ms). This does
not resolve the intermittent failure from the preceding two runs. The next diagnostic mode, `--console-signals`,
adds a `Console.CancelKeyPress` subscription around each iteration and logs unsubscription separately, matching
the runtime signal registration used by Rafter while still omitting Rafter and xUnit entirely.
