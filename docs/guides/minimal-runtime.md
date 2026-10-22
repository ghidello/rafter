# First working runtime slice

Issue [#9](https://github.com/sotsera/rafter/issues/9), implemented and locally verified on 2026-10-20.
[examples/minimal.cs](../../examples/minimal.cs) now runs against [Sotsera.Rafter](../../src/Sotsera.Rafter/Sotsera.Rafter.csproj).
This records the original slice. [Typed inputs and validation](typed-inputs.md) now make two more examples runnable.

## Implemented contract

`Rafter.Command(description)` creates command metadata. The two body overloads accept
`Action<RafterContext>` and `Func<RafterContext, Task>` and return `Task<int>`. Description and body are required by
the compiler; null/blank descriptions and null body delegates are rejected at invocation time with exit 2.
Declaration validation precedes parsing and help. A null task returned by an asynchronous body is an execution failure.

Each invocation captures its working directory before the first asynchronous suspension. State, cancellation, and
output belong to that run. The captured root is internal for now; no explicit-root or path API is introduced. Rafter
does not change the process working directory, replace console writers, or dispose host writers.

The built-in `--help`, `--quiet`, `--verbose`, and `--plain` controls are case-sensitive switches without values or
aliases. Repetition, attached values, positional input, colon syntax, response files, directives, and quiet plus verbose
are invalid input. This slice originally supported only controls; [typed inputs](typed-inputs.md) extend it. Malformed input still fails with help, and safe
diagnostics do not echo rejected tokens. Help writes stdout only and never runs the body. Verbose currently has the
same content as normal for a body-only invocation; extra process context belongs to the process slice.

`context.WriteLine` writes literal stdout with its original indentation and embedded newlines. Lifecycle and errors
go to stderr. Terminal capabilities and redirection are independent per stream; explicit plain mode removes styling
and prefixes, while nonempty NO_COLOR removes colors but retains emphasis/prefixes. Quiet hides routine lifecycle
output and successful summaries, retaining results and failures. Full unexpected exception details remain visible.

RunAsync contains recoverable setup, callback, and reporting exceptions. It returns 0 for success/help, 2 for invalid
declarations/input, 1 for execution/output failures, and 130 for clean invocation cancellation. Unrelated cancellation
exceptions remain failures. Process-ending runtime failures and detached work are outside the boundary.

Ctrl+C signals `context.CancellationToken`. Cancellation is cooperative: a callback that ignores the token can delay
completion. Catching cancellation cannot restore success, and a second interrupt does not abandon managed callbacks.
Cancellation registrations settle before output closes. The console subscription stays installed until output flushes
complete, so further interrupts cannot terminate final settlement; it is removed before RunAsync returns.
Errors in cancellation registrations are contained and take precedence over cancellation. No external-token overload,
timeout, forceful managed shutdown, or process-ownership claim is introduced.

Writer errors remain invocation failures even if the body catches the write exception. Both destinations receive a
completion attempt; late context writes are rejected. A broken stderr may make diagnostics unavailable. If the final
flush fails after a summary has already been written, exit 1 is authoritative; output cannot be retracted. A late interrupt
during final flushing similarly selects exit 130 without restarting settled user cancellation registrations or writing
into closing destinations. A summary already sent before that interrupt cannot be retracted. A best-effort
literal fallback attempts to report through the host error writer without leaking a second reporting exception.

## Verification

Local environment: Windows 10.0.26220 x64, SDK 10.0.402, managed net10.0 execution, runtime 10.0.13.
The existing native-MTP test entry point reports **Passed: 166, Failed: 0, Skipped: 0**: 109 previous evaluation cases
and 57 runtime/consumer cases. The final Release build has zero warnings/errors. The PR records GitHub CI evidence.

- [Invocation tests](../../tests/Rafter.Tests/Commands/InvocationTests.cs) cover validation/help ordering, controls,
  safe diagnostics, literal output, full sync/async exceptions, null tasks, independent invocation state, cancellation
  settlement/failure precedence, delayed flushes, writer failures, ownership, and runtime NO_COLOR styling.
- [Host tests](../../tests/Rafter.Tests/Commands/RuntimeHostTests.cs) change the actual process working directory in an
  isolated test collection, proving capture at invocation entry and retention after a later host change.
- [Consumer tests](../../tests/Rafter.Tests/Consumers/RuntimeConsumerTests.cs) build and run actual file-app and ordinary
  project consumers in temporary workspaces outside the checkout. They execute sync/awaited async bodies, help,
  declaration errors, and nested failures; build-negative cases prove missing description/body rejection.
  The actual minimal.cs file is also built and run unchanged from its copied workspace.
- Manual Windows pseudoterminal checks exercised both terminal streams, each independently redirected, colored output,
  NO_COLOR help/results, and plain Ctrl+C. Redirected captures contained no ANSI or result prefixes. A real ETX/Ctrl+C
  reached the console subscription and printed managed cleanup before the Cancelled summary. The fixture persisted
  RunAsync's returned **130** separately because the enclosing PowerShell terminal tool reported wrapper exit 1.
  This distinguishes the verified invocation result from that wrapper's exit status.

Run the [current development commands](../README.md#developing-this-repository) for build/test. For the real example:

```powershell
dotnet run --file examples/minimal.cs -- --help
dotnet run --file examples/minimal.cs -- --quiet
```

For interactive cancellation, build the regular consumer and run it in a terminal, then press Ctrl+C:

```powershell
dotnet build tests/testassets/consumers/runtime/project/Consumer.csproj --configuration Release
dotnet tests/testassets/consumers/runtime/project/bin/Release/net10.0/Consumer.dll cancel --plain
```

The fixture's optional RAFTER_FIXTURE_RESULT environment variable names a file to record the invocation result.
This is test instrumentation, not a runtime setting. Manual signal checks complement deterministic cancellation tests;
the CI suite does not synthesize terminal Ctrl+C.

## Captured minimal output

Actual redirected stdout from the minimal example:

```text
Hello from Rafter.
```

Its separate stderr capture (elapsed time varies):

```text
Print a greeting.


[OK] Completed | 124 ms
```

The two blank stderr lines bracket the stdout result in the intended combined terminal view. Separate streams do not
promise merged ordering. Quiet produces the greeting and an empty stderr. Help succeeds with empty stderr and includes
the description, Command options / None, Execution / supplied body, and all four Rafter controls.

## Dependencies, skills, and remaining scope

The runtime adopts the evaluated System.CommandLine 2.0.13 and Spectre.Console 0.57.2 internally. Its restored net10.0
package graph contains only those and Spectre.Console.Ansi 0.57.2. There is no test, analyzer, hosting, or logging package
in that graph. Versions remain central. Runtime and evaluation fixtures are separate source trees; the runtime does not
reference historical code or compile evaluation sources. No upstream implementation source was copied.

The scaffold-dotnet-test-project skill led to reusing the existing test project and CI entry point with one production
reference. No placeholder solution or analyzer project was added. csharp-scripts guided real file-app project references
and explicit managed execution; run-tests guided native MTP and TRX reporting. The checked-in fixtures are retained.

Subsequent slices implement [typed inputs/validators](typed-inputs.md), [target graphs](targets.md), and
[Windows processes and descendant settlement](process-runtime.md). [Initial analyzers](authoring-diagnostics.md), automatic
imports, and [local NuGet packaging](local-package.md) are now implemented as a later slice too. Child ANSI policy,
native AOT, and Linux/macOS compatibility need their own verification. No production startup budget is claimed
from the evaluation benchmarks or the elapsed number in this transcript.
