# Phase 4: roots, working directories, and filesystem safety

## Objective

Resolve command and logical target paths without changing global process state, and provide the two initial
context-owned filesystem operations with a fail-closed containment and link-traversal contract.

Phase 4 builds the path model, context surface, and filesystem engine. Phase 5 supplies real graph execution and
therefore proves target concurrency and cleanup integration. Phase 7 consumes the same resolver for per-process
working-directory overrides.

## Questions to resolve before implementation

### Phase ownership and lifecycle

- [x] **Which Phase 4 behavior enters `RunAsync` before graph execution exists?** Define whether successful binding
      resolves the command root before reaching the temporary execution stub, which internal outcome retains the
      result, and how sequential invocation reuse observes a fresh invocation root without changing the frozen model.
- [x] **Which claims must remain deferred?** Assign actual target callback contexts, concurrent target directories,
      target and command cleanup scopes to Phase 5, and process-relative overrides to Phase 7. Define the Phase 4
      compile fixtures and internal context tests that prepare those integrations without claiming execution.
- [x] **Does exact help remain independent from the filesystem?** Preserve the Phase 3 guarantee that help does not
      capture or validate roots, inspect source metadata, resolve working directories, or perform filesystem I/O.
- [x] **How are root and working-directory failures classified?** Record exit codes and internal outcomes for missing
      source metadata, unavailable invocation-directory capture, malformed paths, nonexistent roots, option-derived
      escapes, authored literal escapes, inaccessible metadata, link rejection, and mutation failures.

### Public context and filesystem API

- [x] **What are the exact public types and signatures?** Lock `context.Root`, `context.WorkingDirectory`,
      `context.FileSystem`, both string-based ensure methods, their return types, and their null/error behavior.
- [x] **Which option-handle overloads exist?** Decide whether both ensure methods accept required and defaulted string
      handles, confirm that optional and repeated handles remain unsupported, and require lookup through the Phase 3
      ownership-checked snapshot without rebinding.
- [x] **Which failures use standard I/O exceptions and which use Rafter-owned policy failures?** Avoid exposing an
      accidental exception surface that later phases must replace, and define what safe operation/path metadata may
      appear in diagnostics.
- [x] **How does sensitivity flow into path diagnostics?** A path can originate from a sensitive option; define how
      normalized paths pass through the invocation redactor before any Rafter-managed presentation.

### Root and logical-directory semantics

- [x] **What is the base for a relative `Root.At(path)`?** Decide whether it resolves against the invocation directory
      captured for that invocation or whether explicit roots must be absolute.
- [x] **What exact metadata defines `Root.Source`?** Decide whether `AppContext` `EntryPointFilePath` is the only
      authoritative file-app source, how its directory is derived, and what happens for conventional compiled apps
      or hosts that do not supply it.
- [x] **Which directories must already exist?** Decide whether the command root must exist and be a directory while a
      logical target working directory may initially be absent so `EnsureDirectory(".")` can create it.
- [x] **Can the selected command root itself be a symbolic link, junction, mount point, or reparse point?** Define
      whether root selection establishes authority over its resolved destination or rejects redirected roots.
- [x] **Can an absolute target working directory be used when it remains contained by the command root?** Define the
      same rule for future process overrides and ensure operations.
- [x] **May `EnsureDirectory` name the command root or active target working directory?** Reconcile idempotent creation
      with the stricter rule that `EnsureEmptyDirectory` must reject both destructive targets.

### Path identity and containment

- [x] **What path namespaces are supported?** Record behavior for drive-relative paths, UNC paths, Windows extended
      and device namespaces, alternate separators, trailing separators, filesystem roots, and invalid path text.
- [x] **What equality and containment comparison applies?** Address ordinary Windows and macOS case behavior,
      case-sensitive Windows directories, sibling-prefix traps, volume boundaries, and roots with different casing.
- [x] **Is containment lexical, physical, or both?** Define how normalized full paths, existing-component metadata,
      links, reparse points, and mount boundaries contribute to the decision without relying on string prefixes.
- [x] **Are Unix mount-point or bind-mount crossings inside the command root accepted?** If not, identify the device or
      handle metadata used to reject them and the behavior when that metadata is unavailable.

### Mutation threat model and platform guarantee

- [x] **Is the command-root boundary a correctness guardrail for ordinary automation races, or a security boundary
      that must resist an adversary replacing path components concurrently?** State explicitly what actor and race
      conditions the contract does and does not resist.
- [x] **Which handle-relative or no-follow primitives can provide the selected guarantee on Windows, Linux, and
      macOS?** Distinguish managed metadata checks from native handle-relative traversal and mutation.
