# Phase 6 implementation evidence

## Status

Partial implementation, with capability-profile work added on 2026-09-20. The [closeout audit](phase-05-07-closeout.md) records the
repository baseline and explicitly identifies unfinished presentation and verification work. This is not a phase
completion certificate.

## Routing and redaction coverage

| Input | Managed destination and protection | Executable evidence |
| --- | --- | --- |
| Line, success, property | stdout; invocation-wide redaction before rendering | `PhaseSixOutputTests.RoutesAndFormatsSemanticOutputWithoutChangingSuccess`, `RedactsSensitiveValuesBeforeSemanticOutputReachesTheSink` |
| String property in a file-app host | JSON escaping without reflection-based serialization | `FoundationIntegrationTests.PropertyOutputWorksWhenJsonReflectionIsDisabled`, canonical `presentation.cs` |
| Warning, error, recovery | stderr; presentation alone does not fail a target | `RoutesAndFormatsSemanticOutputWithoutChangingSuccess` |
| Managed console | Target identity flows through await and Task.Run; host writers restored | `AttributesPartialConsoleWritesAcrossAsyncFlowsAndRestoresTheHostWriters`, canonical `console.cs` |
| Console during binding | Quarantined per stream; released only after successful complete binding | `BindingOutputTests.QuarantinesValidationOutputUntilTheCompleteRedactorIsKnown` |
| Binding overflow | At 1,048,577 characters, discard both quarantines and skip execution | `EnforcesThePerStreamQuarantineLimitBeforeTargetExecution`, including below/at-limit rows |
| Split/multiline/long console text | Incremental redaction before bounded presentation | `RedactsASecretSplitAcrossConsoleWrites`, `RedactsMultilineAndSegmentBoundarySecretsIncrementally` |
| Process-local CRLF secret | Normalized streaming match across one-byte writes; no process-local registration outside that launch | `ProcessOutputBoundaryTests.RedactsProcessLocalMultilineSecretsAcrossByteChunks` |
| Raw capture re-entry | Capture preserves original newlines and secret; managed semantic/console output redacts registered invocation secrets | `CapturePreservesRawNewlinesAndIsRedactedOnlyWhenPresented` |
| Failed output sink | Infrastructure failure; callback continues and both child pipes still drain | `ConvertsSinkFailureIntoInfrastructureFailureWithoutThrowingThroughTheCallback`, `SinkFailureStillDrainsBothChildPipesAndSettlesTheTarget` |
| Concurrent commands | Separate injected sinks and shared console ownership | `KeepsOverlappingCommandsInTheirOwnConsoleSinks` |

## Existing approved plain snapshots

`RoutesAndFormatsSemanticOutputWithoutChangingSuccess` compares complete stdout and stderr strings, including:

```text
[present] Starting.
[present] missing=null
[present] paths=["src","tests"]
[present] success: Finished.
```

Warnings, errors, and recovery lines are asserted separately on stderr. The redaction tests assert `<redacted>` and
the absence of the disposable input. `console.cs` is checked for each authored target prefix on its expected stream.
These snapshots cover existing semantic output; final target-state rows, rich properties and live transitions
still require implementation and their own approved snapshots.

## Capability profiles

`OutputCapabilities` replaces the two ANSI flags with immutable per-stream redirection, static-layout, ANSI,
color, Unicode, cursor and width values. Invocation preparation selects plain output before any report when an
exact `--plain` token is present, or independently for a redirected/incapable stream or width outside 20–4,096.
Malformed and duplicate common-option validation remains unchanged.

`NO_COLOR` is read once per invocation, including early reports, and its captured result is reused if an option
also binds that variable. A non-empty value, including whitespace, disables only color. A failed lookup disables
color; if the command binds that variable, binding still observes the original exception. Rendering uses explicit
Spectre settings, disables default profile enrichment, and takes width and Unicode from the injected profile.
Production ANSI detection uses the pinned Spectre implementation with explicit color policy, avoiding automatic
`NO_COLOR`/`FORCE_COLOR` color detection. Capability-probe failures select plain output for only that stream;
failure to capture the writers remains an infrastructure failure.

The cached environment name follows platform semantics: case-insensitive on Windows and case-sensitive on Unix.
The review regressions reproduce duplicate Windows lookups for `no_color`/`No_Color` before the fix and cover both
successful values and cached failures. Unix keeps differently cased variables distinct.

`OutputCapabilityTests` supplies 29 cases covering mixed stdout/stderr redirection, color policy, width boundaries,
failed cosmetic probes, early model/graph/cancellation/input reports, malformed `--plain`, shared environment
lookup, per-invocation refresh, and writer-capture failure. All tests inject complete profiles rather than use
host-terminal detection. The Release build and 216-test solution suite pass locally with formatting unchanged.
CI for the three added review cases is deferred until the planned final verification run.
Live cursor coordination, status glyphs and rich property/final-summary layouts remain separate unfinished work.

## Remaining work

O1–O9 remain open except individually substantiated gates in the plan. The current suite is not the full phase matrix.
Missing cases include every writer overload, sealing races with caller-owned formatting, repeated cross-command
replacement, all renderer/observer failures, and process-wide ordering across semantic, console, and host writes.
The implementation gaps and repository/CI limits are enumerated in the closeout audit.
