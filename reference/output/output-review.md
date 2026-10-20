# Compact-marker output review

[Output contracts](output.md) · [Design comparison: proposal A selected](output-design-review.html)

These seventeen scenarios ran as canonical file-based apps on Windows with SDK 10.0.401 on 6 October 2026.
Both streams were redirected and captured separately; automatic output therefore uses static uncolored text.
Elapsed durations are replaced with `<elapsed>` and the repository absolute path with `<repository>`.
Source locations, callback details, and deadline values are otherwise retained. This does not establish the visual
order of independently captured streams in a shared terminal. Colored/live output, narrow widths, cancellation,
and cleanup settlement are checked through controlled behavioral tests. These captures did not exercise interactive Ctrl+C;
the subsequent [private-use pass](private-use-review.md) checks cancellation through real VS Code terminal input.
Linux/macOS verification remains outstanding. The design comparison remains illustrative, including its running snapshots.

The real Release build/test/package example builds with zero warnings/errors, passes all 879 then-existing tests without
skips, and creates `0.1.0-dev.5`. The combined run was stopped during package verification after an SDK compiler child
remained alive. After repairing the verifier's source-edit build path, a separate verification of that package finishes
successfully: both consumer hosts, analyzer failures, deadlines, diagnostics, metadata, Source Link, and reproducible
payloads pass. Eight focused SDK/package tests also pass, including the new source-only-edit settlement regression.

## minimal

```sh
dotnet run --file examples/minimal.cs -- --plain
```

Exit code: 0.

stdout:

```text
Hello from Rafter.
```

stderr:

```text
> minimal.cs
Run the smallest useful Rafter command.

:: hello
[OK] hello | <elapsed>

Succeeded | 1 target | <elapsed>
```

## options-success

```sh
dotnet run --file examples/options.cs -- --plain --name Ada --repeat 2
```

Exit code: 0.

stdout:

```text
Hello, Ada!
Hello, Ada!
```

stderr:

```text
> options.cs
Print a greeting using typed command-line options.

:: hello
[OK] hello | <elapsed>

Succeeded | 1 target | <elapsed>
```

## options-invalid

```sh
dotnet run --file examples/options.cs -- --plain --repeat two
```

Exit code: 2.

stdout:

Empty.

stderr:

```text
Invalid arguments
  --name is required.
  --repeat expects an integer; received 'two'.

Usage: dotnet run examples\options.cs -- --name <string> [options]

Run with --help to see all options.
```

## options-help

```sh
dotnet run --file examples/options.cs -- --help
```

Exit code: 0.

stdout:

```text
Print a greeting using typed command-line options.

Usage: dotnet run examples\options.cs -- --name <string> [options]

Command options:
  -n, --name <string>     The name to greet. [required]
  -r, --repeat <int>      How many greetings to print. Rule: Must be zero or greater. [default: 1]
  -u, --uppercase [bool]  Print the greeting in uppercase. [default: false]

Execution:
  Target: hello - Print the requested greetings.
  Root:   Invocation directory.

Rafter options:
  -h, --help              Show help.
      --plain             Disable styling and live status.
      --verbose           Show process command lines and working directories.
  -q, --quiet             Hide lifecycle and informational messages; keep results, warnings, and failures.
```

stderr:

Empty.

## dependencies

```sh
dotnet run --file examples/dependencies.cs -- --plain
```

Exit code: 0.

stdout:

```text
Inputs prepared.
Build completed.
Tests completed.
```

stderr:

```text
> dependencies.cs
Run dependent targets sequentially, sharing common work.

:: prepare
[OK] prepare | <elapsed>

:: build
[OK] build | <elapsed>

:: test
[OK] test | <elapsed>
[OK] all | <elapsed>

Succeeded | 4 targets | <elapsed>
```

## conditions-skip

```sh
dotnet run --file examples/conditions.cs -- --plain
```

Exit code: 0.

stdout:

```text
Inputs prepared.
Workflow completed.
```

stderr:

```text
> conditions.cs
Skip optional work while allowing dependent targets to continue.

:: prepare
[OK] prepare | <elapsed>
[SKIPPED] publish | Condition not met: Run when release is requested.

:: finish
[OK] finish | <elapsed>

Succeeded | 3 targets | 2 succeeded | 1 skipped | <elapsed>
```

