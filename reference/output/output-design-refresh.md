# Output design exploration

11 October 2026. [Open the local comparison](output-design-refresh.html).

The eight recommendations for this phase are accepted: focus on output design; compare two alternatives with the
current layout; make command/target/cleanup hierarchy explicit; reduce successful cleanup detail; compare completed
target timing policies; keep dependencies flat with relationships in verbose output; cover successful and unsuccessful
scenarios across modes and widths; and present a preview and recommendation before changing the renderer.

The scenario review is complete and **Fixed gutters** is selected for the runtime. The implementation follows that
layout, with terminal-only content prefixes and one final entry result after command cleanup. Public APIs, execution
order, cancellation/settlement rules, exit codes, and stream destinations retain their existing contracts.

## Earlier compact review

This section records the intermediate compact design. The selected fixed gutters below supersede its recursive
diagnostic nesting; terminal-only prefixes are the runtime choice. The other preview choices remain available for comparison.

Current was preferred for the greeting and the build/test/package workflow. The subsequent review used
**Current + reviewed compact**, retaining the earlier proposals for comparison. Reviewed compact keeps the current
spacing and successful cleanup rows, with these refinements:

- The command heading names the selected entry: `> rafter.cs | verify`.
- Invalid input also retains this invocation heading and command description in normal/verbose mode, before the
  diagnostic block. This identifies the script and selected entry without implying target execution. Quiet retains the
  errors and help hint; invalid input still has no target section, cleanup, or execution summary.
- Target headings use `[build]`. An initial inline-description trial was too busy at 80 columns; target descriptions
  now appear only in verbose mode, on the following indented line.
- Dependency steps show their own elapsed time, including their eligible target cleanup. Normal mode no longer shows
  cumulative totals or timing tuples: both repeated dependency time and the labeled tuples made the output too busy.
- A dependency-only entry has no extra execution section or completion before the summary. An entry with a body gets
  its `[verify]` section after dependencies, including its processes, results, and target cleanup. Both cases complete
  once in the final summary: `[OK] verify | 5 targets | 9m 55s`, after command cleanup settles.
- Failed/cancelled final results name the entry goal and retain outcome counts. If another target stopped execution,
  `Stopped at: target` distinguishes that failing step from the entry, which may never have reached its body.
- Other grouping targets retain explicit `(group)` results without a normal-mode own-work `0 ms`. Verbose output names
  dependency, body, and cleanup components and can show traversal totals. Dependency-only bodies are `none`; entry bodies
  that never started are `not run`. Final elapsed time covers the whole invocation, including command cleanup and overhead.
- Child-process presentation can prefix each logical output line with four spaces and `> `, retaining the child's
  existing indentation and stdout/stderr routing. Script-authored results use four spaces and `| `, making messages
  such as `Local packages: ...` part of their target section. Exception details preserve their original content and stack spacing,
  with presentation indentation for the owning phase.
- Verbose shows the full representative executable/argument list and working directory. The workflow commands include
  all arguments for the illustrated Release/package invocation rather than abbreviated excerpts. Placeholder executable
  names with spaces are quoted; runtime quoting and sensitive-value redaction still require implementation tests.
- Process-failure diagnostics sit inside the process section: error headings at four spaces, causes/metadata/excerpt
  headings at six, and captured stderr or verbose exception details at eight. Quiet uses a standalone error heading,
  two-space metadata, and four-space excerpts. Captured stderr is a Rafter-owned diagnostic excerpt, so it retains this
  indentation even in plain mode; the `>` marker still follows the selected prefix routing. Raw process capture is unchanged.
  Reviewed literal rows use browser-only hanging indentation when they soft-wrap, keeping continuations inside their
  block without adding whitespace or line breaks to the logical transcript or clipboard text. Actual terminal wrapping
  remains an implementation/verification concern.
- Callback error headings sit two spaces inside their target, with exception details at four. Target cleanup headings
  remain at two spaces, their errors at four, and exception details at six. Command cleanup starts at column zero,
  with errors at two spaces and exception details at four. Quiet uses standalone error headings and two-space exception
  details. This diagnostic hierarchy also applies in plain mode and retains the stack trace's own indentation, all
  exception details, stderr routing, and red message/yellow stack styling on color-capable terminals. Expected cleanup
  failures nest under the same cleanup heading without acquiring an exception stack.
- Command cleanup remains separated from the preceding target cleanup in quiet mode, even when lifecycle headings
  are hidden. Reviewed exception rows prefer word boundaries when visually wrapping; long identifiers and paths can
  still break to fit narrow views. This browser-only wrapping preserves their logical text and hanging indentation.
