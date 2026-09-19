# Phase 7: .NET 10 process runtime

Status: generic runtime implemented; exhaustive verification remains open. See the
[evidence](phase-07-process-runtime-evidence.md) and [2026-09-19 gate audit](phase-05-07-closeout.md), including the
remaining startup/teardown matrix, memory measurements and an earlier unexplained macOS test stall.

## Objective

Implement the complete generic, argument-safe, deadlock-safe, cancellation-safe child-process API and runtime on
.NET 10. Phase 7 owns public streamed execution and bounded capture end to end; phase 8 consumes those finished modes
for extensions and typed tools. No BCL process type leaks into Rafter's public API.

## Questions to resolve before implementation

- [x] **What ownership guarantee does Rafter make for descendants after the direct child exits, especially when a
      descendant retains stdout or stderr and prevents EOF?** Guarantee bounded terminal settlement and truthful
      direct-child termination reporting. Descendant termination is a documented best-effort outcome on .NET 10, not
      a portable guarantee. After an ordinary direct-child exit, wait one fixed drain-completion window for EOF; if a
      descendant still retains either pipe, close/cancel both drains, expose no partial capture, and fail with
      `ProcessOutputException` reason `RetainedPipe`. On cancellation or timeout while the direct child remains live,
      use `Kill(entireProcessTree: true)`, but do not treat direct-process `WaitForExit` as proof that every descendant
      exited. Guarantee bounded terminal settlement and never claim direct-child termination without independent
      confirmation; if kill or verification fails, return the primary cancellation/timeout classification with an
      incomplete-teardown inner failure rather than promising that the child is gone.
- [x] **Can the required guarantee be implemented proactively on .NET 10 with Windows job objects, Unix process
      groups/sessions, or another supervised-launch mechanism on every supported platform?** Not through one
      supported `ProcessStartInfo` contract. Correct Windows job assignment would require suspended native process
      creation to avoid a post-start race; correct Unix session or process-group ownership would likewise require a
      native launch path or wrapper. Do not build parallel platform launchers in v1. Use the portable BCL launch,
      bounded drain settlement, and best-effort `Kill(entireProcessTree: true)` contract, and revisit proactive
      supervision only as a separately designed future capability.
- [x] **If the direct child has already exited and the remaining descendants can no longer be identified reliably,
      should Rafter close/cancel its drains and report incomplete output, fail the operation, or require stronger
      process-group ownership from launch time?** Start one fixed drain-completion deadline when the direct child
      exits. If either redirected stream has not reached EOF when it expires, cancel/close both drains and fail both
      `Run()` and `Capture()` with `ProcessOutputException` reason `RetainedPipe`. `Capture()` exposes no partial
      `ProcessCapture`, even if one stream completed. Safe streaming output already presented remains visible but does
      not make the terminal operation successful. Do not return incomplete data or require native process-group
      ownership in v1.
- [x] **Does `Kill(entireProcessTree: true)` satisfy only best-effort teardown, and how will Rafter verify descendant
      termination rather than treating direct-process `WaitForExit` as proof?** Treat tree kill as a best-effort
      request. Capture platform, aggregate, and race-related failures, then independently require the direct child to
      exit within the forced-termination deadline. Direct-child exit never proves descendant exit. If direct-child
      termination cannot be confirmed, report that teardown may be incomplete. Preserve external cancellation as an
      `OperationCanceledException` and an authored timeout as `ProcessTimeoutException`, attaching any teardown
      failure as the inner exception. Tests use fixture-reported descendant PIDs and sentinels to record the observed
      platform result, then perform independent cleanup; that stronger test observation is not a runtime guarantee.
- [x] **Record the ownership model, platform implementation, retained-pipe outcome, and survivor guarantee before
      building the process runtime.** Use the matrix below as the v1 contract. The portable .NET 10 runtime has no
      general graceful-termination stage: after cancellation or timeout wins the launch/exit race, proceed directly
      to best-effort tree kill and bounded direct-child verification. Revisit native supervision and graceful control
      only as separately designed future capabilities.

| Situation | Runtime action | Successful guarantee |
| --- | --- | --- |
| Natural direct-child exit and both pipes reach EOF | Complete normally | Direct child exited; accepted output drained |
| Natural direct-child exit with either pipe retained | Close both drains; fail with `RetainedPipe` | Bounded settlement; no partial capture |
| Cancellation or timeout while the direct child is live | Best-effort tree kill; bounded direct-child wait | Direct child confirmed exited |
| Tree kill or direct-child verification fails | Preserve the primary classification and attach teardown failure | No false termination claim |
| Descendants | Observe with fixture PIDs and clean independently | No portable survivor guarantee |

### Internal settlement deadlines

These process-wide implementation defaults are not public execution timeouts and do not limit healthy process
execution. Keep them together with a `TimeProvider` in one immutable internal policy. Production uses
`TimeProvider.System`; state-machine and synthetic-operation tests inject controlled time, while real-process tests
use broad system-clock bounds rather than exact scheduling assertions.

| Deadline | Production default | Begins | Expiry outcome |
| --- | ---: | --- | --- |
| Direct-exit drain completion | 2 seconds | Direct child exits normally | Close both drains and fail with `RetainedPipe` |
| Tree-kill request | 2 seconds | `Kill(entireProcessTree: true)` is dispatched | Report incomplete teardown; transfer kill ownership to the operation reaper |
| Forced-kill direct-child verification | 5 seconds | Tree-kill request returns | Report teardown failure; do not claim termination |
| Forced-close drain settlement | 2 seconds | Rafter closes/cancels redirected streams | Report failure; transfer a late task to the tracked operation reaper |

## Questions to resolve before public API freeze

- [x] **What are the exact `RafterContext.Process(...)` overloads, and which scalar option states may supply an
      executable without introducing a nullable or repeated launch path?** Expose `Process(string)`,
      `Process(RequiredOption<string>)`, and `Process(DefaultedOption<string>)` only. Optional, repeated, and
      non-string handles cannot identify an executable. Resolve an accepted handle immediately from the invocation
      snapshot into immutable executable text plus sensitivity metadata; never retain the handle. A null argument or
      foreign-command handle throws immediately, while empty or whitespace resolved text records a specification
      diagnostic reported by the terminal operation. Builder creation never probes or launches the executable.
- [x] **What are the exact immutable `ProcessBuilder` token-appender signatures for arguments, flags, and named
      options; when is an optional option omitted, and which empty values remain intentional tokens?** Expose
      `Argument(string|Option<string>|RequiredOption<string>|DefaultedOption<string>)`, unconditional `Flag(string)`,
      conditional `Flag(string, bool|Option<bool>|RequiredOption<bool>|DefaultedOption<bool>)`, and
      `Option(string, string|Option<string>|RequiredOption<string>|DefaultedOption<string>)` overload families.
      Append distinct tokens in authored order. An absent optional argument or option value omits that argument or
      the complete name/value pair; an absent optional Boolean is false. A false conditional flag and a complete
      pair omitted by optional absence do not validate their unused names or add diagnostics. Preserve an empty
      string as an intentional argument or option-value token, including a value bound from `--name=`. Empty or
      whitespace names for emitted flags/options and an embedded NUL in any emitted token record terminal diagnostics
      without requiring a dash prefix. Nulls and foreign handles fail immediately; resolve accepted handles once at
      the fluent call. Do not add repeated-handle expansion or general object formatting in v1.
