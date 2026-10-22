# First working examples: completion summary

Completed on 2026-10-21. The [First working examples milestone](https://github.com/sotsera/rafter/milestone/1)
delivered the selected Windows x64 baseline; its nine assigned issues are closed. Option-policy diagnostics
([#21](https://github.com/sotsera/rafter/issues/21)) and mixed-launcher process isolation
([#23](https://github.com/sotsera/rafter/issues/23)) were completed as follow-ups, with the latter merged through
[PR #24](https://github.com/sotsera/rafter/pull/24) at `eda306513ee9cd0154d6712fc3821569348d398e`.

| Delivered area | Scope and evidence |
| --- | --- |
| Runnable authoring | All eight [examples](../../examples/README.md) run as real C# file apps; regular project consumers are also verified. |
| Commands and inputs | Sync/async bodies, invocation roots, help and exit codes; string/int/bool options, aliases, required/default/optional values, and individual/combined validation. See [minimal runtime](../guides/minimal-runtime.md) and [typed inputs](../guides/typed-inputs.md). |
| Workflows and processes | Sequential target graphs, grouping and deduplication; Windows process ownership, separate streams, child settings, failure actions, Ctrl+C and descendant cleanup. See [targets](../guides/targets.md) and [process runtime](../guides/process-runtime.md). |
| Presentation | Human terminal output, clean redirected streams, help, quiet/verbose/plain modes, and `NO_COLOR`. The actual-runtime review below passed 38 terminal cases; CI runs 23 redirected cases. |
| Authoring and packaging | Four [analyzers](../guides/authoring-diagnostics.md), a [local package](../guides/local-package.md), automatic imports with an opt-out, and live VS Code feedback for file apps and projects. |

The final implementation commit `1e466b2` passed **413 tests, zero failed/skipped**, locally and in both
[upstream CI](https://github.com/sotsera/rafter/actions/runs/39607738575) and
[fork CI](https://github.com/ghidello/rafter/actions/runs/39607837933). Both hosted runs passed 23 redirected-output
checks, built without warnings/errors, and packed successfully. The
[post-merge main run](https://github.com/sotsera/rafter/actions/runs/39614972315) also passed. Actual Ctrl+C recorded
invocation exit `130` and no remaining child. Detailed reproduction and historical evidence limits are retained in
the [process investigation](process-test-isolation.md).

This is a completed first milestone, with public publication, additional platforms, AOT, and later product capabilities
still outside its scope. The [repository organization review](../development/repository-review.md) records the next maintenance
discussion; it proposes changes without treating them as implemented.

## Original output and editor review

Review date: 2026-10-21. [Issue #19](https://github.com/sotsera/rafter/issues/19), starting from merged PR #18
(`ad36178c662a79d8502254ef129f1a779926bde1`). The source changes in this review add repeatable evidence tooling and fix
terminal write coordination. This is a Windows x64 baseline, not a cross-platform or public-release sign-off.

## Actual runtime output

The prior terminal tool exercises the renderer evaluation prototype. The new
[`Verify-RuntimeTerminal.ps1`](../../tools/verification/Verify-RuntimeTerminal.ps1) instead runs the current runtime through a real
consumer and deterministic child processes. A fixture-only tee records text before terminal interpretation while
preserving actual OS handle redirection and bulk-write boundaries. It does not inject Rafter capability policies.

The terminal matrix passed 38 cases: five verbosity/presentation choices across all four stdout/stderr redirection
combinations, plus help and Continue/Fail/Throw/handler/launch failures in normal, plain, and quiet modes. The headless
CI form passed 23 fully redirected cases. Scripts fail on unexpected exit codes, leaked Rafter ANSI on redirected/plain
streams, missing dependency/failure headings, wrong child prefixes, or help contamination of stderr.

Observed behavior matches the chosen layout: descriptions at column zero, one two-space gutter, dependency suffixes,
literal bracket text, preserved child indentation, and one final invocation outcome. Help stays on stdout, with readable
types, primary-name-first aliases separated by commas, rule text, group identity, and body order. Verbose adds the
working directory; process arguments and environment values are not printed automatically. Quiet preserves child/script
output and failure diagnostics. Nonempty `NO_COLOR` removes colors while retaining emphasis and prefixes.

The review exposed a concrete presentation defect: independently locked stdout/stderr renderers could interleave a
child prefix or ANSI style/reset with the other stream's text on the shared terminal. They now share a write gate when
**both** handles are terminal destinations. Each render/chunk stays together; either redirected handle restores
independent gates. Four regression cases check this distinction, including a blocked writer. The terminal matrix was
repeated after the fix and no longer exhibited prefix interleaving.

This does not promise global stream ordering, line buffering, or atomic whole lines when children emit partial chunks.
Independent partial lines can still meet on a combined terminal; arbitrary child ANSI/cursor control remains deferred.

A separate terminal resized to 48 columns reported that actual width and rendered wrapped help with Unicode `界面`,
literal `[red]brackets[/]`, and preserved continuation indentation. That capture inherited `NO_COLOR=1`; colored help
was verified by the normal 80-column matrix. Ctrl+C in an actual terminal emitted yellow `Cancellation requested.`,
completed the managed cleanup message, and recorded Rafter exit `130` before returning. The enclosing PowerShell host
translated its own Ctrl+C result to `1`; the fixture's recorded invocation result is the Rafter evidence.

See [captured samples](runtime-review-samples.md). The samples keep stdout and stderr separate rather than inventing
a merged ordering. Failure-handler checks retain the full inner exception and stack; declaration/input and known
process failures retain concise diagnostics. Existing integration tests separately verify real build/test workflows,
owned descendants, cancellation escalation, and uncertainty outcomes.

## VS Code authoring baseline

The user selected VS Code. Verified locally with VS Code **1.142.0**, Microsoft C# **2.160.4**, and C# Dev Kit
**3.40.215**, using the local package and SDK 10.0.402. The probe launches an isolated profile and package-only consumer
workspace using the installed extensions; it neither installs extensions nor changes the user's normal profile.

Both a regular project and a standalone file app passed five live editor checks:

- RAF001 at the blank description, error severity.
- RAF002 at the nullable type argument, warning severity.
- RAF003 at the discarded process call, error severity.
- Automatic namespace import resolves the API without an explicit using directive.
- Member completion offers `Option<>`, `Target`, and `RunAsync`.

These observations come from the running editor's diagnostic and completion APIs after opening each document, not
from a compiler log or an MSBuild asset list. The test consumer is intentionally invalid and is never executed.
The first completion assertion expected `Option`; the editor correctly displayed the generic `Option<>` label, and
the corrected probe passed. Evidence establishes live editor feedback, not pixel-level squiggle styling, other editor
versions, Rider, or every editor configuration. No editor-specific runtime dependency is introduced.

VS Code's own development-host log contains Node deprecation notices and extension proposal/channel messages.
These are recorded host-log observations, not Rafter build warnings or analyzer load failures. The ten authoring checks
passed and the isolated editor exited. Public API references:
[extension testing](https://code.visualstudio.com/api/working-with-extensions/testing-extension) and
[editor APIs](https://code.visualstudio.com/api/references/vscode-api).

## Reproduce and evidence

The [tool inventory](../../tools/README.md) contains commands for the current layout and output locations.
The commands and artifact paths below record this milestone's original verification layout.

```powershell
dotnet build tests/testassets/consumers/runtime/project/Consumer.csproj --configuration Release
./tools/Verify-RuntimeTerminal.ps1
# For headless CI:
./tools/Verify-RuntimeTerminal.ps1 -RedirectedOnly

dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --output artifacts/packages
./tools/Verify-Editor.ps1 -CodeExecutable 'C:\path\to\Microsoft VS Code\Code.exe'
```

Terminal mode requires both parent streams attached. Raw records are retained under `artifacts/runtime-review/`;
terminal and redirected matrices have separate result manifests. To repeat the narrow/cancellation checks in a terminal,
set `RAFTER_CAPTURE` to an absolute JSON output path and run the built Consumer.dll with `review-help --help` after
setting `[Console]::WindowWidth=48`, or with `cancel` and press Ctrl+C after the ready message. Restore the terminal width
afterward when reusing that terminal. `TERM=xterm-256color` enables the reviewed ANSI capability baseline.

Editor runs create unique `artifacts/editor-review-*/` folders containing their isolated workspace, profile, logs, and
JSON result. The harness uses existing installed C# extensions and allows up to two minutes for each document's live
diagnostics. It closes its own test editor; no personal workspace is modified. CI runs redirected output checks and
retains their records along with regression reports and the package; it does not claim to run the local interactive
terminal or installed-editor checks.

The first full regression attempt, run alongside editor startup, had two process output-drain deadline failures.
Those fixtures use redirected destinations and do not acquire the new shared terminal gate. The deliberately blocking
coordination fixture was moved to dedicated threads to avoid occupying process-pump thread-pool workers. The subsequent
full local suite passed **374 tests**, with no failures, skips, or build warnings. The PR records CI results separately;
failed attempts are not counted as verification.

## Remaining boundaries

The initial Windows examples, package integration, selected diagnostics, output review, and selected editor feedback
are covered. RAF004 subsequently added bounded repeated/conflicting option-policy checks. Broader configuration and
empty-target analyzers remain deferred, together with code fixes, child
ANSI policy, additional platforms, AOT, public publication, and the later capabilities listed in the design plan.
The milestone establishes a working baseline; it does not imply those deferred features or compatibility gates are done.
