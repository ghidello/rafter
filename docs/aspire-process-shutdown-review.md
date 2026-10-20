# Aspire process shutdown review

Reviewed on 2026-10-19 at microsoft/aspire commit
[`949287a1b6ceaf8d8db49b41e594f74b1a74ba2c`](https://github.com/microsoft/aspire/commit/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c),
resolved from `main` through GitHub. This is a source review, not a claim about an installed/released Aspire version.
No Aspire code was copied into Rafter and no Aspire tests were run. Older web-indexed ProcessExecution code differs
from this revision; the findings below use the pinned GitHub files.

## Signal handling and escalation

Aspire's [ConsoleCancellationManager](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/ConsoleCancellationManager.cs)
centralizes shutdown. It prefers PosixSignalRegistration for SIGINT/SIGTERM, additionally handles SIGQUIT on Windows,
and has a Console.CancelKeyPress fallback. It maintains separate cancellation tokens for ordinary command work and
expiration of the graceful window. First signal cancels work; second signal expires the graceful window; further
signals are ignored. The graceful clock starts once and is not reset by another child. Debugger attachment disables
automatic grace expiry; a second signal still escalates.

[RunCommand](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/Commands/RunCommand.cs#L85-L113)
selects a five-second graceful budget. BaseCommand's default budget is zero, so this is not the default for every Aspire
command. [Program](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/Program.cs#L1150)
constructs the normal CLI cancellation manager with another five-second final drain budget.

[BaseCommand](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/Commands/BaseCommand.cs#L132-L179)
races its handler against the forced-termination task. If the final deadline wins, it returns the termination outcome
without awaiting the unfinished handler. This is a deliberate standalone-CLI escape path, not proof that handler work
has settled. It also delays its stopping message by 200 ms to avoid noise for quick shutdowns.

## Child execution and output

The CLI [ProcessExecution adapter](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/DotNet/ProcessExecution.cs)
now delegates to shared ChildProcess. Its options pass the command-wide graceful token and platform signalling callback.

[ChildProcess](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Shared/ChildProcess.cs#L254-L453)
signals and waits concurrently under the shared graceful window, then escalates if necessary. Shutdown waits use tokens
independent of the already-cancelled work token. Cancellation plus shutdown failure becomes an AggregateException so
both causes remain available. Owned-tree paths additionally verify containment cleanup.

Output is drained separately. A five-second idle drain limit can log a warning and return while the reader remains
unfinished; output callback faults are logged rather than necessarily invalidating the child exit code. This differs
from Rafter's agreed output-success and settlement contracts. See
[draining](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Shared/ChildProcess.cs#L633-L684)
and [default termination/drain limits](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Shared/ChildProcessOptions.cs).

## Descendant ownership

Aspire distinguishes CallerManaged, OwnedTree, and AppHost lifetimes. OwnedTree runs the tool under a separate
[ProcessSupervisor](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Shared/ProcessSupervisor.cs).
The supervisor establishes containment before launching the real tool, monitors its owner's identity/liveness, and
cleans up on owner disappearance. It hands off the actual tool's exit code separately because supervisor termination
does not always preserve that code. This is more than a one-time walk of descendant PIDs.

On Windows, the supervisor creates a
[kill-on-close Job Object and assigns itself to it](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Shared/ProcessSupervisor.Windows.cs#L14-L38)
before starting the tool, with no breakaway flag. Its children inherit job membership. The inspected Windows verifier
does not separately query the job's active process count; it relies on the supervisor/job lifetime mechanism. Do not
equate observing supervisor exit with independently measuring every descendant's termination.

On Unix, the supervisor establishes a session/process group using setsid, forwards graceful shutdown to the tool,
and kills its group on scope teardown. ChildProcess sends group termination and polls for disappearance under a deadline.
This includes orphaned group members, but is not evidence that a descendant deliberately creating a new session cannot
escape the group. Normal root exit also triggers owned-scope cleanup; background workers are not implicitly retained.

## Platform-specific graceful requests

[ProcessTreeGracefulShutdownService](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/src/Aspire.Cli/Processes/ProcessTreeGracefulShutdownService.cs#L228-L345)
uses SIGTERM on Unix. On Windows it delegates to DCP's `stop-process-tree --skip-descendants --pid ...` helper; the
comments describe console attachment and control-event delivery. This review inspected the invocation, not the DCP
helper's internals, so it does not establish the exact event or all helper guarantees.

AppHost ownership is special: selected Windows paths preserve descendants so DCP can finish orchestrated-resource
cleanup. ChildProcess's graceful escalation honors this distinction, while owned trees require tree termination.
This is an Aspire orchestration policy, not a generic Rafter requirement.

## Relevant tests inspected

- [ConsoleCancellationManagerTests](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/tests/Aspire.Cli.Tests/ConsoleCancellationManagerTests.cs):
  first/second signals, shared grace expiry, final drain, idempotency, and disposal races.
- [ChildProcessTests](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/tests/Shared/Processes/ChildProcessTests.cs):
  cancellation, output tails, owned workers surviving root exit, escalation, AppHost versus owned-tree treatment,
  and preservation of cancellation plus shutdown failure.
- [ProcessSupervisorTests](https://github.com/microsoft/aspire/blob/949287a1b6ceaf8d8db49b41e594f74b1a74ba2c/tests/Shared/Processes/ProcessSupervisorTests.cs):
  cancellation, owner/supervisor crashes, descendant cleanup, and actual tool exit-code preservation.

The tests provide useful scenario designs, not fresh execution evidence from this review.

## Implications for Rafter

Keep the accepted five-second shared grace, second-interrupt escalation, and verified-settlement policy. Borrow the
separation of work cancellation from shutdown deadlines, centralized timing, process ownership established before tool
execution, and real descendant/crash fixtures. Explore a supervisor only if it materially simplifies our supported
platforms; Aspire can relaunch its own controlled executable, whereas Rafter must not blindly rerun a user's script
and execute its top-level side effects again.

Do not inherit Aspire's abandoned-handler escape path or warning-only output-drain behavior into a library method that
promises settled work and owned-output success. A hard stop of the script host would require a separately designed
explicit policy. Also avoid importing the DCP dependency or AppHost-specific lifetime exceptions without a Rafter use case.

Rafter's subsequent [owned-scope completion decision](example-design-plan.md#completion-of-owned-process-scopes)
requires cleanup of surviving owned descendants even after a normal root exit. Scripts are finite; persistent helpers
are outside the initial scope. Unconfirmed shutdown is reported as **Termination unknown**, with **Output incomplete**
reserved for cases where processes stopped but output completion could not be confirmed.

Rafter also gives unhandled or recorded execution failures and unconfirmed settlement precedence over cancellation:
exit `1` with the relevant diagnostics, versus `130` for clean cancellation. Expected cancellation errors alone do not
establish an independent failure.

Rafter's initial defaults now allow five seconds for graceful shutdown and a separate shared five seconds for termination
verification and output draining. A second interrupt skips grace but preserves verification. Unlike Aspire's handler
escape path, these budgets do not authorize abandoning Rafter's readers, cleanup operations, or script callbacks.

Still open: cleanup-error propagation and supported platforms.
The ownership mechanism remains an implementation investigation, not an implemented feature or a universal
descendant-containment guarantee.
