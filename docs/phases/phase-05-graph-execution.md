# Phase 5: graph planning and execution

Status: implementation present; closeout remains open. See the [evidence](phase-05-graph-execution-evidence.md) and
[2026-09-19 gate audit](phase-05-07-closeout.md). Checked items below are supported by the current tests; unchecked
compound cases still require their complete matrix, even where part of the behavior is already covered.

## Objective

Validate the selected target graph before execution and run it with bounded concurrency, deterministic state
transitions, cancellation, failure aggregation, and cleanup.

## Questions to resolve before implementation

### Invocation pipeline and planning boundary

- [x] **Where does graph planning occur in the invocation pipeline?** Decide whether exact help bypasses planning and
      whether an invalid graph is rejected before binding converters and validators, root resolution, command-cleanup
      qualification, and every execution callback. Preferred: help remains independent; otherwise plan after a
      successful freeze and entry-ownership check but before binding.
- [x] **Does planning validate only the selected reachable graph or every authored target?** Define whether a cycle or
      invalid reference in a disconnected target can fail an unrelated entry invocation. Preferred: plan and
      cycle-check only the entry target's reachable graph while retaining Phase 2 command-wide model validation.
- [x] **What exact graph diagnostics are emitted?** Define missing-node handling, cycle representation, diagnostic
      identity, whether one diagnostic is emitted per strongly connected component, and stable ordering. Reconcile
      these checks with Phase 2, which already rejects self-dependencies and authored cross-command references.
- [x] **How does graph depth affect planning?** Decide whether traversal is iterative, recursively bounded, or
      protected by a documented product limit. Preferred: use iterative traversal with no arbitrary graph-depth
      limit and memory proportional to the selected reachable graph.

### Deterministic ordering and scheduling

- [x] **What creates the stable plan order?** Define traversal, dependency visitation, shared-node deduplication, and
      the single ordering key consumed by ready scheduling, primary failures, cleanup failures, and later target
      presentation. Keep each target's direct blockers in its authored dependency order rather than re-sorting that
      local relationship. Preferred: dependency-first traversal in authored dependency order, assigning one immutable
      plan index to each reachable target.
- [x] **What continues after an ordinary target failure?** Decide whether newly ready independent work may still
      start, rather than settling only work that was already running. Preferred: continue scheduling every branch
      whose prerequisites remain successful enough, while blocking only transitive dependents of failed targets.
- [x] **When do conditions run relative to dependencies?** Reconcile ready-state condition evaluation with the
      existing claim that a condition on an aggregate or entry target suppresses its dependencies. Preferred: keep
      the single ready-state lifecycle and remove branch-wide suppression from v1; dependencies settle first.
- [x] **Do condition-bearing aggregates and no-op targets consume concurrency?** Reconcile condition evaluation under
      a permit with permit-free callback-less settlement. Preferred: any node with conditions acquires one permit for
      condition evaluation; only nodes with no condition, execution, or cleanup callback settle permit-free.
- [x] **Does concurrency apply equally to synchronous callbacks?** Define whether a synchronous callback can prevent
      other admitted targets from starting. Preferred: dispatch every admitted lifecycle independently so synchronous
      and asynchronous callback forms share the same concurrency contract without thread-affinity guarantees.

### Cancellation and outcome precedence

- [x] **Where does invocation cancellation originate?** `RunAsync` currently has no cancellation parameter. Decide
      whether to add an optional `CancellationToken` while preserving ordinary two-argument invocation expressions,
      whether Rafter also owns a temporary Ctrl+C handler for file applications, and how global handler installation,
      concurrent commands, and restoration are tested. Preferred: expose the optional token and decide console-signal
      ownership explicitly rather than relying on an internal-only test seam.
- [x] **Where are cancellation checkpoints and which completed result wins?** Define pre-cancellation, help, planning,
      binding, root initialization, callback-boundary, and synchronous-callback behavior without pretending managed
      code can be interrupted midway.
- [x] **Which outcome wins when failure and cancellation coexist?** Define stable exit-code and primary-outcome
      precedence independent of task-completion races. Preferred: an ordinary target failure from a condition or
      execution callback remains primary with exit `1`; cancellation produces `130` only when no such failure exists;
      cleanup remains secondary in either case.
