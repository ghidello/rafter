# Phase 6 implementation evidence

## Status

Partial implementation, verified locally and in three-OS CI on 2026-09-19. The [closeout audit](phase-05-07-closeout.md) records the
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
These snapshots cover existing semantic output; final target-state rows, rich properties, live transitions and
capability combinations still require implementation and their own approved snapshots.

## Remaining work

O1–O9 remain open except individually substantiated gates in the plan. The current suite is not the full phase matrix.
Missing cases include every writer overload, sealing races with caller-owned formatting, repeated cross-command
replacement, all renderer/observer failures, and process-wide ordering across semantic, console, and host writes.
The implementation gaps and repository/CI limits are enumerated in the closeout audit.
