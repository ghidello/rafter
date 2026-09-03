# Phase 5 implementation evidence

## Status

Phase 5 implements reachable graph planning, deterministic bounded scheduling, conditions, failure isolation,
cooperative cancellation, target cleanup, command cleanup, and the minimal deterministic execution-failure report.
The local completion baseline passes. Cross-platform CI evidence remains to be recorded after the branch is pushed.

## Invocation and planning

Exact help remains independent of graph planning and cancellation. A normal invocation checks pre-cancellation,
plans only the selected reachable graph, binds command-wide options, resolves only planned target paths, then executes.
Reachable planning uses iterative traversal and strongly connected components; the 20,000-target verification graph
does not depend on recursive traversal or repeated full-plan scheduler scans. Cycles produce one stable diagnostic per
reachable component, while a disconnected cycle and its invalid working directory do not affect an unrelated entry
invocation.

The immutable plan is dependency-first in authored dependency order. Shared targets occur once, and one stable plan
index drives admission and outcome ordering. Missing frozen-model dependencies are treated as infrastructure
invariant failures rather than author diagnostics.

## Scheduling and outcomes

Every admitted lifecycle runs independently on the thread pool. The concurrency bound covers conditions, execution,
and qualified target cleanup for synchronous and asynchronous callback forms. Callback-free aggregates and implicit
no-op targets settle without consuming a permit and retain their distinct successful shapes.

Conditions run once in authored order after dependencies settle. False conditions skip only their own target;
throwing conditions fail without qualifying execution or target cleanup. Failed targets block only their transitive
dependents, while independent work remains eligible. Direct blockers retain authored dependency order.

The internal execution outcome retains the immutable plan, lifecycle transitions, target outcomes, successful
shapes, direct blockers, original exceptions, cleanup exceptions, infrastructure failure, command-cleanup failure,
and invocation-cancellation state. Phase 5 reports failures in stable plan order without exposing exception messages;
Phase 6 remains responsible for rich lifecycle rendering.

## Cancellation and cleanup

`Command.RunAsync(...)` accepts an optional cancellation token, and conditions and execution observe the linked
invocation token through `RafterContext.CancellationToken`. Matching requested cancellation is distinguished from
tokenless or unrelated `OperationCanceledException`. Ordinary condition or execution failure wins over concurrent
cancellation; otherwise cancellation returns `130` and stops new callback admission while awaiting admitted work.

One reference-counted coordinator owns the temporary `Console.CancelKeyPress` subscription. Injected-signal tests
prove that one subscription cancels concurrent commands, the first useful signal is handled, a repeated signal is
left to the host even while the first signal is still invoking cancellation callbacks, caller-cancelled invocations
do not consume a signal, and the exact subscription is removed after the final lease. A process-isolated fixture
detaches from its inherited console on Windows, allocates a private console, and raises a real control event; Unix
hosts raise `SIGINT` inside the isolated fixture process. This exercises the production adapter without risking
cancellation of the test host.

Target cleanup runs exactly once after qualified execution success, failure, or cancellation. It is part of target
settlement, so cleanup-only failure blocks dependents. Command cleanup becomes qualified only after successful path
initialization and runs after every target settles. Cleanup contexts preserve snapshot and path values while using
`CancellationToken.None`; cleanup failures remain separate from an earlier execution failure or cancellation.

## Verification

Focused tests cover chains and diamonds, shared dependencies, fan-in, disconnected graphs, aggregate and no-op
shapes, multiple reachable cycles, missing dependency invariants, deep planning, synchronous concurrency, condition
families and ordering, skip and throw paths, failure isolation, deterministic blocker and failure ordering,
cancellation classification and precedence, queued cancellation, cancellation at the condition/execution and path
boundaries, target and command cleanup, overlap rejection through cleanup, context scope, signal coordination, public
API shape, process-isolated production signal delivery, and minimal failure presentation.

Observed locally on Windows x64 with .NET SDK `10.0.400`:

- `dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal` passed.
- the analyzer-enabled Release solution build completed with zero warnings and zero errors;
- all 118 tests passed across the unit, integration, and analyzer assemblies;
- the runtime and symbol packages were created successfully;
- package contents and isolated conventional-project and file-app restores passed;
- all 29 canonical examples retained their project-mode references;
- `git diff --check` passed.

The remaining phase-close work is to reconcile the required-verification and completion-gate checklists during code
review, then record the Windows, Ubuntu, and macOS CI matrix and package-integrity job.
