# Phase 2: immutable command model

## Objective

Implement the authored object model behind the examples and normalize every fluent overload into immutable internal
definitions suitable for later binding and execution.

## Initial gate: questions to resolve before implementation

Phase 2 implementation must not begin until the following questions are analyzed, answered, and recorded as fixed
decisions in this document. The resulting contracts must also be reflected in the checklist and completion gates.

- [x] Define the freeze and invocation lifecycle. Specify how `RunAsync` triggers model freeze, how its entry target
      participates in ownership validation, whether a frozen command may be invoked more than once, and what the
      controlled Phase 2 execution stub does after a successful or failed freeze.
- [x] Define the Phase 2 portfolio-compilation boundary. Decide how construction-only compile fixtures cover the
      command-model syntax without requiring callback bodies to use output, filesystem, process, and typed-tool APIs
      owned by later phases; do not introduce speculative placeholder APIs merely to compile the canonical examples.
- [x] Complete the option type-state contract. Specify the public identities and fluent return types for optional,
      required, defaulted, and repeated handles across `.Description(...)`, `.Alias(...)`, `.FromEnvironment(...)`,
      `.Default(...)`, `.Sensitive()`, and `.Validate(...)`. Decide whether repeated options support authored defaults
      and whether repeated-option validation observes individual items or the complete immutable list.
- [x] Define diagnostic timing and total ordering. Separate immediate programming-contract exceptions from accumulated
      semantic model diagnostics, and specify how diagnostics tied to fluent calls interleave with freeze-derived
      diagnostics such as missing descriptions and unsupported option types.
- [x] Define identifier handling without ambiguous normalization language. Specify whether names are stored exactly as
      authored, confirm that identity uses ordinal case-sensitive comparison without trimming or case folding, and
      distinguish identifier validation from the preservation rules for descriptions and environment names.
- [x] Enumerate every model-attaching overload and its ownership rules. In particular, specify the accepted
      `.WorkingDirectory(...)` forms, reject optional handles where absence is not meaningful, and cover dependencies,
      entry targets, option handles, and every other cross-command attachment point at the phase where it can be
      checked reliably.
- [x] Reorder the implementation checklist into executable work packages: public API and type state; mutable builders
      and ownership identities; diagnostic recording and ordering; callback and condition normalization; atomic
      freezing and immutable definitions; Phase 2 compile fixtures; and public API/model evidence.

**Gate M0 — Initial contracts locked:** every question above is checked, its answer is recorded under fixed decisions,
and the implementation checklist and later completion gates agree with those answers.

## Fixed decisions

- Rafter does not convert an expected managed exception into target success. Authors catch exceptions explicitly
  inside a callback when that behavior belongs to the application.
- Expected native exit codes remain process configuration through `ValidExitCodes`; they are not target exceptions.
- `.Default(...)` accepts an already-computed value. The initial API has no deferred default factory.
- An authored default must be snapshot-safe: `string` or a value type containing no managed references. Other
  supported `IParsable<T>` types remain valid for command-line and environment binding, but model freeze rejects an
  authored default that would retain caller-owned mutable state and guides the author to bind text or use a
  snapshot-safe value type.
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
- `RunAsync(entryTarget, args)` ends authoring and atomically freezes the complete command. Freeze runs exactly once
  and caches either the immutable model or the complete ordered model-diagnostic result; a failed freeze also closes
  every builder permanently.
- The entry target is invocation input rather than part of the frozen command. Every invocation verifies that it is
  owned by that command. A successfully frozen command may be invoked sequentially more than once with different
  argument sets or owned entry targets, while overlapping invocations of the same command are rejected in v1.
- During Phase 2, `RunAsync` performs the real freeze and then throws an explicit temporary `NotSupportedException`
  instead of pretending that execution succeeded. Phase 2 tests inspect the internal cached freeze result directly;
  later phases replace only the execution stub and preserve the freeze lifecycle.
