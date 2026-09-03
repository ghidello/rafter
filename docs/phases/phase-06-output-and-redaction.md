# Phase 6: output, console attribution, and redaction

## Objective

Provide a semantic output pipeline with Spectre.Console presentation, concurrent target attribution, and one
redaction boundary covering every supported output and failure channel.

Phase 6 consumes the immutable Phase 5 plan order, lifecycle transitions, terminal outcomes, blockers, original
exceptions, and cleanup failures. It may render those facts but does not redefine scheduling, cancellation, or cleanup
semantics.

## Questions resolved before implementation

- [x] **Who owns process-wide console interception when different commands overlap?** Use one shared, reference-counted
      coordinator and one installed writer pair for the process. Each invocation registers its own sinks and carries
      invocation and target identity through execution context. Do not reject the different-command concurrency
      already supported by Phase 5 or let each command replace the global writers independently.
- [x] **Where does interception begin and end?** A normal invocation joins the coordinator after model and graph
      validation and before binding. It leaves only after target and command cleanup, final presentation, buffered
      output settlement, ownership verification, and output-failure capture. Exact help and failures before this
      boundary do not install interception, but an already-installed shared coordinator still serializes their direct
      presentation against another invocation's live display.
- [x] **How is output produced while binding still constructs the immutable redactor handled?** Quarantine intercepted
      binding and validation console text independently per stream. Release it only after successful binding produces
      a usable immutable redactor; discard it on every binding failure or redaction uncertainty. The quarantine has a
      fixed internal limit and exceeding it fails closed as output infrastructure failure.
- [x] **How does Phase 6 observe live Phase 5 lifecycle state?** Add an internal thread-safe execution observer to the
      runtime scope. Emit transitions when scheduler state changes, including `CleaningUp` before cleanup invocation;
      observer failure is captured as output infrastructure failure and never throws back into or changes scheduling.
- [x] **What data does the execution observer receive?** Each immutable notification carries target identity and plan
      index, lifecycle, and terminal outcome/shape/direct blockers only when `Settled`. Emit initial `Pending`
      notifications in plan order before admission, then notify at each actual transition after its state mutation.
      Disable the observer after its first failure while scheduling continues unchanged.
- [x] **Are synchronous output calls queued in the background?** No. Event creation, property snapshotting, sequence
      assignment, redaction, and serialized publication occur at the call boundary. There is no unbounded background
      queue. Sink failure is recorded once as invocation infrastructure failure, suppresses later managed output, and
      does not rewrite target callback outcomes or stop otherwise eligible graph work.
- [x] **How do synchronous semantic calls coexist with asynchronous `TextWriter` APIs?** Spectre presentation and the
      approved semantic API use one synchronous terminal-publication primitive under the coordinator lock. Async
      writer overloads validate cancellation, enter the same primitive synchronously, and return an already-completed
      task/value task; no code blocks on a task or holds a monitor across an `await`.
- [x] **How long may a context-owned output facade be used?** It is invocation-owned and accepts calls only until the
      managed-input seal immediately after command cleanup. A live call uses the current target or command fallback;
      a later call throws `InvalidOperationException` and emits nothing. Retaining a context never extends Rafter's
      input or interception lifetime.
- [x] **Can sealing race a synchronous output call that formats outside the coordinator?** Every call first acquires an
      invocation-local admission. Sealing atomically rejects later calls and asynchronously waits for already-admitted
      calls to publish or fail before finalizing redaction buffers; it never blocks on a task or holds the coordinator
      while waiting. The facade is safe for concurrent callers.
- [x] **How are partial lines and chunk-split secrets bounded?** Use one serialized streaming-redaction pipeline per
      invocation stream, retaining only the unresolved suffix required by the longest registered pattern. Redacted
      partial-line presentation buffers have a fixed internal segment limit and flush attributed continuation events
      on a scope change, limit, or same-stream ordering barrier; output finalization flushes every final fragment
      before the summary.
- [x] **What terminal capabilities drive presentation?** Capture an injectable profile for stdout and stderr
      independently: redirection, ANSI/color, Unicode, cursor control, and width. `--plain` overrides every capability;
      non-empty `NO_COLOR` disables only color. Live presentation requires cursor support and is coordinated across
      both physical streams so stderr or direct console output cannot corrupt it.
- [x] **What happens when cosmetic capability detection is unavailable?** Writer capture failure is infrastructure
      failure. An individual capability-probe failure downgrades that stream to plain; a failed `NO_COLOR` lookup
      disables color. Unknown, less-than-20, or greater-than-4,096 widths select plain. Cosmetic uncertainty never
      guesses rich behavior or fails otherwise valid work.
- [x] **What is the exact initial public surface?** Expose one context-owned `RafterOutput` with synchronous `Line`,
      `Success`, `Warning`, `Error`, and `Property` methods. Freeze signatures and argument validation before adding
      implementation types; user callbacks never receive Spectre.Console objects.