## quiet-success

```sh
dotnet run --file examples/diagnostics.cs -- --quiet
```

Exit code: 0.

stdout:

```text
Work completed.
Dependent work completed.
```

stderr:

```text
Warning [target: work]: This is a demonstration warning; work can continue.
```

## quiet-failure

```sh
dotnet run --file examples/diagnostics.cs -- --quiet --fail Body
```

Exit code: 1.

stdout:

Empty.

stderr:

```text
Warning [target: work]: This is a demonstration warning; work can continue.
Error [target: work]: The result could not be produced.
[FAILED] work | <elapsed>

Failed | 1 failed | 1 not run | Target: work | <elapsed>
```

## target-cleanup-failure

```sh
dotnet run --file examples/diagnostics.cs -- --quiet --fail TargetCleanup
```

Exit code: 1.

stdout:

```text
Work completed.
```

stderr:

```text
Warning [target: work]: This is a demonstration warning; work can continue.
Error [target: work / cleanup]: Local resource release could not complete.
  [FAILED] cleanup: work | <elapsed>
[FAILED] work | <elapsed>

Failed | 1 failed | 1 not run | Target: work | <elapsed>
```

## command-cleanup-failure

```sh
dotnet run --file examples/diagnostics.cs -- --quiet --fail CommandCleanup
```

Exit code: 1.

stdout:

```text
Work completed.
Dependent work completed.
```

stderr:

```text
Warning [target: work]: This is a demonstration warning; work can continue.
Error [command cleanup]: Shared resource release could not complete.
[FAILED] command cleanup | <elapsed>

Failed | 2 succeeded | Cleanup: command | <elapsed>
```

## process-verbose

```sh
dotnet run --file examples/processes.cs -- --verbose --plain
```

Exit code: 0.

stdout:

```text
10.0.401
SDK inspection completed.
```

stderr:

```text
> processes.cs
Run dotnet with streamed output and an explicit child environment policy.

:: version
  Print the SDK version using the command root as the working directory.
  Process | Inspect the selected .NET SDK.
  $ dotnet --version
  Directory: <repository>
[OK] version | <elapsed>

Succeeded | 1 target | <elapsed>
```

## managed-timeout

```sh
dotnet run --file examples/managed-timeout.cs -- --quiet --timeout 100ms --work 1s
```

Exit code: 1.

stdout:

```text
Target work started.
Target work settled.
Target cleanup completed.
Command cleanup completed.
```

stderr:

```text
Timeout [target: work]: Deadline elapsed after 100ms; cancellation requested, awaiting completion.
Timeout [target: work]: Work timed out; callback and owned process work has settled.
[TIMED OUT] work | <elapsed>

Failed | 1 failed | 1 not run | Target: work | <elapsed>
```

## cleanup-success

```sh
dotnet run --file examples/cleanup.cs -- --plain
```

Exit code: 0.

stdout:

```text
Inputs prepared.
Preparation resources released.
Read: prepared inputs
Workflow completed.
Shared resources released.
```

stderr:

```text
> cleanup.cs
Release target resources immediately and shared resources when the command finishes.

:: prepare
  Cleanup
  [OK] cleanup: prepare | <elapsed>
[OK] prepare | <elapsed>

:: finish
[OK] finish | <elapsed>

Command cleanup
[OK] command cleanup | <elapsed>

Succeeded | 2 targets | <elapsed>
```

## cleanup-verbose

```sh
dotnet run --file examples/cleanup.cs -- --plain --verbose
```

Exit code: 0.

stdout:

```text
Inputs prepared.
Preparation resources released.
Read: prepared inputs
Workflow completed.
Shared resources released.
```

stderr:

```text
> cleanup.cs
Release target resources immediately and shared resources when the command finishes.

:: prepare
  Create one temporary file for this target and one shared input for the workflow.
  Cleanup
    Delete preparation resources before the next target starts.
  [OK] cleanup: prepare | <elapsed>
[OK] prepare | <elapsed>

:: finish
  Read the shared input, optionally fail, and finish the workflow.
[OK] finish | <elapsed>

Command cleanup
  Delete the shared input file after all entered work settles.
[OK] command cleanup | <elapsed>

Succeeded | 2 targets | <elapsed>
```

## callback-failure

