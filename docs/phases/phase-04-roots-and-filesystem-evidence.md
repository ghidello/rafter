# Phase 4 completion evidence

## Status

Phase 4 implements invocation root resolution, immutable logical target paths, the public context path surface, and
the two initial context-owned filesystem operations. Successful initialization still stops at the explicit Phase 5
graph-execution barrier.

## Phase ownership

| Phase | Ownership |
| --- | --- |
| Phase 4 | Root resolution, target-directory resolution, context factories, containment, ensure operations |
| Phase 5 | Real condition/target execution, concurrent target scopes, target cleanup, command cleanup |
| Phase 7 | Per-process overrides applied through the Phase 4 resolver |
| Phase 9 | Complete root, filesystem, working-directory, and repository examples |

## Public API

| API | Contract |
| --- | --- |
| `context.Root` | Normalized absolute invocation root |
| `context.WorkingDirectory` | Normalized absolute logical callback directory |
| `context.FileSystem` | Context-owned `RafterFileSystem` facade |
| `EnsureDirectory(...)` | String, required-string, and defaulted-string overloads; idempotent creation |
| `EnsureEmptyDirectory(...)` | Same overloads; protected-target rejection and no-follow emptying |

All option overloads resolve the Phase 3 snapshot and enforce command ownership without rebinding.

## Root and invocation outcomes

| Condition | Result |
| --- | --- |
| Exact help | Exit `0`; no root delegates or filesystem metadata are read |
| Valid invocation/source/explicit root | Absolute root and all logical target paths retained once |
| Missing or malformed root policy input | Safe path diagnostic, exit `2` |
| Host delegate or unexpected metadata failure | Infrastructure failure, exit `1` |
| Successful path initialization | Temporary `NotSupportedException` at the Phase 5 barrier |

`Root.Invocation` captures the current directory once. Relative explicit roots use that captured base. `Root.Source`
requires fully qualified file-app metadata and uses its containing directory. The selected root must exist and be a
directory; logical target directories need not exist yet.

## Path identity and platform policy

| Area | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Ordinary equality | Ordinal ignore-case | Ordinal | Ordinal ignore-case |
| Drive-relative path | Rejected | Not applicable | Not applicable |
| UNC path | Supported when fully qualified and contained | Platform path semantics | Platform path semantics |
| Extended/device namespace | Rejected | Not applicable | Not applicable |
| Descendant symlink/reparse traversal | Rejected | Rejected | Rejected |
| Existing mount beneath root | Reparse points rejected | Accepted as root namespace | Accepted as root namespace |

Containment uses normalized full paths plus `Path.GetRelativePath`; sibling string prefixes cannot pass it. Absolute
operation and target paths remain valid only within the selected root. `EnsureDirectory` may select the root or active
working directory. `EnsureEmptyDirectory` rejects those paths and every filesystem root.

## Threat model and mutation guarantee

The contract prevents accidental escape and destructive traversal in a stable or ordinarily changing automation
tree. It is not an adversarial security boundary. .NET 10 exposes link/reparse metadata but no portable public
handle-relative atomic directory traversal and deletion primitive.

`EnsureEmptyDirectory` performs a complete deterministic preflight, never recursively enumerates a link, and
revalidates every planned entry kind immediately before mutation. A changed entry fails closed. An internal link is
removed as one entry. A residual race remains between revalidation and the BCL mutation call. Preflight rejection is
non-mutating; a failure after deletion begins may leave a partial result and stops immediately without rollback.

## Capability report

| Capability | Local Windows result |
| --- | --- |
| Physical ordinary directory/file traversal | Passed |
| Deterministic injected file-to-link replacement | Passed; zero delete calls |
| External-sentinel physical symlink test | Host denied symlink creation privilege; test records unavailable capability |
| Unix physical symlink behavior | Assigned to Linux/macOS CI |

## Verification

Focused tests cover help side-effect freedom, all root policies, exactly-once invocation input capture, missing source
metadata, infrastructure failure, relative and absolute normalization, traversal and sibling-prefix rejection,
target option lookup, foreign ownership, distinct context directories, idempotent creation and emptying, protected
targets, link traversal, external sentinel preservation when link creation is available, and injected replacement
between preflight and deletion.

The completion commands are:

```powershell
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --no-restore
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build --no-restore
pwsh -NoProfile -File ./eng/verify-package.ps1 -Configuration Release
pwsh -NoProfile -File ./eng/validate-examples.ps1
git diff --check
```

Cross-platform physical link coverage remains the branch integration gate after the user pushes the eventual commit.

Observed locally on Windows x64 with .NET SDK `10.0.400`:

- Debug and Release builds completed with zero warnings and zero errors with .NET and Meziantou analyzers enabled.
- All 82 tests passed across the unit, integration, and analyzer assemblies.
- Runtime and symbol packages passed content checks and restored into isolated conventional-project and file-based
  consumers.
- All 29 canonical examples retained their project-mode references.
- Formatting and whitespace verification passed.
- Physical symbolic-link creation was unavailable to the current user token; the injected replacement test and all
  ordinary physical filesystem tests passed.
