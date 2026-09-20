# Accepted Phase 6 presentation contract

Status: **accepted by the user on 2026-09-20**. These authored expected documents define the rendering contract.
After trying the example, the user deferred reconsidering the visual design. Keep these as the current regression
baseline while internal correctness work continues; further appearance changes are postponed for that review.
Final target summaries now have executable snapshot coverage, including all nine profiles. The real canonical
`presentation.cs` success and failure runs are compared with the plain fixtures by the example harness. Rich property
layout, console continuation notation and live repainting remain to be implemented against these accepted documents.

## Reading the fixtures

Each JSON document contains nine independently specified output profiles. Every profile has separate exact `stdout`
and `stderr` strings, including empty streams, LF newlines and ANSI SGR sequences where color is enabled. JSON's
`\n` and `\u001b` encode actual newline and escape characters; `\\n` is visible backslash-plus-n text in a property.
All documents end in LF unless empty. Live lifecycle fixtures instead contain exact terminal surface frames: they
are repaintable content, not lines appended to the permanent transcript. Cursor-control transport is verified
separately when implementing the shared display coordinator.

| Profile | Width | Layout | Symbols | Color | Live surface |
| --- | --- | --- | --- | --- | --- |
| plain | 80, or 40 for the narrow fixture | Plain | ASCII labels | No | No |
| rich-unicode-color | Same | Rich | Unicode plus labels | Yes | No |
| rich-unicode-no-color | Same | Rich | Unicode plus labels | No | No |
| rich-ascii-color | Same | Rich | ASCII labels | Yes | No |
| rich-ascii-no-color | Same | Rich | ASCII labels | No | No |
| live-unicode-color | Same | Rich | Unicode plus labels | Yes | Yes |
| live-unicode-no-color | Same | Rich | Unicode plus labels | No | Yes |
| live-ascii-color | Same | Rich | ASCII labels | Yes | Yes |
| live-ascii-no-color | Same | Rich | ASCII labels | No | Yes |

Plain text is width-independent. Each stream selects its profile independently; mixed-profile invocations combine
the corresponding stdout and stderr documents. Live mode additionally requires safe coordination of both physical
streams. Non-empty `NO_COLOR` selects the corresponding colorless profile without changing layout or symbols.
`--plain` selects plain for both streams and disables live frames. Static profiles emit no transient frames.

## Visible grammar

Keep `[target]` and `[command]` attribution and existing lowercase semantic severity prefixes. Prefix every physical
line of an attributed multiline event. Preserve caller text literally rather than treating it as Spectre markup.
Escape terminal control characters in managed text. Host pass-through remains application-owned.

Plain properties retain `name=value`, JSON-style string quoting, null and collection literals. Rich properties use
`name: value`, `<null>` for null, `""` for an empty string, indented lines for multiline strings, and a single inline
collection when it fits. Otherwise place each collection item on an attributed line with two spaces and `- `.
Use invariant numeric values. Tabs and other non-newline control characters inside values remain visibly escaped.
The narrow fixture fixes a 40-column vertical-collection example; other fixtures use 80 columns. Long indivisible
text may wrap at terminal width, without truncation or omission; further wrap-boundary fixtures remain required.

Start the final summary with one blank line, then `Command succeeded`, `Command failed`, or `Command cancelled`.
Print it once after all target/command cleanup and buffered output settlement. Success goes to stdout; failure and
cancellation go to stderr. List all reachable targets in plan order with two-space indentation. Successful targets
with callbacks say `Succeeded`; successful callback-free targets say `Aggregate` or `No work`. Other outcomes say
`Skipped`, `Failed`, `Cancelled`, or `Blocked`. A blocked row appends `; blocked by: name, name` in authored dependency
order. No durations, percentages, process IDs or duplicate Phase 5 execution report.

Keep the existing primary failure wording with phase and exception type. Do not add raw exception messages or stack
traces. Put secondary target and command cleanup failures under `Cleanup also failed`, separated by one blank line;
do not repeat a cleanup exception already reported as primary. Preserve the scheduler's selected exit code.

| State | Unicode symbol | Color |
| --- | --- | --- |
| Succeeded, Aggregate, No work | `✓` | Green |
| Skipped | `−` | Grey |
| Failed | `✗` | Red |
| Cancelled, Blocked | `!` | Red |
| Waiting | `·` | Grey |
| Running | `›` | Default |
| Cleaning up | `↳` | Default |