- [x] **How do sensitive option handles propagate redaction metadata through arguments, named options, environment
      edits, safe diagnostics, and process-start failures without exposing a public raw-token model?** Resolve every
      executable, argument, option value, and environment value internally as exact raw text plus sensitivity
      metadata. Pass raw text unchanged to `ProcessStartInfo`; redaction never changes child behavior. A sensitive
      option handle propagates its metadata through every handle overload, while literal strings are non-sensitive
      unless an explicit `SetSensitive` environment method marks them. Converting a handle through
      `context.Value(...)` loses provenance, although invocation-wide registered-value matching remains a best-effort
      presentation safeguard. Rafter-owned diagnostics and exception rendering redact sensitive values; public safe
      exception messages contain no raw tokens. Preserved platform `InnerException` values and raw capture are
      application-owned when inspected directly. Document that operating-system process and environment inspection
      remains outside Rafter's output-redaction guarantee. Build one immutable process-local redactor from all
      sensitivity-tagged executable, argument, option, and environment values and apply it before streamed output
      enters the Phase 6 coordinator. `SetSensitive` therefore protects this process's presentation and safe metadata
      but does not register an arbitrary literal invocation-wide. Values originating from a command option marked
      `.Sensitive()` are already in the invocation redactor and remain protected if raw capture is later sent through
      a Rafter-managed channel; other raw captured values remain the application's responsibility.
- [x] **What is the exact `ProcessEnvironmentBuilder` surface and callback contract for inherited edits, `Clear`,
      `Set`, `SetSensitive`, and `Unset`; how are duplicate keys ordered and resolved under host case semantics?**
      Expose `Environment(Action<ProcessEnvironmentBuilder>)`; `Clear()`; `Unset(string)`; and `Set`/`SetSensitive`
      overloads accepting a name plus `string`, `Option<string>`, `RequiredOption<string>`, or
      `DefaultedOption<string>`. The callback receives a mutable, fluent, callback-scoped draft; snapshot its ordered
      edits immutably on return and reject later use of a retained draft. Begin from inherited environment state,
      apply edits in authored order, let repeated edits within the block use last-edit-wins host key semantics, and
      let `Clear` remove all preceding state. An absent optional value performs no edit; `Unset` is explicit removal;
      empty values are valid. An absent optional `Set`/`SetSensitive` does not validate its unused key. Preserve key
      spelling for diagnostics while the host owns effective casing. Invalid keys (empty, whitespace, NUL, or `=`)
      and values containing NUL record terminal diagnostics. Nulls and foreign handles fail immediately. Callback
      exceptions escape unchanged. Only one environment block is valid per
      process specification; evaluate and snapshot later blocks but preserve the first and record a duplicate-policy
      diagnostic.
- [x] **What are the exact working-directory, timeout, capture-limit, and valid-exit overloads, including accepted
      option states, immediate argument validation, accumulated duplicate-policy diagnostics, and immutable reuse?**
      Expose `WorkingDirectory` overloads for `string` and optional/required/defaulted string handles; `Timeout`
      overloads for `TimeSpan` and optional/required/defaulted `TimeSpan` handles; `CaptureLimitBytes` overloads for
      `long` and optional/required/defaulted `long` handles; and `ValidExitCodes(params int[])`. An absent optional
      handle does not set or consume the policy slot: working directory inherits the context, timeout remains absent,
      and capture uses its 1 MiB per-stream default. Resolve working directories through Phase 4 policy relative to
      the target context. Accept timeout values from 1 through 4,294,967,294 milliseconds inclusive, matching the
      .NET 10 timer range. Accept capture limits from 1 through `int.MaxValue` bytes inclusive without eager
      allocation; this keeps each successfully decoded stream representable by the single `string` exposed by
      `ProcessCapture`. Require at least one
      valid exit code, snapshot the array, accept every `int`, remove duplicates while preserving first occurrence,
      and replace the default `{ 0 }` set. A repeated present policy preserves the first and records a terminal
      diagnostic. Nulls and foreign handles fail immediately; invalid resolved values become terminal diagnostics.
      Every modifier derives a new builder.
- [x] **What are the exact task-returning terminal signatures, cancellation source, and post-invocation behavior for
      `Run()` and `Capture()`; should either terminal accept an explicit `CancellationToken`?** Expose
      `Task<ProcessExit> Run()` and `Task<ProcessCapture> Capture()` with the approved fluent terminal names and no
      explicit token overloads. Both use the creating context's token—invocation cancellation for conditions and
      execution, `CancellationToken.None` for cleanup—while `.Timeout(...)` is the per-process narrower lifetime.
      Each terminal call acquires independent execution state and launches independently, including concurrent calls
      on one builder. Pre-cancellation returns a cancelled task carrying the creating context token and launches
      nothing. Operational and specification failures complete the returned task; a terminal call after
      its creating callback's process-operation scope closes throws `InvalidOperationException` synchronously and
      launches nothing, including while another target in the invocation remains active. Pure fluent derivation does
      not extend execution authority. Give each condition, execution, target-cleanup, and command-cleanup callback a
      distinct active process-operation scope. When any callback returns with an operation still active, cancel and
      settle it and fail that callback rather than allowing a detached child. Returning a terminal task directly and
      awaiting composed tasks remain valid because callback normalization awaits them before closing the scope.
- [x] **What are the exact public shapes of `ProcessExit`, `ProcessCapture`, `RafterException`, each process exception,
      and `ProcessOutputReason`; which constructors remain internal and which safe properties are exposed?** Expose
      public `readonly record struct ProcessExit(int ExitCode)` and sealed immutable
      `ProcessCapture(int ExitCode, string StandardOutput, string StandardError)` with public construction and null
      rejection for stream strings. Expose abstract `RafterException`, non-sealed `ProcessException`, and sealed
      `ProcessStartException`, `ProcessExitException`, `ProcessTimeoutException`, and `ProcessOutputException`; keep
      their construction internal. `ProcessException` itself represents unclassified process infrastructure failure.
      `ProcessExitException` exposes `ExitCode` and nullable `Capture`; capture is present only after complete bounded
      decoding and an invalid exit. `ProcessTimeoutException` exposes `Timeout`. `ProcessOutputException` exposes
      `Reason`, `Stream`, and nullable `LimitBytes`. `ProcessOutputReason` contains `CaptureLimitExceeded`,
      `InvalidUtf8`, and `RetainedPipe`; `ProcessOutputStream` contains `StandardOutput`, `StandardError`, and `Both`.
      Populate the limit only for limit overflow and use `Both` when one reason affects both streams. For unlike
      concurrent failures choose `RetainedPipe`, then `InvalidUtf8`, then `CaptureLimitExceeded`, and stdout before
      stderr within one reason; retain secondary facts internally. Exception messages remain safe and expose no raw
      launch metadata. Cancellation remains `OperationCanceledException`.
- [x] **When process specification diagnostics exist, what exception type carries them, what stable ordering applies,
      and can any builder callback or fluent method throw before a terminal operation?** Fault the terminal task with
      `ProcessException` itself, using a safe `Process specification is invalid.` heading and every diagnostic's
      stable internal code; expose no separate public diagnostic model. Validate completely and launch nothing when
      any diagnostic exists. Order common diagnostics by fluent-call sequence, use fixed validation order within one
      call, retain environment sub-edit order, and evaluate terminal-mode diagnostics after common diagnostics. Keep
      the first present single-valued policy and diagnose every later one. Fluent construction throws only
      `ArgumentNullException` for nulls, `InvalidOperationException` for foreign handles or a closed environment
      draft, process creation outside an active callback scope, and an environment callback's original exception.
      Semantic invalidity—including empty executable,
      invalid names/environment/path/timeout/limit, empty valid-exit set, and duplicate policies—accumulates. Except
      for the already-approved synchronous post-invocation authority failure, terminal specification failures are
      observed by awaiting the returned task. A present capture-limit policy is a terminal-mode diagnostic for
      streaming `Run()` rather than an ignored setting; it is valid only for `Capture()`.