- [x] **What can cancellation forcibly stop?** Apply the cooperative managed-code limitation to target execution as
      well as cleanup. Preferred: stop scheduling new callbacks promptly, signal running callbacks, await them to
      settlement, and make no hard-termination promise for arbitrary managed delegates.

### Cleanup, state, and phase ownership

- [x] **Does target-cleanup failure block dependents?** Define the target terminal state when execution succeeds but
      qualified cleanup fails. Preferred: cleanup is part of target settlement, so cleanup-only failure marks the
      target failed and blocks its dependents; an existing execution failure remains primary.
- [x] **May callback phases reuse the same context instance?** A cleanup token cannot be the already-cancelled
      execution token. Preferred: promise snapshot and path equality, not object identity; use an execution context
      for conditions and execution and a distinct context for target or command cleanup.
- [x] **What is the exact state-transition table?** Lock every legal transition and terminal meaning for executable,
      aggregate, no-op, skipped, blocked, failed, and cancelled nodes before implementing mutable scheduler state.
- [x] **What presentation belongs to Phase 5?** Separate the structured execution outcome and minimum deterministic
      failure report from Phase 6 rich lifecycle tables, semantic output, console attribution, and final rendering.
      Preferred: Phase 5 owns complete internal facts and a minimal report; Phase 6 owns the richer presentation.
- [x] **Which deterministic seams prove concurrency and cancellation?** Prefer injected cancellation and explicit
      synchronization barriers over delay-based races; use repeated schedules only as supplementary stress coverage.

**Gate P0 — Execution contracts locked:** every question above is answered, the selected behavior is recorded in a
fixed-decisions section, and the implementation checklist, verification matrix, completion gates, planned public API,
and Phase 6 handoff all agree with those decisions.

## Fixed decisions

- The invocation pipeline remains model freeze and entry ownership, invocation-services capture, then exact help.
  Valid help returns `0` without planning or observing cancellation. A normal invocation checks cancellation, builds
  the reachable graph plan, binds command-wide options, resolves the command root and only the planned targets'
  Phase 4 paths, qualifies command cleanup, and executes. A pre-cancelled normal invocation returns `130` before
  planning, binding, or root work.
- Phase 2 argument validation, argument snapshotting, model-freeze, mutation rejection, and same-command overlap
  rejection remain in force. The overlap guard is held until graph and cleanup settlement, minimal Phase 5 reporting,
  Ctrl+C coordinator unregistration, and linked-token disposal complete; only then may the same frozen command begin a
  sequential invocation.
- Planning is limited to the selected entry target's reachable graph. Disconnected graph structure and target working
  directories neither execute nor make that invocation fail. Phase 2 command-wide model errors still fail before help
  and planning, and Phase 3 option parsing, conversion, validation, and binding remain command-wide.
- Public authoring cannot produce missing or foreign dependency IDs in a successfully frozen model. Encountering one
  is an internal infrastructure invariant failure, not a user graph diagnostic. The foreign entry target and authored
  dependency ownership errors retain their Phase 2 diagnostics.
- Planning uses iterative traversal with memory proportional to reachable nodes and edges. Each reachable cyclic
  strongly connected component produces one graph diagnostic listing its targets in declaration order; diagnostics
  order by the earliest declared member. No arbitrary graph-depth limit is introduced.
- A valid graph receives one dependency-first plan order. Dependencies are visited in authored order, shared nodes
  are retained once, and each target receives one immutable plan index. Scheduler admission, primary failures,
  cleanup failures, and later target presentation use that index rather than completion order. A target's direct
  blockers retain its authored dependency order, which may differ from plan order when dependencies share nodes.
- Conditions run once, in authored order, only after all dependencies settle successfully enough and the target
  acquires a permit. Consequently, dependencies may execute before their target is skipped. Rafter v1 has no
  branch-wide suppression construct; authors apply the condition to every branch target whose own work should skip.