```sh
dotnet run --file examples/cleanup.cs -- --plain --fail
```

Exit code: 1.

stdout:

```text
Inputs prepared.
Preparation resources released.
Read: prepared inputs
Shared resources released.
```

stderr:

```text
> cleanup.cs
Release target resources immediately and shared resources when the command finishes.

:: prepare
  Cleanup
  [OK] cleanup: prepare | <elapsed>
[OK] prepare | <elapsed>

:: finish
Error [target: finish / callback]
System.InvalidOperationException: Requested failure after reading shared inputs.
   at Program.<>c__DisplayClass0_0.<<<Main>$>b__3>d.MoveNext() in <repository>\examples\cleanup.cs:line 52
--- End of stack trace from previous location ---
   at Sotsera.Rafter.Internal.Invocation.ExecuteCallbackAsync(Func`2 callback, RafterContext context, InvocationState state, CancellationToken callerCancellation, String callbackKind) in <repository>\src\Sotsera.Rafter\Internal\Invocation.cs:line 623
[FAILED] finish | <elapsed>

Command cleanup
[OK] command cleanup | <elapsed>

Failed | 1 succeeded | 1 failed | Target: finish | <elapsed>
```

## body-fault-and-cleanup-timeout

```sh
dotnet run --file examples/cleanup-timeout.cs -- --plain --fail-body --cleanup-timeout 100ms
```

Exit code: 1.

stdout:

```text
Body entered.
Target cleanup settled.
Command cleanup settled.
```

stderr:

```text
> cleanup-timeout.cs
Give target and command cleanup independent cooperative deadlines.