- [x] **When a platform cannot provide the required atomic traversal and mutation semantics, must the operation fail
      closed, may the platform be unsupported, or is a documented weaker guarantee acceptable?**
- [x] **What is revalidated after preflight and immediately before each mutation?** Define the accepted TOCTOU window,
      partial-mutation behavior after deletion begins, and the point after which rollback is no longer promised.
- [x] **What is the deterministic deletion policy?** Define enumeration and mutation order, treatment of locked or
      concurrently changed entries, and whether ordinary failures stop immediately or continue collecting failures.

### Testability and evidence

- [x] **What narrow internal filesystem-primitives seam is required?** It must support deterministic replacement
      between inspection, enumeration, and mutation without becoming the public general filesystem abstraction that
      this phase excludes.
- [x] **How are real links and reparse points tested on every CI platform?** Define capability probing, Windows
      symlink/junction coverage, Unix symlink coverage, and which missing capability fails, skips, or makes a platform
      report incomplete rather than silently satisfying the link-safety gate.
- [x] **How are before/after trees represented safely and deterministically?** Snapshot names, entry kinds, and safe
      metadata without following links or exposing unrelated filesystem contents.
- [x] Record the threat model, phase-ownership table, public API table, invocation outcome table, path-identity table,
      platform guarantee table, and test-capability report before implementation.
- [x] Reorder the implementation checklist after these answers are recorded so every work package and completion gate
      agrees with the chosen guarantee.

**Gate P0 — Initial path contracts locked:** every question above is checked, its answer is recorded under fixed
decisions, and the work packages, verification plan, and downstream phase handoffs reflect those answers.

## Fixed decisions

- Phase 4 resolves and retains invocation and logical target paths after successful Phase 3 binding and before the
  temporary execution stub. Paths are invocation state, never frozen command-model state. Sequential invocations
  resolve again; the overlap guard remains unchanged.
- Exact help remains filesystem-free: it does not read invocation-directory or source-file delegates, validate a
  root, resolve target directories, or inspect filesystem metadata.
- Phase 4 constructs internal target and command contexts and compiles the public syntax. Phase 5 owns real condition,
  target, concurrency, and cleanup execution. Phase 7 owns child-process working-directory application through the
  same internal resolver.
- `RafterContext.Root` and `WorkingDirectory` are normalized absolute strings. `FileSystem` returns the public
  `RafterFileSystem` facade. `EnsureDirectory` and `EnsureEmptyDirectory` are synchronous `void` methods with string,
  `RequiredOption<string>`, and `DefaultedOption<string>` overloads. Optional and repeated handles are not accepted.
- Handle overloads use the Phase 3 snapshot and command identity. They never read an environment fallback, convert,
  or validate again. Null arguments throw `ArgumentNullException`; foreign handles throw `InvalidOperationException`.
- Policy failures are an internal `IOException` subtype and ordinary physical I/O failures retain their BCL
  exception. During invocation initialization, path-policy failures are diagnostics with exit `2`; unavailable host
  or filesystem metadata is infrastructure failure with exit `1`. Filesystem calls made by future callbacks follow
  the ordinary execution-failure path. Rafter-managed presentation passes every message through the invocation
  redactor and does not expose raw paths in Phase 4 policy messages.
- `Root.Invocation` reads `Directory.GetCurrentDirectory()` once after binding. A relative `Root.At(path)` resolves
  against that captured directory; an absolute explicit root does not read it. `Root.Source` requires a fully
  qualified `AppContext` `EntryPointFilePath` and uses its containing directory. Missing or relative source metadata
  is an actionable path diagnostic.
- The command root must already be an ordinary directory or a selected directory link. Selecting a root establishes
  authority over that namespace; descendants below it are still checked and may not traverse links or reparse
  points. Logical target directories may initially be absent.
- Relative target and filesystem paths resolve against their documented logical base. Fully qualified paths are
  accepted only when containment succeeds. `EnsureDirectory` may name the command root or active target directory;
  `EnsureEmptyDirectory` rejects a filesystem root, command root, and active target directory.
- Empty, whitespace, drive-relative, partially qualified, Windows extended, and Windows device paths are rejected.
  Ordinary fully qualified drive and UNC paths use `Path.GetFullPath` and `Path.GetRelativePath`. Containment never
  uses string-prefix matching.
- Windows uses ordinary case-insensitive path comparison, Linux uses ordinal comparison, and macOS uses ordinary
  case-insensitive equality for protected-path decisions. Case-sensitive Windows directories and unusual
  case-sensitive macOS volumes are outside the stronger guarantee; lexical containment remains conservative where
  the platform `Path` implementation distinguishes them.
- Unix mount and bind-mount crossings already present beneath the selected root are accepted as part of that root's
  namespace. Phase 4 does not claim device-bound physical containment.