- [x] **Is captured output application-owned or automatically redacted?** Future `ProcessCapture` values contain exact
      raw program data so structured formats and other exact consumers are not corrupted. The same rule applies to the
      complete capture attached to a future invalid-exit exception.
- [x] **Where is the capture trust boundary?** Rafter never presents future raw capture automatically. Text is redacted
      whenever it crosses back into semantic output, intercepted console, diagnostics, exception presentation,
      command rendering, or a future persistent-output channel. Direct application use is caller-owned.
- [x] **Are separate raw and redacted capture buffers required?** No persistent redacted duplicate is retained solely
      for presentation. Future process capture retains only bounded raw result data; managed sinks redact on re-entry,
      while future streaming child output is redacted before presentation.
- [x] **Who owns captured data after return?** Rafter releases execution-only buffers when the terminal operation
      settles. The application controls the lifetime and disclosure of future raw `ProcessCapture` strings.
- [x] **How does output infrastructure failure combine with the Phase 5 outcome?** Preserve the complete Phase 5
      outcome and attach the first output infrastructure exception separately. Infrastructure exit code `1` wins over
      cancellation `130`; target and cleanup failures remain available for the final best-effort diagnostic.
- [x] **When is the visible rendering grammar frozen?** Before renderer implementation, approve golden rich and plain
      documents for `presentation.cs`, every terminal target state, concurrent attribution, and failure plus secondary
      cleanup. Renderers may be implemented only against those committed fixtures.

**Gate O0 — Initial behavioral contracts reconciled:** every question above is reflected in the fixed decisions,
public API, implementation checklist, verification matrix, completion gates, and Phase 7/8 handoff. Exact visible
grammar is deliberately frozen in the presentation contract gate before renderer implementation begins.

## Fixed decisions

### Process-wide ownership and invocation boundary

One process-wide coordinator owns console interception. The first participating normal invocation captures the true
host writers and installs exactly one coordinating stdout/stderr pair; later invocations register with that pair, and
the final registration restores the exact captured writers. `InvocationServices.Capture` must recognize an active
coordinator and recover the host sinks and capabilities rather than treating Rafter's wrapper as a new host writer.
Injected invocation services remain independently routable in tests.

An `AsyncLocal`-backed immutable scope identifies the active invocation and optional target. It flows through ordinary
`await`, `Task.Run`, and `ConfigureAwait(false)`. Closing a target scope changes stale writes to command attribution
while that invocation remains active. Intercepted console output from a stale scope used after its invocation closes
receives pass-through, application-owned behavior with no attribution or redaction promise. The same pass-through
rule begins when the invocation seals managed input after command cleanup. An explicit call through that invocation's
`RafterOutput` after the seal instead throws `InvalidOperationException` and emits nothing. A write with no Rafter
scope is likewise host output, but the coordinator serializes it and suspends any live display before passing it
through.

The normal pipeline is model validation, exact-help handling, initial cancellation, graph planning, coordinator
registration, binding quarantine, path initialization, graph execution, command cleanup, managed-input sealing,
admitted-call draining, output finalization, final summary, ownership verification, and coordinator unregistration.
Each semantic call takes an invocation-local admission before invoking caller-owned formatting. Sealing atomically
stops later admission and asynchronously waits for admitted calls without holding the coordinator; only then are the
redactor and line buffers completed. Later stale console writes become serialized host pass-through and later explicit
output calls fail. Successful binding installs its immutable redactor before quarantined output is released. Every
binding failure discards quarantined text. Exact help and earlier failures do not register a new interception scope;
when another invocation already owns the coordinator, one-shot presentation still uses its serialized renderer bypass.

Replacing either global writer while the coordinator is installed is unsupported. At callback and presentation
boundaries, loss of ownership marks every active invocation as an infrastructure failure for the affected stream and
reinstalls Rafter's coordinating writer without reading or chaining the replacement. Only the final registration
restores the true entry writers. A temporary replace-and-restore between checks remains explicitly unsupported and
undetectable.

### Ordering, delivery, and failure

Every semantic call, lifecycle transition, and intercepted console write receives a process-monotonic sequence while
holding the coordinator boundary. Each invocation preserves that order independently within stdout and stderr. No
ordering is promised after independently redirected streams are recombined, but a shared interactive terminal uses
one serialization boundary and suspends/resumes its live display around every physical write.

Argument validation, collection enumeration, scalar formatting, and immutable event construction occur before the
coordinator is entered. No caller-owned code runs under the process-wide lock. The boundary covers only sequence
assignment, bounded redaction/buffering, rendering of already-safe immutable data, and the physical sink call.

