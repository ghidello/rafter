# Output and help

Current design direction, accepted for now on 2026-10-19. Refine it through examples as implementation progresses.
This replaces the initial proposal and its inline preview. Transcripts below are illustrative, not captured executions.
The [minimal runtime](minimal-runtime.md) implements body-only output and help; [typed inputs](typed-inputs.md) and
[targets](targets.md) extend it with dynamic metadata and graph output. Process output remains pending.
The separate [renderer evaluation](renderer-evaluation.md) provides broader executable checks and
[captured help samples](renderer-samples.md) alongside these illustrative transcripts.

The [example design plan](example-design-plan.md) defines execution behavior and exit codes. The old output explorations
are preserved in [reference/output](../reference/output/README.md). The new
[output comparison](output-preview.html) applies our current direction. The
[old interactive comparison](../reference/output/output-design-refresh.html) preserves its **Fixed gutters** inspiration.
Historical implementation and verification claims apply only to the old project.

## Agreed visual direction

- Use compact target sections such as `[build]`, with headings and outcomes at column zero.
- Show direct dependencies beside target names using `[test] <-- (build)` or `[package] <-- (build, test)`.
  List dependencies in declaration order, without expanding the transitive graph. The suffix is muted in styled
  output and preserved as text in plain output. Omit it for targets without dependencies. Dependency-only entry
  headings use the same notation, such as `> verify <-- (build, test)`, without adding an empty target section.
  Dependency names appear in normal and verbose output; quiet hides these routine headings.
- Use the selected flatter layout: target descriptions at column zero, with a blank line before their content.
  Process headings, scoped error headings, and diagnostic details share one two-space gutter. Do not add deeper
  presentation indentation for nested phases; preserve any original indentation inside child output or exception stacks.
- Show target descriptions on separate lines in normal output for now. Reconsider verbose-only descriptions after
  reviewing longer examples.
- Show each executed target's own duration and the whole invocation's elapsed time in the final result. Do not add
  shared dependency time to each target's own duration or count a group as another executed body.
- Give the invocation one final result. For a single grouping entry, use its name there without a redundant empty
  execution section or separate group-completion line immediately before it.
- In styled terminal output, prefix script-authored result lines with `  | ` and child lines with `  > `.
  Preserve original indentation and stream destinations. Plain/redirected results retain their original content
  without these presentation prefixes.
- Use restrained color and emphasis, with explicit status words that remain understandable in plain text.
- Highlight `Cancellation requested.` in yellow in styled terminal output, consistently with the final Cancelled status.
  It remains a routine lifecycle message hidden by quiet mode; plain output retains the wording without color.
- Keep a description heading and one completion line for a body-only script; do not invent an implicit target.
- Group input errors under one heading. Separate **Command options**, **Execution**, and **Rafter options** in help.

## Streams and terminal behavior

| Content | Destination |
| --- | --- |
| `context.WriteLine(...)` and requested help | stdout |
| Command heading, progress, and final execution summary | stderr |
| Declaration/input diagnostics and execution failures | stderr |
| Child stdout | stdout |
| Child stderr | stderr |

Stderr content does not itself indicate failure. Successful runs can have lifecycle messages and child diagnostics
there; use the command exit code. Failed runs can have produced partial stdout before failure.

Detect capabilities separately for each destination. Redirected output receives no Rafter ANSI styling, live cursor
updates, or result prefixes. Redirecting stdout does not prevent styled progress on terminal stderr. Child-generated
ANSI sequences need a separate policy; Rafter's plain renderer does not automatically control arbitrary child tools.

Separate streams do not provide a guaranteed total ordering when merged. Combined examples illustrate one readable
ordering. Agents should use exit codes, stdout, and readable diagnostics; these labels and spacing are not a stable
machine-readable event protocol.

## Agreed output controls

| Mode | Behavior |
| --- | --- |
| Normal (default) | Descriptions, progress, results, timings, and the final outcome use the agreed layout. |
| `--quiet` | Preserve script and child output. Hide routine lifecycle messages; retain warnings, errors, and the unsuccessful final outcome. |
| `--verbose` | Add execution context, including process working directories. Direct dependency names already appear in normal headings; further detail remains to be defined. |
| `--plain` | Disable Rafter styling, animation, and presentation prefixes without changing the information selected by the verbosity mode. |

`--quiet` and `--verbose` are mutually exclusive; supplying both is invalid input (exit `2`) before execution.
`--plain` is independent and can accompany either mode. Redirected streams automatically use plain presentation,
independently of one another. Plain affects Rafter's presentation, not arbitrary formatting emitted by child tools.
These names are Rafter controls and belong in the Rafter options section of help.