- Scoped information and warnings follow the same hierarchy: two spaces inside a target, four inside target cleanup,
  and two inside command cleanup. Quiet still suppresses information and leaves warnings standalone. Diagnostic
  wrapping retains that gutter in plain views as well; authored results and child streams keep their separate prefix rules.

The earlier bare and labeled timing-tuple trial has been replaced by verbose-only breakdowns. Terminal-only child
prefixes versus prefixes in plain/redirected transcripts remain open. Preview controls expose both routing alternatives.
The default uses terminal-only indentation/prefixes; quiet keeps results literal. Selecting prefixes in
plain/redirected output also enables authored-result indentation there. Presentation never modifies raw process captures
or authors' direct Console writes.

Timing fixtures model first traversal from the entry target in declared dependency order. Already completed shared
dependencies contribute no additional time. Nested target totals can overlap and should not be added together; command
cleanup is separate. The component values are derived from illustrative own-work durations, not freshly measured
execution spans, and final command durations can include additional overhead and rounding.

The greeting and build workflow led to these decisions. Failure, skip, shared-dependency, and cleanup scenarios were
subsequently reviewed. An additional design-only scenario explicitly demonstrates an entry body launching a process; it is
not a new canonical example or executable added to the repository.

## Preferred fixed gutters

The user remains unconvinced by indentation across the examples. Incremental nesting made process causes sit at six
spaces and excerpts at eight, while authored output stayed at four. Cleanup exception details also moved relative to
body exceptions. The preview defaults to **Reviewed + fixed gutters** to compare the previous proposal with a
consistent alternative across every scenario. The user prefers fixed gutters; it is now the design to carry forward.
This is the implemented layout.

| Role | Preferred indentation in normal/verbose mode |
| --- | --- |
| Command/target sections, command cleanup, target outcomes, final summary | Column zero |
| Process, target cleanup, and scoped error headings; target cleanup outcomes | Two spaces |
| Authored/child output, scoped information/warnings, error details, process metadata/excerpts, verbose descriptions/timing details | Four spaces |

Diagnostics do not acquire extra indentation inside a process or cleanup phase. Explicit error scopes continue to identify
the owner; expected error causes move to a separate content row rather than sharing the heading's gutter. Original child output and stack indentation is added after the fixed presentation gutter, so an `at` frame
still retains its native spacing. Wrapped lines keep their content gutter. Quiet keeps the existing standalone diagnostics
and shallow exception blocks. Authored/child output in plain/redirected mode still follows the prefix selector; this
comparison deliberately keeps that separate routing choice available.

Command cleanup explicitly names its owning invocation, matching the command header:

```text
Command cleanup | probe.cs | finish
    | Shared cleanup completed.
[OK] command cleanup | probe.cs | finish | 2 ms
```

The script identifies the command; the entry identifies the goal selected for that invocation. This does not make command
cleanup a target-cleanup callback. Its result repeats the owner even in quiet mode, where the heading is hidden. Target
cleanup retains `Cleanup | target` and `[OK] cleanup: target | elapsed`.

The previous reviewed design and earlier alternatives remain selectable. The fixed-gutter design uses the same fixture
content, stream routing, complete exception details, execution order, timings, and entry completion policy. The runtime
now follows the fixed-gutter choice; the canonical examples exercise the same API as before.

## Runtime implementation

The renderer names the entry in the invocation header and completes it once after command cleanup. Dependency results
retain their own duration; other groups show `(group)` instead of a zero-duration body. Verbose timing records first-visit
dependency attribution, body/condition work, and target cleanup, with command cleanup separate in the final breakdown.

Terminal result lines use `    | ` and child stream lines use `    > ` independently per stream, including `NO_COLOR`.
Quiet, plain, and redirected result streams remain literal. Captured stderr excerpts are owned diagnostics, with a
four-space gutter in every normal mode; raw capture data remains unchanged. Each process stream tracks logical lines
across arbitrary read boundaries. A final partial terminal stdout line is closed before later lifecycle output.

Wrapped terminal diagnostics keep their fixed gutter, including complete callback stacks and native stack spacing.
Plain/redirected callback details and process context remain unwrapped. Quiet command cleanup separates visible
messages, failures, and exceptions without emitting a blank section for successful silent cleanup.

Regression checks cover entry/cleanup completion order, shared timings, chunked streams, multiline and empty result
rows, validation gutters, redaction, colored/uncolored wrapping, output-writer failures, cancellation, and actual
file-app invocation. [The output guide](output.md) documents the resulting contract. Earlier terminal/editor captures
remain evidence for their recorded versions rather than captures of this revision.