- [x] **How does the runtime honor both bounded terminal settlement and the prohibition on abandoned drain tasks if a
      redirected read remains incomplete after its stream is closed and the forced-close drain deadline expires?**
      Prioritize bounded terminal settlement without abandoning ownership. Cancel the drain, dispose its reader and
      stream, and wait two seconds. If it remains incomplete, suppress later presentation and transfer the task plus
      its residual owned resources to one process-wide internal `ProcessOperationReaper`. The reaper strongly owns and
      observes each task, records eventual exceptions internally, and disposes resources exactly once; it emits no
      user output and does not block application shutdown. Attach a safe drain-settlement failure to the terminal's
      already-selected primary classification. Real-process tests require an empty reaper after every case; a
      controllable synthetic non-settling read proves bounded return, tracked ownership, and eventual cleanup. A
      permanently broken platform read may retain small tracked state until process shutdown.
- [x] **Record the frozen signatures, validation table, environment edit semantics, exception/result matrix, and
      pathological drain-settlement decision before implementing the public process model.** The sections below are
      the Phase 7 public and behavioral contract. Later implementation details may remain internal but cannot widen
      or reinterpret this surface without reopening Gate R0.

**Gate R0 — Public process contract frozen:** every question above is answered in this document, the examples use
only the approved surface, and the implementation checklist and verification matrix reflect the answers.

### Frozen public signatures

```csharp
public sealed class RafterContext
{
    public ProcessBuilder Process(string executable);
    public ProcessBuilder Process(RequiredOption<string> executable);
    public ProcessBuilder Process(DefaultedOption<string> executable);
}

public sealed class ProcessBuilder
{
    public ProcessBuilder Argument(string value);
    public ProcessBuilder Argument(Option<string> value);
    public ProcessBuilder Argument(RequiredOption<string> value);
    public ProcessBuilder Argument(DefaultedOption<string> value);

    public ProcessBuilder Flag(string name);
    public ProcessBuilder Flag(string name, bool condition);
    public ProcessBuilder Flag(string name, Option<bool> condition);
    public ProcessBuilder Flag(string name, RequiredOption<bool> condition);
    public ProcessBuilder Flag(string name, DefaultedOption<bool> condition);

    public ProcessBuilder Option(string name, string value);
    public ProcessBuilder Option(string name, Option<string> value);
    public ProcessBuilder Option(string name, RequiredOption<string> value);
    public ProcessBuilder Option(string name, DefaultedOption<string> value);

    public ProcessBuilder Environment(Action<ProcessEnvironmentBuilder> configure);

    public ProcessBuilder WorkingDirectory(string path);
    public ProcessBuilder WorkingDirectory(Option<string> path);
    public ProcessBuilder WorkingDirectory(RequiredOption<string> path);
    public ProcessBuilder WorkingDirectory(DefaultedOption<string> path);

    public ProcessBuilder Timeout(TimeSpan timeout);
    public ProcessBuilder Timeout(Option<TimeSpan> timeout);
    public ProcessBuilder Timeout(RequiredOption<TimeSpan> timeout);
    public ProcessBuilder Timeout(DefaultedOption<TimeSpan> timeout);

    public ProcessBuilder CaptureLimitBytes(long limit);
    public ProcessBuilder CaptureLimitBytes(Option<long> limit);
    public ProcessBuilder CaptureLimitBytes(RequiredOption<long> limit);
    public ProcessBuilder CaptureLimitBytes(DefaultedOption<long> limit);

    public ProcessBuilder ValidExitCodes(params int[] codes);

    public Task<ProcessExit> Run();
    public Task<ProcessCapture> Capture();
}

public sealed class ProcessEnvironmentBuilder
{
    public ProcessEnvironmentBuilder Clear();
    public ProcessEnvironmentBuilder Unset(string name);

    public ProcessEnvironmentBuilder Set(string name, string value);
    public ProcessEnvironmentBuilder Set(string name, Option<string> value);
    public ProcessEnvironmentBuilder Set(string name, RequiredOption<string> value);
    public ProcessEnvironmentBuilder Set(string name, DefaultedOption<string> value);

    public ProcessEnvironmentBuilder SetSensitive(string name, string value);
    public ProcessEnvironmentBuilder SetSensitive(string name, Option<string> value);
    public ProcessEnvironmentBuilder SetSensitive(string name, RequiredOption<string> value);
    public ProcessEnvironmentBuilder SetSensitive(string name, DefaultedOption<string> value);
}

public readonly record struct ProcessExit(int ExitCode);

public sealed record ProcessCapture(int ExitCode, string StandardOutput, string StandardError);

public abstract class RafterException : Exception;
public class ProcessException : RafterException;
public sealed class ProcessStartException : ProcessException;
public sealed class ProcessExitException : ProcessException;
public sealed class ProcessTimeoutException : ProcessException;
public sealed class ProcessOutputException : ProcessException;

public enum ProcessOutputReason
{
    CaptureLimitExceeded,
    InvalidUtf8,
    RetainedPipe,
}

public enum ProcessOutputStream
{
    StandardOutput,
    StandardError,
    Both,
}
```

Exception construction is internal. `ProcessExitException` exposes `int ExitCode` and nullable
`ProcessCapture Capture`; `ProcessTimeoutException` exposes `TimeSpan Timeout`; `ProcessOutputException` exposes
`ProcessOutputReason Reason`, `ProcessOutputStream Stream`, and nullable `long LimitBytes`. All public APIs receive
XML documentation during implementation.

### Validation timing and ordering

| Timing | Conditions | Result |
| --- | --- | --- |
| Fluent call | Null argument | `ArgumentNullException`; source builder remains unchanged |
| Fluent call | Foreign-command option handle | `InvalidOperationException`; source builder remains unchanged |
| `context.Process(...)` | No process-operation scope is active for the context | Synchronous `InvalidOperationException`; no builder and no launch |
| Environment callback | Callback throws or retained draft is reused | Original exception or `InvalidOperationException`; no derived builder |
| Terminal call boundary | Creating callback's process-operation scope already closed | Synchronous `InvalidOperationException`; no task and no launch |
| Awaited terminal task | Invalid text, path, numeric policy, empty valid-exit set, or duplicate policy | `ProcessException`; all diagnostics reported and no launch |
| Awaited terminal task | Creating context token already cancelled | Cancelled task with that token; no launch |

Assign every accumulated diagnostic the process builder's monotonic fluent-call sequence. Sort common diagnostics by
that sequence and fixed within-call validation order; retain environment sub-edit order; append terminal-mode
diagnostics afterward. Optional absence contributes neither tokens, edits, policy assignment, nor diagnostics.

Use the following stable internal catalog. Messages identify the field or policy but never include raw executable,
argument, environment-value, or path text.