For the first version, show the executable name in process headings and diagnostics. Show the resolved child working
directory in verbose output and relevant failure diagnostics. Do not automatically print process arguments or
environment values in any mode, including verbose, because they may contain sensitive data. Rafter-authored process
diagnostics must not construct a full command line or environment dump as an implicit part of an error message.

An explicit facility for exposing selected arguments with redaction is a future design topic, not an initial
configuration API. This policy governs Rafter-authored process metadata; it does not promise to redact content authored
by scripts or child tools. Exception details follow the agreed failure policy below, including full unexpected callback
exception details. The policy is not a blanket guarantee that arbitrary exception messages contain no sensitive data.
These controls do not introduce a live renderer into the first milestone; animation remains deferred.

During the renderer evaluation, we also agreed to respect a **nonempty `NO_COLOR` environment variable** by disabling
Rafter's colors. This does not itself disable emphasis or terminal presentation prefixes. `--plain` remains the complete
presentation override, and redirected destinations remain plain independently of `NO_COLOR` and CI environment variables.

## Minimal success

For [minimal.cs](../examples/minimal.cs):

```text
Print a greeting.

  | Hello from Rafter.

[OK] Completed | 24 ms
```

Only the greeting is stdout; the heading and final result are stderr. Redirected stdout contains only
`Hello from Rafter.` and its newline. There is no target name or synthetic target count.

## Target workflow

For [dependencies.cs](../examples/dependencies.cs):

```text
> verify <-- (build, test)
Build the project and run its tests.

[build]
Build the project.

  Process | dotnet
  > Build succeeded.
[OK] build | 8.9 s

[test] <-- (build)
Run the tests after a successful build.

  Process | dotnet
  > All tests passed.
[OK] test | 2.1 s

[OK] verify | 2 target bodies completed | 11.0 s
```

Child messages and timings are illustrative. The dependency-only group `verify` completes in the final outcome.
Shared dependencies execute and appear once. `Process | dotnet` uses the executable identity already available;
it does not introduce a process-description API or automatically expose arguments. Completion follows settlement
of the corresponding owned work and output.

Several selected entry targets are also allowed by the new API. The preview's **Multiple selected entries** scenario
establishes the accepted format: list requested entries in supplied order in the header and final summary, while target
sections follow actual dependency execution order. Count each executed body once, including a target that is both an
entry and a dependency. Dependency-only groups do not add to the body count. Neither entry is presented as the owner
of all selected work, and no synthetic grouping name is introduced.

For `RunAsync(args, test, build)`, the header is `> test, build`, the sections execute `build` then `test`, and the
successful summary is `[OK] test, build | 2 target bodies completed | 11.0 s` with an illustrative total duration.
Script-file identity needs a reliable source before adding it to headings.

## Invalid inputs

For a missing `--name` and an invalid integer for `--repeat`:

```text
Invalid arguments
  --name: A value is required.
  --repeat: Expected an integer.

Use --help to see options and rules.
```

Stdout is empty; stderr contains these diagnostics and the command exits `2`. No target section or execution summary
appears. Aggregating parsing/presence errors depends on the parser-recovery decision; ordinary validation-rule rejections
already aggregate. Declaration errors should identify the affected declaration without suggesting that CLI arguments
can fix an invalid script. Do not echo arbitrary rejected values automatically.

## Agreed failure details

Use scenario 18's compact layout consistently for process diagnostics: one status heading with the executable,
followed by relevant details in the same two-space gutter. Yellow identifies an explicitly handled `Continue` failure;
red identifies an unhandled failure or one recorded against the invocation. Color does not replace status words or
change control flow: `Fail` can still let the current body continue, and cancellation keeps its yellow presentation.
Plain output preserves the same wording without color. Unexpected callback exceptions retain their full details below
the compact heading; compact presentation must not truncate the cause or stack.

| Failure | Presentation |
| --- | --- |
| Invalid declarations or inputs | Clear, grouped diagnostics without stack traces. |
| Rejected process exit | Target when applicable, executable, actual exit code, and accepted codes; no framework stack trace. |
| Body or target finishes with outstanding process operations | Clear execution error identifying the owning body or target and unfinished process work; exit `1`, with any cleanup uncertainty reported separately. |
| Owned-process termination cannot be confirmed | **Termination unknown**, with the affected process scope and any observed main-process exit code; do not claim successful completion or clean cancellation. |
| Processes stopped but output completion cannot be confirmed | **Output incomplete**, distinguishing incomplete or unverified output delivery from unknown process termination. |
| Unexpected callback exception | Responsible scope, exception type, message, inner exceptions, and full stack trace. |

