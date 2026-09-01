# Phase 5: graph planning and execution

## Objective

Validate the selected target graph before execution and run it with bounded concurrency, deterministic state
transitions, cancellation, failure aggregation, and cleanup.

## Lifecycle model

Each reachable target transitions once through explicit states such as pending, ready, running, succeeded, skipped,
blocked, failed, or cancelled. The final names may differ, but transitions and terminal-state meanings must be
documented and testable.

## Implementation checklist

### Planning

- [ ] Freeze the command and receive exactly one entry target.
- [ ] Reject an entry target owned by another command.
- [ ] Traverse dependencies, deduplicate shared nodes, and build an immutable execution plan.
- [ ] Detect unknown/cross-command dependencies and cycles before binding-dependent callbacks execute.
- [ ] Produce a stable topological rank or equivalent ordering key for diagnostics and ready queues.
- [ ] Preserve executable, aggregate, implicit no-op, and condition-skipped target distinctions.
- [ ] Define whether conditions are evaluated when a node becomes otherwise ready; evaluate each at most once.

### Scheduling

- [ ] Enforce the phase-2 default concurrency of `1` when no override is authored.
- [ ] Enforce command concurrency with one scheduler-owned permit mechanism.
- [ ] Do not acquire a permit while a target waits for dependencies.
- [ ] Acquire one permit when a target becomes ready and hold it across deferred condition evaluation, execution,
      and target cleanup.
- [ ] Release the permit only after target cleanup settles, including failure and cancellation paths.
- [ ] Never consume a permit for callback-free aggregate or implicit no-op settlement.
- [ ] Start a node only after every dependency reaches a successful-enough terminal state.
- [ ] Run shared dependencies once.
- [ ] Avoid starvation among simultaneously ready nodes using the documented stable order.
- [ ] Do not hold scheduler locks while invoking user callbacks, output, cleanup, or process code.
- [ ] Set the active target context for the complete callback lifetime.

### Conditions and blocking

- [ ] Evaluate Boolean, deferred, context-aware, and asynchronous conditions through the normalized model.
- [ ] Evaluate multiple conditions at most once each in authored order; stop on the first false or exception.
- [ ] Treat a false condition as skipped rather than failed.
- [ ] Treat skipped and no-op prerequisites as successful enough for dependents to proceed.
- [ ] Document that branch-wide suppression requires the condition on the branch aggregate or selected entry target.
- [ ] Complete an implicit no-op successfully and publish a distinct completed-with-no-work outcome.
- [ ] Convert a thrown condition into a target failure with phase information.
- [ ] Block transitive dependents of a failed prerequisite without invoking their callbacks.
- [ ] Allow independent and already-running work to settle after an ordinary target failure.

### Cancellation and failures

- [ ] Link external cancellation with scheduler-owned cancellation where appropriate.
- [ ] Stop scheduling new work promptly after cancellation.
- [ ] Allow running callbacks to observe `context.CancellationToken`.
- [ ] Distinguish cancellation from an ordinary callback failure.
- [ ] Aggregate concurrent failures in stable plan order, not task-completion race order.
- [ ] Retain target identity, lifecycle phase, primary exception, and safe diagnostic context.
- [ ] Record callback exceptions without wrapping or replacing them so their original type, identity, stack, and
      safe details remain available to Rafter's internal outcome for classification, presentation, and verification.
- [ ] Return `0` for success, help, skipped/no-op completion, and explicitly valid nonzero process exits; return `1`
      for execution, process, infrastructure, or cleanup failure; return `2` for command-model, parsing, binding,
      validation, or graph-planning diagnostics; and return `130` for invocation cancellation.
- [ ] Recognize `OperationCanceledException` as invocation cancellation only when the invocation token was actually
      requested; otherwise classify it as a callback failure. Keep process timeout in the failure category.
- [ ] Keep concurrent and cleanup details in the internal structured outcome and presentation rather than inventing
      additional exit codes.

### Cleanup