- The boundary is a destructive-operation correctness guardrail for a stable or ordinarily changing local tree, not
  a security boundary against an adversary racing path replacement. Existing components are checked without
  recursive following, the complete empty-directory tree is inspected before deletion, and entry kind is rechecked
  immediately before mutation. Public .NET 10 APIs do not provide a portable handle-relative atomic traversal, so a
  residual check/use window is documented.
- Preflight rejection performs no mutation. Once deletion starts, failures stop immediately and may leave a partial
  change; rollback is not promised. Enumeration is deterministic by platform path comparison and deletion is
  depth-first in that order. An internal link is deleted as an entry and never recursively enumerated.
- A narrow internal filesystem-primitives interface supports deterministic replacement tests. Real symbolic-link
  tests run when the host permits link creation; unavailable Windows link privilege is recorded rather than treated
  as proof. CI on link-capable Unix hosts remains the physical cross-platform integration gate.

## Intended path model

These constraints describe the desired user experience but do not resolve the blocking questions above:

- The command root is absolute and immutable within one invocation.
- The logical target working directory is absolute and derived from the command root unless an explicitly absolute
  path is permitted by the approved containment policy.
- A future process-relative working directory is resolved against its target directory by the same shared resolver.
- Rafter never uses `Environment.CurrentDirectory` to scope concurrent work.
- Context filesystem operations default to the active logical working directory and remain bounded by the command
  root.

## Implementation checklist

The final ordering is confirmed at P0. The current work-package split makes phase ownership explicit.

### Work package 1: invocation path services and outcomes

- [x] Extend the immutable invocation-services boundary with only the approved invocation-directory and source-file
      inputs, captured once without mutating process-global state.
- [x] Keep exact help independent from every Phase 4 service and filesystem probe.
- [x] Add internal root-resolution outcomes using the approved exit-code and diagnostic classifications.
- [x] Resolve and retain an invocation root only after successful Phase 3 binding and before the temporary execution
      barrier.
- [x] Preserve overlap rejection and sequential invocation reuse; never cache an invocation-specific resolved root in
      the frozen command model.

### Work package 2: root and path policy

- [x] Implement invocation, source, and explicit root policies using the approved bases and metadata.
- [x] Return the approved actionable failure when source metadata is unavailable.
- [x] Normalize paths with platform-appropriate full-path semantics and the approved namespace restrictions.
- [x] Implement equality, containment, volume, casing, and root rules without sibling-prefix vulnerabilities.
- [x] Enforce the approved existence, directory-kind, and redirected-root policy.
- [x] Resolve relative and permitted absolute target directories against the command root.
- [x] Expose one internal resolver contract that Phase 7 can reuse for process-relative overrides without adding
      process APIs in Phase 4.

### Work package 3: context surface and logical scopes

- [x] Implement the approved `Root`, `WorkingDirectory`, and `FileSystem` context properties.
- [x] Build Phase 4 contexts over the Phase 3 invocation snapshot, resolved command root, logical target directory,
      sensitivity registry, and narrow filesystem engine.
- [x] Resolve required/defaulted string handles through the existing ownership-checked snapshot exactly once.
- [x] Make filesystem methods default relative inputs to the context's logical target directory.
- [x] Define a command-root context factory for Phase 5 command cleanup and a target-directory context factory for
      conditions, execution, and target cleanup.
- [x] Prove internal contexts with different logical directories never read or mutate `Environment.CurrentDirectory`.
- [x] Compile all public Phase 4 syntax while deferring real callback and cleanup execution to Phase 5.

### Work package 4: traversal and containment engine

- [x] Centralize policy decisions separately from physical filesystem primitives.
- [x] Resolve every requested path against the active logical directory and require the approved containment relation
      beneath or equal to the command root.
- [x] Inspect every existing component required by the selected guarantee without following links.
- [x] Reject symbolic links, junctions, reparse points, mount-like redirects, or unsupported metadata according to the
      platform table.
- [x] Revalidate the approved identity and metadata immediately before mutation.
- [x] Produce bounded, redacted diagnostics containing only the operation and approved safe path information.

### Work package 5: `EnsureDirectory`

- [x] Implement the exact string and option-handle overloads approved at P0.
- [x] Treat an existing ordinary directory as success.
- [x] Fail clearly when an existing non-directory occupies the target.
- [x] Create missing parents only after the complete path passes validation.
- [x] Apply the approved command-root and active-target equality rules.
- [x] Fail closed when an existing component has redirected or unsupported metadata.
- [x] Prove successful calls are idempotent.

### Work package 6: `EnsureEmptyDirectory`