Final Windows verification passes all **1,062 tests**, including **118 actual file-app checks**, with no failures or
skips. The complete Debug solution build has zero warnings/errors. The selected and earlier reviewed previews pass
594 headless Edge combinations. Native terminal checks cover the greeting in dumb and capable `NO_COLOR` profiles,
colored expected failure with both cleanup phases, rejected process exit with captured stderr, and live target work
through cleanup and final entry completion. These terminal checks use the actual `dotnet run` file-app host; they do
not replace fresh integrated-editor visual acceptance. Test/build reports remain under ignored
`.test-work/output-runtime/`; no new package was produced. Linux and macOS verification remain deferred.

## Alternatives

| Aspect | Current modeled baseline | Refined compact | Clear sections |
| --- | --- | --- | --- |
| Command heading | `> script.cs` | Same familiar marker | `Command: script.cs` with a short underline |
| Target heading | `:: target` | Same marker | `Target: target` |
| Completed targets | Named outcome and elapsed time | Named outcome; timings verbose by default | Same outcome/timing policy as refined compact |
| Successful target cleanup | Heading and named result | Authored messages retained; heading when there is visible output; description/result verbose | Same, with `Cleanup: target` for explicit ownership |
| Successful command cleanup | Separate heading and result | Authored messages retained; description/result verbose | Same policy |
| Failed cleanup | Diagnostic, failed cleanup result, target result, final summary | All remain visible | All remain visible |
| Dependency relationships | Existing execution order | Explicit relationships in verbose output | Explicit relationships in verbose output |
| Final summary | Outcome, counts, affected target where relevant, total elapsed time | Retained | Retained |

Both proposals keep one visible settled outcome per target. Grouping targets stay compact, and shared dependencies appear
once. Successful silent cleanup needs no normal-mode lifecycle rows; verbose output retains its complete lifecycle.
When cleanup writes authored results, the normal-mode heading still identifies the phase. Quiet mode retains results,
warnings, failures, failed cleanup/outcomes, and unsuccessful summaries according to today's policy.

## Initial recommendation, before scenario review

The initial recommendation was **Clear sections**, with overall elapsed time in normal output and completed per-target
timings in verbose output. The scenario review above now guides the next revision.
The explicit labels directly address the earlier concern that the command start is unclear. A short command underline
provides a boundary without repeating a full-width divider for every dependency. Target sections use flat execution
order rather than an execution tree, which could imply that a shared dependency executes more than once.

Refined compact is the smaller visual change and produces slightly shorter headings at narrow widths. Its marker
hierarchy still needs interpretation from a first-time reader. Clear sections uses more characters and one additional
line at command start; the 36-column view exposes that cost.

Timings are an independent choice. The selector compares overall-only normal output with always-visible completed
per-target timings. Both proposals retain current elapsed time in live status and total elapsed time in the final summary;
verbose output always includes per-target durations. Failure causes, process deadlines, and raw exception details remain
visible independently of this preference.

## What to review

Eleven scenarios use the same content for all five designs:

- A greeting with one fast target.
- Rafter's build/test/package/verification workflow, with representative tool-log excerpts.
- An illustrative entry target executing its own process after a build dependency, with target and command cleanup.
- Shared dependencies and a dependency-only grouping target.
- An intentional skip followed by a dependent target.
- Two invalid inputs, with invocation context but no target execution lifecycle or summary.
- Rejected process exit and captured stderr.
- A callback exception followed by command cleanup.
- An illustrative target cleanup exception followed by a separate command cleanup exception, retaining both stacks.
- Cancellation shown after child and cleanup settlement.
- Target cleanup failure with command cleanup still attempted.

Choose normal, quiet, or verbose output; color, `NO_COLOR`, or plain/redirected appearance; 36, 80, or 108 columns; and
combined, stdout-only, or stderr-only views. Dark/light terminal backgrounds expose contrast. During-work snapshots show
the active target without claiming completion. Fast work shows no live line before its delay. Plain/redirected views are
static; `NO_COLOR` retains live status. Copy buttons preserve unwrapped logical transcript text.

Reviewed compact prefixes script-authored results with `    | ` and can prefix child stdout/stderr with `    > ` according to the routing selector;
the underlying text and existing indentation are retained. Quiet and default plain/redirected views keep literal output.
Literal stdout, paths, and callback details can soft-wrap visually in
the browser at the chosen width; the preview inserts no new characters into them. Lifecycle paragraphs use illustrative
word wrapping. Exception type/message lines are red and subsequent details yellow in color views. Process details remain
verbose-only; callback exceptions retain their complete representative details in all modes.