:: work
Error [target: work / callback]
System.IO.IOException: Demonstration body failure.
   at Program.<>c__DisplayClass0_0.<<Main>$>b__3(RafterContext context) in <repository>\examples\cleanup-timeout.cs:line 50
   at Sotsera.Rafter.Target.<>c__DisplayClass25_0.<Run>b__0(RafterContext context) in <repository>\src\Sotsera.Rafter\Target.cs:line 87
   at Sotsera.Rafter.Internal.Invocation.ExecuteCallbackAsync(Func`2 callback, RafterContext context, InvocationState state, CancellationToken callerCancellation, String callbackKind) in <repository>\src\Sotsera.Rafter\Internal\Invocation.cs:line 615
  Cleanup
Timeout [target: work / cleanup]: Deadline elapsed after 100ms; cancellation requested, awaiting completion.
Timeout [target: work / cleanup]: Work timed out; callback and owned process work has settled.
  [TIMED OUT] cleanup: work | <elapsed>
[FAILED] work | <elapsed>

Command cleanup
[OK] command cleanup | <elapsed>

Failed | 1 failed | Target: work | <elapsed>
```

## process-failure

```sh
dotnet run --file examples/rafter.cs -- --plain --dotnet missing-rafter-output-review-executable
```

Exit code: 1.

stdout:

Empty.

stderr:

```text
> rafter.cs
Build and test Rafter, optionally creating and verifying a local package.

:: build
Error [target: build / process]: Process 'missing-rafter-output-review-executable' could not complete: Executable 'missing-rafter-output-review-executable' was not found on the child environment's PATH. Supply an explicit executable path or configure PATH.
  Executable: missing-rafter-output-review-executable
  Directory: <repository>
[FAILED] build | <elapsed>

Failed | 1 failed | 4 not run | Target: build | <elapsed>
```

## Completed package verification

This is the actual standalone verifier's stdout after the tooling repair, with the artifact path normalized:

```text
Verified metadata, runtime dependencies, API documentation, symbols, and Source Link.
Verified isolated file-app: help, inputs, dependencies, processes, managed deadlines, diagnostics, RAF001, and RAF002.
Verified isolated project: help, inputs, dependencies, processes, managed deadlines, diagnostics, RAF001, and RAF002.
Verified reproducible DLLs, portable PDBs, documentation, and substantive package contents.
Verification artifacts: <repository>/.test-work/package-verification/<run>
```

Its stderr uses the same compact command/target/process markers and ends with:

```text
[OK] verify-package | <elapsed>

Succeeded | 1 target | <elapsed>
```

Exit code: 0.

## Process API consistency captures

These additional canonical file-app runs use Windows / SDK 10.0.401 after the process API consistency fix.
They retain the existing compact layout. The [API review](process-api-review.md) separates implemented fixes from
proposed analyzer and diagnostic improvements. Stdout/stderr were redirected separately; elapsed time, the repository
path, SDK directory, and generated file-app executable paths are normalized below. Installed SDK versions are actual
local observations. No colored/live or human visual review is implied.

Every launch used `dotnet run --file <source> --no-cache --disable-build-servers -p:UseSharedCompilation=false --`,
with telemetry, MSBuild node reuse, and shared compilation disabled in the launcher environment.
Raw streams and `captures.result.json` are under ignored `.test-work/process-api-review/`.

| Scenario | Script arguments | Exit code |
| --- | --- | --- |
| Shared configuration success | `--plain --verbose` | 0 |
| Shared configuration launch failure | `--plain --dotnet .test-work/process-api-review/missing-dotnet` | 1 |
| Shared configuration quiet launch failure | `--plain --quiet --dotnet .test-work/process-api-review/missing-dotnet` | 1 |
| Invalid configuration duration | `--plain --timeout 0` | 2 |
| Captured child failure | `--plain --scenario Failure` | 1 |
| Process finishes before deadline | `--plain --work 0 --timeout 5s` | 0 |
| Process deadline and successful cleanup | `--plain` | 1 |

### Shared configuration, verbose success

stdout:

```text
Selected SDK: 10.0.401
Installed SDKs:
6.0.428 [<SDK directory>]
8.0.424 [<SDK directory>]
9.0.318 [<SDK directory>]
10.0.400 [<SDK directory>]
10.0.401 [<SDK directory>]
11.0.100-rc.1.26425.128 [<SDK directory>]
```

stderr:

```text
> process-configuration.cs
Reuse execution settings across independent dotnet processes.

:: selected
  Print the SDK selected in the invocation directory.
  Process | Inspect the selected SDK.
  $ dotnet --version
  Directory: <repository>
[OK] selected | <elapsed>

:: installed
  Print installed SDKs using a separate process with the same defaults.
  Process | Inspect installed SDKs.
  $ dotnet --list-sdks
  Directory: <repository>
[OK] installed | <elapsed>

Succeeded | 2 targets | <elapsed>
```

### Shared configuration, quiet launch failure

stdout:

Empty.

stderr:

```text
Error [target: selected / process]: Process '.test-work/process-api-review/missing-dotnet' could not complete: Failed to start a process with file path '<repository>\.test-work\process-api-review\missing-dotnet'. Target file or working directory doesn't exist, the provided credentials are invalid, or the resource policy cannot be set due to insufficient permissions. See the inner exception for more information.
  Executable: .test-work/process-api-review/missing-dotnet
  Directory: <repository>
[FAILED] selected | <elapsed>

Failed | 1 failed | 1 not run | Target: selected | <elapsed>
```

### Captured child failure

stdout:

Empty.

stderr:

```text
> process-capture.cs
Capture structured child output within explicit bounds, with optional live echo.

:: inspect
  Process | Produce a greeting as structured text.
Error [target: inspect / process]: Process '<file-app executable>' exited with code 7.
  Executable: <file-app executable>
  Directory: <repository>
Error [target: inspect / process]: Captured stderr:
The demonstration child could not produce a greeting.
[FAILED] inspect | <elapsed>

Failed | 1 failed | Target: inspect | <elapsed>
```

### Process deadline with successful cleanup

stdout:

```text
Child work started.
Child cleanup started.
Child cleanup finished.
Parent cleanup completed.
```

stderr:

```text
> process-timeout.cs
Start process shutdown at a deadline, then await it before running cleanup.

:: run
  Process | Run bounded child work.
Child work acknowledged interruption.
Error [target: run / process]: Process '<file-app executable>' timed out after 1s.
  Executable: <file-app executable>
  Directory: <repository>
  Cleanup
  Process | Restore child resources.
  [OK] cleanup: run | <elapsed>
[FAILED] run | <elapsed>

Failed | 1 failed | Target: run | <elapsed>
```

## Process failure presentation captures

Observed on Windows / SDK 10.0.401 on 8 October 2026. Both streams were redirected independently. These are actual
static file-app captures; ANSI-capable styling is covered separately by renderer tests, with no new interactive visual
review. Paths and lifecycle timings are normalized; diagnostic values and child text are retained.

Every launch disabled telemetry, node reuse, and shared compilation, and used `--no-cache --disable-build-servers`.
Raw captures and exit results are under `.test-work/process-failure-output/`.

| Capture | Source | Script arguments | Exit code |
| --- | --- | --- | --- |
| capture-normal | `process-capture.cs` | `--scenario Failure` | 1 |
| capture-quiet | `process-capture.cs` | `--scenario Failure --quiet` | 1 |
| capture-verbose | `process-capture.cs` | `--scenario Failure --verbose` | 1 |
| capture-plain | `process-capture.cs` | `--scenario Failure --plain` | 1 |
| launch-plain | `process-configuration.cs` | `--plain --dotnet <missing executable>` | 1 |
| launch-quiet | `process-configuration.cs` | `--quiet --dotnet <missing executable>` | 1 |
| launch-verbose | `process-configuration.cs` | `--verbose --dotnet <missing executable>` | 1 |
| deadline | `process-timeout.cs` | `--plain --work 2s --timeout 200ms` | 1 |
| overflow | `process-capture.cs` | `--plain --scenario Overflow` | 1 |
| invalid-input | `process-configuration.cs` | `--plain --timeout 0` | 2 |

### Captured child failure, normal

stdout is empty. stderr:

```text
> process-capture.cs
Capture structured child output within explicit bounds, with optional live echo.

:: inspect
  Process | Produce a greeting as structured text.
Error [target: inspect / process]: Produce a greeting as structured text.
  Exit code 7 was not accepted (accepted: 0).
  Executable: <file-app executable>
  Directory: <repository>
  Captured stderr:
The demonstration child could not produce a greeting.
[FAILED] inspect | <elapsed>

Failed | 1 failed | Target: inspect | <elapsed>
```

### Captured child failure, quiet

stdout is empty. stderr:

```text
Error [target: inspect / process]: Produce a greeting as structured text.
  Exit code 7 was not accepted (accepted: 0).
  Executable: <file-app executable>
  Directory: <repository>
  Captured stderr:
The demonstration child could not produce a greeting.
[FAILED] inspect | <elapsed>

Failed | 1 failed | Target: inspect | <elapsed>
```

### Verbose details

The same failure retains its original raw message in a labeled details section:

```text
  Details:
    Process '<file-app executable>' exited with code 7.
  Captured stderr:
The demonstration child could not produce a greeting.
[FAILED] inspect | <elapsed>

Failed | 1 failed | Target: inspect | <elapsed>
```

The verbose missing-executable capture additionally includes the full original dependency exception, native inner
exception, and available stack. Normal/quiet captures use the concise native cause instead.

### Launch failure, quiet

stdout is empty. stderr:

```text
Error [target: selected / process]: Inspect the selected SDK.
  Executable or a required launch file or directory was not found.
  Executable: <repository>\.test-work\process-failure-output\missing-dotnet
  Directory: <repository>
[FAILED] selected | <elapsed>

Failed | 1 failed | 1 not run | Target: selected | <elapsed>
```

### Deadline with successful cleanup

stdout:

```text
Child work started.
Child cleanup started.
Child cleanup finished.
Parent cleanup completed.
```

stderr:

```text
> process-timeout.cs
Start process shutdown at a deadline, then await it before running cleanup.

:: run
  Process | Run bounded child work.
Child work acknowledged interruption.
Error [target: run / process]: Run bounded child work.
  Process timed out after 200ms.
  Executable: <file-app executable>
  Directory: <repository>
  Cleanup
  Process | Restore child resources.
  [OK] cleanup: run | <elapsed>
[FAILED] run | <elapsed>

Failed | 1 failed | Target: run | <elapsed>
```

### Capture overflow

stdout is empty. stderr:

```text
> process-capture.cs
Capture structured child output within explicit bounds, with optional live echo.

:: inspect
  Process | Produce a greeting as structured text.
Error [target: inspect / process]: Produce a greeting as structured text.
  Process stdout exceeded the capture limit of 1024 UTF-16 characters.
  Executable: <file-app executable>
  Directory: <repository>
[FAILED] inspect | <elapsed>

Failed | 1 failed | Target: inspect | <elapsed>
```
