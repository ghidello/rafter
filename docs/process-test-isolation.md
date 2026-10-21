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

## Harness correction and remaining runtime boundary

The tests that launch native Rafter processes now share an xUnit collection with `DisableParallelization = true`.
They do not overlap unrelated SDK-consumer launchers in the test host. Tests still explicitly launch multiple Rafter
processes concurrently, checking shared grace periods, second interrupts, and awaited concurrent operations. No test
is skipped and no process deadline is extended. Process-host disposal also writes captured stdout/stderr to the test
report so future failures include runtime diagnostics.

With this isolation, the full local suite passed **405 tests**, with no failures or skips. The two-case inheritance
reproduction also passed independently. The PR records hosted CI results for this correction separately from the
two earlier failed attempts.

This is test-harness isolation, not a runtime interoperability fix. Applications that mix Rafter launches with other
broad-inheritance launchers in the same host can encounter the same exposure. A per-Rafter lock cannot coordinate an
unrelated launcher's private lock. Avoid overlapping those launch paths; use Rafter consistently or isolate the external
launcher. Issue #23 remains open for supported interoperability design and further failure evidence. A clean CI run
does not establish that arbitrary mixed launchers are safe.

No upstream source was copied. The implementation references above explain the observed OS behavior.

## Readiness-file race found by the improved logs

At commit `5f14db4`, upstream CI passed its tests while the fork failed the output-writer-failure test with an empty PID
file. Its new captured stderr confirmed the intended `Output incomplete` outcome and stopped process; the failure
was in the test's subsequent PID parsing. The fixture emitted output before writing its ready file, so immediate
shutdown could interrupt that write. This is a distinct demonstrated fixture race, not evidence that the earlier
five-second failures share that cause.

The fixture now publishes its complete PID file through a same-directory rename before emitting output. Hold and
graceful-stop modes use the same readiness publication. The output-failure test now requires the PID file and verifies
that process exited; the previous optional check is removed. No production runtime behavior changes.
