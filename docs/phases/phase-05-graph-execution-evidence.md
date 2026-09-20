# Phase 5 implementation evidence

## Status

Phase 5 implements reachable graph planning, deterministic bounded scheduling, conditions, failure isolation,
cooperative cancellation, target cleanup, command cleanup, and the minimal deterministic execution-failure report.
The local repository baseline passes. Historical CI jobs and the earlier macOS stall are recorded in the
[closeout audit](phase-05-07-closeout.md#cross-platform-evidence).

The [closeout audit](phase-05-07-closeout.md) records the current baseline and remaining CI gates.
The original 118-test record below is historical; the completion pass has 709 passing local solution tests.

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

The remaining repository-quality gate requires the new revision's Windows, Ubuntu and macOS CI results.
Historical results are recorded separately in the closeout audit and do not certify these changes.

## State, cleanup, and exit tables

| Lifecycle path | Meaning | Representative test |
| --- | --- | --- |
| Pending → Ready → Running → Settled | Executed callback or evaluated condition | `RunsDependenciesBeforeConditionsAndStopsAtTheFirstFalseCondition` |
| Running → CleaningUp → Settled | Qualified target cleanup is part of settlement | `CleanupOnlyFailureBlocksDependentsAndCommandCleanupRunsLast` |
| Pending/Ready → Settled without callback | Blocked/cancelled work or callback-free success | `CallbackFreeTargetsKeepTheirDistinctSuccessfulShapes`, `FailureAndCancellationBlockDependentsButCancelUnrelatedQueuedWork` |

| Trigger | Target cleanup | Command cleanup |
| --- | --- | --- |
| Condition false or throws | Not qualified | Runs after paths initialized and all targets settled |
| Execution succeeds, fails, or observes invocation cancellation | Exactly once, non-cancellable cleanup context | Runs after target cleanup |
| Target cleanup fails | Failure blocks dependents; preserve prior primary failure | Still runs |
| Pre-cancellation, graph/binding/path failure | Not qualified | Not qualified |
| Cancellation after successful path initialization | Only for targets whose execution began | Qualified |

`CleanupReceivesEquivalentScopesAndANonCancellableToken`, `AThrowingConditionFailsWithoutQualifyingTargetCleanup`,
`CancellationDuringSuccessfulPathInitializationQualifiesOnlyCommandCleanup`, and the failure presentation tests
cover these distinctions. `InvocationBoundaryTests` additionally covers final presentation and signal unsubscription.

| Outcome | Command exit |
| --- | --- |
| Help, success, skipped/no-work/aggregate success | 0 |
| Model, graph, input or path diagnostics | 2 |
| Execution, cleanup or infrastructure failure | 1 |
| Invocation cancellation without ordinary failure | 130 |

An explicitly accepted nonzero child exit remains successful; authored process timeout is a failure, not invocation
cancellation. Phase 7 tests now exercise both handoff cases. Cleanup-only failure during cancellation remains a
secondary outcome under the established Phase 5 precedence; output infrastructure failure forces exit 1.

## Completion pass: concurrency and condition matrices

`GraphContractMatrixTests` adds 14 cases. Both synchronous and asynchronous callbacks exercise concurrency 1, 2,
and 8 against graph width 4, measuring the complete callback lifetime and verifying exactly four executions and
cleanups. Every condition overload is tested with false; every deferred family is also tested with an exception,
while a shared dependency and independent branch execute once and guarded execution/cleanup remain unqualified.
A command is reused for 25 concurrent-failure runs, checking plan-ordered primary failures, cleanup failures and
blocker identities on each run. These cases passed locally on Windows.

## Completion pass: invocation boundaries and signals

Six `InvocationBoundaryTests` cases retain same-command overlap rejection through final presentation and signal
unsubscription for success, target failure, cleanup failure, help, input failure and cancellation, then prove reuse.
Two `SignalSubscriptionRaceTests` reproduce and fix overlapping subscriptions during final unsubscription and reject
retired handler snapshots after a new subscription begins. The production adapter preserves a host handler's existing
handled decision. Existing process-isolated integration tests continue to exercise real control signals.

| Gate | Local evidence |
| --- | --- |
| G1 | `ReportsReachableCyclesBeforeBindingOrCleanup`; graph planning precedes callback/process authority |
| G2–G5 | `PhaseFiveGraphTests`, `PhaseFiveExecutionTests`, measured concurrency/conditions/repeated outcomes in `GraphContractMatrixTests` |
| G6 | Cleanup qualification/context/order cases in `PhaseFiveExecutionTests`, terminal-path overlap in `InvocationBoundaryTests`, callback ownership in `ProcessCallbackScopeTests` |
| G7 | Pre-cancellation, queued/running cancellation, condition/path boundaries, distinct cleanup tokens, `ConsoleCancellationCoordinatorTests`, subscription races and production-signal integration fixture |
| G8 | State, cleanup and exit tables above, with the completion-pass results |
| G9 | Local baseline passes; new supported-OS CI and package job pending permission to push |