- The canonical examples remain unchanged as the final public syntax contract and become fully compilable only as
  their owning phases implement every API used by their callback bodies. Phase 2 adds compile-only API-shape fixtures
  for every command-model form used by those examples, with Phase 2-safe callback bodies and a traceability matrix
  mapping each form to its canonical example and fixture. Future-phase placeholder members are not added merely to
  compile the complete portfolio early.
- The public option handle states are `Option<T>`, `RequiredOption<T>`, `DefaultedOption<T>`, and
  `RepeatedOption<T>`. Metadata methods preserve their receiver's exact static state. `.Default(value)` exists only
  on `Option<T>` and returns a `DefaultedOption<T>` view over the same option identity; required and defaulted scalar
  handles can flow directly into context-owned APIs, while optional handles cannot.
- Ignoring the handle returned by `.Default(...)` leaves the caller with an optional static view but still records
  the default in the shared authored option. Model freeze remains the complete duplicate-default guard.
- `RepeatedOption<T>` has no authored default or environment fallback in v1. Absence resolves to an empty immutable
  `IReadOnlyList<T>`. Its validator receives the complete immutable list and runs even when that list is empty, so an
  author can express a non-empty requirement without another repeated-option state. Repeated options support
  `.Description(...)`, `.Alias(...)`, `.Sensitive()`, and list-level `.Validate(...)`.
- Null delegates, handles, arrays, array elements, names, and metadata violate the public programming contract and
  throw standard argument exceptions immediately. Mutation after freeze and overlapping `RunAsync` calls throw
  standard invalid-operation exceptions immediately. Each method validates its complete programming contract before
  mutating a builder, so these exceptions never leave a partial registration.
- Non-null authored mistakes are accumulated model diagnostics rather than immediate exceptions. An invalid fluent
  value still occupies that single-valued setting; a later valid call cannot repair it and instead records the
  applicable duplicate-setting diagnostic, preserving the first-value rule.
- Every command creation, definition declaration, and fluent call receives a monotonically increasing authored
  sequence. A model diagnostic is anchored to the responsible operation even when discovered at freeze: omissions
  and unsupported types anchor to their definition declaration, duplicate settings to the duplicate call, cleanup
  without execution to `.Finally(...)`, and name collisions to the later declaration.
- Diagnostics sort by authored operation sequence, then argument or collection-element position within that
  operation, then validation stage in this fixed order: invalid value or syntax; reserved identity or name collision;
  ownership, self-reference, or other reference validity; duplicate declaration or single-valued setting; missing
  required metadata; unsupported or ambiguous type; invalid structural combination. Operation-wide diagnostics sort
  after individual argument diagnostics, and stable internal diagnostic code is the final tie-breaker.
- Freeze accumulates every ordinary structural diagnostic without short-circuiting and caches the complete ordered
  result. Diagnostic kinds and codes are stable internally for testing and later presentation but are not printed in
  ordinary user-facing output. Structural diagnostics never include option values, defaults, or other potentially
  sensitive payloads.
- Option and target names are validated once and stored exactly as authored. Rafter never trims, case-folds,
  Unicode-normalizes, or otherwise rewrites them. The exact text must satisfy the ASCII lowercase kebab-case grammar,
  and identity uses `StringComparer.Ordinal` on every platform. Aliases are exact lowercase ASCII characters from
  `a` through `z`.
- Descriptions are preserved exactly after validation that they are neither empty nor entirely whitespace; Rafter
  does not derive them from identifiers. Environment fallback names are likewise preserved exactly after their
  portable validation and receive no Rafter-owned casing or trimming behavior. Stable internal IDs, rather than
  authored names, provide ownership and snapshot identity.
- `Target.DependsOn(params Target[])` accepts only targets owned by the same command, validates the array and every
  element before mutation, copies it immediately, and preserves authored order. Foreign targets, self-dependency,
  and duplicate dependencies are model diagnostics anchored to that call.
- Target `.WorkingDirectory(...)` accepts exactly `string`, `RequiredOption<string>`, or
  `DefaultedOption<string>`. It has no optional, repeated, `DirectoryInfo`, or general option-interface overload.
  A foreign option handle is a model diagnostic anchored to the call; path interpretation remains phase 4 work.