| Code | Specification problem |
| --- | --- |
| `RAFTER1501` | Executable is empty, whitespace, or contains NUL |
| `RAFTER1502` | Path-like executable is malformed or uses an unsupported namespace |
| `RAFTER1503` | An emitted argument, flag, option name, or option value contains NUL |
| `RAFTER1504` | An emitted flag or option name is empty or whitespace |
| `RAFTER1505` | Environment key is empty, whitespace, contains NUL, or contains `=` |
| `RAFTER1506` | Environment value contains NUL |
| `RAFTER1507` | Process working directory is malformed, unsupported, or outside the command root |
| `RAFTER1508` | Authored timeout is outside the supported range |
| `RAFTER1509` | Capture limit is outside the supported range of 1 through `int.MaxValue` bytes |
| `RAFTER1510` | Valid-exit declaration contains no codes |
| `RAFTER1511` | A single-valued process policy is present more than once |
| `RAFTER1512` | Capture limit is present on streaming `Run()` |
| `RAFTER1513` | Streaming `Run()` cannot select a safe process-local redaction marker |

### Terminal result and exception precedence

After the synchronous invocation-authority check, validate the complete immutable specification before observing
pre-cancellation. This makes authored mistakes deterministic: an invalid specification faults with `ProcessException`
even when the invocation token is already cancelled, and neither case launches. For a valid specification, one
atomic lifecycle arbiter chooses natural exit, external cancellation, or authored timeout. Pre-cancellation wins
before launch. During execution, the first successfully committed terminal transition wins; only the documented
outcomes may result from a real race. Teardown and drain failures are retained as secondary or inner exceptions and
never change an already-selected external-cancellation or timeout classification.

| Condition after safe settlement | Terminal result |
| --- | --- |
| External cancellation wins | `OperationCanceledException` carrying the invocation token |
| Authored timeout wins | `ProcessTimeoutException` exposing the authored timeout |
| Startup fails | `ProcessStartException` with the platform failure as inner exception |
| Retained pipe, invalid UTF-8, or capture overflow | `ProcessOutputException` using the approved deterministic reason/stream precedence |
| Invalid exit after streaming | `ProcessExitException` with exit code and null capture |
| Invalid exit after complete capture | `ProcessExitException` with exit code and complete raw capture |
| Valid exit after streaming | `ProcessExit` |
| Valid exit after complete capture | `ProcessCapture` |
| Otherwise unclassified process infrastructure failure | `ProcessException` |

An output-policy failure precedes invalid-exit evaluation because no valid terminal result exists without accepted
output settlement. Rafter-owned exception messages contain safe metadata only. A process task still active when its
owning callback returns is cancelled and settled, and the callback fails with `InvalidOperationException`; it never
becomes a detached process.

Collect unexpected secondary failures in fixed lifecycle order: tree-kill request, direct-child verification,
stdout drain, stderr drain, and resource disposal. Attach no inner exception when none exists, the original exception
when exactly one exists, and an `AggregateException` preserving that order when several exist. Expected cancellation
from Rafter-owned drain closure is not a secondary failure. This rule applies beneath cancellation, timeout, output,
and other already-selected primary outcomes without changing their classification.

### Callback-scoped process ownership

Every individual condition, execution, target-cleanup, and command-cleanup callback owns an internal
process-operation scope. The runtime activates a fresh scope before invoking the callback; `context.Process(...)`
captures that scope and throws `InvalidOperationException` if no callback scope is active. A terminal call atomically
registers before launch and unregisters only after complete process, drain, and teardown settlement. Callback
completion closes the scope and snapshots operations that were active at that exact boundary; later registration
fails synchronously. Rafter cancels and settles every snapshotted operation. The callback is considered to have
discarded those tasks even if a process wins a concurrent natural-exit race immediately afterward. A builder created
in one condition cannot be launched from a later condition or the target execution, even though the runtime may reuse
the surrounding context object.

Scope closure uses a separate internal ownership-cancellation signal even when the context token is
`CancellationToken.None`. It exists only to settle a discarded operation and does not masquerade as invocation
cancellation; the owning callback's `InvalidOperationException` remains the user-visible failure. A caller still
holding the discarded process task observes any lifecycle transition already committed; otherwise it may observe
cancellation from that internal signal.

Close and settle the process-operation scope before closing the context's Phase 6 output scope. Streaming output
produced during discarded-operation teardown therefore retains its target or cleanup attribution; it must not fall
back to command scope merely because the callback delegate has returned. After process settlement and callback
outcome composition, close the output scope under the existing Phase 6 rules.

If the callback otherwise succeeded, any active operation makes it fail with a safe `InvalidOperationException`. If
the callback already threw, preserve that original exception instance as primary and retain discarded-operation or
teardown failures as internal secondary facts. Apply the same rule to target and command cleanup, where the failure
participates in the existing Phase 5 cleanup outcome. Completed terminal tasks do not delay callback closure; inspect
their exception state internally so a caller-discarded fault cannot raise `UnobservedTaskException`, but do not infer
whether an already-completed task was awaited or override a callback that deliberately caught its failure.

For a condition, close and validate its process scope before interpreting the returned Boolean. A condition that
returns `false` while leaving an active process is a condition-phase failure, not a skipped target.

Conditions and target execution inherit the invocation cancellation token assigned to their contexts. Target and
command cleanup retain Phase 5's `CancellationToken.None` behavior; a process started in either cleanup callback
therefore needs an authored `.Timeout(...)` if the author wants a bound. Rafter does not invent an execution timeout
for cleanup processes.

### Raw pipe and decoding algorithm

Drain `StandardOutput.BaseStream` and `StandardError.BaseStream` directly with independent asynchronous loops; do not
use `ReadToEnd`, sequential reads, `BeginOutputReadLine`, or replacement-fallback `StreamReader` decoding. Each drain
owns fixed 16 KiB byte storage, a strict no-BOM UTF-8 `Decoder`, and bounded reusable character storage. A UTF-8 BOM
on a process pipe is decoded as U+FEFF application data rather than stripped as file metadata. Flush the decoder at
EOF so an incomplete final sequence is an `InvalidUtf8` failure.

Count every captured byte before passing it to the decoder. Exactly the configured limit is valid; the first byte
beyond it records capture overflow and stops capture retention, but the loop continues reading and strict decoding
into reusable discard storage. Continuing to decode preserves the approved output-failure precedence when malformed
UTF-8 occurs after overflow. On decoding failure, finalize presentation of the valid decoded prefix already admitted
to the streaming redactor, then stop presenting or retaining that stream while continuing to drain its raw bytes to
EOF or bounded forced closure. Safe streaming text emitted before a later decoding failure remains visible; capture
exposes no partial result. Buffer ownership follows the drain into the reaper on an exceptional late handoff.

Streaming uses Phase 6's 65,536-character presentation-segment bound. A child line longer than that is emitted as
attributed continuation segments rather than accumulated without limit; streaming presentation does not promise to
preserve physical line boundaries beyond that bound. This segmentation never affects exact bounded `Capture()` data.

In capture mode, retain accepted raw bytes in fixed-size segments while the incremental decoder validates the same
bytes without retaining decoded characters. Do not use a geometrically growing contiguous byte buffer. On complete,
output-valid settlement, materialize each final UTF-16 string directly from its segments and release those segments;
this includes the capture attached to an invalid-exit failure. On overflow, decoding failure, cancellation, timeout,
retained pipe, or startup failure, release retained segments
without constructing partial strings. The configured limit therefore bounds logical retained input bytes per stream,
with at most one segment of allocator slack, while successful materialization temporarily also owns the returned
UTF-16 strings. Document this memory envelope instead of claiming the byte limit is an exact managed-memory ceiling.