Spectre rendering and all coordinating writer overloads converge on one synchronous terminal-publication primitive.
Synchronous calls perform the physical terminal write before returning. `WriteAsync` overloads check their cancellation
token before publication, use the same primitive, and return a completed task or value task; they never call `.Wait()`,
`.Result`, or `GetAwaiter().GetResult()`, and no coordinator monitor is held across an `await`. This is the deliberate
terminal-I/O exception to the repository's asynchronous-I/O preference required by the approved synchronous public
API and Spectre's synchronous console abstraction.

Semantic methods return after their immutable event has been synchronously validated, redacted, and published. They
do not create a background queue. Property enumeration and scalar formatting happen before publication; their
original exceptions escape the semantic call and remain ordinary target callback failures. A renderer or sink failure
is instead recorded once as output infrastructure failure, suppresses later managed presentation for that invocation,
and is not thrown through `Output` or coordinating `TextWriter` APIs. Later semantic calls still perform argument
validation, property enumeration, and scalar formatting before publication is suppressed, so an earlier sink failure
cannot hide a later caller-owned exception or alter callback behavior. Graph execution and qualified cleanup continue
under Phase 5 rules. The complete execution and cleanup outcome is retained, while the first output infrastructure
exception becomes the invocation's primary infrastructure fact. Exit code `1` therefore wins over cancellation code
`130`; ordinary target and cleanup failures remain available to the guarded final diagnostic rather than being
discarded. Final diagnostics use one best-effort write to the true captured error sink and never recurse through the
failed pipeline.

### Streaming redaction and bounded buffering

The binding quarantine retains at most 1,048,576 characters per stream per invocation. Overflow discards the complete
quarantine and records output infrastructure failure. After binding, one incremental redaction state per invocation
stream retains at most the longest registered pattern minus one unresolved raw character. Confirmed raw match text is
discarded immediately: a finite active-redaction state records the replacement marker and furthest covered position,
so a chain of overlapping matches can coalesce without retaining the chain's raw characters. The state applies the
Phase 3 ordinal match, interval-union, and safe-marker policy incrementally rather than inventing different matching
semantics. Its raw retention is bounded by the longest pattern even for an arbitrarily long overlap chain.

Only redacted text enters line presentation buffers. A buffer holds at most 65,536 characters; newline, attribution
change, scope closure, limit, or later non-console event on the same physical stream flushes an attributed fragment;
host pass-through first flushes all earlier managed fragments on that physical stream. A limit or ordering-barrier
split uses explicit continuation notation so it cannot masquerade as a completed source line. Tagged pending spans
preserve their originating scope. A replacement covering more than one tagged span is emitted once under the scope of
its first contributing character, and later contributing spans emit no duplicate marker. A secret crossing writes,
newlines, event kinds, or scope boundaries is replaced before any contributing fragment is emitted. Invocation
finalization completes the redactor, flushes unterminated fragments, and settles physical writes before the final
summary. If safe redaction or final verification cannot be proven, the affected managed output is discarded and the
invocation fails closed.

Line decoding recognizes a split or contiguous CRLF as one boundary and lone CR or LF as one boundary. Managed
renderers write the newline captured in the destination profile; deterministic fixtures inject LF. Host pass-through
preserves the caller's characters unchanged. The coordinating writers preserve `TextWriter.NewLine`: the process-wide
value begins from each true host writer, its getter and setter remain serialized process-global operations, and the
final value is applied to the restored writer. Setting it validates with the underlying `TextWriter` contract and a
host-writer failure propagates to that caller as ordinary host I/O while marking active users of the failed sink.

### Presentation profiles and live state

Stdout and stderr each receive an injectable immutable capability profile. Plain mode is selected for `--plain`,
redirection, or a stream without the required static-layout capability. A capable non-plain stream uses rich static
layout; color additionally requires color capability and an empty or absent `NO_COLOR`. Rich live state is enabled
only when stdout has cursor control and both streams can be coordinated safely. `NO_COLOR` never disables Unicode,
layout, or cursor control, and whitespace is a non-empty value. `FORCE_COLOR` is ignored in v1.

Production capture treats failure of the writer itself as infrastructure failure. Each cosmetic capability is probed
independently; a failed probe makes only that stream plain, and a failed `NO_COLOR` lookup disables color. Width is an
integer from 20 through 4,096 inclusive; unknown or out-of-range width makes the stream plain. Tests inject complete
profiles directly and never depend on the host running the test process.

A rich profile without Unicode retains rich layout and any permitted color but substitutes the same stable ASCII
state labels used by plain mode for Unicode symbols. A Unicode-capable colorless profile retains the symbols and omits
only color. Cursor capability never implies Unicode or color capability.

An exact `--plain` token is recognized before any report selection, including model, graph, and initial-cancellation
failures that occur before ordinary parsing or binding. Malformed and duplicate common-option behavior remains the
Phase 3 contract; this early observation selects presentation only and never changes input validation.