- `RunAsync(Target entryTarget, string[] args)` requires an entry target owned by the command and snapshots the
  validated argument array before later parsing. A foreign entry target is an invocation diagnostic and never
  contaminates the cached command model.
- Phase 2 ownership checks cover only explicit authored attachments. Handles captured inside callbacks cannot be
  inspected statically: phase 3 checks `context.Value(...)`, phase 4 checks filesystem and resolved-directory use,
  and phases 7–8 check generic-process and typed-tool specifications using the same ownership identity.
- Implementation proceeds through the seven ordered work packages below. Each package's focused tests pass before
  work begins on the next package; later packages may add coverage to earlier components but do not weaken their
  completed contracts.
- `.Sensitive()` is redaction metadata, not secure input transport. It cannot prevent command-line values from
  appearing in shell history or process inspection, and `.FromEnvironment(...)` is a configuration fallback rather
  than a recommendation to store secrets in environment variables. Documentation directs authors to
  application-owned secret stores, credential files, standard input, or another appropriate secure channel. Values
  obtained outside Rafter option binding are not registered automatically, and v1 has no manual registration API.

## Public contracts in scope

Command creation, descriptions, concurrency, options, targets, dependencies, conditions, cleanup callbacks, root
policies, working-directory declarations, and callback overloads are in scope. Execution remains a controlled stub.

## Implementation checklist

### Work package 1: public API and type state

- [x] Implement `Rafter.Command(root)` with descriptions configured only through `.Description(...)`.
- [x] Define the public command, target, context, root-policy, and working-directory declaration types required by
      Phase 2 without adding members owned by later phases.
- [x] Implement `RunAsync(entryTarget, args)` as the public terminal signature and freeze trigger.
- [x] Default concurrency to `1` and expose `.Concurrency(...)`.
- [x] Implement scalar option declarations and metadata for descriptions, aliases, environment fallback, defaults,
      sensitivity, and synchronous value-only validators.
- [x] Implement the four public handle states `Option<T>`, `RequiredOption<T>`, `DefaultedOption<T>`, and
      `RepeatedOption<T>`.
- [x] Make scalar metadata methods return the receiver's exact handle state; expose `.Default(value)` only on
      `Option<T>` and return `DefaultedOption<T>`.
- [x] Expose `.FromEnvironment(...)` only on scalar states; expose no repeated default or environment fallback.
- [x] Make `RepeatedOption<T>` resolve later as immutable `IReadOnlyList<T>` and expose descriptions, aliases,
      sensitivity, and list-level synchronous validation.
- [x] Expose target creation, `.DependsOn(...)`, execution, conditions, target and command cleanup, and target
      `.WorkingDirectory(...)` with exactly the string, required-string, and defaulted-string overloads.
- [x] Do not add a custom converter abstraction; retain binding as `string` plus application conversion as the v1
      escape hatch for unsupported application-owned types.

### Work package 2: mutable builders and ownership

- [x] Define mutable authored command, target, option, condition, cleanup, root, and working-directory records.
- [x] Give every command and definition a stable opaque internal ID independent of authored names and generic views.
- [x] Make all option type-state handles views over one underlying option identity; ignoring the result of
      `.Default(...)` still updates that identity without registering another option.
- [x] Preserve definition, metadata, validator, condition, and dependency registration order where later diagnostics
      or presentation depend on it.
- [x] Enforce command ownership for dependencies and target working-directory option handles.
- [x] Validate dependency arrays completely, copy them immediately, and preserve their authored order.
- [x] Validate and snapshot each `RunAsync` argument array before later parsing.
- [x] Keep the entry target invocation-specific; validate its ownership without adding it to the frozen command.
- [x] Separate mutable authored builders from the immutable definition types consumed by later phases.

### Work package 3: diagnostic recording and ordering

- [x] Validate each method's complete null and programming contract before mutation; use standard argument exceptions
      and prove that immediate failures never partially register state.
- [x] Give every command creation, declaration, and fluent call a monotonically increasing authored sequence.
- [x] Record non-null authored mistakes as Rafter-owned model diagnostics while preserving the first authored value
      for single-valued settings.
