# Phase 5–7 closeout audit

## Status as of 2026-09-19

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

## Local verification

Environment: Windows x64, .NET SDK 10.0.401 selected through `global.json` patch roll-forward, PowerShell 7.

| Check | Result |
| --- | --- |
| Normal restore, with auditing and warnings-as-errors | Passed after removing the obsolete override |
| Release solution build | Passed, zero warnings and errors |
| Solution tests | 173 passed, zero failed or skipped |
| Formatting verification | Passed |
| Runtime and symbol package layout | Passed |
| Packaged PDB identity and canonical Source Link map | Passed; 58 runtime documents mapped |
| Fresh-cache external project and file-app consumers | Passed; both bind and execute the public command API |
| Canonical reference check | All 29 examples retain their original project reference |
| Implemented-example compilation and plain help | All 24 classified examples passed |
| Deterministic example execution | All 14 scenarios passed; canonical `.cs` sources unchanged |

The example harness records compilation, help, and scenario results under `artifacts/example-verification`.
The full list and its limits are in the development guide. Artifacts are generated evidence and remain ignored.

## Gate reconciliation

| Phase | Established evidence | Still required before phase completion |
| --- | --- | --- |
| 5 | Reachable graph planning, dependency ordering, failure isolation, conditions, cleanup, cancellation and callback-scope ownership tests; state/cleanup/exit tables in the evidence report | Remaining exhaustive overlap/transition/concurrency cases and a recorded current Windows/Ubuntu/macOS matrix and package job |
| 6 | Semantic routing, console attribution, binding quarantine, restoration, incremental redaction, capture re-entry and sink-failure regressions | Independent capability profiles and `NO_COLOR`; lifecycle observer and live states; full final summary and rich property layout; process-wide ordering/serialization audit; complete boundary, replacement, observer, and renderer-failure matrices |
| 7 | Exact hostile argument vectors, simultaneous pipe drainage, raw capture, per-stream overflow, UTF-8, exit classification, environment, timeout/cancellation, retained pipes and tracked late teardown | Synthetic start/observer/exit/kill/dispose race matrix; measured memory envelope and concurrent-launch stress; exhaustive diagnostic/handle/path/policy tables; full per-test process/resource cleanup evidence and current OS matrix |

The Phase 6 renderer still uses two ANSI Booleans and retains the Phase 5 failure report. `ExecutionRuntime` records
transitions internally but has no live execution observer. Rich properties currently reuse the canonical plain value
representation with color. These are implementation gaps, not missing checkmarks, and must be implemented before
claiming the presentation gates. No new syntax or weakened requirement is proposed here.

The Phase 7 adapter seam exists, but its existing synthetic test covers a late kill/drain transfer only. In particular,
failure while acquiring streams or establishing the exit observer after successful start requires an owned teardown
audit. `DrainAccumulator` is segmented, but the planned allocator-slack and materialization envelope has not been
measured. Do not mark these gates complete based on ordinary successful child processes.

## Cross-platform evidence

The live remote Phase 7 branch still pointed at the audited starting commit. The GitHub connector returned no workflow
runs for that commit or the Phase 6 commit. No current cross-platform success is claimed. The older Phase 4 CI link
remains historical evidence only. After this change is pushed, record the exact commit, run URL, all three OS jobs,
and package-integrity result here; fix failures before marking the repository-quality gates.

## Next implementation order

1. Complete the Phase 6 capability, lifecycle, presentation and shared-ordering contracts with their specified tests.
2. Complete the Phase 7 startup/teardown failure matrix, memory measurements and stress/resource checks.
3. Reconcile the remaining Phase 5 exhaustive verification cases, then record the current cross-platform CI results.
4. Close all affected gates with evidence before beginning the Phase 8 typed builders.
