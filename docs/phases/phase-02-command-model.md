# Phase 2: immutable command model

## Objective

Implement the authored object model behind the examples and normalize every fluent overload into immutable internal
definitions suitable for later binding and execution.

## Fixed decisions

- Rafter does not convert an expected managed exception into target success. Authors catch exceptions explicitly
  inside a callback when that behavior belongs to the application.
- Expected native exit codes remain process configuration through `ValidExitCodes`; they are not target exceptions.
- `.Default(...)` accepts an already-computed value. The initial API has no deferred default factory.
- Asynchronous conditions are context-aware so they can observe invocation cancellation; there is no
  `.When(Func<Task<bool>>)` overload.
- Each target and command accepts at most one `.Finally(...)` callback. Duplicate registration records a model
  diagnostic and preserves the first; it is not replacement or an ordered cleanup stack.
- Duplicate single-valued model settings preserve the first value, accumulate authored-order diagnostics, and fail
  together at model freeze before parsing or execution.
- Target `.Finally(...)` requires `.Run(...)`; aggregate and implicit no-op targets cannot own cleanup.
- A target with neither a callback nor dependencies is a valid implicit no-op. No `.NoOp()` marker or analyzer is
  required.
- Command concurrency defaults to `1`; `.Concurrency(...)` accepts only positive values.

## Public contracts in scope

Command creation, descriptions, concurrency, options, targets, dependencies, conditions, cleanup callbacks, root
policies, working-directory declarations, and callback overloads are in scope. Execution remains a controlled stub.

## Implementation checklist

### Definitions and ownership

- [ ] Define command, target, option, condition, cleanup, root, and working-directory identities and ownership rules.
- [ ] Give definitions stable internal IDs independent of display names.
- [ ] Normalize names once and define comparison semantics explicitly.
- [ ] Use ordinal case-sensitive option-name and alias identity on every platform and reject exact duplicates.
- [ ] Require option names without `--` in lowercase kebab-case: start with `a`–`z`, then lowercase letters, digits,
      or non-repeated internal hyphens.
- [ ] Apply the same lowercase kebab-case and ordinal case-sensitive identity rules to target names and reject
      duplicates.
- [ ] Restrict `.Alias(char)` to `a`–`z` and reject duplicate aliases or collisions with another option's
      one-character long name.
- [ ] Allow at most one alias per option and report a second `.Alias(...)` call at model freeze.
- [ ] Reserve the option names `plain` and `help` plus alias `h`; reject author declarations that collide with them.
- [ ] Preserve registration order where it is needed for deterministic diagnostics and presentation.
- [ ] Prevent handles from one command being attached to another command.
- [ ] Separate mutable fluent builders from frozen definitions consumed by later phases.
- [ ] Freeze the entire command atomically before binding or planning.
- [ ] Reject mutation after freeze with one consistent diagnostic.

### Fluent API surface

- [ ] Implement `Rafter.Command(root)` with descriptions configured only through `.Description(...)`.
- [ ] Require a non-empty, non-whitespace description for the command and every target and option at model freeze;
      preserve authored wording rather than inventing fallbacks.
- [ ] Default concurrency to `1`; implement `.Concurrency(...)` and validate positive values.
- [ ] Implement option declarations and fluent metadata, including `.Validate(predicate, message)`, used by all
      option examples.
- [ ] Implement `RepeatedOption<T>` separately from scalar `Option<T>` and resolve it as immutable
      `IReadOnlyList<T>`.
- [ ] Classify option value types at model freeze: allow `string`, Boolean, enum, nullable wrappers, and types that
      implement the appropriate `IParsable<T>` contract; diagnose every unsupported type before binding.
- [ ] Reject an enum option whose declared member names are not unique under ordinal case-insensitive comparison,
      identifying every colliding name without exposing option values.
- [ ] Do not add a custom converter abstraction in v1; document binding as `string` and converting inside a target
      as the escape hatch for complex application-owned types.
- [ ] Keep validators synchronous and value-only; do not add asynchronous or context-aware validation overloads.
- [ ] Preserve multiple validators per option in authored order.
- [ ] Implement executable, aggregate, and implicit no-op target shapes.
- [ ] Implement `.DependsOn(...)`, target `.Finally(...)`, command `.Finally(...)`, and `.WorkingDirectory(...)`.
- [ ] Make `.DependsOn(params Target[])` a single declaration of the complete dependency set; report a second call
      or duplicate dependency through the common model-freeze diagnostic policy.
- [ ] Implement context-free `Action` and `Func<Task>` execution callbacks plus context-aware synchronous and
      asynchronous forms without ambiguous overload resolution.
- [ ] Record a model diagnostic for a second `.Run(...)` registration and preserve the target's first callback.
- [ ] Apply the same context-free and context-aware callback shapes to target and command `.Finally(...)`.
- [ ] Implement `.When(bool)`, `.When(Func<bool>)`, context-aware synchronous, and context-aware asynchronous
      condition overloads.
- [ ] Preserve multiple conditions per target in authored order as one normalized short-circuit AND chain.
- [ ] Normalize all callbacks and conditions into one internal async representation while preserving exception identity.
- [ ] Model required/defaulted option handles separately from optional handles so later context APIs can constrain
      their overloads correctly.
- [ ] Reject a frozen optional non-nullable value-type option with guidance to use `T?`, `RequiredOption<T>`, or
      `.Default(...)`.

### Structural diagnostics

- [ ] Diagnose duplicate option and target names deterministically.
- [ ] Diagnose invalid or empty names and descriptions according to one documented normalization policy.
- [ ] Diagnose self-dependency and cross-command references at the earliest reliable point.
- [ ] Record a model diagnostic for a second target or command `.Finally(...)` registration and preserve the first
      callback.
- [ ] Apply the same first-value-preserving diagnostic policy to duplicate descriptions, command concurrency, and
      target working-directory declarations.
- [ ] Treat `.FromEnvironment(...)` as single-valued option metadata and report a duplicate at model freeze.
- [ ] Validate each fallback name at model freeze: reject empty or whitespace-only text, NUL, and `=`, while
      preserving its authored spelling without enforcing a casing convention.
- [ ] Treat `.Default(...)` and `.Sensitive()` as single-valued, prevent duplicate defaults through type-state where
      practical, and report every remaining duplicate at model freeze.
- [ ] Expose `.Sensitive()` consistently for scalar and repeated options of every supported value type; do not
      constrain sensitivity metadata to `string` options.
- [ ] Report all accumulated model diagnostics in authored order at freeze and begin no parsing or invocation work.
- [ ] Reject a frozen target that declares `.Finally(...)` without `.Run(...)`.
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

## Non-goals

Do not parse arguments, evaluate conditions, resolve roots, traverse the graph, render output, start processes, or
add target-level exception assertion or timeout policy.