- A target with any condition consumes one permit while evaluating its conditions. A condition-free aggregate or
  implicit no-op settles without a permit. The result of an admitted condition callback is applied before cancellation
  is checked at the next callback boundary: false or an exception settles the target, while true proceeds. When the
  final condition of a callback-free target returns true, the target settles atomically as its authored aggregate or
  no-work shape even if cancellation raced with that callback. Cancellation may still cancel the invocation and other
  work.
- Every admitted target lifecycle is dispatched independently through the thread pool. This allows synchronous and
  asynchronous callbacks to overlap under the same bound. Stable admission does not promise callback thread or
  physical start order, and callbacks have no thread-affinity guarantee.
- An ordinary failure blocks only transitive dependents. Independent branches remain eligible and may start after the
  failure; already-running work settles normally. An ordinary failure does not request invocation cancellation.
- `RunAsync` adds an optional `CancellationToken cancellationToken = default` parameter. Existing two-argument calls
  remain unchanged. Each normal invocation exposes one linked token combining that argument with the shared
  process-wide Ctrl+C coordinator. The coordinator registers one handler, cancels every active Rafter invocation,
  marks the first signal handled, and removes the handler after the last invocation. If every active invocation was
  already cancelled by Ctrl+C, a later signal is not handled so the host can perform its default hard termination.
- Cancellation is checked before each cancellable stage and user-callback boundary. A synchronous callback already
  entered is allowed to return or throw before cancellation is considered again. A failure or diagnostic completed by
  that callback or stage retains precedence; otherwise observed cancellation stops new callback admission and returns
  `130`. During graph execution, any ordinary target failure from a condition or execution callback makes exit `1`
  primary over concurrent cancellation.
- An `OperationCanceledException` represents invocation cancellation only when its `CancellationToken` equals the
  invocation token and that token was requested when the callback settled. An exception carrying no token or another
  token is an ordinary callback failure even if invocation cancellation races it. Authors use
  `context.CancellationToken.ThrowIfCancellationRequested()` or pass that token to cancellable operations.
- After cancellation stops admission, Rafter awaits every running target before settling work that never started. An
  unstarted target with a direct `Failed` or `Blocked` dependency settles as `Blocked`; otherwise it settles as
  `Cancelled`. Direct blockers are the target's direct failed or blocked dependencies in authored dependency order.
  This precedence preserves failure causality and makes target states independent of cancellation/completion races.
- Running managed callbacks receive cancellation cooperatively and are awaited to settlement. Rafter does not detach,
  abort, or claim a hard completion bound for arbitrary target or cleanup delegates. Bounded termination remains
  limited to Rafter-owned resources implemented in later phases.
- Conditions and execution share one target execution context carrying the invocation token. Qualified target cleanup
  receives a distinct context with the same binding snapshot, root, and target working directory and with
  `CancellationToken.None`. Command cleanup receives a distinct root-scoped context with that same cleanup token.
  Context object identity is not part of the public contract, and cleanup authors own operation-specific deadlines.
- Target cleanup is part of target settlement. Cleanup-only failure makes the target failed and blocks its dependents.
  If execution already failed, that exception remains primary and cleanup exceptions remain separate. Command cleanup
  runs only after every target settles and never changes which dependents ran.
- Successful invocation path initialization atomically qualifies command cleanup before the next cancellation
  checkpoint. Cancellation observed at the checkpoint before path initialization, or failure during initialization,
  does not qualify it. Once initialization returns successfully, cleanup is qualified even if cancellation raced the
  operation; the following checkpoint observes that cancellation before any target callback starts.
- Scheduler lifecycle and terminal meaning are separate. Lifecycle is `Pending`, `Ready`, `Running`, `CleaningUp`,
  then `Settled`. Terminal outcome is `Succeeded`, `Skipped`, `Failed`, `Cancelled`, or `Blocked`; a successful target
  additionally records `Executed`, `Aggregate`, or `NoWork`. A thrown condition fails in the condition phase, a false
  condition skips, and invocation cancellation marks work that never starts as cancelled unless a direct failed or
  blocked dependency gives it the deterministic blocked outcome described above.