The Phase 5 runtime accepts a non-throwing thread-safe observer and reports actual lifecycle transitions at the point
they occur. `Pending` and `Ready` both present as `Waiting`; `Running` and `CleaningUp` retain distinct live states.
Its immutable notification contains target identity and plan index, lifecycle, and terminal outcome, successful shape,
and direct blockers only for `Settled`. The runtime emits every initial `Pending` notification in stable plan order
before admitting work and emits later notifications immediately after mutating scheduler state. The guarded observer
disables itself after its first exception. It does not fabricate transitions from the final outcome and cannot affect
permit ownership, cancellation, cleanup, or settlement. Plain and static-rich profiles emit no transient lines and
consume only the final outcome.
Phase 6 replaces Phase 5's temporary minimal execution report after the execution outcome is produced; it does not
emit both reports or alter the Phase 5 outcome used to select the command status.

## Output model

Semantic events are immutable data carrying target/command scope, severity or kind, safe text, optional recovery or
property information, and ordering metadata. Renderers consume events; user callbacks do not manipulate Spectre
objects directly through the initial API.

## Planned public API

```csharp
public sealed class RafterContext
{
    public RafterOutput Output { get; }
}

public sealed class RafterOutput
{
    public void Line(string text);

    public void Success(string text);

    public void Warning(string text);

    public void Error(string text, string? recovery = null);

    public void Property(string name, object? value);
}
```

All methods are synchronous and return `void`. Text arguments reject `null`; `Line` accepts an empty string as an
intentional blank line, while success, warning, error, property name, and non-null recovery reject empty or whitespace
text. Property names and recovery text reject embedded newlines. These failures occur synchronously inside the caller's
callback. The context returns the same facade instance on every access. The runtime seals it atomically after command
cleanup and asynchronously drains calls admitted before the seal prior to output finalization. Every call starting
after the seal throws `InvalidOperationException` before formatting, redaction, or publication. Concurrent calls are
supported.

`Property` treats `null` and `string` as scalar shapes. Any other synchronous non-dictionary `IEnumerable` is
enumerated exactly once and snapshotted, stopping and throwing `ArgumentException` for `value` before accepting a
1,025th item or more than 1,048,576 formatted UTF-16 characters in the complete property. This bounds infinite and
pathological sequences without enumerating beyond the rejected item. Multidimensional arrays, dictionaries,
`IAsyncEnumerable<T>`, nested enumerables, and structured key/value elements are rejected with guidance to format them
in application code. Null, string, and non-enumerable scalar elements are supported. Scalars are converted immediately
with `IFormattable` and invariant culture when available, otherwise `ToString()`; a null formatting result is an error.
The snapshot retains formatted immutable text rather than caller-owned objects or collections. The size limit includes
the property name and the canonical plain JSON-style literal representation: delimiters, escaping, and formatted
element text, but not renderer-added target attribution. Rich rendering consumes the same snapshot and does not
recompute the budget.

## Presentation contract gate

Before implementing either renderer, commit and approve golden documents for the representative calls in
`presentation.cs`; command- and target-scoped line, warning, error, recovery, success, and property events; concurrent
target attribution; every final target shape; cancellation; primary failure; and secondary cleanup failure. Each
fixture includes an independently captured stdout and stderr document for forced plain, static rich with and without
color/Unicode, and live-capable profiles. The fixture defines exact prefixes, indentation, quoting, collection layout,
continuation notation, headings, symbols, and blank lines. Width-dependent rich cases state their injected width;
plain cases are width-independent. Renderer work does not begin until this grammar is frozen, so implementation does
not make unreviewed user-experience choices.

## Implementation checklist

### Semantic events and sinks

- [ ] Freeze and approve the presentation-contract fixtures before implementing renderers or event formatting.
- [ ] Add the exact `RafterContext.Output` and `RafterOutput` public surface, XML documentation, public API baseline,
      null/whitespace validation, and API-shape tests.
- [ ] Return one allocation-free facade per context; accept calls until the managed-input seal, fall stale target
      identity back to command scope while input remains open, and reject every call after sealing before doing user
      formatting.
- [ ] Guard the thread-safe facade with invocation-local call admission; atomically seal it after command cleanup and
      asynchronously drain admitted calls without holding the coordinator or blocking on a task.
- [ ] Implement line, success, warning, error with optional recovery, and named property events.
- [ ] Implement `Output.Property(string name, object? value)` without generic-overload ambiguity for `null`.
- [ ] Snapshot property input synchronously into immutable null, empty-string, scalar, multiline-string, or
      one-dimensional ordered-collection semantic data; special-case `string` as a scalar and enumerate every other
      supplied collection exactly once.
- [ ] Complete caller-owned validation, enumeration, and formatting before taking the coordinator lock; never invoke
      caller code while process-wide output is serialized.