- [x] Anchor freeze-derived diagnostics to their responsible authored operation and implement the fixed
      operation/argument/validation-stage/code ordering contract.
- [x] Validate option and target names exactly as authored against lowercase ASCII kebab-case, use ordinal
      case-sensitive identity, and perform no trimming, case folding, or Unicode normalization.
- [x] Validate aliases as `a` through `z`; diagnose duplicate aliases, one-character long-name collisions, a second
      alias on one option, and collisions with reserved `plain`, `help`, or `h` identities.
- [x] Diagnose duplicate option and target names at the later declaration.
- [x] Validate descriptions as non-empty and non-whitespace while preserving their exact authored wording.
- [x] Validate scalar environment fallback names as non-empty and non-whitespace and reject NUL or `=` while
      preserving exact spelling and host-owned casing semantics.
- [x] Diagnose duplicates for descriptions, concurrency, execution, cleanup, target working directory, dependency
      declarations, scalar environment fallback, defaults, and sensitivity without replacing the first value.
- [x] Diagnose self-dependency, duplicate dependencies, and explicit cross-command attachments at their authored
      calls; keep a foreign `RunAsync` entry in invocation diagnostics rather than cached model diagnostics.
- [x] Keep stable internal diagnostic kinds and codes, exclude builder internals and sensitive payloads, and do not
      print codes in ordinary user-facing output.
- [x] Document that `.Sensitive()` supplies redaction metadata rather than secure transport and that
      `.FromEnvironment(...)` is not a secret-store recommendation; document that v1 performs no manual registration
      for values obtained outside option binding.

### Work package 4: callbacks, conditions, and target shapes

- [x] Implement executable targets, dependency-only aggregate targets, and callback-free implicit no-op targets.
- [x] Treat `.DependsOn(params Target[])` as one declaration of the complete dependency set; diagnose a second call
      or duplicate element without accumulating or deduplicating dependencies.
- [x] Implement context-free `Action` and `Func<Task>` execution callbacks and context-aware synchronous and
      asynchronous execution callbacks without ambiguous overload resolution.
- [x] Record a second `.Run(...)` as a model diagnostic and preserve the first callback.
- [x] Apply the same context-free and context-aware synchronous and asynchronous shapes to target and command
      `.Finally(...)`; allow only one cleanup callback at each scope.
- [x] Implement `.When(bool)`, `.When(Func<bool>)`, context-aware synchronous, and context-aware asynchronous
      conditions, with no context-free asynchronous overload.
- [x] Preserve multiple conditions in authored order and normalize them as one later short-circuit AND chain.
- [x] Normalize callbacks and conditions into one internal asynchronous representation while preserving original
      delegate exception identity.
- [x] Preserve multiple validators per option in authored order; repeated validators receive the complete immutable
      list and later run even for absence as an empty list.

### Work package 5: atomic freezing and immutable definitions

- [x] Freeze the complete command atomically before any parsing, planning, or execution work.
- [x] Require non-empty, non-whitespace command, target, and option descriptions without inventing fallbacks.
- [x] Validate positive explicit concurrency while retaining the default of `1`.
- [x] Classify option value types at freeze: allow `string`, Boolean, enum, nullable wrappers, and the appropriate
      `IParsable<T>` implementations; diagnose every unsupported type before binding.
- [x] Reject enum types whose member names collide under ordinal case-insensitive comparison, identify every
      colliding name, and include no option values.
- [x] Reject an optional non-nullable value-type option without a default and guide the author to `T?`,
      `RequiredOption<T>`, or `.Default(...)`.
- [x] Reject an authored default whose type is neither `string` nor a value type containing no managed references,
      so the frozen model never retains caller-owned mutable default state.
- [x] Reject target `.Finally(...)` without `.Run(...)`; leave graph-wide cycle and reachability validation to phase 5.
- [x] Accumulate and sort every ordinary model diagnostic without short-circuiting, then cache either the complete
      immutable model or the complete ordered failed result.
- [x] Make internal freeze idempotent for successful and failed results without repeating validation or snapshots.
- [x] Permanently close every builder after the first freeze attempt and reject later mutation with one consistent
      invalid-operation exception and no state change.
