# Windows process-test isolation

Recorded on 2026-10-21 during [PR #22](https://github.com/sotsera/rafter/pull/22), tracked separately in
[issue #23](https://github.com/sotsera/rafter/issues/23).

## Observed failures and evidence limits

The analyzer change passed 403 tests locally and on the fork. Upstream CI failed two attempts on different existing
process tests. Attempt 1 returned invocation exit `1` instead of cancellation exit `130` after 5.202 seconds. Attempt 2
missed the expected cleanup message in the sticky-failure workflow after 5.651 seconds. Both attempts passed 402 of
403 tests. Neither captured the runtime stderr in its assertion, so the exact native/output failure cannot be recovered
from those logs. See [the upstream run and its attempts](https://github.com/sotsera/rafter/actions/runs/39583283264).

The similar durations suggest the five-second settlement verification budget, but timing alone does not establish
the cause. No deadline or expected exit code was changed to make the tests pass.

## Controlled inheritance reproduction

The harness mixes two launchers in one test process: Rafter's native launcher with an explicit inherited-handle list,
and `System.Diagnostics.Process.Start` for isolated SDK consumers. The latter uses broad handle inheritance on Windows.
Its private launch lock coordinates its own calls, not Rafter's temporary inheritable pipe handles. This follows the
[.NET 10.0.13 implementation](https://github.com/dotnet/runtime/blob/v10.0.13/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs)
and [Windows inheritance rules](https://learn.microsoft.com/en-us/windows/win32/procthread/inheritance).

`WindowsHandleInheritanceTests` makes that overlap deterministic: create an unrelated inheritable pipe writer, launch
a child that stays alive, close the parent's writer, and observe pipe completion. With the general .NET launcher, EOF
remains blocked until the foreign child exits. With Rafter's explicit handle list, EOF arrives while its child is still
alive. Both cases passed locally. This proves the interference mechanism; it does not prove which handle or process
caused each historical failure.

## Harness correction in PR #22

The tests that launch native Rafter processes now share an xUnit collection with `DisableParallelization = true`.
They do not overlap unrelated SDK-consumer launchers in the test host. Tests still explicitly launch multiple Rafter
processes concurrently, checking shared grace periods, second interrupts, and awaited concurrent operations. No test
is skipped and no process deadline is extended. Process-host disposal also writes captured stdout/stderr to the test
report so future failures include runtime diagnostics.

With this isolation, the full local suite passed **405 tests**, with no failures or skips. The two-case inheritance
reproduction also passed independently. The PR records hosted CI results for this correction separately from the
two earlier failed attempts.

That correction isolated the harness; it did not fix runtime interoperability. A per-Rafter lock cannot coordinate an
unrelated launcher's private lock. Issue #23 retained that runtime work, addressed by the implementation below.

No upstream source was copied. The implementation references above explain the observed OS behavior.

## Readiness-file race found by the improved logs

At commit `5f14db4`, upstream CI passed its tests while the fork failed the output-writer-failure test with an empty PID
file. Its new captured stderr confirmed the intended `Output incomplete` outcome and stopped process; the failure
was in the test's subsequent PID parsing. The fixture emitted output before writing its ready file, so immediate
shutdown could interrupt that write. This is a distinct demonstrated fixture race, not evidence that the earlier
five-second failures share that cause.

The fixture now publishes its complete PID file through a same-directory rename before emitting output. Hold and
graceful-stop modes use the same readiness publication. The output-failure test now requires the PID file and verifies
that process exited; the previous optional check is removed. Those PR #22 changes did not alter production behavior.

## Runtime handle isolation for issue #23

Rafter now creates its local stdio handles without inheritance. A suspended copy of the host executable holds the
inheritable duplicates in a separate handle table. The target uses that helper as its nominal parent and inherits
only the listed stdio handles. Another launcher in the script process therefore cannot inherit Rafter's temporary
copies, even if it launches precisely while those copies exist. This follows Microsoft's
[suspended-helper guidance](https://devblogs.microsoft.com/oldnewthing/20200306-00/?p=103538) and the documented
[parent-process and handle-list attributes](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute).
No upstream code was copied or new package introduced.

The helper joins the private kill-on-close job at creation. The target inherits that job before executing code;
breakaway remains disabled. Rafter terminates and waits for the helper before returning from launch. Failed
preparation or target creation also stops it; closing the job remains final cleanup. The helper's main thread is
never resumed. There is no persistent helper or process-wide cache. Remote duplicate values are never closed as
local handles; terminating the helper releases its copies together.

The accepted tradeoff is one extra process per launch and a different reported parent PID. A local alternating
measurement against merged commit `37e2fd2` used 20 samples per implementation after five warmup pairs. Median native
launch time was **2.49 ms before** and **6.18 ms with isolation** (ranges 2.29–3.48 and 5.68–9.95 ms). This includes
helper termination but excludes waiting for the target's exit; it is a local observation, not a latency guarantee.
Measurement source and TRX are retained locally under `artifacts/issue23` and `artifacts/test-results/issue23-measurement`.

### Windowless-host correction

The first full prototype run passed 411/412 tests but failed the owner-crash fixture: its child exited with Windows
status `0xC0000142` before publishing readiness. The failing owner had a nonempty console process list but a null
console window handle. An experimental delay before donor termination did not fix it. Using headless creation for
that case did; the delay was removed. Rafter now requires both a console window and a console process list before
choosing shared-console creation/signalling. A pseudoconsole still provides a message-only window, as documented by
[GetConsoleWindow](https://learn.microsoft.com/en-us/windows/console/getconsolewindow). Windowless hosts take the
immediate-force path; a separate explicit-detach fixture remains covered.

### Regression coverage and evidence

The deterministic mixed-launcher test starts a foreign `System.Diagnostics.Process` after all donor stdio copies
exist and before target creation. While that foreign child remains alive, Rafter must settle both output streams
exactly for normal output, large dual-stream output, graceful stop, forced stop, and a surviving descendant. The tests
also verify that the donor has exited before launch returns, including missing executables and preparation failures.
A redirected-input host verifies stdin delivery. The original two-case broad-inheritance reproduction remains as a
baseline; its intentionally inheritable host pipe is why the test collection still disables outside parallelization.
Explicit Rafter concurrency and mixed-launcher overlap remain enabled inside tests.

The local full suite passed **413 tests, zero failed/skipped**. An earlier run exposed a UTF-8 mismatch in the new
stdin fixture's writer; it now explicitly matches the probe's UTF-8 reader. After a final cleanup change prevented
restarting a failed donor wait from `Dispose`, all ten inheritance/input/cleanup cases passed again. Direct console
execution passed all four native process checks, with scoped graceful signalling confirmed. The broader process
checks cover owner death, breakaway denial, headless shutdown, cancellation, second interrupts, and concurrent work.

A pseudoterminal input check returned `console:hello console` with exit zero. Actual Ctrl+C recorded invocation result
`130` and left no running child (the PowerShell wrapper reported exit 1). All 38 terminal/output review cases passed,
and local packaging succeeded. Hosted CI for the final commit is recorded in the issue/PR when complete.
No shutdown deadline or cancellation exit-code assertion was weakened. These checks establish the demonstrated
inheritance fix; they do not retroactively identify the exact causes of the two historical CI failures.