- [ ] Reject dictionaries, multidimensional arrays, asynchronous collections, nested enumerables, and structured
      key/value elements with clear guidance to format them in application code; preserve an enumeration or
      scalar-formatting exception as the target callback failure.
- [ ] Bound a property snapshot to 1,024 collection items and 1,048,576 formatted UTF-16 characters; stop before
      reading the rejected item's value and throw `ArgumentException` for `value` at either limit.
- [ ] Format scalar values invariantly. In rich mode render `<null>`, `""`, indented multiline text, and readable
      inline or vertical collections; in plain mode render one physical `name=value` line with JSON-style null,
      quoting, collection delimiters, and newline escaping.
- [ ] Allocate one process-monotonic ordering key while holding the shared output boundary.
- [ ] Separate event creation, redaction, buffering/routing, and rendering.
- [ ] Publish synchronously without an unbounded background queue; distinguish property/event-construction exceptions
      from renderer or sink infrastructure failure.
- [ ] Converge synchronous semantic calls and every coordinating `TextWriter` overload on one serialized synchronous
      terminal primitive; honor async-overload cancellation before entry and return completed tasks without blocking
      on tasks or holding a monitor across `await`.
- [ ] Serialize writes through the shared process coordinator so concurrent targets, commands, host writes, and
      stderr cannot corrupt terminal control sequences or a live display.
- [ ] Make sinks injectable for deterministic tests.

### Presentation

- [ ] Extend the Phase 3 Spectre.Console report renderer for general semantic events in an interactive capable
      terminal; reuse rather than replace its help and diagnostic document models.
- [ ] Extend the Phase 3 deterministic plain renderer without introducing ANSI sequences for redirected or
      explicitly plain output.
- [ ] Replace, rather than supplement, Phase 5's temporary minimal execution report after an execution outcome exists.
- [ ] Make the common `--plain` option force both stdout and stderr into deterministic width-independent output with
      no ANSI, cursor/live control, or Unicode-only status glyphs.
- [ ] Observe exact `--plain` before selecting any early report, including invalid-model, graph, and initial
      cancellation paths, without bypassing Phase 3 duplicate or malformed-token diagnostics.
- [ ] Without `--plain`, detect stdout and stderr capabilities independently and use plain output for each redirected
      or incapable stream even when the other stream remains rich.
- [ ] Replace the two ANSI Booleans with independently injectable immutable stream profiles covering redirection,
      static layout, ANSI/color, Unicode, cursor control, and width; read `NO_COLOR` exactly once per invocation.
- [ ] Treat writer-capture failure as infrastructure failure, but downgrade only the affected stream to plain after a
      cosmetic probe failure, disable color after `NO_COLOR` lookup failure, and reject unknown or out-of-range width.
- [ ] Route line, success, property, ordinary lifecycle/progress, successful help, managed stdout, and child stdout
      to stdout.
- [ ] Route warning, error with recovery, command diagnostics, failure/cancellation summaries, usage caused by
      invalid input, managed stderr, and child stderr to stderr.
- [ ] Keep `Output.Error(...)` as presentation only; require actual callback failure or cancellation to make a target
      unsuccessful.
- [ ] Preserve event order within each physical stream and document that independently redirected stdout/stderr
      cannot provide a cross-stream ordering guarantee.
- [ ] Preserve readable target attribution under concurrency without promising impossible cross-process ordering.
- [ ] Present implicit no-op targets as completed with no work, distinctly from aggregates and condition-skipped
      targets.
- [ ] Render final target rows in stable plan order with `Succeeded`, `Skipped`, `Failed`, `Cancelled`, and `Blocked`
      terminal states; render successful callback-free targets as `Aggregate` or `No work` and list direct blockers
      in authored dependency order.
- [ ] Let rich interactive output show transient `Waiting`, `Running`, and `Cleaning up` states with a live display;
      emit no synthetic transient-state lines in plain mode.
- [ ] Add a thread-safe non-throwing Phase 5 execution observer, map `Pending` and `Ready` to `Waiting`, and prove that
      emitted state reflects actual transition timing without changing scheduler behavior.
- [ ] Emit initial `Pending` notifications in plan order and immutable notifications after every state mutation; add
      terminal outcome, successful shape, and direct blockers only to `Settled`, then disable observation after its
      first exception.
- [ ] Emit the final summary to stdout when the command succeeds and to stderr with detailed failures when it fails
      or is cancelled; omit durations from v1 output.
- [ ] Render secondary target- and command-cleanup failures from the Phase 5 structured outcome beneath a distinct
      `Cleanup also failed` heading without changing the selected primary failure or exit code.
- [ ] Use the rich symbol/color map: `○` waiting/dim grey, `●` running/cyan, `◐` cleanup/cyan, `✓`
      succeeded/green, `◇` aggregate/green, `–` no-work/grey, `↷` skipped/grey, `■` cancelled/yellow,
      `⊘` blocked/yellow, and `✗` failed/red. Keep symbols under `NO_COLOR`, reserve red for actual failures, and
      use full stable ASCII labels under `--plain`.