### Executable and working-directory resolution

The process working directory always uses the Phase 4 resolver. With no override it is the creating context's
normalized working directory. A present override resolves relative to that directory and must remain contained by
the command root; an absolute override is accepted only when the same containment check succeeds. Syntax and
containment failures are specification diagnostics. An otherwise valid directory that is missing, inaccessible, or
replaced between validation and launch is a `ProcessStartException`, because filesystem availability is an
operational race rather than model invalidity.

Executable resolution is deliberately not a command-root security boundary. A bare executable name containing no
directory separator is passed unchanged to `ProcessStartInfo.FileName` for the host runtime's ordinary executable
lookup. A path-like relative executable is normalized against the effective process working directory, and a fully
qualified executable is normalized directly. Perform this normalization at the terminal boundary, after the retained
working-directory policy is known; option handles are still resolved only once when their fluent call occurs. Both
forms may identify a file outside the command root: selecting a command root grants filesystem-mutation authority but
is not an executable allowlist. Reject malformed, partially qualified, drive-relative, Windows extended, and Windows
device paths through the shared Phase 4 normalization rules. Do not probe existence, executable permission, extension
inference, or `PATH` during specification construction; launch reports those operational failures.

### Start, timeout, and output coordination

Configure `UseShellExecute = false`, redirect stdout and stderr in both terminal modes, and never redirect stdin.
`Run()` streams both decoded channels through the invocation output coordinator without retaining complete output;
`Capture()` drains and retains both channels without presenting them. Each streaming drain holds an output admission
for its complete presentation lifetime so invocation sealing cannot race accepted child output. Child stdout keeps
stdout identity and child stderr keeps stderr identity; target scoping, redaction, serialized writer access, newline
normalization for presentation, and output-writer failures remain owned by the Phase 6 coordinator. Raw capture never
passes through presentation normalization. A streaming sink or renderer failure is recorded as the invocation's
Phase 6 output-infrastructure failure and suppresses later presentation, but does not stop either pipe drain or
replace the independently determined process terminal result with a process exception.

Before that handoff, streaming drains apply one incremental redactor built from the union of the Phase 6 invocation
patterns and every value explicitly tagged sensitive in the immutable process specification. Its replacement marker
must be safe for that complete union, preventing a later presentation stage from reintroducing a process-local
sensitive value as its own marker. The Phase 6 invocation redactor remains a defense-in-depth verification layer but
has no remaining invocation pattern to replace in correctly processed child output. Capture bypasses presentation
redaction and remains exact raw application-owned data.

Each terminal call creates fresh start information and takes the host environment snapshot supplied by
`ProcessStartInfo.Environment` at that time, then applies the immutable ordered edit list. Builder reuse therefore
does not freeze ambient variables across launches. Rafter never mutates the parent environment; authors requiring a
fully reproducible child environment use `Clear()` and explicitly set every required value. Concurrent application
mutation of process-wide environment state remains outside Rafter's guarantees.

Arm external-cancellation and authored-timeout observation before entering synchronous `Process.Start()`. The
authored timeout measures launch plus direct-child execution and stops when natural direct-child exit wins the atomic
lifecycle race; final pipe drainage and teardown use only the internal settlement deadlines. If cancellation or
timeout is observed while `Process.Start()` is blocked, act on it immediately after a successful start. If start
instead returns false or throws before Rafter owns a child, report `ProcessStartException`: there is no owned process
to cancel or time out. After launch, the first committed natural-exit, external-cancellation, or timeout transition
wins, including simultaneous cancellation and timeout.

After the explicit synchronous scope-authority check, the terminal's async core performs specification validation,
pre-cancellation, `Process.Start()`, and observer initialization before its first incomplete await. The terminal call
therefore does not return while start is still pending: it returns only a cancelled/faulted/completed task or a task
whose started child, drains, and exit observer are already owned. Exceptions from validation, start, and observer
initialization are captured by that task rather than thrown from the public call. Do not dispatch start to a worker;
that would permit a timed-out worker to launch a child after ownership had been transferred. The documented .NET 10
caveat is explicit: a genuinely blocked synchronous start can block the terminal call itself until the platform call
returns.

The creating context token and authored timer only signal that arbiter. Do not pass either to the lifetime exit
observer or directly to the pipe reads: those operations must remain observable while the runtime kills, verifies,
drains, and disposes. Cancellation registrations perform no synchronous process operation. The single teardown owner
dispatches tree kill, tolerates an already-exited race when the independent exit observer confirms it, and then
applies the internal verification and forced-close deadlines.

When external cancellation, authored timeout, or callback-scope abandonment wins, perform the tree-kill request and
direct-child verification attempt, then close/cancel both redirected streams immediately and allow the forced-close
drain-settlement deadline. Do not apply the ordinary direct-exit drain-completion window: these outcomes expose no
capture, and already-published safe streaming output is sufficient.

Put BCL launch and lifetime operations behind one narrow internal process-platform adapter that returns an internal
owned handle exposing process ID, redirected streams, exit observation, exit code, tree kill, and disposal. The
production adapter is a transparent `System.Diagnostics.Process` implementation. Synthetic tests inject false or
throwing starts, blocked start/kill calls, controlled exit, retained reads, and disposal observation; real fixture
tests remain mandatory for pipe capacity, OS races, path lookup, and descendant behavior. Do not expose this adapter
or use it to create a second runtime path.

## Fixed invariants

- No shell is involved.
- Every argument is a distinct `ProcessStartInfo.ArgumentList` token.
- stdout and stderr are drained concurrently whenever redirected.
- Waiting for exit never begins as a sequential substitute for draining either stream.
- Capture limits bound retained data, not draining; a full pipe must never be the child's reason for hanging.
- Cancellation and authored timeout use `Kill(entireProcessTree: true)` after winning the coordinated exit race.
- After synchronous `Process.Start()` returns, terminal calls settle every Rafter-owned asynchronous wait within the
  documented internal deadlines; retained descendant pipe handles never turn direct-child exit into apparent success
  or an unbounded wait. The portable .NET 10 start call itself cannot be preempted.
- On .NET 10, Rafter independently confirms termination only for the direct child and reports incomplete teardown if
  that confirmation cannot be obtained. Descendant teardown is attempted and observed where the fixture protocol
  permits it, but remains explicitly best effort across supported operating systems.
- .NET 11 Process APIs are out of scope until .NET 11 is final.

## Implementation checklist

### Public and internal model

- [ ] Consume the Phase 4 path resolver for target-relative process overrides; do not duplicate containment,
      namespace, casing, or command-root policy inside the process runtime.
- [ ] Distinguish bare executable names from path-like names; pass bare names unchanged, normalize relative path-like
      names against the effective process working directory, permit executable paths outside the command root, and
      perform no eager existence or executable-permission probe.
- [ ] Implement the generic process fluent surface demonstrated by `processes.cs`, `environment.cs`,
      `working-directory.cs`, `redaction.cs`, and `process-cancellation.cs`.
- [ ] Normalize executable, argument tokens, valid exits, environment edits, working directory, stream mode,
      capture limit, public timeout, and cancellation policy into an immutable specification.
- [ ] Make every fluent modifier return a new `ProcessBuilder` without mutating its source; keep generic process
      builders reusable rather than consuming them at a terminal operation.