- [ ] Import the phase-2 invariant that each target and command owns at most one cleanup callback.
- [ ] Import the phase-2 invariant that target cleanup cannot exist without a target execution callback.
- [ ] Run target cleanup at most once for every target whose lifecycle qualifies it.
- [ ] Qualify target cleanup when its execution callback starts; do not run it after a false or throwing condition.
- [ ] Run qualified target cleanup after callback success, failure, or cancellation.
- [ ] Run target cleanup with its target working directory and cancellation policy.
- [ ] Run command cleanup exactly once after all target settlement and target cleanups.
- [ ] Qualify command cleanup only after parsing, binding, graph preflight, and invocation initialization succeed.
- [ ] Once qualified, run command cleanup after success, failure, cancellation, and an all-skipped graph.
- [ ] Aggregate cleanup failures without hiding the primary failure.
- [ ] Preserve execution failure or cancellation as the primary outcome and store cleanup failures separately; if
      execution succeeded, make any cleanup failure the sole reason the command is unsuccessful.
- [ ] Order target-cleanup failures by stable target plan order regardless of completion races, then append the
      command-cleanup failure if present.
- [ ] Preserve every original primary and cleanup exception in the internal outcome and render secondary cleanup
      failures beneath a distinct `Cleanup also failed` heading.
- [ ] Keep `RunAsync` integer-returning in v1 and expose no structured public command outcome. Document that authors
      needing programmatic exception handling catch inside their execution or cleanup callback before it escapes to
      Rafter.
- [ ] Give cleanup a dedicated token that starts non-cancelled even when invocation cancellation triggered cleanup;
      do not pass the already-cancelled execution token to cleanup operations.
- [ ] Await managed cleanup callbacks to settlement and document that they must cooperate and remain finite; do not
      detach a callback or claim Rafter can forcibly terminate arbitrary managed code.
- [ ] Keep bounded teardown guarantees scoped to resources Rafter owns and can terminate, notably child processes;
      application cleanup owns any operation-specific timeout policy.

## Required verification

- [ ] Test chains, fans in/out, diamonds, disconnected nodes, aggregate nodes, and implicit no-op nodes.
- [ ] Test missing dependencies, self-cycles, multi-node cycles, and an entry from another command.
- [ ] Instrument maximum simultaneous callbacks and verify limits of one, two, and larger than graph width.
- [ ] Repeat race-sensitive schedules enough to prove stable result and diagnostic ordering.
- [ ] Test false and throwing conditions for every overload family.
- [ ] Test one failure, concurrent failures, failure plus cancellation, and failure plus cleanup failure.
- [ ] Test the complete exit-code table, including help, all-skipped/no-op graphs, valid nonzero child exits,
      unrelated `OperationCanceledException`, invocation cancellation, timeout, and cleanup-only failure.
- [ ] Test success plus target-cleanup failure, success plus command-cleanup failure, multiple concurrent cleanup
      failures, cancellation plus cleanup failure, and deterministic presentation for every combination.
- [ ] Test cancellation before scheduling, while queued, and while running; verify cancellation-triggered cleanup
      observes a fresh non-cancelled token and that cooperative cleanup settles normally.
- [ ] Assert every callback, condition, and cleanup invocation count.

## Completion gates

- [ ] **G1 — Preflight validation:** invalid graphs execute no condition, target, process, or cleanup callback.
- [ ] **G2 — Exactly-once graph:** all reachable nodes and shared dependencies have correct invocation counts.
- [ ] **G3 — Concurrency bound:** measured running callbacks never exceed the configured limit.
- [ ] **G4 — Deterministic outcomes:** repeated concurrent runs produce identical states and failure ordering.
- [ ] **G5 — Failure isolation:** dependents block while independent and already-running work follows the contract.
- [ ] **G6 — Cleanup contract:** target and command cleanup order/count/context tests pass for every terminal path.
- [ ] **G7 — Cooperative cancellation:** finite test callbacks settle within test deadlines, cleanup receives its
      dedicated token, and Rafter-owned operations leave no orphaned work; arbitrary managed cleanup has no false
      hard-termination guarantee.
- [ ] **G8 — Evidence recorded:** state-transition table, cleanup matrix, and exit-code table are committed.

## Non-goals

No distributed execution, persistent graph state, retries, incremental builds, target selection CLI, or dynamic graph
mutation during execution is added.