- [ ] Keep diagnostics useful when color, Unicode, or cursor control is unavailable.
- [ ] In rich layout without Unicode, substitute the stable plain ASCII state labels while independently retaining
      permitted color; never infer Unicode or color from cursor support.
- [ ] Treat a non-empty `NO_COLOR` as disabling color only, retaining supported static rich layout and Unicode; treat
      an empty value as unset. Do not support `FORCE_COLOR` in v1, and never let an enabling environment convention
      override redirection.

### Managed console attribution

- [ ] Implement one shared reference-counted console coordinator and install only its single reversible `Console.Out`
      and `Console.Error` writer pair.
- [ ] Preserve the true host writers, expose them to later `InvocationServices.Capture` calls while interception is
      active, and restore them exactly once after the final participating invocation.
- [ ] Register each normal invocation after graph planning and before binding; quarantine binding output and leave only
      after final output, reporting, ownership verification, and cleanup settle.
- [ ] Associate writes with the active target through execution context that flows across ordinary `await`,
      `Task.Run`, and `ConfigureAwait(false)`.
- [ ] Attribute writes during a target's conditions, execution, and cleanup to that target; attribute command cleanup
      and invocation work without an active target, including binding and validation, to the command.
- [ ] Mark target attribution scopes closed when their callback settles; if unawaited work writes before the
      managed-input seal, route it to the command rather than retaining stale target attribution.
- [ ] Intercept only between successful writer installation inside `RunAsync` and exact restoration. Leave writes
      before installation and after restoration untouched by Rafter.
- [ ] Serialize one-shot help and early-failure presentation through an already-active coordinator without registering
      those invocations for interception.
- [ ] Document that callbacks must await spawned work: stale work falls back to command attribution only until the
      managed-input seal and is application-owned afterward, even before `RunAsync` returns.
- [ ] Buffer partial writes until newline, scope completion, the segment limit, or a same-stream ordering barrier
      without combining different targets.
- [ ] Before a same-stream semantic/lifecycle event or host pass-through overtakes a partial console write, flush the
      earlier redacted fragment with continuation notation; retain the first contributing scope for a cross-span
      replacement marker.
- [ ] Support `Write`, `WriteLine`, asynchronous writer APIs, and concurrent writes.
- [ ] Recognize split/contiguous CRLF and lone CR/LF, render through the destination profile's newline, and preserve
      serialized process-global `TextWriter.NewLine` getter/setter behavior through final writer restoration.
- [ ] Prevent recursive routing when Rafter's renderer writes to the underlying console.

### Redaction

- [ ] Consume the invocation-scoped immutable redaction registry populated during Phase 3 binding.
- [ ] Keep manual sensitive-value registration outside v1; do not mutate the registry from target contexts because
      target-time registration cannot protect earlier or concurrent output.
- [ ] Reuse the Phase 3 exact ordinal, duplicate, overlap, substring, Unicode, multiline, marker-selection,
      verification, and fail-closed text-redaction contract without introducing another matching algorithm.
- [ ] Apply the recorded raw-capture trust model; in every case, redact before text reaches semantic sinks, original
      console writers, terminal renderers, diagnostics, exception rendering, command previews, or persistent
      artifacts.
- [ ] Quarantine at most 1,048,576 binding characters per stream, release only after successful binding freezes a
      usable redactor, and discard the complete quarantine on binding failure, overflow, or redaction uncertainty.
- [ ] Implement incremental redaction across write, newline, attribution, and scope boundaries while retaining only
      the unresolved maximum-pattern suffix plus finite non-raw active-match metadata; never retain a confirmed raw
      overlap chain.
- [ ] Bound redacted partial-line segments at 65,536 characters and flush an explicit attributed continuation without
      retaining unbounded raw or redacted lines.
- [ ] Never retain an unredacted duplicate solely for later presentation; bounded raw capture exists only for the
      application-owned result.
- [ ] Preserve Phase 3 replacement-marker safety and fail-closed verification across every streaming boundary.

### Failure resilience

- [ ] Restore console writers even if rendering, user callbacks, or cleanup throws.
- [ ] Capture the exact entry `Console.Out` and `Console.Error` instances, verify Rafter still owns both globals at
      callback and presentation boundaries, and treat replacement of either as a command infrastructure failure.
- [ ] Never adopt, chain, inspect, or emit through an application replacement. In `finally`, restore both exact entry
      writers even when one or both were replaced during the invocation.
- [ ] When ownership is lost, mark every active invocation failed for the affected stream and reinstall the shared
      coordinating writer immediately so one command cannot silently disable protection for another.
