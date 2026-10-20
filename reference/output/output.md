# Output, help, and failures

[Start here](../README.md) · [Examples](examples.md)

The renderer follows the selected **Fixed gutters** layout in the [design comparison](output-design-refresh.html).
See the [design decisions](output-design-refresh.md) for the reviewed examples.

## Output and failures

Rafter owns all library-controlled output: help, validation, lifecycle, and failure reporting. System.CommandLine is
used only for parsing; Rafter renders its results through the configured writers. Direct `Console` calls from script
code remain untouched.

Use `context.Diagnostics.Info("...")` and `context.Diagnostics.Warning("...")` for literal, scope-labelled messages
on stderr. Warnings are advisory. `context.Fail("...")` records and displays an expected failure without throwing or
creating a stack trace; explicitly `return` to stop later callback statements. Entered work stays awaited, cleanup runs
normally, dependents do not start, and the invocation returns 1. Failure in a condition prevents the body even if the
condition returns true. Failures during cleanup preserve earlier errors; command cleanup remains attempted.

`--quiet` (`-q`) hides lifecycle output, live status, informational messages, and the successful summary. Results,
child streams, warnings, failure details, cancellation/timeout notices, and unsuccessful summaries remain visible.
Help remains complete. Combine it with `--plain` if desired; combining it with `--verbose` is invalid input.
`--verbose` shows execution descriptions, process command lines and directories, and full underlying process-failure
details. Unexpected callback exceptions retain their full available details in every mode.
All diagnostic helpers redact known sensitive values and strip terminal controls. Null or blank diagnostic messages
record handled API misuse, even when hidden. See the [diagnostics example](../examples/diagnostics.cs) and
[diagnostic and quiet-mode details](diagnostics.md).

`context.Output.WriteLine` writes results to stdout. On a capable terminal, each line receives a four-space gutter and
`| ` marker; child process lines use the same gutter with `> `. Native child indentation is preserved. Quiet, plain,
and redirected result streams retain literal text and newlines without these markers. Detection is independent for
each stream. Results are never styled or wrapped. Terminal controls are removed and known sensitive values are masked. Requested
help also uses stdout. Headings, target status, success/failure summaries, and diagnostics use stderr. A successful run
can therefore write to stderr; use the exit code to determine its outcome.

Help puts usage on one line when it fits, then lists command options in declaration order with short aliases first.
Wide terminals align option descriptions in columns; narrow terminals use stacked rows. Required inputs and defaults
use subdued annotations such as `[required]`, `[default: 1]`, and `[default: false]`; string defaults retain quotes.
A compact `Execution` section describes the target and root, followed by `Rafter options` for `--help`, `--plain`,
`--verbose`, and `--quiet`.
Commands without custom options omit the command-options section. On a capable stdout terminal, help uses colored
headings and emphasized option names; descriptions and usage remain literal. Redirecting stdout or using `--plain`
produces unstyled text, while `NO_COLOR` removes color and retains emphasis. Help capability detection is independent
of stderr. Wrapped descriptions stay aligned under their column, and narrow layouts preserve the complete text.

| Exit code | Meaning |
| --- | --- |
| `0` | Success or requested help |
| `1` | Expected script failure, callback/framework/process failure, or timeout |
| `2` | Invalid arguments, definitions, or root configuration |
| `130` | Requested cancellation |

Argument/configuration errors and recoverable framework failures have concise messages. Unexpected callback exceptions
include their full available exception and stack details. They are reported by `RunAsync` and do not escape it.
When a framework failure precedes a callback exception, both diagnostics are preserved, followed by one failed summary.
Output failures are contained as well; reporting is best effort if the diagnostic writer is unavailable.

Rafter flushes help and owned result/diagnostic output before treating delivery as complete. A recoverable flush failure
is a framework failure, including one discovered while writing the final successful summary. Output may already have
been partially delivered when a writer fails; Rafter cannot retract it, and fallback reporting is best effort. Caller-
owned writers are not disposed by the invocation.

Invalid arguments produce one diagnostic block on stderr, followed by a short usage synopsis and a help hint:

```text
> options.cs | hello
Print a greeting using typed command-line options.

  Invalid arguments
    --name is required.
    --repeat expects an integer; received 'two'.

Usage: dotnet run examples/options.cs -- --name <string> [options]

Run with --help to see all options.
```

Multiple errors appear on consecutive lines beneath the same heading. Capable terminals use a red heading, neutral
messages with emphasized option names, and a subdued help hint. The synopsis includes required options; full help is
shown only when requested. Command-definition errors instead use `Invalid command definition`, and invalid resolved
roots use `Invalid root`, without argument usage hints. Validation never starts a target or prints an execution summary.
Usage stays on one line when it fits and moves below its label on narrow terminals. Rafter gives concise messages for
identifiable missing, excess, and invalid typed values while System.CommandLine continues to decide what is valid.
Unrecognized tokens and ambiguous errors retain the native message; this includes malformed Boolean values that the
parser reports only as unrecognized tokens. Diagnostics never depend on matching localized English error strings.
Provided values in friendly diagnostics escape quotes, backslashes, and line breaks so each error remains readable.

File-app usage shows a path relative to the current shell directory when the script is in that directory or a
descendant. Scripts outside that branch retain their absolute path, avoiding `..` traversal in usage. If directory or
path inspection fails, the host's source path remains available as a fallback. The same rule applies to help.

