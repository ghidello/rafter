# Phase 4: roots, working directories, and filesystem safety

## Objective

Resolve command and target paths without changing global process state, and provide the two initial filesystem
operations with a fail-closed containment and link-traversal contract.

## Questions to resolve before implementation

- [ ] Is the command-root boundary a correctness guardrail for ordinary automation races, or a security boundary
      that must resist an adversary replacing path components concurrently?
- [ ] Which handle-relative or no-follow primitives can provide the selected guarantee on Windows, Linux, and macOS?
- [ ] When a platform cannot provide the required atomic traversal and mutation semantics, must the operation fail
      closed, may the platform be unsupported, or is a documented weaker guarantee acceptable?
- [ ] How will tests exercise path replacement between validation, enumeration, and deletion rather than checking
      only a static preflight tree?
- [ ] Record the threat model, platform guarantee table, and chosen failure behavior before implementing filesystem
      mutation.

## Path model

- The command root is absolute and immutable for an invocation.
- The target working directory is absolute and derived from the command root unless explicitly absolute and still
  permitted by the root policy.
- A process-relative working directory is resolved against its target directory.
- Rafter never uses `Environment.CurrentDirectory` to scope concurrent work.

## Implementation checklist

### Root resolution

- [ ] Implement invocation, source, and explicit root policies from their examples.
- [ ] Define source-root behavior when source metadata is unavailable and return an actionable diagnostic.
- [ ] Normalize roots with platform-appropriate full-path semantics.
- [ ] Define treatment of trailing separators, drive-letter casing, UNC paths, and case sensitivity.
- [ ] Capture the invocation directory once rather than rereading mutable process-wide state.
- [ ] Reject nonexistent or non-directory roots according to one documented policy.

### Working-directory scopes

- [ ] Resolve target-relative paths against the command root.
- [ ] Resolve process-relative overrides against the target working directory.
- [ ] Make `context.WorkingDirectory` the normalized absolute target directory.
- [ ] Make context filesystem operations default to the target directory.
- [ ] Make target cleanup inherit its owner's target directory.
- [ ] Make command cleanup use the command root.
- [ ] Support required/defaulted string option handles without rebinding them.
- [ ] Prove two concurrent targets can use different directories without changing `Environment.CurrentDirectory`.

### `EnsureDirectory`

- [ ] Resolve the requested path against the active working directory.
- [ ] Require the normalized result to be contained beneath the command root.
- [ ] Treat an existing directory as success.
- [ ] Fail clearly when an existing non-directory occupies the target.
- [ ] Create missing parents only after the complete path passes validation.
- [ ] Define behavior for links or reparse points in existing path components and fail closed.

### `EnsureEmptyDirectory`

- [ ] Apply the same resolution and containment checks as `EnsureDirectory`.
- [ ] Reject a filesystem root, command root, and active target working directory.
- [ ] Inspect every existing component needed to reach the target before mutating anything.
- [ ] Reject traversal through symbolic links, junctions, mount-like redirects, or reparse points.
- [ ] Enumerate entries without following links.
- [ ] Remove an internal link as an entry without traversing its destination.
- [ ] Delete ordinary contents and leave the requested directory present and empty.
- [ ] Define failure behavior for permissions, concurrent changes, locked files, and unsupported metadata.
- [ ] Do not promise transactional rollback; do guarantee that preflight rejection performs no mutation.

### Diagnostics and testability

- [ ] Centralize path-policy decisions separately from physical filesystem operations.
- [ ] Include operation and safe normalized path in diagnostics without leaking unrelated filesystem contents.
- [ ] Implement the recorded atomicity and no-follow decision on each supported platform.
- [ ] Document any accepted race limit and ensure public safety language matches the approved threat model.

## Required verification

- [ ] Test relative, absolute, dotted, parent-segment, alternate-separator, and trailing-separator inputs.
- [ ] Test containment against sibling-prefix traps such as `root` versus `root-other`.
- [ ] Test platform case behavior, drive roots, UNC paths where available, and filesystem roots.
- [ ] Test nonexistent, already-created, file-in-place, and deeply nested destinations.
- [ ] Test a link at the target, in every parent position, and inside the directory being emptied.
- [ ] Prove an external sentinel reached through a link is never removed or modified.
- [ ] Prove every preflight rejection leaves a complete before/after tree snapshot unchanged.
- [ ] Test concurrent target working directories and cleanup scopes.

## Completion gates

- [ ] **P1 — Root determinism:** every root policy resolves once to the expected absolute directory.
- [ ] **P2 — No global cwd mutation:** concurrency tests observe an unchanged process-wide current directory.
- [ ] **P3 — Containment:** traversal and sibling-prefix tests cannot escape the command root.
- [ ] **P4 — Link safety:** link/reparse tests preserve every external sentinel on supported platforms.
- [ ] **P5 — Rejection is non-mutating:** rejected requests produce identical before/after filesystem snapshots.
- [ ] **P6 — Operation contract:** successful ensure operations are idempotent and end in the documented state.
- [ ] **P7 — Evidence recorded:** platform behavior table and link-test capability report are committed.
- [ ] **P8 — Threat model resolved:** the approved atomicity decision is implemented, tested under path replacement,
      and reflected in public documentation.

## Non-goals

No general filesystem abstraction, copy/move/delete-file API, globbing API, watcher, rollback engine, or opt-out from
the command-root boundary is introduced.
