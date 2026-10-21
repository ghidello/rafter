# Owned Windows process execution

Work is tracked in [issue #15](https://github.com/sotsera/rafter/issues/15). The Windows implementation supports the
process and build/test examples through project references. The accepted product contract remains in the
[example design plan](example-design-plan.md#external-processes).

The current Windows implementation has a [mixed-launcher inheritance limitation](process-test-isolation.md): another
broad-inheritance launcher in the same host can inherit temporary pipe handles if its launch overlaps Rafter's launch.
The integration harness isolates those paths; runtime interoperability remains tracked in issue #23.

## Current API

The [process example](../examples/processes.cs) uses the real Windows implementation:

```csharp
return await command.RunAsync(args, async context =>
{
    var result = await context.Process("dotnet")
        .Arguments("--version")
        .RunAsync();

    context.WriteLine($"Process exited with code {result.ExitCode}.");
});
```

Arguments are separate tokens; no shell is inserted. Empty tokens, quotes, trailing backslashes, Unicode, and shell
metacharacters are preserved. Repeated `Arguments` calls append tokens. Null arrays, null tokens, NUL characters,
and blank executable names are rejected before launch. Child output is currently decoded as UTF-8 with BOM detection;
configurable encodings are not exposed yet. The child inherits standard input, or receives `NUL` when
the host has no standard-input handle. Stdout and stderr remain separate; plain output preserves partial lines and
whitespace. Routine process headings respect quiet mode; verbose output includes the child directory. Rafter does
not automatically print argument or environment values.

`WorkingDirectory(path)` resolves relative paths against the captured invocation root. It may be specified once.
`AcceptExitCodes(params int[])` also allows one call, requires a non-null, nonempty list, and ignores duplicates within
that list. Both reject a repeated call even when its value is unchanged. The accepted-code array is copied.

`Environment(name, value)` permits one setting per name on each child builder. Repeating that name is an error, with
case-insensitive comparison on Windows. A child may override a variable inherited from the script process; another
child may choose a different value for the same name. Neither changes the host environment. Null values, blank names,
`=` in names, and NUL characters are rejected; an empty value is passed as an empty child variable. Removal and a clean
environment mode are deferred.

Executable paths containing a directory separator or drive prefix resolve against the captured root. Bare names use
Windows direct-launch lookup, including the script process's PATH. A child PATH override affects that child's
environment, not the executable lookup. Batch files and shell built-ins require an explicitly selected shell.

An accepted exit returns `ProcessResult` with `Succeeded` and `ExitCode` after the owned scope and output have settled.
The default accepted exit is zero. `OnFailure` accepts one non-null synchronous callback for rejected exits:

- `Continue` returns the unsuccessful result and emits a handled-failure warning.
- `Fail` returns that result, emits a failure diagnostic, and records an invocation failure. The current body can finish
  its cleanup, but later targets do not run.
- `Throw`, also the default without a handler, throws `ProcessFailureException` carrying the result. Its diagnostic is
  deferred until it escapes the body. Catching it can recover without a framework error or stack trace.

Callback exceptions preserve both the rejected exit and the callback's full exception details. Launch, output, and
cancellation failures never invoke the exit-code callback or fabricate a process result.

Every body has its own operation tracker. It includes launch, output, cleanup, and the failure callback. Outstanding
operations when the body finishes cause an invocation failure and must settle before later scheduling or final output.
Awaited concurrent process calls, including `Task.WhenAll`, are supported. A captured context cannot start work after
its owning body has finished. Detecting discarded tasks that already completed still needs the planned analyzer.

## Windows ownership

The implementation uses BCL APIs and Windows interop; it adds no runtime package. Each launch creates a private,
non-inherited Job Object with kill-on-close enabled and breakaway disabled. The job is assigned during process
creation, before child code runs, using `PROC_THREAD_ATTRIBUTE_JOB_LIST`. A handle list limits inherited handles to
standard input/output/error. Output uses asynchronous named-pipe readers so pending reads can be interrupted.

The root's exit code and the job's active-process count are observed separately. Root exit starts cleanup of remaining
descendants; root exit alone cannot complete the operation. Scoped Ctrl+Break is attempted for the dedicated process
group when the caller has a console. Without a supported scoped signal, forceful job termination starts immediately.

Invocation cancellation shares one five-second grace window across its operations. A second interrupt ends grace.
Forceful termination is followed by a separate shared five-second verification/drain budget. Normal root exit also
uses bounded descendant cleanup. Deadlines never restart. Expiry cancels pending reads, but both pumps are still
awaited, including any caller-supplied writer that is blocked inside a synchronous write. There is no unconditional
ten-second return guarantee for blocked user code.

Missing termination confirmation reports **Termination unknown**; confirmed termination with incomplete output reports
**Output incomplete**. Both retain an observed root exit when available and prevent invocation success. Output and
termination must be distinguished even when script code catches the process exception.

Exit-code observation is also independent: if the job and output are confirmed complete but the exit code cannot be
read, the process operation throws a descriptive exception without a result or exit handler. That is not reported as
unknown termination. Job-query failures are retried within the verification budget; persistent failure to confirm
termination remains sticky even if the script catches the exception.

The implementation was written against the documented Windows APIs; no Aspire or other upstream source was copied:

- [Process creation attributes](https://learn.microsoft.com/en-us/windows/desktop/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)
- [Scoped console signals](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent)
- [Job state queries](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-queryinformationjobobject)

## Scope and limits

All four process-based examples run against the implementation. The dependency example uses .NET 10 native
Microsoft.Testing.Platform syntax with an explicit `--project`; its fixture contains a library and one xUnit test.
The isolated consumer suite verifies successful execution, a failed build, and a failed test, including the target
where scheduling stops. See [example prerequisites](../examples/README.md).

Linux/macOS ownership and native AOT publishing are unverified; process calls on non-Windows platforms fail explicitly
before launch. Configurable timeouts, capture APIs, reusable settings, child ANSI control, encoding configuration,
and further analyzer coverage are separate work. [Local packaging](local-package.md) and the initial
[discarded-process-task diagnostic](authoring-diagnostics.md#raf003) are implemented in the subsequent authoring slice.
Rafter owns processes it creates and their inherited descendants; a tool
requesting work from an already-running external service does not place that service in the new Job Object.

## Verification

On the local Windows x64 host, the full regression run including the file-app consumer additions passed:
**Passed: 338, Failed: 0, Skipped: 0**, with no build warnings. Command:

```powershell
dotnet test --project tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --report-trx --results-directory artifacts/test-results
```

The scoped signalling check records `Scoped console signal supported: True` on the local host; its child exited zero
after emitting its graceful-stop message. A separate host explicitly detaches its console and verifies immediate
forceful descendant cleanup. A real terminal Ctrl+C run emitted the cancelled summary, recorded invocation result
`130` in its result file, and left no running child. The PowerShell terminal wrapper translated the external shell
status, so the fixture's recorded Rafter result is the evidence for `130`.

Fault-injection checks wrap a real Windows-owned process and simulate failures in job/root-exit observation or the
forceful termination request. The additional force-failure case was added after that full run; the separate
`--filter-class '*ProcessObservationTests*'` run passed all three fault checks. These verify uncertainty classification,
retained observed exits, absence of fabricated results/failure callbacks, resource disposal, cancelled pending readers,
and kill-on-close as final cleanup after a failed force request.
These supplement the real ownership checks; they do not claim an actual Windows kernel failure.

The tests exercise real child processes for token fidelity, separate/large output streams, accepted/rejected results,
failure actions, caught exceptions, target boundaries, normal-exit descendant cleanup, cancellation, shared grace,
second-interrupt escalation, stale contexts, unfinished work, and blocked/failing output writers. The existing native
MTP runner remains authoritative; use the installed `run-tests` skill for invocation and filtering.
