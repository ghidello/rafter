# Phase 6: output, console attribution, and redaction

## Objective

Provide a semantic output pipeline with Spectre.Console presentation, concurrent target attribution, and one
redaction boundary covering every supported output and failure channel.

## Questions to resolve before implementation

- [ ] Is a value returned by `Capture()` application-owned program data or an output channel governed by automatic
      redaction?
- [ ] If capture remains raw, where is the exact trust boundary that guarantees it is redacted before presentation,
      diagnostics, exception excerpts, command rendering, or persistent artifacts?
- [ ] If capture is redacted before return, how do `CaptureJson`, exact output consumers, and secrets embedded in
      structured payloads avoid corruption?
- [ ] Do we need separate raw internal buffers and redacted presentation buffers, and how are their lifetimes and
      memory exposure constrained?
- [ ] Record the capture/redaction trust model before implementing redaction or the process-capture integration.

## Output model

Semantic events are immutable data carrying target/command scope, severity or kind, safe text, optional recovery or
property information, and ordering metadata. Renderers consume events; user callbacks do not manipulate Spectre
objects directly through the initial API.

## Implementation checklist

### Semantic events and sinks

- [ ] Implement line, success, warning, error with optional recovery, and named property events.
- [ ] Define null/empty value handling and multiline normalization.
- [ ] Allocate a monotonic ordering key at the output boundary.
- [ ] Separate event creation, redaction, buffering/routing, and rendering.
- [ ] Serialize writes through an output coordinator so concurrent targets cannot corrupt terminal control sequences.
- [ ] Make sinks injectable for deterministic tests.

### Presentation

- [ ] Implement rich Spectre.Console rendering for an interactive capable terminal.
- [ ] Implement plain rendering without ANSI sequences for redirected or explicitly plain output.
- [ ] Define stdout versus stderr routing for semantic kinds.
- [ ] Preserve readable target attribution under concurrency without promising impossible cross-process ordering.
- [ ] Keep diagnostics useful when color, Unicode, or cursor control is unavailable.
- [ ] Define `NO_COLOR` behavior and leave `FORCE_COLOR`/final .NET 11 behavior for later review unless supported
      explicitly on .NET 10.

### Managed console attribution

- [ ] Install reversible `Console.Out` and `Console.Error` coordinating writers for command execution.
- [ ] Preserve original writers and restore them exactly once on every exit path.
- [ ] Associate writes with the active target through execution context that flows across ordinary `await`,
      `Task.Run`, and `ConfigureAwait(false)`.
- [ ] Define attribution for writes outside a target and during command cleanup.
- [ ] Buffer partial writes until newline or scope completion without combining different targets.
- [ ] Support `Write`, `WriteLine`, asynchronous writer APIs, and concurrent writes.
- [ ] Prevent recursive routing when Rafter's renderer writes to the underlying console.

### Redaction

- [ ] Maintain an invocation-scoped immutable/redaction-safe registry populated during binding.
- [ ] Define handling for duplicate, empty, overlapping, substring, Unicode, and multiline sensitive values.
- [ ] Apply the recorded capture trust model; in every case, redact before text reaches semantic sinks, original
      console writers, terminal renderers, diagnostics, exception rendering, command previews, or persistent
      artifacts.
- [ ] Redact across chunk boundaries and partial-line buffering.
- [ ] Never retain an unredacted duplicate solely for later presentation.
- [ ] Use a stable replacement marker that cannot be mistaken for the original value.
- [ ] Ensure redaction failures fail closed rather than emitting raw text.

### Failure resilience

- [ ] Restore console writers even if rendering, user callbacks, or cleanup throws.
- [ ] Define behavior when the host replaces `Console.Out` during execution.
- [ ] Prevent output sink failure from causing recursive diagnostics.
- [ ] Keep cancellation from truncating buffered final lines without redaction.

## Required verification

- [ ] Snapshot every semantic event in rich-capability and plain modes.
- [ ] Assert redirected output contains no ANSI control sequences.
- [ ] Run concurrent console examples repeatedly across all await patterns.
- [ ] Exercise character-at-a-time, partial-line, multiline, stdout, and stderr writes.
- [ ] Emit synthetic secrets through every semantic, console, diagnostic, exception, buffered, and injected
      process-output path; real child-process integration is completed in phase 7.
- [ ] Split secrets at every possible chunk boundary and verify no raw fragment sequence reconstructs the secret.
- [ ] Test overlapping secrets and secrets containing markup/control characters.
- [ ] Verify original console writers are restored after success, failure, cancellation, and initialization failure.

## Completion gates

- [ ] **O1 — Semantic contract:** event and renderer snapshots are approved and deterministic.
- [ ] **O2 — Concurrent integrity:** stress tests show no mixed target lines or corrupted terminal sequences.
- [ ] **O3 — Async attribution:** all patterns in `console.cs` retain the correct target identity.
- [ ] **O4 — Cross-channel redaction:** the disposable secret is absent from all captured bytes and failure artifacts.
- [ ] **O5 — Chunk safety:** boundary and partial-write tests cannot bypass redaction.
- [ ] **O6 — Console restoration:** process-wide writers are identical before and after every tested terminal path.
- [ ] **O7 — Evidence recorded:** output routing table, snapshots, and redaction coverage matrix are committed.
- [ ] **O8 — Capture trust boundary:** the approved raw-versus-redacted capture decision is implemented and its data
      flow is covered by tests used again in phases 7 and 8.

## Non-goals

No JSON/JSONL event protocol, logging-provider integration, interactive prompts, progress API, dashboard, persistent log
store, or arbitrary Spectre.Console passthrough is included initially.