- [ ] Report only which stream lost ownership and document that temporary replace-and-restore mutations may be
      undetectable and are outside Rafter's supported attribution/redaction boundary.
- [ ] Prevent output sink failure from causing recursive diagnostics.
- [ ] Record renderer and sink failure once, suppress later managed output, continue Phase 5 graph and cleanup
      settlement, return infrastructure exit `1`, and attempt only one guarded diagnostic through the true error sink.
- [ ] Continue validating arguments, enumerating properties, and formatting scalars after publication is suppressed so
      output infrastructure failure cannot hide a later caller-owned exception.
- [ ] Retain the complete Phase 5 outcome beside the first output infrastructure exception and make exit `1` take
      precedence over cancellation `130` without erasing target or cleanup failures.
- [ ] Keep cancellation from truncating buffered final lines without redaction.

## Required verification

- [ ] Approve the golden stdout/stderr presentation grammar before renderer implementation and fail snapshots on any
      unreviewed prefix, indentation, quoting, symbol, continuation, heading, or blank-line change.
- [ ] Verify the exact public API surface, XML documentation, argument validation, public API baseline, and
      allocation-free access to the context-owned output facade.
- [ ] Retain a context and output facade across target closure, managed-input sealing, and invocation settlement;
      verify command fallback before the seal, synchronous `InvalidOperationException` afterward, no formatting side
      effects, and no emitted text.
- [ ] Race many concurrent facade calls against sealing; prove admitted calls finish before finalization, later calls
      fail before formatting, the seal waits asynchronously without the coordinator, and no event appears after the
      final flush.
- [ ] Snapshot every semantic event in rich-capability and plain modes.
- [ ] Assert the complete semantic, help/usage, managed-console, and child-process routing table independently for
      stdout and stderr.
- [ ] Use `presentation.cs` to snapshot null, empty, multiline, collection, string, and numeric property values in
      both modes and prove plain mode emits exactly one physical line per property.
- [ ] Exercise property snapshots with 1,023/1,024/1,025 items, at/beyond the character limit, an infinite enumerable,
      a throwing `MoveNext`, a throwing `Current`, and a throwing formatter; prove one-pass enumeration and the exact
      exception boundary.
- [ ] Assert redirected output contains no ANSI control sequences.
- [ ] Snapshot forced plain, independently redirected stdout/stderr, colorless interactive, fully rich, incapable
      terminal, empty/non-empty `NO_COLOR`, and `--plain --help` behavior across fixed platform fixtures.
- [ ] Exercise `--plain` with invalid models, graph failures, initial cancellation, input failures, and malformed or
      duplicate common options; prove it only selects presentation and does not change diagnostics.
- [ ] Test capability profiles rather than relying on the test host terminal; cover width, Unicode, cursor, color, and
      redirection independently for each stream.
- [ ] Snapshot rich Unicode, rich ASCII with and without color, and cursor-capable ASCII profiles independently; prove
      every terminal state remains distinguishable without unsupported glyphs.
- [ ] Throw from every cosmetic capability probe and the `NO_COLOR` lookup; prove conservative per-stream fallback,
      then fail writer capture separately and prove infrastructure classification.
- [ ] Snapshot every transient and terminal target state, each successful callback-free shape, blocker ordering,
      cleanup-failure combinations, success/failure stream routing, `NO_COLOR` symbols, and `--plain` ASCII labels.
- [ ] Assert actual observer timing for `Waiting`, `Running`, `Cleaning up`, and settlement, and prove an observer
      failure records infrastructure failure without changing target transitions or callback counts.
- [ ] Assert initial observer notifications are in plan order, terminal notifications contain the already-mutated
      outcome/shape/blockers, and no observer call occurs after its first exception.
- [ ] Run concurrent console examples repeatedly across all await patterns.
- [ ] Overlap different commands with distinct injected sinks; prove they share one global writer pair, retain
      invocation attribution, do not capture Rafter wrappers as host sinks, and restore only after the final command.
- [ ] Exercise character-at-a-time, partial-line, multiline, stdout, and stderr writes.
- [ ] Exercise partial lines across target and command scope changes, 65,535/65,536/65,537-character boundaries,
      unterminated final content, and continuation attribution without reordering complete stream events.
- [ ] Interleave a partial console write with same-stream semantic and lifecycle events plus host pass-through; prove
      physical order follows sequence keys and cross-span replacement markers use the first contributing scope once.
- [ ] Exercise every synchronous and asynchronous `TextWriter` overload, including pre-cancelled async calls; prove
      one ordering boundary and no task-blocking bridge.
- [ ] Split CRLF at every write boundary, mix lone CR and LF, change `TextWriter.NewLine` during interception, and
      verify managed normalization, byte-exact host pass-through, validation, failure propagation, and restored state.
- [ ] Exercise binding quarantine below, at, and above 1,048,576 characters; prove successful binding releases only
      redacted text and every binding failure or overflow emits none of the quarantine.