Simple paths omit quotes; whitespace and special characters receive quoting. Generated commands target PowerShell on
Windows and POSIX shells elsewhere. Paths with expansion characters such as `$` or backticks use literal single quoting
and escape embedded single quotes. Usage normally shows `dotnet run script.cs`; when the current directory contains a
project file, it keeps `--file` so the SDK selects the script. If project discovery fails, it also keeps `--file`.
The [SDK file-app rules](https://learn.microsoft.com/dotnet/core/sdk/file-based-apps) describe this distinction.

Execution and rejected input start with `> script | entry` and the command description once. Each executing body has a
compact bold `[name]` heading. Dependency results include their own elapsed time; the entry has one final result after
command cleanup. Target descriptions appear in help and with `--verbose`. Dependency-only grouping targets have a
`(group)` result without a misleading zero-duration body. A grouping entry needs no separate heading or intermediate
result. Skips have one brief line with the condition description. All targets, including groups and skips, count toward
the summary.

```text
> options.cs | hello
Print a greeting using typed command-line options.

[hello]
    | Hello, boh!

[OK] hello | 1 target | 24 ms
```

All output modes use compact `[OK]`, `[FAILED]`, and `[CANCELLED]` markers. Success uses green, failures red, and
cancellation yellow on colored terminals; successful timings are subdued. Durations use milliseconds, tenths of a second, minutes/seconds, or hours/minutes:
`850 ms`, `1.3 s`, `2m 14s`, `1h 3m`. Successful summaries include total targets and duration, adding skip counts only
when needed. Failure and cancellation summaries identify the affected target and show nonzero outcome counts, including
targets not run. `Stopped at: name` identifies a failed dependency when the entry could not finish. The example above
shows both terminal streams together; the greeting alone goes to stdout. In plain or redirected stdout it is `Hello, boh!`.

The layout uses fixed gutters: target headings and final results at column zero; process, target-cleanup, and error
headings at two spaces; authored text, child text, and diagnostic details at four. Callback stack traces retain their
native indentation within that gutter. Captured stderr excerpts follow the same four-space content gutter, including
in plain mode, because they are owned failure diagnostics rather than the raw captured result.

Processes show `  Process | description` by default, or their executable when no description is supplied. `--verbose` also
shows the quoted command line and working directory. These details are redacted like other Rafter-owned output.
Process failures include executable and working-directory context regardless of verbosity.

Target cleanup appears as `  Cleanup | hello` with a named result, such as `  [OK] cleanup: hello | 10 ms`.
It finishes before dependents start. Command cleanup has a separate `Command cleanup | script.cs | hello` section and
`[OK] command cleanup | script.cs | hello | 10 ms` result. Cleanup descriptions appear in help and verbose output. An unsuccessful cleanup
result remains visible in quiet mode. Target durations include their cleanup; the overall duration includes command cleanup.

`--verbose` adds dependency, body, and cleanup timings beneath results, plus command-cleanup time beneath the final
summary. Shared dependencies are attributed to the first branch that visits them and are counted once. A group has
`Body: none`; skipped or unentered bodies have `Body: not run`. Body timing includes condition evaluation and target
overhead; the overall duration also includes orchestration overhead.

Failure labels identify their scope and kind: `Error [target: hello / callback]`,
`Error [target: hello / cleanup]`, `Error [target: hello / process]: ...`, or
`Error [command / framework]: ...`. Callback details appear after that callback and its owned process work settle,
before the next phase starts. Exception type/message lines use red and subsequent details use yellow on colored terminals.
The complete available exception text is retained, including long lines, which may wrap visually in the terminal.
Repeated observations of the same exception are deduplicated, while distinct body and cleanup errors remain visible.

Spectre.Console styles diagnostics when stderr supports color. After a callback has been running for 300 ms, a capable
terminal shows a short ASCII spinner with the running target's name and elapsed time on stderr. Fast runs never show it. Rafter clears
that temporary line before its output writes. During a process, live status resumes
after 500 ms of quiet only when both streams have complete lines. Partial lines suppress the spinner until completed
or the process settles. Long live labels are clipped with `...`, keeping elapsed time when space permits; headings retain
the full name. Cleanup live rows identify `name / cleanup` or `command cleanup`, with independent elapsed time. Rafter awaits callback
work and stops live refresh before reporting the final outcome, including when execution or rendering fails.

Redirecting either stream and terminals too narrow for the running line use static output. `--plain` uses a static ASCII layout
without colors, cursor controls, or hyperlinks, while preserving authored text. `NO_COLOR` disables color only; it does
not disable live status or terminal content markers. The [earlier captures](output-review.md) and earlier design
previews remain historical references; the [selected comparison](output-design-refresh.html) shows the current layout.

To capture results separately from diagnostics in PowerShell or a POSIX shell:

```sh
dotnet run examples/minimal.cs -- --plain > result.txt 2> diagnostics.txt
```

Use `> transcript.txt 2>&1` to combine both streams. SDK build messages are produced by the launcher and are outside
Rafter's routing guarantees. Ordinary `Console.WriteLine` and `Console.Error.WriteLine` calls keep their normal behavior;
Rafter coordinates only its own output API.