Unexpected callback exception details remain visible in normal, quiet, and verbose modes, including plain/redirected
output. Do not require a rerun with verbose enabled to diagnose the original failure. Preserve the full available
exception details and original stack indentation; presentation may add the agreed gutter. A thrown validation callback
is an unexpected callback failure, not an ordinary input-rule rejection.

The final execution summary stays concise: identify the failed invocation and where execution stopped without repeating
the exception or process diagnostic. This presentation policy does not change failure handling, scheduling, or exit codes.
Process failures handled by the script follow the agreed Continue/Fail/Throw behavior. Reporting for Continue, Fail,
and caught Throw is accepted below; remaining failure wording can be refined without changing that timing.

## Accepted help layout and remaining failure details

Help starts with the command description and separates:

- **Command options:** names, types, descriptions, required/optional/default information, and individual rules.
- **Execution:** the supplied body or selected targets and dependencies, distinguishing groups from bodies.
  Include cross-input rule descriptions where appropriate without executing callbacks.
- **Rafter options:** built-in controls.

List an option's primary name first, followed by aliases in declaration order, separated by commas:
`--name, -n, --recipient <string>`. Separators affect help presentation only, not invocation syntax.

Use restrained styling for help on capable stdout terminals: emphasize the command description and section headings,
give primary names and aliases equal color/emphasis, and mute separators. Type hints must remain readily readable and
visually distinct from option names. Keep descriptive text readable
without relying on color to communicate required/default information. The preview uses teal section headings and pale
blue option names, with the selected brighter lavender type hints instead of the earlier muted type color. Exact palette
values are illustrative. `--plain` or redirected stdout removes Rafter color/emphasis
while preserving all wording, punctuation, and ordering. Stderr capability does not determine help's stdout styling.

Keep descriptions readable at narrow widths. Successful help emits stdout without execution-status output on stderr.
Existing declaration/argument validation behavior remains unchanged. Target names describe the supplied graph rather
than introducing CLI selection. The three preview help layouts are accepted for now. Launch/usage discovery, an exit-code section, and detailed
verbose content remain open. Include the agreed quiet, verbose, and plain controls in the Rafter options section.

Failure transcripts should use scoped diagnostics and one final outcome, without repeating a process diagnostic in
the summary. Refine remaining failure and cancellation wording. Process exception recovery is agreed in the design
plan: a caught process exception can be handled,
while an already recorded `Fail` persists. Invocation cancellation is reported as Cancelled with exit `130` after running
callbacks and owned process/output work settle, provided no unhandled or recorded execution failure remains. Such a
failure takes precedence with exit `1`; preserve its original diagnostic. **Termination unknown** and **Output incomplete**
also use exit `1`, with the specific status visible even during cancellation. Expected cancellation errors do not
automatically become independent failures, but output completion still needs verification. Catching cancellation cannot
restore success. The initial shutdown budgets are five seconds for graceful shutdown and another shared five seconds
for termination verification and output draining. Expiry selects the applicable uncertainty diagnostic; it does not
prove cleanup completed. Detailed cleanup-error propagation remains open. Build unexpected callback exception examples
using the full-detail policy above; recognized process failures retain their concise presentation.

## Current comparison and next review

Open [the new output preview](output-preview.html) in a browser. It is self-contained and requires no build, server,
or network access. The layout selector defaults to **Flatter + plain**, showing the selected terminal layout beside its
plain presentation. The flatter layout puts target descriptions at the left margin with a blank line below them, and uses
one two-space gutter for process headings, script/child output, and diagnostics. It preserves original content
indentation, including exception stacks, and leaves help/input-validation formatting unchanged. It replaces the earlier
two/four-space presentation as the agreed direction.

Choose **Compare with previous** to inspect the old and selected layouts side by side, or **Previous + plain** to
inspect the old layout with its redirected presentation.
Original script and child text stays unprefixed in plain output in either layout. Select normal, quiet, or verbose detail,
combined/stdout/stderr output, and available width or a narrow 48-column content area. Stream capabilities remain
independent in the intended runtime; the two panels compare each selected stream with and without Rafter styling.

The 23 scenarios cover:

- All seven current drafts: minimal greeting, typed inputs, validation rejection, successful process execution,
  child working directory, dependency workflow, and `ProcessFailureAction.Fail`.
