# Phase 6: output, console attribution, and redaction

## Objective

Provide a semantic output pipeline with Spectre.Console presentation, concurrent target attribution, and one
redaction boundary covering every supported output and failure channel.

## Questions resolved before implementation

- [x] **Is captured output application-owned or automatically redacted?** `ProcessCapture` contains exact raw program
      data so structured formats and other exact consumers are not corrupted. The same rule applies to the complete
      capture attached to an invalid-exit exception.
- [x] **Where is the trust boundary?** Rafter never presents raw capture automatically. Text is redacted whenever it
      crosses back into a Rafter-managed semantic output, intercepted console, diagnostic, exception presentation,
      command rendering, or persistent-output channel. Direct application use outside those channels is caller-owned.
- [x] **Are separate raw and redacted capture buffers required?** No persistent redacted duplicate is retained solely
      for presentation. Capture retains only the bounded raw data needed for the public result; managed sinks redact
      when data crosses their boundary, while streaming output is redacted before presentation.
- [x] **Who owns lifetime after return?** Rafter releases execution-only buffers when the terminal operation settles.
      The application controls the lifetime and disclosure of raw strings reachable through `ProcessCapture`.
- [x] Record this capture/redaction trust model before implementing redaction or process capture.

## Output model

Semantic events are immutable data carrying target/command scope, severity or kind, safe text, optional recovery or
property information, and ordering metadata. Renderers consume events; user callbacks do not manipulate Spectre
objects directly through the initial API.

## Implementation checklist

### Semantic events and sinks

- [ ] Implement line, success, warning, error with optional recovery, and named property events.
- [ ] Implement `Output.Property(string name, object? value)` without generic-overload ambiguity for `null`.
- [ ] Snapshot property input synchronously into immutable null, empty-string, scalar, multiline-string, or
      one-dimensional ordered-collection semantic data; special-case `string` as a scalar and enumerate every other
      supplied collection exactly once.
- [ ] Reject nested structured collections with clear guidance to format them in application code; preserve an
      enumeration or scalar-formatting exception as the target callback failure.
- [ ] Format scalar values invariantly. In rich mode render `<null>`, `""`, indented multiline text, and readable
      inline or vertical collections; in plain mode render one physical `name=value` line with JSON-style null,
      quoting, collection delimiters, and newline escaping.
- [ ] Allocate a monotonic ordering key at the output boundary.
- [ ] Separate event creation, redaction, buffering/routing, and rendering.
- [ ] Serialize writes through an output coordinator so concurrent targets cannot corrupt terminal control sequences.
- [ ] Make sinks injectable for deterministic tests.

### Presentation

- [ ] Extend the Phase 3 Spectre.Console report renderer for general semantic events in an interactive capable
      terminal; reuse rather than replace its help and diagnostic document models.
- [ ] Extend the Phase 3 deterministic plain renderer without introducing ANSI sequences for redirected or
      explicitly plain output.
- [ ] Make the common `--plain` option force both stdout and stderr into deterministic width-independent output with
      no ANSI, cursor/live control, or Unicode-only status glyphs.
- [ ] Without `--plain`, detect stdout and stderr capabilities independently and use plain output for each redirected
      or incapable stream even when the other stream remains rich.
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
- [ ] Emit the final summary to stdout when the command succeeds and to stderr with detailed failures when it fails
      or is cancelled; omit durations from v1 output.
- [ ] Use the rich symbol/color map: `○` waiting/dim grey, `●` running/cyan, `◐` cleanup/cyan, `✓`
      succeeded/green, `◇` aggregate/green, `–` no-work/grey, `↷` skipped/grey, `■` cancelled/yellow,
      `⊘` blocked/yellow, and `✗` failed/red. Keep symbols under `NO_COLOR`, reserve red for actual failures, and
      use full stable ASCII labels under `--plain`.
- [ ] Keep diagnostics useful when color, Unicode, or cursor control is unavailable.
- [ ] Treat a non-empty `NO_COLOR` as disabling color only, retaining supported static rich layout and Unicode; treat
      an empty value as unset. Do not support `FORCE_COLOR` in v1, and never let an enabling environment convention
      override redirection.

### Managed console attribution

- [ ] Install reversible `Console.Out` and `Console.Error` coordinating writers for command execution.
- [ ] Preserve original writers and restore them exactly once on every exit path.
- [ ] Associate writes with the active target through execution context that flows across ordinary `await`,
      `Task.Run`, and `ConfigureAwait(false)`.
- [ ] Attribute writes during a target's conditions, execution, and cleanup to that target; attribute command cleanup
      and invocation work without an active target, including binding and validation, to the command.