- Phase 5 retains the complete immutable plan, state transitions, original exceptions, blockers, and cleanup failures
  in one internal structured outcome. Its minimal deterministic failure report uses the existing presentation and
  redaction boundary. Phase 6 consumes those facts to add semantic output, live state, target tables, console
  attribution, and rich final rendering without redefining execution behavior.
- Concurrency and cancellation tests use explicit asynchronous barriers, injected signal/cancellation seams, and
  continuations that run asynchronously. Timing delays and repeated schedules are supplementary stress coverage, not
  the primary correctness proof.

## Planned public API

Phase 5 makes cancellation available at the invocation boundary and inside callbacks without changing existing
two-argument `RunAsync` invocation expressions. Compatibility for method-group conversions is not promised before the
v1 API is frozen:

```csharp
public Task<int> RunAsync(
    Target entryTarget,
    string[] args,
    CancellationToken cancellationToken = default);

public CancellationToken RafterContext.CancellationToken { get; }
```

Both additions require XML documentation, `PublicAPI.Unshipped.txt` entries, and public API-shape tests. Context
construction must carry the invocation token for conditions and execution and `CancellationToken.None` for target and
command cleanup.

## Lifecycle model

Each reachable target progresses through `Pending`, `Ready`, `Running`, optional `CleaningUp`, and `Settled` without
re-entering an earlier lifecycle state. A settled target records one terminal outcome: `Succeeded`, `Skipped`,
`Failed`, `Cancelled`, or `Blocked`. Successful targets additionally record the authored execution shape:
`Executed`, `Aggregate`, or `NoWork`.

Pure aggregate and no-op targets may move directly from `Ready` to `Settled`. Condition evaluation occurs inside
`Running`; false settles as skipped and an exception settles as failed without qualifying target cleanup. Once an
execution callback starts, its cleanup is qualified and the target enters `CleaningUp` when that callback exists.
External cancellation settles work that never starts as cancelled, including a target whose conditions completed but
whose execution callback did not begin, unless a direct failed or blocked prerequisite makes the target blocked.

| Scenario | Legal transition | Terminal facts |
| --- | --- | --- |
| Execution callback without cleanup | `Pending → Ready → Running → Settled` | `Succeeded`, `Failed`, or `Cancelled` from execution |
| Execution callback with qualified cleanup | `Pending → Ready → Running → CleaningUp → Settled` | Execution-derived outcome; cleanup may change success to `Failed` |
| False condition | `Pending → Ready → Running → Settled` | `Skipped`; execution and cleanup never start |
| Throwing condition | `Pending → Ready → Running → Settled` | `Failed` in condition phase; execution and cleanup never start |
| Condition-free aggregate | `Pending → Ready → Settled` | `Succeeded`, `Aggregate`; no permit |
| Condition-free implicit no-op | `Pending → Ready → Settled` | `Succeeded`, `NoWork`; no permit |
| True condition-bearing aggregate or no-op | `Pending → Ready → Running → Settled` | `Succeeded`, authored successful shape; settlement is atomic with the final true condition return |
| Failed prerequisite | `Pending → Settled` | `Blocked` with direct blockers in authored dependency order |
| Cancellation before callback entry | `Pending → Settled` or `Pending → Ready → Settled` | `Cancelled`; no condition, execution, or cleanup callback starts |
| Cancellation after true conditions and before execution | `Pending → Ready → Running → Settled` | `Cancelled`; conditions ran, but execution and cleanup never start |
| Cooperative cancellation after execution starts | `Running → Settled` or `Running → CleaningUp → Settled` | `Cancelled` when invocation cancellation caused `OperationCanceledException` |
| Callback ignores cancellation and returns | `Running → Settled` or `Running → CleaningUp → Settled` | Callback-derived target outcome; invocation remains cancelled |
| Failed dependency and concurrent cancellation | `Pending → Settled` or `Pending → Ready → Settled` | `Blocked` when a direct dependency is `Failed` or `Blocked`; otherwise `Cancelled` |
| Execution success and cleanup failure | `Running → CleaningUp → Settled` | `Failed` in cleanup phase; dependents block |

## Cleanup outcome matrix

