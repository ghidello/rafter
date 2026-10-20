# Repository automation output review

The [11 October design exploration](output-design-refresh.html) records the selected **Fixed gutters** layout, now
implemented. The [review decisions](output-design-refresh.md) explain entry completion, cleanup ownership, and timing.
The comparison is illustrative; [earlier file-app captures](output-review.md) document their recorded renderer versions.
The [earlier refinement preview](output-refinement-preview.html) and [first captures](output-preview.html) remain
available for historical comparison. Verification entries below identify their original baselines.

## What to run

```sh
dotnet run examples/rafter.cs
dotnet run examples/rafter.cs -- --verbose
dotnet run examples/rafter.cs -- --quiet
dotnet run examples/rafter.cs -- --plain --package
dotnet run examples/rafter.cs -- --help
dotnet run examples/rafter.cs -- --plain --dotnet missing-rafter-dotnet
dotnet run examples/rafter.cs -- --plain --timeout 1ms
```

Use Ctrl+C during a build or test for an interactive cancellation review. The one-millisecond deadline demonstrates
failure rather than asserting deterministic timing. The workflow awaits each external process and its owned shutdown.
Managed callbacks still use cooperative cancellation.

## Agreed structure

Lifecycle information goes to stderr. Tool stdout remains on stdout, tool stderr remains on stderr, help goes to stdout,
and the package location is an authored stdout message. Separate captures preserve this routing, without establishing
their visual interleaving in a shared terminal. SDK launcher output before Rafter starts remains outside its renderer.

The lifecycle for a successful package run has this structure, with variable timings:

```text
> rafter.cs | verify
Build and test Rafter, optionally creating and verifying a local package.

[build]
  Process | Build the solution.
[OK] build | 4.2 s

[test]
  Process | Run the test suite.
[OK] test | 2m 14s

[package]
  Process | Create the local package.
[OK] package | 850 ms

[verify-package]
  Process | Verify the local package.
[OK] verify-package | 1.0 s

[OK] verify | 5 targets | 2m 20s
```

The command description appears once. Target descriptions remain in help and verbose output; executing targets have compact headings.
Dependency-only grouping steps have brief `(group)` results; the entry completes only in the final summary after command
cleanup. A skipped target has one line with its name and
condition description. A condition that emits output or runs long enough for live status gets a heading before that
output. Without `--package`, the summary includes the skipped target; grouping targets count toward the total too.

Process descriptions appear by default, with the executable as a fallback when no description is supplied.
`--verbose` adds the quoted command line and working directory, including when the working directory equals the root.
Known sensitive values remain redacted. Failures identify executable and working-directory context at either verbosity.

Durations use rounded milliseconds below a second, tenths of a second below a minute, minutes/seconds below an hour,
and hours/minutes thereafter. Failure and cancellation summaries identify the affected target (or command cleanup),
omit zero counts, and include targets not run. They preserve the actual outcome and existing exit-code policy.

## Live behavior

Live status starts after 300 ms on capable terminals when both stdout and stderr are attached to terminals. It includes
the current target's name and elapsed time, clipped to fit the live row while preserving the full name in the heading. Rafter clears the
row before writing its own output. During a process it resumes after 500 ms of quiet, only after both streams have
complete lines. A partial line suppresses cursor movement until completed or the process settles. Process stdout
uses four spaces and `> ` on a capable terminal, retaining native indentation. Authored output uses four spaces and `| `.
Raw captures, quiet/plain presentation, and redirected result streams retain literal text. A final partial terminal
stdout line is closed on stdout before later lifecycle output.

Redirecting either stream, `--plain`, or a terminal too narrow for live status uses static lines. `NO_COLOR` disables
color without disabling live status. Direct script-authored `Console` writes retain their ordinary behavior.

Quiet mode and authored diagnostic helpers are now implemented; see [diagnostics and quiet mode](diagnostics.md).
Human visual feedback can still revise the design. The [output review](output-review.md) records earlier
canonical-file captures separately from the historical design previews below.

## Earlier verification

The revised Windows suite passes 744 tests with no failures or skips. It checks complete versus partial process lines
on both streams, quiet-period resumption, redirected-stream suppression, narrow live names, duration boundaries,
verbose redaction, complete live-label redaction before clipping, output failures, target outcomes, and actual canonical
file-app execution. The final solution build has zero warnings or errors.

The actual Windows Release workflow passed its build and all 742 tests available before the final live-redaction
regressions were added. Its redirected lifecycle capture includes:

```text
[OK] test | 2m 26s
[SKIPPED] package | Condition not met: Run when --package is requested.
[OK] verify | 0 ms

Succeeded | 4 targets | 3 succeeded | 1 skipped | 2m 41s
```

An isolated current companion confirms colored help, target-named live status resuming during quiet child execution,
and a plain deadline failure with executable/directory context followed by awaited cleanup. Narrow, wide, plain,
NO_COLOR, and redirected rendering are also exercised through the controlled renderer tests. The terminal transport
terminated its shell on simulated Ctrl+C, so that manual attempt is inconclusive for interactive cancellation;
automated cancellation checks pass and no isolated probe process remained running.

The preceding workflow commit (`bfdafc3`) built, tested, and created a local package on Windows and Linux. At that
baseline Windows passed 721 tests; Linux passed 717 with four expected Windows-only skips. A Linux run exposed reusable
MSBuild workers retaining the owned process group after a build, so the workflow disables build-server and node reuse.
Those observations are historical evidence, not verification of the revised renderer. Further Linux and macOS checks
are deferred at the user's request. Packaging metadata, reproducibility, consumer checks, and CI remain milestone 6 work.

The subsequent [script diagnostics and quiet-mode phase](diagnostics.md) keeps this layout as the normal-mode baseline.
It adds literal, scope-labelled information/warnings and expected failures through the existing renderer, plus `--quiet`
to hide routine lifecycle and informational output while retaining results, child streams, and unsuccessful outcomes.
Windows verification with `0.1.0-dev.3` passes 838 tests and the complete build/package/consumer/reproducibility workflow.