- Help for typed inputs, validation rules, and the dependency workflow, plus a multiple-entry variant of the dependency example.
- Rejected exit with default `Throw` and an unexpected callback exception with an inner exception.
- Graceful cancellation, forced shutdown followed by confirmed cancellation, termination unknown, output incomplete,
  failure followed by cancellation, and outstanding process work after a body returns.
- A handled `Continue` and a caught process-failure exception, both with successful invocation outcomes.
- A process launch failure and a failure-handler exception that preserves the original rejected exit.

The fixtures distinguish existing example drafts from hypothetical variants. All transcripts, SDK versions, paths,
exceptions, and timings are synthetic. Unknown termination never becomes a successful result merely because the main
process exited with code zero. The incomplete-output scenario explicitly assumes Rafter subsequently settled its own
reader and cleanup operations; the deadline does not authorize leaving those operations running.

Review next:

1. Whether scoped diagnostics and the concise final statuses make failure, cancellation, and uncertainty easy to distinguish.
2. Additional failure and cancellation transcripts for multiple entries, using the accepted entry-order header and summary.
3. Remaining output-failure cases, retaining the agreed reporting timing for Continue, Fail, and Throw.

The help examples establish the following accepted organization:

- **08a / Help · validation rules:** defaults and optionality stay with the option description; individual rule
  descriptions appear beneath it. The optional label explicitly limits its rules to supplied values. The combined
  minimum/maximum rule appears under Execution. Help displays these descriptions without evaluating predicates.
- **08b / Help · dependency workflow:** the selected `verify` entry is labeled as a group without a body. The `build`
  and `test` bodies appear once with their direct dependencies, and a separate line states body order. This describes
  the graph supplied by the script, without introducing CLI target selection or executing any callbacks.

All three help examples use the same Rafter controls, remain entirely on stdout in every verbosity mode, and retain
the same text in plain presentation. Terminal wrapping still needs implementation and actual narrow-terminal verification.

### Accepted handled-process reporting

The control-flow, exit-code semantics, and following reporting behavior are agreed:

- **07 / ProcessFailureAction.Fail:** when the handler returns `Fail`, record invocation failure and print the red
  process diagnostic before returning the unsuccessful result to the body. The body can inspect it and continue
  with cleanup or other work, but later targets do not start. Keep the diagnostic in quiet mode. The final summary
  reports failure without repeating the diagnostic; later successful work cannot clear the recorded failure.
- **18 / ProcessFailureAction.Continue:** after the handler selects `Continue`, report one yellow handled-failure
  warning with the executable, actual/accepted exit codes, and chosen action. Retain it in quiet output under the
  existing warning policy. The returned process result is still unsuccessful, but the invocation may finish with
  `[OK]` and exit `0`. This automatic warning remains useful even when the script explicitly handles the failure.
- **19 / Caught process failure:** defer the framework error diagnostic until the exception escapes the body.
  If script code catches and handles it, preserve original child output and let the script describe its recovery.
  Do not preemptively print a framework error or stack trace. This does not require detecting catch blocks, and it
  does not hide independent failures or cleanup uncertainty. The fixture assumes all owned work settled successfully.

Both examples are synthetic variants of `process-failure.cs`, not changes to that example's existing `Fail` policy.
Quiet preserves child and script output but hides the successful lifecycle summary. A failed child writing to stderr
does not contradict invocation exit `0` after successful recovery.

### Compact launch and handler diagnostics

- **20 / Process cannot start:** report the executable, launch reason, and working directory, followed by one failed
  invocation summary. There is no child exit code, completed process result, or exit-code handler invocation. The
  compact diagnostic uses `Process launch failed | dotnet` and no framework stack for this recognized launch failure. Arguments
  and environment values are not automatically printed.
- **21 / Failure handler throws:** use one `Process failure handler threw | dotnet` heading, followed by the original
  rejected exit details once. Preserve the callback exception type, message, inner exception, and full available
  stack in all modes. End with one concise failed summary. The example groups two causes without treating the callback
  exception as a returned Continue/Fail/Throw action.

Both examples assume exceptions escape the user body and follow the agreed outer exception boundary with exit `1`.
They use the accepted compact process-diagnostic layout; they do not change exception recovery or process ownership rules.

Remaining diagnostic wording is proposed for review, not additional
accepted API requirements. The earlier inline preview remains superseded. The current comparison is a design artifact;
runtime renderer implementation and actual terminal verification remain pending.