- [ ] Make every `Run()` and `Capture()` invocation launch an independent process with no cached completion or
      output, and support concurrent terminal calls on the same builder safely.
- [ ] Compile and execute the base-builder reuse and defaulted `Option<TimeSpan>` timeout syntax in `processes.cs`.
- [ ] Tie execution authority to the creating callback's process-operation scope and reject a terminal call after
      that scope closes, including while another target in the same invocation is still active.
- [ ] Activate a distinct process-operation scope around each condition, execution, target-cleanup, and
      command-cleanup callback; atomically register terminals, close at that callback's completion, cancel and settle
      the exact active snapshot, and reject process creation or terminal registration outside the captured scope.
- [ ] Settle that process-operation scope before closing its Phase 6 output scope so discarded-operation teardown
      output keeps the owning target or cleanup attribution.
- [ ] Observe completed terminal-task faults without treating a deliberately caught process exception as callback
      failure; preserve an existing callback exception as primary when discarded-operation teardown also fails.
- [ ] Prove a Phase 5 graph-planning diagnostic occurs before any process builder can launch a child process.
- [ ] Allow one working directory, timeout, capture limit, valid-exit declaration, and environment block per process
      specification; preserve first values and accumulate duplicate-setting diagnostics.
- [ ] Validate all accumulated specification diagnostics at terminal `Run()` or `Capture()` and launch nothing on
      failure; keep argument, flag, and option token appenders repeatable.
- [ ] Perform complete specification validation before the pre-cancellation check so malformed builders have one
      deterministic outcome; after a valid specification, pre-cancellation returns a cancelled task and launches
      nothing.
- [ ] Default valid exit codes to `{ 0 }`; make `.ValidExitCodes(params int[] codes)` replace the complete set, require
      at least one code, and normalize duplicates.
- [ ] Implement `.Timeout(TimeSpan)` on the generic builder and reject non-positive or otherwise unsupported values
      before attempting launch.
- [ ] Apply no execution timeout by default; distinguish authored execution timeouts from bounded internal drain,
      forced-kill, and retained-handle deadlines.
- [ ] Represent the four approved production deadlines in one immutable internal policy and inject shorter policies
      plus a controlled `TimeProvider` for deterministic tests; do not expose these safety deadlines or the clock as
      public process-builder settings.
- [ ] Reject option handles whose phase-2 ownership identity does not match the active invocation, resolve accepted
      handles from the invocation snapshot exactly once, and never retain unresolved handles in the runtime
      specification.
- [ ] Validate environment edit keys before launch with the fallback-name rules: reject empty or whitespace-only
      text, NUL, and `=`, preserve authored spelling, and rely on the host operating system's case semantics.
- [ ] Validate empty executable names, capture limits, and working directories before attempting launch.
- [ ] Reject embedded NUL in the executable and every emitted token before launch; validate inactive conditional or
      optional token calls as genuine omissions with no name diagnostic.
- [ ] Reject a present capture-limit policy on streaming `Run()` instead of silently ignoring it.
- [ ] Execute relative and permitted absolute process-working-directory tests from distinct target contexts and prove
      the parent `Environment.CurrentDirectory` never changes.
- [ ] Implement `.CaptureLimitBytes(long)` as a per-stream retained-byte limit from 1 through `int.MaxValue`, measured
      before decoding, so a successful stream remains representable by `ProcessCapture`.
- [ ] Default capture retention to 1 MiB independently for stdout and stderr; do not apply that retention policy to
      streaming `Run()`.
- [ ] Implement public `RafterException` and `ProcessException`, with dedicated `ProcessStartException`,
      `ProcessExitException`, `ProcessTimeoutException`, and `ProcessOutputException` derived types; allow the process
      base to represent otherwise unclassified infrastructure failures without adding a type per internal stage.
- [ ] Give `ProcessOutputException` a stable reason enum covering capture-limit overflow, strict-UTF-8 decoding, and
      retained-pipe termination, together with safe stream/limit metadata where applicable.
- [ ] Preserve original platform failures as `InnerException`, avoid raw captured text in exception messages, and
      represent cancellation with standard `OperationCanceledException` carrying the relevant token.
- [ ] Expose the invalid exit code from `ProcessExitException` and, in capture mode only, the complete capture
      permitted by the phase-6 trust-boundary contract.
- [ ] Keep failure output ownership mode-specific: streaming execution retains no output for exceptions, while
      capture may attach only a complete, bounded, successfully decoded result after an invalid exit.
- [ ] Preserve safe executable/argument diagnostics while redacting sensitive values.
- [ ] Build one immutable streaming redactor from the union of the invocation redactor's patterns and every
      sensitivity-tagged launch value; apply it incrementally before Phase 6 publication without registering literal
      `SetSensitive` values invocation-wide; fail `Run()` before launch with `RAFTER1513` if no marker is safe for the
      complete union, while raw non-presenting `Capture()` does not require a presentation marker.

### Launch and ownership

- [x] Set `UseShellExecute = false` and populate `ArgumentList` token by token.
- [ ] Apply the normalized absolute working directory and environment changes without mutating parent state.
- [ ] Let each terminal call take a fresh inherited environment snapshot when constructing its start information,
      then apply ordered edits; test independent launches and document that ambient parent mutation is caller-owned.
- [ ] Configure redirection consistently for stream and capture modes before start.
- [x] Redirect both stdout and stderr for both modes, never redirect stdin, route streaming output through the Phase 6
      invocation coordinator, and keep raw capture outside presentation normalization.
- [ ] Hold one output admission for each streaming drain until it can publish no more data so invocation sealing
      cannot overtake accepted child output or its final unterminated content.
- [ ] Record ownership of the `Process`, readers/streams, cancellation registrations, timers, and drain tasks.
- [ ] Isolate BCL process creation and lifetime operations behind one narrow internal adapter so synthetic tests can
      control false/throwing/blocked start, exit, kill, streams, and disposal while production retains one transparent
      `System.Diagnostics.Process` path.
- [ ] Keep execution-owned state local to one terminal call so two launches from the same immutable builder cannot
      share process handles, buffers, timers, registrations, or completion state.
- [ ] Handle `Process.Start()` returning false or throwing without starting drains or leaking registrations.
- [ ] Construct the complete start information and arm cancellation/timeout observation before calling synchronous
      `Process.Start()`. The authored timeout begins immediately before that call, but cannot preempt it on .NET 10.
- [ ] Keep start in the terminal async core before its first incomplete await so the public call returns only after
      launch ownership is established or the returned task is terminal; do not dispatch start to unbounded worker
      work that could launch a late child.
- [ ] If cancellation or timeout arrives during `Process.Start()`, record it and act immediately after a successful
      return. If start instead returns false or throws without an owned child, preserve `ProcessStartException` as the
      causal failure rather than fabricating cancellation of a process that never started.
- [ ] After successful start, establish ownership of both drain tasks and the independent exit observer without
      awaiting; only then act on a cancellation or timeout recorded during start. If observer initialization fails,
      enter the same owned teardown path and do not leak the child.
- [ ] Stop the authored timeout when natural direct-child exit wins; use only the internal deadlines for final pipe
      drainage and teardown.
- [ ] Obtain the process ID only after successful start and tolerate rapid natural exit.

### Concurrent stream draining

