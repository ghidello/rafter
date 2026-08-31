# Phase 3: bounded parsing and exactly-once binding

## Objective

Parse the intentionally small Rafter command grammar and bind every authored option exactly once into an immutable
invocation snapshot before any graph behavior can run.

## Grammar boundary

The accepted grammar is derived from the option examples. Unsupported command-system features remain errors rather
than accidental compatibility promises.

## Implementation checklist

### Tokens and option lookup

- [ ] Define option spelling, case sensitivity, long-option format, value separation, Boolean flag behavior, repeated
      occurrence behavior, `--` handling, and unknown-token behavior.
- [ ] Preserve original tokens for diagnostics while using normalized tokens for lookup.
- [ ] Detect missing values, duplicate scalar occurrences, unknown options, and unexpected positionals.
- [ ] Keep help parsing side-effect free and independent from option default evaluation.
- [ ] Define stable parse diagnostic codes, token locations, ordering, and exit classification.

### Conversion and precedence

- [ ] Support the scalar and collection types demonstrated by `options.cs` and `option-types.cs`.
- [ ] Implement invariant conversion unless an option explicitly owns another culture policy.
- [ ] Implement enum conversion and diagnostics for permitted values.
- [ ] Apply precedence in one documented order: command line, declared environment fallback, authored default, absent.
- [ ] Evaluate a default factory no more than once.
- [ ] Read each declared environment fallback no more than once.
- [ ] Run conversion and validation no more than once for the selected raw value.
- [ ] Distinguish a missing optional value from a present value equal to `default(T)`.

### Snapshot and sensitivity

- [ ] Store values by option identity in a read-only invocation snapshot.
- [ ] Copy repeated values and other mutable inputs before exposing them.
- [ ] Register a sensitive value only after successful conversion and validation, before any later diagnostic can
      render it.
- [ ] Ensure diagnostics never echo sensitive raw tokens or environment values.
- [ ] Make `context.Value(option)` a typed snapshot lookup with cross-command protection.
- [ ] Provide the same lookup primitive to context-owned APIs so convenience overloads do not rebind.

### Invocation barrier

- [ ] Complete all parsing, fallback reads, defaults, conversion, validation, and sensitivity registration before
      evaluating any condition.
- [ ] On binding failure, return a failed invocation without creating execution contexts.
- [ ] Prove target callbacks, process factories, and target/command cleanup cannot run after binding failure.

## Required verification

- [ ] Table-test every grammar rule, including empty input and malformed final tokens.
- [ ] Test command-line/environment/default/absence precedence for every option category.
- [ ] Count calls to fallback readers, default factories, converters, validators, and caller enumerables.
- [ ] Read one option from multiple targets and through multiple context APIs; every counter remains one.
- [ ] Mutate source collections after binding and prove the snapshot is unchanged.
- [ ] Verify sensitive invalid input is absent from all diagnostics and exception text.
- [ ] Snapshot help and error output in interactive-neutral form.
- [ ] Add fuzz/property tests for parser termination, token preservation, and absence of unhandled exceptions.

## Completion gates

- [ ] **B1 — Grammar locked:** the accepted and rejected grammar table is documented and fully tested.
- [ ] **B2 — Exactly once:** instrumentation proves every binding stage and mutable enumeration occurs at most once.
- [ ] **B3 — Atomic barrier:** binding-failure tests prove no condition, target, process, or cleanup side effect occurs.
- [ ] **B4 — Immutable values:** repeated and mutable value snapshot tests pass.
- [ ] **B5 — Secret safety:** raw sensitive values are absent from all captured failure channels.
- [ ] **B6 — Portfolio behavior:** all option, condition-input, and user-secret examples bind as designed.
- [ ] **B7 — Evidence recorded:** grammar table, precedence table, and exactly-once counter report are committed.

## Non-goals

No positional arguments, subcommands, response files, shell expansion, configuration file, completion protocol, or
general replacement for System.CommandLine is added without a syntax example and a new decision.
