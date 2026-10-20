# Windows editor and terminal acceptance refresh

9 October 2026. This checks local package `0.1.0-dev.17`, whose runtime/analyzer source is `9f1f6d0`.
The preceding complete Windows workflow passed all 1,040 tests and package verification. This phase changes only the
optional acceptance harness and documentation; it does not alter runtime behavior, analyzers, public syntax, or layout.

## Accepted scope

All seven recommendations were selected: verify current Windows private use, use package-only file-app and companion
consumers, check installed VS Code editions, exercise the three analyzers and nullability, observe interactive failures
and cancellation across output modes and widths, retain the layout unless a demonstrated defect requires repair, and
keep useful probes optional. Linux/macOS, hosted CI execution, publication, and layout redesign remain separate decisions.

## Repeatable probe and package consumers

The [optional harness](../tools/WindowsAcceptance/README.md) builds a companion linked to one original file-based script,
runs that original script, and launches isolated VS Code extension-test hosts. It remains outside the solution's default
tests and CI. The probe is an acceptance fixture; the teaching examples remain canonical sources under `examples/`.

The final invocation was:

```powershell
./tools/WindowsAcceptance/Verify.ps1 -PackageVersion 0.1.0-dev.17 -Editor Both `
    -ExtensionsDirectory 'C:\Users\Alessandro\.vscode-insiders\extensions'
```

The existing C# extensions are installed in the Insiders extension directory. Both editors deliberately use that same
installed set; the probe does not install them into Stable. Editor settings and consumer files use fresh isolated
directories. No persistent user/machine environment settings or personal editor profiles are changed.

The companion Release build reports zero warnings/errors. The original file app exits 0, identifies itself as `probe.cs`,
and sends greeting/cleanup results to stdout and lifecycle/success summary to stderr. Both restored assets contain
packages only, reference exactly `Sotsera.Rafter/0.1.0-dev.17`, and use the fresh acceptance cache as their sole package
folder. The file-app assets additionally include the SDK's IL compiler/linker packages.

## Actual editor observations

These checks call language-service commands and inspect editor diagnostics in actual running editors. They are distinct
from compiler verification and from a human looking at the editor.

| Tool | Observed version |
| --- | --- |
| VS Code Insiders | 1.142.0-insider |
| VS Code Stable | 1.136.2 |
| C# | 2.160.4 |
| C# Dev Kit | 3.40.210 |
| Selected .NET SDK | 10.0.401 |

Both editions pass the same checks on the original script:

- Command completion includes the typed input, target, cleanup, description, and invocation members.
- Definition navigation resolves the packaged method to matching source available in the local checkout. Private Source
  Link download without that checkout remains untested.
- Required lookup method hover returns `string`; unchecked optional dereference reports CS8602, while required lookup
  dereference does not. The optional warning clears after correction.
- RAF001 and RAF002 appear as errors for nullable option types and sensitive direct-console output, then clear after
  correction. The corrected document has no remaining diagnostics in this probe.
- RAF003 appears as a warning for a bare process task in a synchronous helper, synchronous callback, and
  `ConfigureAwait` wrapper. An awaited correction clears it; an explicit discard is excluded.
- An async helper reports compiler CS4014 without a duplicate RAF003. Consumer `.editorconfig` promotes RAF003 to an
  actual editor error; restoring the original source/configuration clears it.

## Integrated terminal observations

Each edition passes 16 real integrated-terminal runs, for 32 runs total. Normal terminals measure 108 columns; narrow
terminals measure 36. The harness changes the isolated terminal font size and records process-observed widths, rather
than assuming that an environment variable or a renderer test represents the terminal's size.

| Cases per edition | Observed outcome |
| --- | --- |
| Normal success, plain success, `NO_COLOR` success | Exit 0; dependent target runs after local cleanup. |
| Normal missing tool, rejected exit, callback exception, process timeout | Exit 1; target/command cleanup completes; dependent target does not run. |
| Quiet missing tool and rejected exit | Exit 1; routine target heading is suppressed; failure context and unsuccessful summary remain. |
| Verbose missing tool and rejected exit | Exit 1; full labeled original process details are present. |
| Plain and `NO_COLOR` rejected exit | Exit 1; no foreground color sequences. |
| Narrow rejected exit and verbose missing tool | Exit 1; cause/context/cleanup/summary remain available at 36 columns. |
| Ctrl+C during an owned child process | Exit 130; child handles cancellation and is gone after parent settlement; both cleanup tokens are uncancelled. |

Normal terminal output contains foreground colors. Normal and `NO_COLOR` success runs contain live status and line
clearing; plain success contains neither foreground colors nor live line clearing. Rejected exits retain accepted codes
and a labeled captured-stderr section. Process details follow verbose policy. Ordinary callback exceptions retain full
type/message/stack details. Every entered inspection reaches target cleanup and then command cleanup.

The observed failure blocks retain operation description, cause, executable/directory context, cleanup, and final
outcome. At 36 columns, headings and explanatory sentences wrap; executable/directory paths remain intact in the
transport data. No demonstrated runtime or renderer defect required a repair. The compact markers remain provisional;
human approval of their appearance is still separate from these programmatic observations.

The ordinary rejected-exit capture has this structure. Paths and durations are shortened only in this documentation;
the raw capture retains them:

```text
:: inspect
  Process | Produce a greeting as structured text.
Error [target: inspect / process]: Produce a greeting as structured text.
  Exit code 7 was not accepted (accepted: 0).
  Executable: <consumer executable>
  Directory: <consumer directory>
  Captured stderr:
The demonstration child could not produce a greeting.
  Cleanup
Local cleanup completed.
  [OK] cleanup: inspect | <elapsed>
[FAILED] inspect | <elapsed>

Command cleanup
Shared cleanup completed.
[OK] command cleanup | <elapsed>

Failed | 1 succeeded | 1 failed | 1 not run | Target: inspect | <elapsed>
```

## Evidence and limits

Final raw evidence is under ignored `.test-work/windows-acceptance/a1079f8261ed4ec99e966feed61d9a1e/`:

- `result.json`: timestamps, package/fixture/runner/launcher SHA-256 hashes, package-only asset observations, and editor results.
- `Insiders-results.json` and `Stable-results.json`: editor versions, extension locations, diagnostics, terminal widths,
  exit codes, color/live observations, cleanup records, and cancellation settlement.
- Per-case `.terminal.txt` files contain actual ConPTY transport data. `.readable.txt` copies strip control sequences;
  they are not screenshots or terminal-emulator snapshots. In particular, stripped live frames can appear concatenated.
- Separate consumer build/file-app stdout/stderr and editor host/progress logs remain available locally.

Earlier attempts are retained: sandbox DNS initially prevented restore; a probe incorrectly queried terminal width with
redirected output; Insiders initially exited while an existing updater held its startup mutex; Stable initially had no
C# extensions in its default directory. These were environment/probe issues, not evidence of Rafter defects. The final
run uses the corrected fixture and the explicitly selected existing extension set, with both editions passing.

The harness's JavaScript syntax and PowerShell parsing were checked. The final package consumer/editor/terminal run
passes. Runtime and packaging inputs are unchanged, so this phase retains the preceding complete 1,040-test Windows
Release workflow as its runtime baseline instead of rerunning it for documentation/tooling changes.
Linux/macOS, hosted CI execution, publication, and a human visual review remain outstanding.