Target cleanup is qualified only after execution begins. Condition failure and skip paths therefore have no target
cleanup row. Invocation cancellation remains a command-level fact even when an admitted callback ignores its token and
produces a target-level result.

| Execution result before cleanup | Cleanup result | Target outcome | Command precedence |
| --- | --- | --- | --- |
| `Succeeded` | Succeeded | `Succeeded` | Success when no other failure or cancellation exists |
| `Succeeded` | Failed | `Failed` | Cleanup failure when no invocation cancellation or earlier failure exists |
| `Failed` | Succeeded or failed | `Failed` | Original execution failure; cleanup failure is secondary |
| `Cancelled` | Succeeded or failed | `Cancelled` | Invocation cancellation; cleanup failure is secondary |
| `Succeeded` after ignoring invocation cancellation | Succeeded | `Succeeded` | Invocation cancellation remains command-primary |
| `Succeeded` after ignoring invocation cancellation | Failed | `Failed` | Invocation cancellation remains command-primary; cleanup failure is recorded separately |

## Implementation checklist

### Planning

- [x] Preserve immediate argument validation, caller-array snapshotting, permanent model freeze, post-freeze mutation
      rejection, and same-command overlap rejection from Phases 2 and 3.
- [x] Hold the same-command overlap guard through the complete invocation, including target and command cleanup,
      minimal reporting, signal-coordinator unregistration, and linked-token disposal; release it on every terminal
      path and only after process-wide resources are detached.
- [x] Freeze the command and receive exactly one entry target.
- [x] Reject an entry target owned by another command.
- [x] After exact-help handling and the first cancellation checkpoint, iteratively traverse only the entry target's
      reachable dependencies, retain shared nodes once, and build an immutable execution plan before binding.
- [x] Treat missing or foreign dependency IDs in a successfully frozen model as infrastructure invariant failure.
- [x] Detect every reachable cyclic strongly connected component and emit one stable diagnostic per component.
- [x] Assign the approved dependency-first immutable plan index for diagnostics, ready admission, primary and cleanup
      failure ordering, and later target presentation; do not use it to reorder a target's authored direct blockers.
- [x] Preserve executable, aggregate, implicit no-op, and condition-skipped target distinctions.
- [x] Pass the immutable execution plan into Phase 4 path initialization and resolve target working directories only
      for planned target IDs; retain command-root resolution and Phase 3 command-wide option binding.
- [x] Do not plan, graph-validate, path-resolve, or execute disconnected targets beyond Phase 2 command-wide model
      validation and Phase 3 command-wide option handling.

### Scheduling

- [x] Consume the Phase 4 context factory so conditions, execution, and target cleanup receive the target's resolved
      logical working directory while command cleanup receives the resolved command root.
- [x] Execute concurrent targets with distinct logical directories and prove their complete callback lifetimes leave
      `Environment.CurrentDirectory` unchanged.
- [x] Enforce the phase-2 default concurrency of `1` when no override is authored.
- [x] Enforce command concurrency with one scheduler-owned permit mechanism.
- [x] Dispatch each admitted lifecycle independently so synchronous callbacks can overlap without exceeding the
      configured permit count or acquiring thread affinity.
- [x] Do not acquire a permit while a target waits for dependencies.
- [x] Acquire one permit when a target becomes ready and hold it across deferred condition evaluation, execution,
      and target cleanup.
- [x] Release the permit only after target cleanup settles, including failure and cancellation paths.
- [x] Never consume a permit for condition-free aggregate or implicit no-op settlement.
- [x] Start a node only after every dependency reaches a successful-enough terminal state.
- [x] Run shared dependencies once.
- [x] Admit simultaneously ready nodes in ascending plan-index order without promising physical callback start order.
- [x] Do not hold scheduler locks while invoking user callbacks, output, cleanup, or process code.
- [x] Set the active target context for the complete condition and execution lifetime; use a distinct context for
      qualified cleanup.

### Conditions and blocking

- [x] Evaluate Boolean, deferred, context-aware, and asynchronous conditions through the normalized model.
- [x] Evaluate multiple conditions after dependencies settle and at most once each in authored order; stop on the
      first false or exception.