- [x] Start independent stdout and stderr drains immediately after successful launch.
- [x] Ensure both drain operations are created before any exit wait is awaited.
- [ ] Drain both raw `BaseStream` instances with independent async loops and fixed 16 KiB byte buffers; do not use
      `ReadToEnd`, sequential stream consumption, `BeginOutputReadLine`, or replacement-fallback decoding.
- [ ] Decode incrementally as strict UTF-8 without splitting multibyte characters incorrectly.
- [ ] Preserve a leading UTF-8 BOM as U+FEFF process data and flush each decoder at EOF so an incomplete final
      sequence fails as `InvalidUtf8`.
- [ ] Report invalid UTF-8 as a distinct decoding failure without emitting replacement characters or raw invalid
      bytes; expose no public encoding override in v1.
- [ ] On invalid UTF-8, flush only the preceding valid decoded prefix through the streaming redactor, suppress all
      later presentation for that stream, discard partial capture, and continue draining raw bytes.
- [ ] Preserve stdout/stderr identity and unterminated final content.
- [ ] Route complete and partial lines through target-aware output without merging concurrent streams accidentally.
- [ ] Reuse Phase 6's 65,536-character presentation segmentation for oversized child lines; do not add an unbounded
      process-specific line buffer or alter exact capture data.
- [ ] Preserve bounded capture as exact raw application-owned data, while always redacting streaming presentation,
      diagnostics, exception rendering, and any capture text sent back through Rafter-managed output.
- [ ] Count captured bytes before decoding and enforce the configured limit separately for each stream.
- [ ] Retain capture bytes in fixed-size segments while validating incrementally; materialize UTF-16 strings only
      after complete success, release byte segments promptly, and test the documented allocator-slack and temporary
      string-materialization envelope.
- [ ] On first limit exceedance, record the policy failure and stop retaining additional content for that stream.
- [ ] Continue reading and discarding both streams until normal termination or bounded shutdown.
- [ ] Continue strict decoding into reusable discard storage after capture overflow so a later decoding failure is
      still observed; after decoding itself fails, continue raw draining without presentation or retention.
- [ ] After safe settlement, report a distinct capture-limit failure even when the process exits with a valid code;
      never return normally with silently truncated output.
- [ ] Identify the affected stream and configured byte limit without embedding raw partial output in diagnostics.
- [ ] Do not build an unbounded line buffer; apply the approved Phase 6 presentation segmentation to a single line
      of any length while capture remains governed by its byte limit.

### Exit and retained handles

- [ ] Await direct-process exit independently from EOF on redirected streams.
- [ ] On ordinary exit, allow the documented two-second drain-completion window for final buffered data.
- [ ] Detect when the direct process exited but EOF is withheld by a descendant retaining a pipe handle.
- [ ] After the bounded window, apply the recorded retained-handle decision and settle/cancel drain operations
      without relying on an already-exited direct process to prove descendant termination.
- [ ] Cancel/close both drains on retained-pipe expiry; discard all capture state, including a completed peer stream,
      while leaving already-published streaming output visible.
- [ ] Classify retained-handle termination as `ProcessOutputException` reason `RetainedPipe` without exposing
      implementation details or partial captured output.
- [ ] Never report success before output accepted by the contract is drained or a bounded policy decision is made.

### Cancellation and race handling

- [ ] Return cancellation without launch when the token is already cancelled.
- [ ] Serialize or atomically coordinate start, natural exit, cancellation, timer, and kill decisions.
- [ ] Let external cancellation and authored timeout signal only the lifecycle arbiter; do not cancel the independent
      process-exit observer or pipe drains with those tokens, and do not call synchronous process APIs from a token
      callback.
- [ ] Treat expiration of the authored timeout as a distinct outcome while using the same bounded tree-termination,
      drain-settlement, and resource-cleanup machinery as external cancellation.
- [ ] Make exactly one path responsible for forced tree kill and direct-child verification.
- [ ] Do not call `CloseMainWindow`, emulate console signals, or imply a general graceful protocol in v1.
- [ ] Bound direct-child verification after tree kill to five seconds, independently from the authored execution
      timeout and retained-pipe deadline.
- [ ] Call `Kill(entireProcessTree: true)` defensively and tolerate already-exited races.
- [ ] Dispatch the synchronous tree-kill call through owned teardown work and bound that call itself to two seconds.
      If it remains incomplete, transfer the kill task and process ownership to the reaper, attach an incomplete-
      teardown failure, and make no direct-child or descendant termination claim.
- [ ] Continue settling drains after cancellation so redirected pipes do not strand tasks.
- [ ] After the kill/verification attempt for cancellation, timeout, or scope abandonment, close both redirected
      streams immediately rather than waiting the ordinary direct-exit drain window; preserve already-published safe
      streaming output and expose no capture.
- [ ] After forcibly closing redirected streams, allow two seconds for drain-task settlement; record failure and keep
      every task observed if that safety deadline expires.
- [ ] Preserve external cancellation as cancellation even if teardown produces secondary exceptions.
- [ ] Surface teardown failures when they materially mean the process tree may still be alive.
- [ ] Preserve an external cancellation or authored-timeout primary classification when teardown also fails, and
      retain the teardown failure as its inner exception.
- [ ] Aggregate multiple unexpected teardown failures in the approved lifecycle order, preserve a lone original
      exception directly, and omit expected Rafter-owned drain-cancellation exceptions.

### Disposal and observability

- [ ] Dispose every owned resource exactly once on all terminal paths.
- [ ] Avoid `async void`, unowned or unobserved tasks, and blocking waits on async operations; the bounded exceptional
      handoff to `ProcessOperationReaper` is the only permitted late-operation ownership transfer.
- [ ] Implement one process-wide `ProcessOperationReaper` that strongly owns exceptional late drains or kill calls,
      suppresses output, observes completion and faults, disposes residual resources once, and exposes internal state
      for tests.
- [ ] Add internal lifecycle events or injectable observers sufficient for deterministic race tests.
- [ ] Keep user-visible timing and process IDs out of deterministic snapshots unless explicitly normalized.

## Deterministic process fixture checklist

Build `Sotsera.Rafter.ProcessFixture` as a framework-dependent executable with an apphost on every test platform so
Rafter always launches one executable token rather than `dotnet` plus a DLL. Freeze these verbs and keep parsing
deliberately strict:

| Verb | Purpose |
| --- | --- |
| `inspect` | Write one JSON document containing the exact remaining argument vector and current directory |
| `environment` | Write deterministic JSON for the environment values used by the environment example |
| `working-directory` | Write the normalized current directory for the working-directory example |
| `json` | Write the fixed valid payload consumed by the Phase 8 public extensibility example |
| `emit` | Write configured raw byte payloads to stdout and stderr in deterministic chunk sizes, optionally delay, and exit with a requested code |
| `wait` | Report readiness through the control directory and wait indefinitely for forced termination |
| `spawn-child` | Launch the fixture recursively as a child or grandchild, report every PID out of band, and then wait |
| `retain-pipe` | Launch a descendant that inherits one or both redirected handles, let the direct child exit, and keep the selected handles open |

`emit` accepts mutually exclusive `--stdout`/`--stdout-base64` and `--stderr`/`--stderr-base64` payloads, plus positive
`--repeat` and `--chunk-bytes`, non-negative `--delay-ms`, and any `int` `--exit-code`. Text payloads encode as UTF-8
exactly as supplied and add no newline; base64 payloads write exact decoded bytes. Stdout and stderr writers start
together when both payloads are present. `inspect` treats every token after its verb as opaque data. `spawn-child`
accepts a positive `--depth`; `retain-pipe` requires `--stream stdout|stderr|both`. Lifecycle verbs accept
`--control-directory`; ordinary data verbs do not require it. Reject unknown, duplicate, malformed, or conflicting
fixture options with a fixed nonzero fixture exit before performing the requested behavior.