- [x] Permit sequential invocations of a successfully frozen command with different owned entries and argument
      snapshots; reject overlapping invocations of the same command.
- [x] Keep the Phase 2 post-freeze execution path as an explicit temporary `NotSupportedException`; never return a
      successful exit code without parsing or execution.

### Work package 6: compile fixtures and behavioral verification

- [x] Add compile-only fixtures for every Phase 2 form used by the canonical examples: command configuration, every
      option state, target shape, dependency declaration, callback and condition overload, cleanup, working
      directory, and `RunAsync`.
- [x] Give context-aware fixtures real Phase 2 context parameter types with bodies that require no output,
      filesystem, process, or typed-tool members owned by later phases.
- [x] Maintain a traceability matrix from every Phase 2 syntax form to a canonical example and compile fixture.
- [x] Verify every callback and condition overload resolves without casts and Boolean versus deferred conditions
      differ only in evaluation timing.
- [x] Add API-shape tests for public names, generic constraints, parameter order, defaults, and return types.
- [x] Verify every scalar metadata ordering preserves its static handle state and ignored `.Default(...)` return
      values still update the shared identity.
- [x] Verify repeated validators receive one immutable list, including absence as an empty list.
- [x] Verify dependency and argument snapshots cannot observe later caller-array mutation.
- [x] Verify every explicit model attachment uses the shared ownership identity and foreign entry targets do not
      poison the cached model.
- [x] Verify immediate programming-contract exceptions do not partially register state.
- [x] Verify invalid non-null first values remain authoritative, later calls diagnose duplication, and freeze reports
      every ordinary semantic error in the documented total order.
- [x] Verify multi-argument, multi-stage, and same-stage diagnostics use the documented parameter position,
      validation-stage, operation-wide, and diagnostic-code tie-breakers.
- [x] Verify freeze blocks every public mutation path, caches failed results, and performs no validation or snapshot
      work twice.
- [x] Verify sequential invocation reuse, invocation-specific owned entries, and overlapping-invocation rejection.
- [x] Compile no complete canonical callback body until all APIs it consumes are implemented; add no speculative
      future-phase member to make one compile early.

### Work package 7: API and model evidence

- [x] Generate and review the public API baseline against the syntax portfolio; retain no speculative public member.
- [x] Record the normalized mutable-to-frozen model diagram and identify every stable ID and immutable collection.
- [x] Record the ownership-check matrix with each attachment point, checking phase, and failure category.
- [x] Record the syntax-form traceability matrix and focused verification results in a Phase 2 evidence document.
- [x] Run clean repository restore, build, test, package, and package-verification workflows before closing the phase.

## Required verification

- [x] Every focused verification item in work package 6 passes.
- [x] The public API baseline, model diagram, traceability matrix, and ownership matrix agree with the implementation.
- [x] The clean repository workflow passes without warnings, ignored failures, or uncommitted generated artifacts.

## Completion gates

- [x] **M0 — Initial contracts locked:** all blocking Phase 2 questions are resolved and incorporated into this plan
      before implementation begins.
- [x] **M1 — Portfolio construction:** every Phase 2 syntax form used by the canonical examples compiles against the
      real API through a mapped compile-only fixture, with no speculative future-phase members.
- [x] **M2 — Immutability:** mutation, collection aliasing, and cross-command ownership tests pass.
- [x] **M3 — Overload clarity:** compile-time fixtures cover every supported callback and condition form without
      ambiguity.
- [x] **M4 — Deterministic diagnostics:** duplicate and invalid-definition tests assert stable codes and ordering.
- [x] **M5 — API review:** the generated public API baseline matches the syntax portfolio with no speculative members.
- [x] **M6 — Evidence recorded:** the normalized model diagram, API baseline, and ownership-check matrix are attached
      to the phase record. The matrix identifies every attachment point, its checking phase, and its failure category.

## Non-goals

Do not parse arguments, evaluate conditions, resolve roots, traverse the graph, render output, start processes, or
add target-level exception assertion or timeout policy.