- [x] Treat a false condition as skipped rather than failed.
- [x] Treat skipped and no-op prerequisites as successful enough for dependents to proceed.
- [x] After cancellation stops admission and running targets settle, mark an unstarted target `Blocked` when any direct
      dependency is `Failed` or `Blocked`; otherwise mark it `Cancelled`.
- [x] Record only direct failed or blocked dependencies as blockers, in authored dependency order, and propagate
      transitive failure causality through blocked dependencies.
- [x] Document that conditions control only their target and do not suppress already-required dependencies; keep a
      dedicated branch-gating construct outside v1.
- [x] Complete an implicit no-op successfully and publish a distinct completed-with-no-work outcome.
- [x] Settle a condition-bearing aggregate or implicit no-op successfully and atomically when its final condition
      returns `true`; let that admitted callback result win concurrent cancellation and do not revise the target.
- [x] Convert a thrown condition into a target failure with phase information.
- [x] Block transitive dependents of a failed prerequisite without invoking their callbacks.
- [x] Continue admitting independent eligible work after an ordinary target failure while blocking only transitive
      dependents.

### Cancellation and failures

- [x] Change `RunAsync` to the exact planned public signature `RunAsync(Target entryTarget, string[] args,
      CancellationToken cancellationToken = default)` and snapshot the caller-owned argument array before
      asynchronous work.
- [x] Add the exact planned public `RafterContext.CancellationToken` property and propagate the invocation or cleanup
      token through every context-construction path.
- [x] Document both public additions, update `PublicAPI.Unshipped.txt`, and add API-shape and token-propagation tests.
- [x] Implement one shared, reference-counted Ctrl+C coordinator that cancels all active Rafter invocations, handles
      the first signal only while at least one invocation still needs cancellation, leaves a repeated signal
      unhandled for host termination, and restores the process handler exactly.
- [x] Put signal subscription and delivery behind an internal injectable seam. Use the production
      `Console.CancelKeyPress` adapter only at that boundary so coordinator races can be tested without signalling the
      test host.
- [x] Stop scheduling new work promptly after cancellation.
- [x] Allow running callbacks to observe `context.CancellationToken`.
- [x] Check cancellation at the approved stage and callback boundaries without attempting to interrupt a synchronous
      callback already in progress.
- [x] Distinguish cancellation from an ordinary callback failure.
- [x] Aggregate concurrent failures in stable plan order, not task-completion race order.
- [x] Retain target identity, lifecycle phase, primary exception, and safe diagnostic context.
- [x] Record callback exceptions without wrapping or replacing them so their original type, identity, stack, and
      safe details remain available to Rafter's internal outcome for classification, presentation, and verification.
- [x] Return `0` for success, help, skipped/no-op completion, and explicitly valid nonzero process exits; return `1`
      for converter or validator author exceptions and execution, process, infrastructure, or cleanup failure;
      return `2` for command-model, syntax, failed-conversion, missing-required, validator-rejection, or graph-planning
      diagnostics; and return `130` for invocation cancellation.
- [x] Recognize `OperationCanceledException` as invocation cancellation only when it carries the invocation token and
      that token was requested when the callback settled. Classify exceptions with no token or another token as
      callback failures even when cancellation races them. Keep process timeout in the failure category.
- [x] Preserve an observed ordinary target failure from a condition or execution callback over concurrent
      cancellation; use exit `130` only when no such failure exists.
- [x] Keep concurrent and cleanup details in the internal structured outcome and presentation rather than inventing
      additional exit codes.

### Cleanup

- [x] Import the phase-2 invariant that each target and command owns at most one cleanup callback.
- [x] Import the phase-2 invariant that target cleanup cannot exist without a target execution callback.
- [x] Run target cleanup at most once for every target whose lifecycle qualifies it.
- [x] Qualify target cleanup when its execution callback starts; do not run it after a false or throwing condition.
- [x] Run qualified target cleanup after callback success, failure, or cancellation.
- [x] Run target cleanup with a distinct context containing the same snapshot, root, and target working directory and
      `CancellationToken.None`; do not promise context object identity.