Use explicit invariant-culture numeric options and base64 payload options rather than locale-sensitive text parsing or
shell quoting. Binary payloads go directly through `Console.OpenStandardOutput()` and
`Console.OpenStandardError()`; the fixture does not use its own asynchronous line redirection. A test-provided
control directory, required by lifecycle tests but optional for ordinary example verbs, contains one
atomically created metadata file per process, keyed by PID, with parent PID, verb, and readiness state. PID metadata
and retained-handle coordination never use stdout or stderr. The harness
opens or otherwise tracks every reported process independently, treats OS-confirmed process exit as the termination
evidence, and deletes the isolated control directory only after cleanup. A killed process is not expected to execute
an exit hook or write a misleading "terminated" sentinel.

- [ ] Emit configurable stdout and stderr bytes/lines independently and simultaneously.
- [ ] Emit more data than typical Windows and Unix pipe capacities.
- [ ] Emit partial writes, multibyte characters, very long lines, and no-final-newline output.
- [ ] Exit with a requested code and optional delay.
- [ ] Report working directory, arguments, and selected environment values as structured fixture data.
- [ ] Wait indefinitely until Rafter terminates the fixture process.
- [ ] Spawn a child/grandchild and report their PIDs through a safe fixture channel.
- [ ] Exit while a descendant deliberately retains stdout or stderr.
- [ ] Report every direct-child and descendant PID through per-process control metadata so the harness can verify
      termination independently of redirected output or child exit hooks.

## Required verification

- [ ] Exercise stdout-only, stderr-only, and simultaneous high-volume output repeatedly.
- [ ] Prove the historical sequential-read deadlock pattern completes under the Rafter runtime.
- [ ] Test retained data below, exactly at, and above both stream limits.
- [ ] Prove limit exceedance still drains enough for the fixture to reach and record natural exit.
- [ ] Test valid UTF-8 split at every byte boundary, a leading BOM, incomplete final sequences, and malformed bytes
      before and after capture overflow on each stream; assert the approved failure precedence and no partial capture.
- [ ] Test zero, nonzero, explicitly valid nonzero, and rapidly exiting processes.
- [ ] Test executable and argument values containing spaces, quotes, empty strings, and shell metacharacters.
- [ ] Test every process diagnostic code, duplicate and within-call ordering, inactive optional/conditional omissions,
      capture-limit rejection in `Run()`, validation-before-pre-cancellation, and proof that no invalid specification
      launches the fixture.
- [ ] Test environment add/replace/remove and target/process working-directory precedence.
- [ ] Test bare, relative path-like, parent-relative, and absolute executables; working-directory containment;
      missing/inaccessible launch paths; and the no-eager-probe boundary.
- [ ] Test minimum, maximum, out-of-range, absent, and defaulted timeouts, including a natural exit racing cancellation
      and timeout and a synthetic synchronous start stall.
- [ ] Test every cancellation race and retained-descendant scenario under strict test deadlines.
- [ ] Test direct returns, `Task.WhenAll`, completed discarded tasks, active discarded tasks, registration racing scope
      closure, callback failure plus active-process teardown, process use from every callback kind, a false condition
      with a discarded active process, and a builder carried from one callback scope into another.
- [ ] Launch many redirected processes concurrently to expose thread-pool, pipe, and disposal issues.
- [ ] Inject Phase 6 renderer and sink failures during streaming; prove both pipes still drain, later presentation is
      suppressed, the process terminal result remains independently classified, and the command reports output
      infrastructure failure.
- [ ] Use controllable synthetic late drain and late kill operations to prove bounded terminal return, reaper
      ownership, fault observation, and eventual exactly-once disposal.
- [ ] Scan streamed output, diagnostics, safe exception messages, and command rendering for sensitive option values
      and process-local `SetSensitive` literals; separately prove raw capture and platform inner exceptions remain
      application-owned.
- [ ] After every test, verify direct-child termination and record observed descendant teardown separately; clean up
      fixture PIDs independently so the documented best-effort descendant policy or a failed assertion cannot pollute
      later tests.

## Repository verification record

Run this baseline from the repository root:

```text
dotnet restore Rafter.slnx --configfile nuget.config
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --no-restore
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build --no-restore
pwsh ./eng/verify-package.ps1 -Configuration Release
dotnet build examples/processes.cs --configuration Release
dotnet build examples/environment.cs --configuration Release
dotnet build examples/working-directory.cs --configuration Release
dotnet build examples/redaction.cs --configuration Release
dotnet build examples/process-cancellation.cs --configuration Release
```

The Phase 7 evidence document must record these results, the Windows/Ubuntu/macOS CI matrix, package-integrity result,
public API diff, process state-machine and diagnostic tables, fixture protocol, race/teardown matrix, measured memory
envelope, and the exact generic process examples compiled. It must explicitly retain `extensibility.cs` and typed-tool
examples as Phase 8 work rather than weakening Phase 7's generic runtime gate.

## Completion gates

- [x] **R1 — Argument safety:** hostile-token fixtures receive the exact authored argument vector without shell
      interpretation.
- [ ] **R2 — Deadlock resistance:** simultaneous output beyond both pipe capacities completes on all supported OSes.
- [ ] **R3 — Bounded capture:** retained input bytes follow per-stream limits, managed-memory overhead matches the
      documented segmented/materialization envelope, and fixtures prove pipes continue draining.
- [ ] **R4 — Retained-handle bound:** descendant-held pipes fail within the two-second direct-exit drain deadline.
- [ ] **R5 — Cancellation bound:** direct-child and process-tree scenarios meet the two-second tree-kill-request,
      five-second kill-verification, and two-second forced-close-drain deadlines for cancellation and authored
      timeout.
- [ ] **R6 — Race determinism:** repeated start/exit/cancel races produce only documented outcomes and no unobserved
      exceptions.
- [ ] **R7 — Resource closure:** real-process stress tests confirm every direct child and ordinary drain has settled;
      after independently cleaning best-effort descendants, no fixture process, reaper entry, registration, or
      undisposed process resource remains detectable by the harness. The synthetic late-operation tests prove
      tracked handoff and eventual reaper cleanup.
- [ ] **R8 — Cross-platform contract:** platform-specific differences are documented and CI-tested, not retry-hidden.
- [ ] **R9 — Evidence recorded:** process state machine, timeout values, fixture protocol, and matrix results are
      committed with the phase completion record.
- [ ] **R10 — Retained-handle decision:** retained pipes fail within the documented deadline with no partial capture;
      direct-child termination is verified and best-effort descendant results are recorded per platform.
- [x] **R11 — Repository quality:** formatting, analyzer-clean Release build, all tests, package creation and integrity,
      public API checks, the five Phase 7 generic process examples, and the supported-OS CI matrix pass with committed
      evidence.

## Non-goals

No shell command strings, stdin API, arbitrary process pipelines, detached/fire-and-forget processes, terminal
emulation, pseudo-terminal support, raw handles, native suspended creation, Windows job-object ownership, Unix
session/process-group ownership, or public `System.Diagnostics.Process` escape hatch is included.