## Evidence and boundaries

The baseline is modeled from the current renderer and its documented contracts, informed by the
[canonical captures](output-review.md), [input/target review captures](input-target-review.md), and
[Windows terminal acceptance](windows-acceptance-refresh.md). It is not a fresh capture or an exact terminal emulator.
Representative tool logs, timings, paths, color palettes, and during-work snapshots are explicitly illustrative.
The callback example retains the representative exception text from the earlier captured cleanup failure.
Combined stream ordering is one possible illustration; independent stdout/stderr captures cannot establish that order.

The self-contained HTML needs no server, package install, external resources, or network access. Headless Edge browser
checks cover 243 combinations: nine scenarios × three modes × three appearances × three widths. Checks also cover stream
filtering, timing changes, quiet callback details, failed cleanup, validation without execution, unstyled plain copies,
`NO_COLOR` live snapshots, cancellation without premature completion, and responsive page overflow. No browser script
errors were observed. Screenshots at desktop and phone widths, narrow cleanup, and light-background callback views were
inspected. The final fixture/preview checks passed; local evidence remains under ignored `.test-work/output-design-refresh/`.

The entry-focused scenario-review revision passes 270 combinations across all ten scenarios for Reviewed compact
alongside Current, plus 270 earlier-design combinations (540 total). Checks cover authored-result prefixes with retained underlying text, stream
preservation, terminal/plain/quiet routing, own-work step timing, one final entry completion, verbose timing breakdowns,
shared-dependency attribution, full verbose commands, complete callback details, stream filtering, unwrapped clipboard
copies, and unsettled cancellation snapshots. An entry-with-body fixture covers a real-shaped process section and both
cleanup phases before the final entry result.
The exception-indentation revision adds a design-only dual-cleanup-exception fixture and passes 297 Reviewed compact
combinations. Checks compare every exception detail against the baseline, including native stack spacing, stderr routing,
message/stack styles, quiet-mode indentation, and both cleanup exceptions before the final result. Callback and cleanup
screenshots also cover narrow/plain and quiet/uncolored views. These synthetic cleanup stacks illustrate nesting;
they are not fresh captures from an executable example.
The subsequent cleanup-failure review checks scoped information/warning indentation and failed-result order across
the same 297 combinations. Normal uncolored and quiet/plain cleanup-failure screenshots were visually inspected.
The dual-cleanup-exception review additionally checks quiet block separation and exception wrapping. Normal/color,
quiet/uncolored, and narrow/plain screenshots cover both exception blocks and their cleanup result rows.
The fixed-gutter trial passes 297 combinations, alongside another 297 checks of the previous reviewed design (594 total).
Checks retain authored/child text, native stack spacing, complete exceptions, stderr routing, outcome order, quiet and
prefix routing, unwrapped clipboard contents, and unsettled cancellation behavior. Desktop comparisons of cleanup,
process failures, and dual exceptions, plus a narrow/plain exception view, were visually inspected. A browser-only
exception-prefix span prevents an empty first visual line when a long type name wraps; DOM and clipboard text retain
the exact logical exception content. No browser errors or page overflow were observed.
The command-cleanup ownership refinement passes the same 594 checks. Heading/result ownership is verified across modes
and widths, including failed cleanup in quiet mode and unwrapped clipboard text. The cancellation comparison and narrow
dual-exception view were visually inspected with the explicit script/entry labels.
Normal and verbose workflow screenshots and a narrow phone layout were visually inspected; no browser errors or page
overflow were observed. These checks validate the prototype, not a changed runtime renderer.

The first headless launch was restricted by the sandbox. An isolated permitted launch succeeded. One initial check
incorrectly expected an unwrapped cleanup error at 36 columns; allowing the intended word wrapping corrected that check.
Visual inspection then prompted browser-only soft wrapping for literal lines, retaining their exact text.

No library, analyzer, example, project, or package input changes are made in this phase. The preceding complete Windows
Release workflow remains the runtime baseline: 1,040 passing tests and verified package `0.1.0-dev.17`.
Actual rendering, live-row transitions, stream routing, widths, redaction, and settlement must be tested if a chosen design
is implemented. The prototypes provide design evidence, not implementation acceptance.

## Next decision

Continue reviewing the preferred **Fixed gutters** design, including command-cleanup ownership, and settle prefix routing.
The chosen design will get its own renderer implementation and focused behavioral/terminal verification.
The accepted preview-first scope deliberately ends at this design review; no renderer change is selected yet.