- [x] Treat cleanup-only target failure as failed dependency settlement and block its transitive dependents.
- [x] Run command cleanup exactly once after all target settlement and target cleanups.
- [x] Qualify command cleanup atomically when invocation path initialization succeeds and before the next cancellation
      checkpoint. Do not start initialization when its preceding checkpoint observes cancellation, and do not qualify
      cleanup when initialization fails; cancellation racing a successful initialization does not undo qualification.
- [x] Once qualified, run command cleanup after success, failure, cancellation, and an all-skipped graph.
- [x] Aggregate cleanup failures without hiding the primary failure.
- [x] Preserve an ordinary condition or execution failure, or invocation cancellation, as the primary command outcome
      and store cleanup failures separately. If callbacks otherwise succeeded and the invocation was not cancelled,
      make any cleanup failure the sole reason the command is unsuccessful.
- [x] Order target-cleanup failures by stable target plan order regardless of completion races, then append the
      command-cleanup failure if present.
- [x] Preserve every original primary and cleanup exception in the internal outcome with stable ordering so Phase 6
      can render secondary cleanup failures without reconstructing execution semantics.
- [x] Keep `RunAsync` integer-returning in v1 and expose no structured public command outcome. Document that authors
      needing programmatic exception handling catch inside their execution or cleanup callback before it escapes to
      Rafter.
- [x] Give target and command cleanup `CancellationToken.None` even when invocation cancellation triggered cleanup;
      do not pass the already-cancelled execution token to cleanup operations.
- [x] Await managed cleanup callbacks to settlement and document that they must cooperate and remain finite; do not
      detach a callback or claim Rafter can forcibly terminate arbitrary managed code.
- [x] Keep bounded teardown guarantees scoped to resources Rafter owns and can terminate, notably child processes;
      application cleanup owns any operation-specific timeout policy.

## Required verification

- [ ] Test chains, fans in/out, diamonds, disconnected nodes, aggregate nodes, and implicit no-op nodes.
- [ ] Attempt an overlapping invocation during target execution, target cleanup, command cleanup, minimal reporting,
      and signal teardown; verify immediate rejection, then verify sequential reuse after every successful, failed,
      diagnostic, and cancelled terminal path.
- [ ] Give a disconnected target an invalid or escaping working directory and prove its path is never resolved while
      command-wide option parsing, conversion, validation, and binding retain their Phase 3 behavior.
- [ ] Test internal missing-dependency invariants, Phase 2 self-dependency rejection, multiple reachable cyclic
      components, a disconnected cycle, and an entry from another command.
- [ ] Prove graph diagnostics occur before binding, path initialization, command-cleanup qualification, and every
      Phase 5 condition, execution, or cleanup callback. Record “no child-process launch after graph diagnostics” as a
      Phase 7 handoff assertion rather than claiming process execution coverage in this phase.
- [x] Plan a very deep chain without recursive traversal or an arbitrary product depth limit.
- [ ] Instrument maximum simultaneous synchronous and asynchronous callbacks and verify limits of one, two, and
      larger than graph width.
- [ ] Repeat race-sensitive schedules enough to prove stable result and diagnostic ordering.
- [ ] Test false and throwing conditions for every overload family.
- [ ] Test a condition-bearing target with dependencies and shared dependencies reached through both skipped and
      executing targets; prove ready-state conditions never claim branch suppression.
- [ ] Test one failure, concurrent failures, failure plus cancellation, and failure plus cleanup failure.
- [x] Race dependency failure with cancellation and prove that direct and transitive dependents settle as `Blocked`
      while unrelated unstarted targets settle as `Cancelled`, independent of event-processing order.
- [ ] Test every Phase 5 exit-code row: help, success, all-skipped/no-op graphs, callback and cleanup failure, planning
      diagnostics, matching-token cancellation, and unrelated or tokenless `OperationCanceledException` both with and
      without concurrent invocation cancellation. Record valid nonzero child exits and process timeout as Phase 7
      handoff cases rather than claiming process execution here.
- [ ] Test success plus target-cleanup failure, success plus command-cleanup failure, multiple concurrent cleanup
      failures, cancellation plus cleanup failure, and the minimal deterministic Phase 5 failure report for every
      combination.
