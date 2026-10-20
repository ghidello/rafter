# Sequential targets and dependencies

Issue [#13](https://github.com/sotsera/rafter/issues/13) implements callback-based target graphs on the typed-input
runtime. [targets.cs](../examples/targets.cs) prepares a greeting and then prints it through a dependency-only group.
The original [dependencies.cs](../examples/dependencies.cs) build/test workflow still requires external processes.

## Authoring and execution

Declare a target with `command.Target(name, description)`. Register one synchronous `Action<RafterContext>` or
asynchronous `Func<RafterContext, Task>` body with `.Run(...)`. `.DependsOn(params Target[] dependencies)` appends
prerequisites. A target with dependencies can omit its body to form a group. A target with neither is invalid.

Select targets with `RunAsync(string[] args, Target target, params Target[] additionalTargets)`. The first target
is required by the compiler. There is no array-only invocation overload and no invocation combining a command body
with targets. Existing body overloads still validate all declarations but execute only their supplied body.

Targets run sequentially in depth-first dependency order. Entry order and dependency declaration order break ties;
prerequisites always precede dependants. For `test.DependsOn(build)`, selecting `test, build` executes `build`, then
`test`. Shared dependencies and repeated references execute once per invocation. Duplicate references retain their
first traversal/display position. Unselected, unreachable targets never execute.

Both bodies and groups participate in planning, but only bodies produce execution sections and completion counts.
An asynchronous body is awaited before the next body starts. A null returned task, an unhandled exception, or an
output failure stops all later scheduling and returns 1. Catching a writer exception does not clear the recorded
output failure. Ordinary user exceptions deliberately caught by the body remain handled under the existing policy.

Ctrl+C cooperatively cancels the invocation. The current body must settle, including its awaited cleanup, before
the invocation completes. Cancellation is checked before scheduling and after each body; catching cancellation
inside a body does not permit the next target to start. Clean cancellation returns 130. Independent failures,
including cancellation-callback or output errors, take precedence and return 1. No forceful managed-body shutdown
or external-process ownership is implemented in this slice.

## Validation and snapshots

Initial validation examines every declared target, including unused declarations, for all invocation forms and help.
It runs before parsing, input validators, or work callbacks and returns 2 for:

- Invalid or duplicate target names, or null/blank descriptions. Names use the shared lowercase ASCII name grammar
  without option prefixes; targets have a separate namespace from options and may use names such as `help`.
- Empty targets, null bodies, or multiple `.Run(...)` calls. Fluent calls collect errors without throwing them.
- Null dependency arrays or entries, dependencies from another command, or null/foreign selected targets.
- Dependency cycles, including self-dependencies, with a diagnostic path such as `build -> test -> build`.

All graph declarations, body delegates, dependency lists, and selected entries are copied at invocation entry alongside
input declarations. Callback mutations affect later invocations; they do not alter the captured plan. Concurrent
declaration mutation is not a supported authoring pattern. Separate invocations have their own plans, prepared values,
completion counts, cancellation, and output state. No completed flag is stored on a reusable target declaration.

Graph validation and planning use explicit stacks, so deep valid graphs do not consume the managed call stack.
The empty-target analyzer and other authoring diagnostics remain deferred; runtime validation is implemented now.

## Help and output

Help describes the script-selected graph without executing input rules or bodies. It lists entry names, distinguishes
groups from bodies, shows each reachable target once with direct dependencies, and states actual body order. It does
not introduce CLI target selection. Malformed supplied arguments still fail with help; omitted required inputs do not.

Lifecycle output goes to stderr. A single grouping entry appears as `> verify <-- (prepare, greet)` with the command
description. Multiple entries use their selection order in the heading and summary. Body headings follow execution
order, use `[greet] <-- (prepare)`, and put descriptions at column zero with a blank line before content. Dependency
suffixes are muted in rich terminals. Each successful body has its own elapsed time excluding dependencies. One final
summary gives selected entry names, completed-body count, and total invocation time; failures identify the target
where execution stopped when one was active. Groups have no empty execution section or extra completion status.

Quiet hides routine headings and successful statuses while retaining script output and failures. Plain/redirected
stdout preserves script text without styling or presentation prefixes. Existing stream-specific color/NO_COLOR
policies also apply to target output. Host writers remain open until invocation output settlement completes.

## Verification

[Target runtime tests](../tests/Rafter.Evaluation.Tests/TargetTests.cs) exercise order, shared dependencies, repeated
references, nested groups, a 12,000-level graph, all-declaration validation, cycles, ownership, awaited execution,
failure/cancellation cleanup, snapshots, overlapping invocations, input integration, and 48/100-column help.
[Consumer tests](../tests/Rafter.Evaluation.Tests/TargetConsumerTests.cs) build and run the actual targets.cs and check
unsupported invocation shapes at compile time. Existing [runtime consumers](../fixtures/runtime/Scenario.cs) also
exercise successful and failed target workflows in both file apps and ordinary projects with warnings as errors.

Local verification on 2026-10-21 used Windows x64, SDK 10.0.402, and managed net10.0 execution. The full native-MTP
suite reports **Passed: 287, Failed: 0, Skipped: 0**: 56 additional cases plus extended existing consumers. The Release
build reports **0 warnings and 0 errors**. Commands from the repository root:

```powershell
dotnet build tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --no-restore -bl:artifacts/build.binlog
dotnet test --project tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --report-trx --results-directory artifacts/test-results
```

Manual Windows pseudoterminal runs of targets.cs and its help confirmed bold target/group headings, muted direct
dependency suffixes, literal result prefixes, flat descriptions, per-body timings, and one final group summary.
Automated redirected captures confirm clean stdout and lifecycle stderr; the narrow help checks use 48 columns.
CI evidence is recorded in the implementation PR.

External-process execution/settlement,
ProcessFailureAction, analyzers, parallel targets, conditional execution, packaging, and Linux/macOS verification
remain separate work. No dependency versions changed.