- [ ] Emit synthetic secrets through every semantic, console, diagnostic, exception, buffered, and injected
      process-output path; real child-process integration is completed in phase 7.
- [ ] Split secrets at every possible chunk boundary and verify no raw fragment sequence reconstructs the secret.
- [ ] Split multiline and overlapping secrets across newlines, attribution changes, continuation boundaries, and
      final flush; include arbitrarily long overlap chains and verify raw retention never exceeds the longest-pattern
      bound while output matches the Phase 3 interval-union contract.
- [ ] Test overlapping secrets and secrets containing markup/control characters.
- [ ] Verify original console writers are restored after success, failure, cancellation, and initialization failure.
- [ ] Replace stdout, stderr, and both during callbacks and final presentation; assert deterministic infrastructure
      failure, no replacement-content inspection, and exact entry-writer restoration. Document the temporary
      replacement limitation.
- [ ] Replace a writer while multiple commands overlap; prove every active invocation fails, the coordinator is
      reinstalled immediately, and the true entry writers are restored only after the last registration.
- [ ] Verify target, command-cleanup, binding/validation, expired-target, pre-installation, and post-restoration
      attribution boundaries with deterministic writer tests.
- [ ] Throw from semantic-event construction, scalar formatting, enumeration, lifecycle observation, rich rendering,
      plain rendering, stdout, and stderr separately; assert the approved callback-versus-infrastructure classification
      and absence of recursive reporting.
- [ ] Re-enter Rafter output from a formatter/enumerator and prove caller-owned work occurs outside the coordinator
      lock without deadlock or order corruption.
- [ ] Combine output infrastructure failure with cancellation, primary target failure, target cleanup failure, and
      command cleanup failure; assert exit `1`, continued Phase 5 settlement, and preservation of every outcome fact.
- [ ] After a sink failure, call every semantic method and prove validation, enumeration, and formatting still happen
      while no further managed event reaches the failed renderer or sink.

## Repository verification record

Run this baseline from the repository root:

```text
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --no-restore
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build --no-restore
```

The Phase 6 evidence document must record these results, the Windows/Ubuntu/macOS CI matrix, package-integrity result,
public API diff, approved presentation snapshots, routing/redaction matrices, and the exact examples compiled against
the Phase 6 API. It must explicitly list examples deferred to process and capture phases.

## Completion gates

- [x] **O0 — Initial behavioral contracts reconciled:** process-wide ownership, interception timing, binding
      quarantine, synchronous delivery, sealing, buffering, streaming redaction, lifecycle observation, capability
      profiles, public API, failure classification, and Phase 7/8 ownership are reconciled throughout the plan.
- [ ] **O1 — Semantic contract:** event and renderer snapshots are approved and deterministic.
- [ ] **O2 — Concurrent integrity:** stress tests show no mixed target lines or corrupted terminal sequences.
- [ ] **O3 — Async attribution:** all patterns in `console.cs` retain the correct target identity.
- [ ] **O4 — Cross-channel redaction:** the disposable secret is absent from all Phase 6 managed renderer/diagnostic
      bytes and failure artifacts; future deliberately raw application-owned `ProcessCapture` data remains deferred.
- [ ] **O5 — Chunk safety:** boundary and partial-write tests cannot bypass redaction.
- [ ] **O6 — Console restoration:** process-wide writers are identical before and after every tested terminal path.
- [ ] **O7 — Evidence recorded:** output routing table, snapshots, and redaction coverage matrix are committed.
- [ ] **O8 — Future raw-data ingress:** an internal synthetic raw payload can re-enter every Phase 6 managed channel
      and is redacted there; public `ProcessCapture`, invalid-exit capture, and child-process wiring remain explicit
      Phase 7/8 completion work.
- [ ] **O9 — Repository quality:** formatting, analyzer-clean Release build, all tests, package creation and integrity,
      public API checks, Phase 6 examples, and the supported-OS CI matrix pass with committed evidence.

## Phase 7 and Phase 8 handoff

Phase 7 feeds decoded streaming child stdout and stderr into the same coordinator, scope, sequence, routing, redaction,
and failure APIs; it does not create a second console or redaction pipeline. Process runtime tests reuse Phase 6 chunk,
stream, attribution, ownership, and sink-failure fixtures with real child processes.

Phase 8 implements bounded raw `ProcessCapture` and invalid-exit capture. Those values remain exact and
application-owned until explicitly sent back through `RafterOutput`, intercepted console, or another managed channel,
where the Phase 6 boundary redacts them. Phase 6 proves this re-entry behavior with an internal synthetic raw payload
without prematurely adding public process or capture types.

## Non-goals

No JSON/JSONL event protocol, logging-provider integration, interactive prompts, progress API, dashboard, persistent log
store, or arbitrary Spectre.Console passthrough is included initially.