ASCII profiles omit symbols and retain the full labels. Color is supplementary: output remains understandable
without it. Success headings are bold green, failure/cancellation and cleanup headings bold red, and the live
`Targets` heading bold blue. Semantic success is green, warning/recovery yellow, error red, and property cyan.
Each styled physical line uses the explicit standard SGR color and a reset before LF; indentation is included in
that style. Unstyled text and blank lines contain no color codes.

Initial `Pending` and later `Ready` both display `Waiting`. Show `Running` and `Cleaning up` at actual transitions.
Repaint the same rows in plan order; identical frames need not cause a physical write. Suspend/erase the live surface
around stdout, stderr, other invocations and host writes, then restore it. Remove it before printing the final summary
so settled rows are not duplicated in the permanent transcript. The fixture specifies visible frames rather than
timers or spinners, so host speed cannot affect snapshots.

A redacted partial console line flushed by an ordering barrier or segment limit ends in ` [continues]`. Its later
fragment begins with `[scope] [continued] `. A completed newline ends the continuation chain. The continuation
fixture fixes these markers; text inside them is renderer-owned and passes final redaction verification.

## Preview

The canonical `presentation.cs` success stdout in plain mode is:

```text
[present] Starting presentation fixture.
[present] Managed console output from the presentation fixture.
[present] fixture="presentation"
[present] missing=null
[present] empty=""
[present] notes="First line.\nSecond line."
[present] include=["src","tests"]
[present] retries=3
[present] success: Presentation fixture passed.

Command succeeded
  [present] Succeeded
```

The same stdout in rich Unicode mode (color omitted from this preview) is:

```text
[present] Starting presentation fixture.
[present] Managed console output from the presentation fixture.
[present] fixture: "presentation"
[present] missing: <null>
[present] empty: ""
[present] notes:
[present]   First line.
[present]   Second line.
[present] include: ["src", "tests"]
[present] retries: 3
[present] success: Presentation fixture passed.

Command succeeded
  ✓ [present] Succeeded
```

Both examples have empty stderr. A failed invocation with secondary cleanup failures has empty stdout and this
plain stderr (including an initial blank line):

```text

Command failed
  [work] Failed
  [entry] Blocked; blocked by: work

error: Target 'work' failed during execution (InvalidOperationException).

Cleanup also failed
error: Target 'work' cleanup failed (IOException).
error: Command cleanup failed (UnauthorizedAccessException).
```

## Fixture index

| File | Contract covered |
| --- | --- |
| [presentation-success.json](presentation-success.json) | All representative properties and successful canonical example |
| [presentation-failure.json](presentation-failure.json) | Canonical failure, recovery, summary routing |
| [semantic-scopes.json](semantic-scopes.json) | Every semantic event at target and command scope; errors as presentation |
| [terminal-shapes.json](terminal-shapes.json) | Success shapes, skipped, failed and blocked; authored blocker order |
| [cancellation-cleanup.json](cancellation-cleanup.json) | Cancellation and secondary target/command cleanup failures |
| [failure-cleanup.json](failure-cleanup.json) | Primary callback failure and both secondary cleanup failures |
| [cleanup-only.json](cleanup-only.json) | Cleanup as primary target failure without duplicate reporting |
| [command-cleanup-only.json](command-cleanup-only.json) | Successful target with failing command cleanup |
| [concurrent-attribution.json](concurrent-attribution.json) | Controlled event interleaving and stable final plan order |
| [console-continuation.json](console-continuation.json) | Partial-line ordering barrier and continuation markers |
| [narrow-properties.json](narrow-properties.json) | Vertical collection layout and escaped controls at width 40 |
| [live-lifecycle.json](live-lifecycle.json) | Actual lifecycle frames and no transient output for static/plain profiles |

There are 99 stdout/stderr document pairs and nine lifecycle-profile entries. The four live entries each specify six
surface frames. These authoring fixtures define the accepted grammar, not evidence that all rendering, concurrency, redaction
or all width boundaries have passed. Existing help and pre-execution diagnostics retain their established grammar.

## Run the actual example

From the repository root in an interactive terminal:

```powershell
dotnet run examples/presentation.cs --configuration Release
dotnet run examples/presentation.cs --configuration Release -- --fail
dotnet run examples/presentation.cs --configuration Release -- --plain
```

The success summary includes `✓ [present] Succeeded`; the failure summary includes `✗ [present] Failed` when the
destination supports Unicode and rich output. Redirected streams and `--plain` use ASCII labels. In a Windows
terminal whose .NET output encoding is not Unicode, set `[Console]::OutputEncoding = [Text.UTF8Encoding]::new()`
before running the example. `NO_COLOR` disables color while retaining the symbols. Until rich properties land,
property lines retain their existing `name=value` layout even when the final summary is rich.
