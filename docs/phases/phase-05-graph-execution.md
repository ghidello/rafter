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
- [ ] Preserve executable, aggregate, and intentional no-op target distinctions.
- [ ] Define whether conditions are evaluated when a node becomes otherwise ready; evaluate each at most once.

### Scheduling

- [ ] Enforce command concurrency with one scheduler-owned permit mechanism.
- [ ] Never consume a permit for an aggregate or skipped node that executes no callback.
- [ ] Start a node only after every dependency reaches a successful-enough terminal state.
- [ ] Run shared dependencies once.
- [ ] Avoid starvation among simultaneously ready nodes using the documented stable order.
- [ ] Do not hold scheduler locks while invoking user callbacks, output, cleanup, or process code.
- [ ] Set the active target context for the complete callback lifetime.

### Conditions and blocking

- [ ] Import the expected-failure outcome table approved in phase 2 without redefining it in the scheduler.
- [ ] Evaluate Boolean, deferred, context-aware, and asynchronous conditions through the normalized model.
- [ ] Treat a false condition as skipped rather than failed.
- [ ] Define dependency semantics for skipped and no-op targets explicitly.
- [ ] Convert a thrown condition into a target failure with phase information.
- [ ] Classify matching, missing, mismatched, aggregate, and cancellation outcomes for expected-failure targets
      exactly as approved in phase 2.
- [ ] Block transitive dependents of a failed prerequisite without invoking their callbacks.
- [ ] Allow independent and already-running work to settle after an ordinary target failure.

### Cancellation and failures

- [ ] Link external cancellation with scheduler-owned cancellation where appropriate.
- [ ] Stop scheduling new work promptly after cancellation.
- [ ] Allow running callbacks to observe `context.CancellationToken`.
- [ ] Distinguish cancellation from an ordinary callback failure.
- [ ] Aggregate concurrent failures in stable plan order, not task-completion race order.
- [ ] Retain target identity, lifecycle phase, primary exception, and safe diagnostic context.
- [ ] Define the command exit-code mapping for success, binding/planning failure, target failure, and cancellation.

### Cleanup

- [ ] Run target cleanup at most once for every target whose lifecycle qualifies it.
- [ ] Document whether cleanup runs after condition failure, skipped targets, callback startup, and cancellation.
- [ ] Run target cleanup with its target working directory and cancellation policy.
- [ ] Run command cleanup exactly once after all target settlement and target cleanups.
- [ ] Aggregate cleanup failures without hiding the primary failure.
- [ ] Give cleanup a bounded policy so a cancelled command cannot hang forever.

## Required verification

- [ ] Test chains, fans in/out, diamonds, disconnected nodes, aggregate nodes, and explicit no-op nodes.
- [ ] Test missing dependencies, self-cycles, multi-node cycles, and an entry from another command.
- [ ] Instrument maximum simultaneous callbacks and verify limits of one, two, and larger than graph width.
- [ ] Repeat race-sensitive schedules enough to prove stable result and diagnostic ordering.
- [ ] Test false and throwing conditions for every overload family.
- [ ] Test one failure, concurrent failures, failure plus cancellation, and failure plus cleanup failure.
- [ ] Test cancellation before scheduling, while queued, while running, and during cleanup.
- [ ] Assert every callback, condition, and cleanup invocation count.

## Completion gates

- [ ] **G1 — Preflight validation:** invalid graphs execute no condition, target, process, or cleanup callback.
- [ ] **G2 — Exactly-once graph:** all reachable nodes and shared dependencies have correct invocation counts.
- [ ] **G3 — Concurrency bound:** measured running callbacks never exceed the configured limit.
- [ ] **G4 — Deterministic outcomes:** repeated concurrent runs produce identical states and failure ordering.
- [ ] **G5 — Failure isolation:** dependents block while independent and already-running work follows the contract.
- [ ] **G6 — Cleanup contract:** target and command cleanup order/count/context tests pass for every terminal path.
- [ ] **G7 — Bounded cancellation:** cancellation tests settle within test deadlines without orphaned work.
- [ ] **G8 — Evidence recorded:** state-transition table, cleanup matrix, and exit-code table are committed.
- [ ] **G9 — Expected-failure lifecycle:** every row of the phase-2 outcome table has scheduler, dependency, and
      cleanup coverage.

## Non-goals

No distributed execution, persistent graph state, retries, incremental builds, target selection CLI, or dynamic graph
mutation during execution is added.