- [ ] Mark target attribution scopes closed when their callback settles; if unawaited work writes before the overall
      invocation settles, route it to the command rather than retaining stale target attribution.
- [ ] Intercept only between successful writer installation inside `RunAsync` and exact restoration. Leave writes
      before installation and after restoration untouched by Rafter.
- [ ] Document that callbacks must await spawned work and that work outliving `RunAsync` is application-owned, with
      no Rafter attribution or redaction guarantee.
- [ ] Buffer partial writes until newline or scope completion without combining different targets.
- [ ] Support `Write`, `WriteLine`, asynchronous writer APIs, and concurrent writes.
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
- [ ] Redact across chunk boundaries and partial-line buffering.
- [ ] Never retain an unredacted duplicate solely for later presentation; bounded raw capture exists only for the
      application-owned result.
- [ ] Preserve Phase 3 replacement-marker safety and fail-closed verification across every streaming boundary.

### Failure resilience

- [ ] Restore console writers even if rendering, user callbacks, or cleanup throws.
- [ ] Capture the exact entry `Console.Out` and `Console.Error` instances, verify Rafter still owns both globals at
      callback and presentation boundaries, and treat replacement of either as a command infrastructure failure.
- [ ] Never adopt, chain, inspect, or emit through an application replacement. In `finally`, restore both exact entry
      writers even when one or both were replaced during the invocation.
- [ ] Report only which stream lost ownership and document that temporary replace-and-restore mutations may be
      undetectable and are outside Rafter's supported attribution/redaction boundary.
- [ ] Prevent output sink failure from causing recursive diagnostics.
- [ ] Keep cancellation from truncating buffered final lines without redaction.

## Required verification

- [ ] Snapshot every semantic event in rich-capability and plain modes.
- [ ] Assert the complete semantic, help/usage, managed-console, and child-process routing table independently for
      stdout and stderr.
- [ ] Use `presentation.cs` to snapshot null, empty, multiline, collection, string, and numeric property values in
      both modes and prove plain mode emits exactly one physical line per property.
- [ ] Assert redirected output contains no ANSI control sequences.
- [ ] Snapshot forced plain, independently redirected stdout/stderr, colorless interactive, fully rich, incapable
      terminal, empty/non-empty `NO_COLOR`, and `--plain --help` behavior across fixed platform fixtures.
- [ ] Snapshot every transient and terminal target state, each successful callback-free shape, blocker ordering,
      cleanup-failure combinations, success/failure stream routing, `NO_COLOR` symbols, and `--plain` ASCII labels.
- [ ] Run concurrent console examples repeatedly across all await patterns.
- [ ] Exercise character-at-a-time, partial-line, multiline, stdout, and stderr writes.
- [ ] Emit synthetic secrets through every semantic, console, diagnostic, exception, buffered, and injected
      process-output path; real child-process integration is completed in phase 7.
- [ ] Split secrets at every possible chunk boundary and verify no raw fragment sequence reconstructs the secret.
- [ ] Test overlapping secrets and secrets containing markup/control characters.
- [ ] Verify original console writers are restored after success, failure, cancellation, and initialization failure.
- [ ] Replace stdout, stderr, and both during callbacks and final presentation; assert deterministic infrastructure
      failure, no replacement-content inspection, and exact entry-writer restoration. Document the temporary
      replacement limitation.
- [ ] Verify target, command-cleanup, binding/validation, expired-target, pre-installation, and post-restoration
      attribution boundaries with deterministic writer tests.

## Completion gates

- [ ] **O1 — Semantic contract:** event and renderer snapshots are approved and deterministic.
- [ ] **O2 — Concurrent integrity:** stress tests show no mixed target lines or corrupted terminal sequences.
- [ ] **O3 — Async attribution:** all patterns in `console.cs` retain the correct target identity.
- [ ] **O4 — Cross-channel redaction:** except for the deliberately raw application-owned `ProcessCapture`, the
      disposable secret is absent from all captured renderer/diagnostic bytes and failure artifacts.
- [ ] **O5 — Chunk safety:** boundary and partial-write tests cannot bypass redaction.
- [ ] **O6 — Console restoration:** process-wide writers are identical before and after every tested terminal path.
- [ ] **O7 — Evidence recorded:** output routing table, snapshots, and redaction coverage matrix are committed.
- [ ] **O8 — Capture trust boundary:** raw application-owned capture and redaction on re-entry into managed channels
      are implemented, and the complete data flow is covered by tests used again in phases 7 and 8.

## Non-goals

No JSON/JSONL event protocol, logging-provider integration, interactive prompts, progress API, dashboard, persistent log
store, or arbitrary Spectre.Console passthrough is included initially.
