# Phase 5–7 closeout audit

## Status as of 2026-09-20

The audit started at `a9e39198a24855bfcb244c74f0c1230e89471638`. Phases 5–7 contain working implementations,
but they are not complete under the phase contracts. Passing the current suite does not establish every behavior in
the plans. This document records repairs, directly observed verification, and the remaining implementation and
verification work without relaxing those contracts.

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

## Local verification

Environment: Windows x64, .NET SDK 10.0.401 selected through `global.json` patch roll-forward, PowerShell 7.

| Check | Result |
| --- | --- |
| Normal restore, with auditing and warnings-as-errors | Passed after removing the obsolete override |
| Release solution build | Passed, zero warnings and errors |
| Solution tests | 479 passed, zero failed or skipped, including 14 process-disposal cases added during the Phase 7 completion audit |
| Formatting verification | Passed |
| Runtime and symbol package layout | Passed |
| Packaged PDB identity and canonical Source Link map | Passed for `d873080`; 59 runtime documents mapped |
| Fresh-cache external project and file-app consumers | Passed; both bind and execute the public command API |
| Canonical reference check | All 29 examples retain their original project reference |
| Implemented-example compilation and plain help | All 24 classified examples passed |
| Deterministic example execution | All 14 scenarios passed; canonical `.cs` sources unchanged |

The example harness records compilation, help, and scenario results under `artifacts/example-verification`.
The full list and its limits are in the development guide. Artifacts are generated evidence and remain ignored.

## Gate reconciliation

| Phase | Established evidence | Still required before phase completion |
| --- | --- | --- |
| 5 | Reachable graph planning, dependency ordering, failure isolation, conditions, cleanup, cancellation and callback-scope ownership tests; state/cleanup/exit tables; passing current OS matrix and package job | Remaining exhaustive overlap/transition/concurrency cases |
| 6 | Semantic routing, final target summaries with Unicode/ASCII states and cleanup details, independent capability profiles and `NO_COLOR`, early `--plain`, actual lifecycle notifications and observer-failure isolation, console attribution, binding quarantine, restoration, incremental redaction, capture re-entry and sink-failure regressions | Live state rendering and rich property layout; process-wide ordering/serialization audit; complete boundary, replacement, observer, and renderer-failure matrices |
| 7 | Exact hostile argument vectors, simultaneous pipe drainage, raw capture, per-stream overflow, UTF-8, exit classification, environment, timeout/cancellation, retained pipes and tracked late teardown; passing current OS matrix | Synthetic start/observer/exit/kill/dispose race matrix; measured memory envelope and concurrent-launch stress; exhaustive diagnostic/handle/path/policy tables; full per-test process/resource cleanup evidence; unexplained earlier macOS stall |

The Phase 6 renderer uses capability profiles and replaces the Phase 5 failure report with final target summaries.
`ExecutionRuntime` publishes actual lifecycle transitions, but has no live state renderer. Rich properties reuse the canonical plain value
representation with color. These are implementation gaps, not missing checkmarks, and must be implemented before
claiming the presentation gates. No new syntax or weakened requirement is proposed here.

The Phase 7 adapter seam now covers post-start observer failures and late kill/drain transfer. The broader matrix
of start, cancellation, timeout, exit-verification, kill and disposal failures remains open. `DrainAccumulator` is
segmented, but the planned allocator-slack and materialization envelope has not been measured. Do not mark these
gates complete based on ordinary successful child processes or the focused regressions.

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
The package rows above refer to this verified revision. The three additional environment-name review cases and
15 lifecycle-observer, 46 summary, 12 publication, 10 host-failure, 148 console-overload, 4 console-validation and
14 output-admission cases have passed locally; pushing and CI are deferred to the planned final verification run.

## Next implementation order

1. Continue the Phase 6 internal ordering/serialization audit and remaining boundary verification. The user has
   deferred reconsidering output appearance; retain the current [rendering fixtures](phase-06-presentation-fixtures/README.md)
   and postpone further visual design, rich property layout and live presentation changes until that review.
2. Complete the Phase 7 startup/teardown failure matrix, memory measurements and stress/resource checks.
3. Reconcile the remaining Phase 5 exhaustive verification cases and investigate the intermittent macOS stall.
4. Close all affected gates with evidence before beginning the Phase 8 typed builders.