- [ ] Test cancellation before planning, between every pre-execution stage, while queued, and while running; verify
      current-stage failure precedence, ordinary-failure precedence, and prompt admission shutdown.
- [x] Test cancellation after true conditions settle but before execution callback entry; verify that execution and
      target cleanup remain unqualified and the target settles as cancelled.
- [x] Race cancellation with the final true condition of a callback-free target and prove the target settles in its
      authored aggregate or no-work success shape while the cancelled invocation and other work follow their rules.
- [ ] Inject cancellation before path initialization, during a successful initialization, and immediately afterward;
      prove command cleanup is unqualified in the first case and runs exactly once in the latter two. Separately prove
      path-initialization failure leaves it unqualified.
- [ ] Test direct-token cancellation and use the injected signal seam to deterministically cover concurrent commands,
      first and repeated Ctrl+C, no-active-command behavior, subscription races, and exact handler restoration on
      every terminal path. Exercise the production console adapter in a process-isolated integration fixture so a
      failure cannot terminate the test host.
- [ ] Verify execution and cleanup contexts share snapshot and path values without sharing object or token identity;
      cancellation-triggered cleanup observes `CancellationToken.None` and settles cooperatively.
- [ ] Prove cleanup-only target failure blocks dependents while preserving the original cleanup exception.
- [ ] Assert every callback, condition, and cleanup invocation count.
- [ ] Execute an output-free Phase 5 form of the Phase 4 working-directory fixture and prove target and command
      cleanup scopes through real graph execution rather than internal context construction alone; retain canonical
      output and process execution for their owning phases.
- [ ] Table-test every legal lifecycle transition and terminal outcome, including callback-ignores-cancellation and
      cleanup-only failure paths.

### Repository verification record

Run this baseline from the repository root:

```powershell
dotnet restore Rafter.slnx --configfile nuget.config
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --no-restore
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build --no-restore
```

The Phase 5 evidence document must record these results, the CI matrix and package-integrity result, and the exact
example or syntax-fixture set verified against the Phase 5 API. It must name each example deferred because its owning
phase is not implemented rather than relying on a moving definition of “currently compilable.”

## Completion gates

- [x] **G0 — Initial contracts locked:** every P0 question is answered and reflected in executable work packages and
      deterministic verification cases.
- [ ] **G1 — Preflight validation:** invalid graphs perform no binding or path work, do not qualify command cleanup,
      and execute no Phase 5 condition, target, or cleanup callback; the future no-process-launch assertion is recorded
      for Phase 7.
- [x] **G2 — Exactly-once graph:** all reachable nodes and shared dependencies have correct invocation counts.
- [ ] **G3 — Concurrency bound:** measured synchronous and asynchronous callback lifecycles never exceed the
      configured limit and admitted synchronous callbacks can overlap.
- [ ] **G4 — Deterministic outcomes:** repeated concurrent runs produce identical states and failure ordering.
- [x] **G5 — Failure isolation:** dependents block while independent queued and already-running work follows the
      contract, and target-cleanup failure participates in dependency settlement.
- [ ] **G6 — Cleanup contract:** target and command cleanup order/count/context tests pass for every terminal path.
- [ ] **G7 — Cooperative cancellation:** finite test callbacks settle within test deadlines, cleanup receives its
      distinct non-cancellable token, and Rafter-owned operations leave no orphaned work; arbitrary managed cleanup
      has no false hard-termination guarantee, and repeated Ctrl+C remains a host escape hatch.
- [ ] **G8 — Evidence recorded:** state-transition table, cleanup matrix, and exit-code table are committed.
- [ ] **G9 — Repository quality:** the recorded baseline commands, analyzer-clean Release build, public API baseline
      checks, CI matrix, package integrity, and explicitly enumerated Phase 5 example or syntax-fixture checks pass;
      every later-phase example deferral is named.

## Non-goals

No distributed execution, persistent graph state, retries, incremental builds, target selection CLI, condition-based
dependency-subtree suppression, or dynamic graph mutation during execution is added.