- [x] Apply the same resolution, namespace, containment, and existing-component checks as `EnsureDirectory`.
- [x] Reject a filesystem root, command root, and active logical target working directory.
- [x] Complete the approved preflight before removing any entry.
- [x] Enumerate entries without following links and represent every entry by the metadata needed for revalidation.
- [x] Remove an internal link as an entry without traversing its destination.
- [x] Delete ordinary contents in the approved deterministic order and leave the requested directory present and
      empty.
- [x] Apply the approved behavior for permissions, concurrent replacement, locked files, and unsupported metadata.
- [x] Do not promise transactional rollback after mutation begins; guarantee that preflight rejection performs no
      mutation.

### Work package 7: verification and handoff evidence

- [x] Complete every Phase 4 verification item below.
- [x] Record the root, path-identity, containment, platform, threat-model, failure-routing, and capability tables.
- [x] Record the exact Phase 5 context/cleanup tests and Phase 7 process-directory tests that remain deferred.
- [x] Keep the public surface limited to the examples and retain the internal filesystem-primitives seam as an
      implementation detail.

## Required Phase 4 verification

- [x] Table-test every root policy with injected invocation-directory and source-file inputs, including unavailable
      and malformed metadata, without reading real test-host identity.
- [x] Test relative, absolute, dotted, parent-segment, alternate-separator, trailing-separator, and invalid path
      inputs under the approved platform policy.
- [x] Test containment against sibling-prefix traps such as `root` versus `root-other`.
- [x] Test approved platform casing, drive, UNC, extended/device namespace, volume, mount, and filesystem-root cases
      where applicable.
- [x] Test nonexistent, already-created, file-in-place, deeply nested, command-root-equal, and target-directory-equal
      destinations.
- [x] Test string, required-option, and defaulted-option overloads, foreign ownership, and repeated context reads
      without rebinding.
- [x] Test a link or redirect at the target, in every parent position, and inside the directory being emptied.
- [x] Prove an external sentinel reached through a link is never removed or modified.
- [x] Inject replacement between validation, enumeration, revalidation, and deletion; assert the approved result for
      each race point.
- [x] Prove every preflight rejection leaves a complete before/after tree snapshot unchanged.
- [x] Prove successful ensure operations are idempotent and reach their documented final state.
- [x] Construct multiple internal contexts with different logical directories and assert the process-wide current
      directory remains unchanged.
- [x] Compile `filesystem.cs`, every root example, and `working-directory.cs` through output-free Phase 4 fixtures.
- [x] Record that actual target concurrency and cleanup scope execute in Phase 5, while process-relative overrides
      execute in Phase 7.

## Deferred integration verification

- Phase 5 executes concurrent targets with distinct logical working directories, proves conditions and target
  cleanup inherit the target directory, and proves command cleanup receives the command root.
- Phase 7 applies relative and absolute process overrides through the Phase 4 resolver and proves no child-process
  specification mutates the parent current directory.
- Phase 9 executes the canonical filesystem, root, working-directory, and repository examples after their output and
  process dependencies exist.

## Completion gates

- [x] **P0 — Initial contracts locked:** every blocking question is answered, recorded, and reflected in the ordered
      work packages, verification plan, and downstream handoffs.
- [x] **P1 — Root determinism:** every root policy resolves once to the approved absolute directory, and help performs
      no root work.
- [x] **P2 — No global CWD mutation:** injected parallel context/path tests observe an unchanged process-wide current
      directory; real concurrent target execution remains assigned to Phase 5.
- [x] **P3 — Containment:** traversal, namespace, equality, volume, casing, and sibling-prefix tests cannot escape the
      approved command-root boundary.
- [x] **P4 — Link safety:** real and injected link/reparse/replacement tests preserve every external sentinel under
      the approved platform guarantees.
- [x] **P5 — Rejection is non-mutating:** every preflight rejection produces an identical safe before/after tree
      snapshot.
- [x] **P6 — Operation contract:** successful ensure operations are idempotent and end in the documented state;
      post-mutation failures follow the recorded partial-change contract.
- [x] **P7 — Context and snapshot contract:** every public context/filesystem syntax shape compiles, ownership is
      enforced, and repeated access does not rebind option values.
- [x] **P8 — Phase handoff:** Phase 5 and Phase 7 plans name their required real execution tests and consume the shared
      context/path contracts without duplicating policy.
- [ ] **P9 — Evidence recorded:** the threat model, API, root, outcome, path-identity, platform-guarantee, capability,
      and deferred-integration tables are committed.

## Non-goals

No public general filesystem abstraction, copy/move/delete-file API, globbing API, watcher, rollback engine, native
security sandbox, or opt-out from the command-root boundary is introduced.
