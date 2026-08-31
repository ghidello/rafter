# Phase 2: immutable command model

## Objective

Implement the authored object model behind the examples and normalize every fluent overload into immutable internal
definitions suitable for later binding and execution.

## Questions to resolve before implementation

- [ ] What exactly does `.ExpectsFailure<TException>()` mean when the callback throws the expected type, a derived
      type, a different type, an aggregate, or no exception?
- [ ] How does expected failure interact with cancellation, target state, dependent targets, output, and target and
      command cleanup?
- [ ] Is expected failure represented as target-definition policy, callback decoration, or execution policy, and
      which phase owns each part?
- [ ] Record the decisions and the complete outcome table before implementing the command model. Phase 5 must adopt
      the same table for execution and tests.

## Public contracts in scope

Command creation, descriptions, concurrency, options, targets, dependencies, conditions, expected-failure policy,
cleanup callbacks, root policies, working-directory declarations, and callback overloads are in scope. Execution
remains a controlled stub.

## Implementation checklist

### Definitions and ownership

- [ ] Define command, target, option, condition, cleanup, root, and working-directory identities and ownership rules.
- [ ] Give definitions stable internal IDs independent of display names.
- [ ] Normalize names once and define comparison semantics explicitly.
- [ ] Preserve registration order where it is needed for deterministic diagnostics and presentation.
- [ ] Prevent handles from one command being attached to another command.
- [ ] Separate mutable fluent builders from frozen definitions consumed by later phases.
- [ ] Freeze the entire command atomically before binding or planning.
- [ ] Reject mutation after freeze with one consistent diagnostic.

### Fluent API surface

- [ ] Implement `Rafter.Command(root)` with descriptions configured only through `.Description(...)`.
- [ ] Implement `.Concurrency(...)` and validate positive values.
- [ ] Implement option declarations and fluent metadata used by all option examples.
- [ ] Implement executable, aggregate, and explicit no-op target shapes.
- [ ] Implement `.DependsOn(...)`, target `.Finally(...)`, command `.Finally(...)`, and `.WorkingDirectory(...)`.
- [ ] Implement the model portion of `.ExpectsFailure<TException>()` according to the recorded outcome table.
- [ ] Implement context-free and context-aware `Action`, `Func<Task>`, and cancellation-compatible callback forms needed
      by the examples without ambiguous overload resolution.
- [ ] Implement `.When(bool)`, `.When(Func<bool>)`, context-aware, and asynchronous condition overloads.
- [ ] Normalize all callbacks and conditions into one internal async representation while preserving exception identity.
- [ ] Model required/defaulted option handles separately from optional handles so later context APIs can constrain
      their overloads correctly.

### Structural diagnostics

- [ ] Diagnose duplicate option and target names deterministically.
- [ ] Diagnose invalid or empty names and descriptions according to one documented normalization policy.
- [ ] Diagnose self-dependency and cross-command references at the earliest reliable point.
- [ ] Leave graph-wide cycle and reachability checks to phase 5.
- [ ] Use Rafter-owned exception and diagnostic types; do not expose builder internals.

## Required verification

- [ ] Compile construction-only forms from every example.
- [ ] Verify every callback overload chooses the intended method without casts.
- [ ] Verify Boolean and deferred conditions normalize identically except for evaluation timing.
- [ ] Verify model snapshots cannot observe subsequent caller collection mutation.
- [ ] Verify failed construction does not partially register a definition.
- [ ] Verify freeze is idempotent internally and blocks every public mutation path afterward.
- [ ] Add API-shape tests for names, generic constraints, parameter order, defaults, and return types.

## Completion gates

- [ ] **M1 — Portfolio construction:** all examples compile through command construction against the real API.
- [ ] **M2 — Immutability:** mutation, collection aliasing, and cross-command ownership tests pass.
- [ ] **M3 — Overload clarity:** compile-time fixtures cover every supported callback and condition form without
      ambiguity.
- [ ] **M4 — Deterministic diagnostics:** duplicate and invalid-definition tests assert stable codes and ordering.
- [ ] **M5 — API review:** the generated public API baseline matches the syntax portfolio with no speculative members.
- [ ] **M6 — Evidence recorded:** the normalized model diagram and API baseline are attached to the phase record.
- [ ] **M7 — Expected-failure decision:** the approved outcome table has an owning model representation and phase-5
      execution requirements.

## Non-goals

Do not parse arguments, evaluate conditions, resolve roots, traverse the graph, render output, or start processes.
