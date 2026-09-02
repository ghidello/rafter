# Phase 3: bounded parsing and exactly-once binding

## Objective

Parse the intentionally small Rafter command grammar and bind every authored option exactly once into an immutable
invocation snapshot before any graph behavior can run.

## Grammar boundary

The accepted grammar is derived from the option examples. Unsupported command-system features remain errors rather
than accidental compatibility promises.

`.Default(...)` stores an already-computed authored value. Binding does not invoke a default factory.
Phase 2 permits that value only when its type can be copied safely into the frozen model: `string` or a value type
containing no managed references.

## Implementation checklist

### Tokens and option lookup

- [ ] Define option spelling, case sensitivity, long-option format, value separation, Boolean flag behavior, repeated
      occurrence behavior, `--` handling, and unknown-token behavior.
- [ ] Parse a bare Boolean option as `true` and accept explicit `true` or `false` values; do not synthesize `--no-*`
      aliases.
- [ ] Accept long-option values as `--name value` or `--name=value`, splitting only on the first equals sign.
- [ ] Accept short aliases only as `-c value`, plus a bare Boolean alias for `true`; reject `-c=value`, attached
      values, and bundled flags with one clear diagnostic.
- [ ] Preserve original tokens for diagnostics while using normalized tokens for lookup.
- [ ] Match option names and aliases ordinally and case-sensitively on every platform.
- [ ] Detect missing values, duplicate scalar occurrences, unknown options, and unexpected positionals.
- [ ] Report an unknown option without fuzzy suggestions or autocorrection, omit any attached `=value` text from
      diagnostics, and render the ordinary safe help/usage after parse errors.
- [ ] Bind one unsplit value per `RepeatedOption<T>` occurrence, preserve occurrence order, and reject duplicate
      scalar occurrences; do not split comma- or semicolon-delimited text.
- [ ] Reject `--` as unsupported in v1; document `--name=value` for values beginning with hyphens.
- [ ] Recognize reserved `--help` and `-h` before binding and return success without environment reads, validation,
      root resolution, or graph callbacks.
- [ ] Render sensitive metadata and environment variable names but never sensitive defaults or values; render
      non-sensitive scalar defaults invariantly.
- [ ] Render command description and `Usage` first, deriving the invocation name from the executable or file-based
      application without adding a command-name API.
- [ ] Separate authored `Command options` from Rafter-owned `Common options`; list `--plain` and `-h, --help` as the
      common options in v1.
- [ ] Reserve the `plain` long name as well as `help` and `h`; consume `--plain` as a valueless Rafter option before
      binding authored options and make `--plain --help` render the deterministic plain manifest.
- [ ] Render a `Targets` execution-manifest section that explicitly says targets are not command-line selections;
      preserve target declaration order and include required descriptions, authored-order dependencies, entry and
      conditional markers, and callback-free `Aggregate` or `No work` shapes.
- [ ] Do not expose callback, cleanup, permit, or working-directory implementation details, and do not add JSON help
      or target-selection syntax in v1.
- [ ] Render friendly scalar/enum metavariables, optional Boolean values, and `...` only for repeated options; do not
      automatically turn validator failure messages into help text.
- [ ] Let an exact standalone `--help` or `-h` token short-circuit successfully despite other malformed tokens;
      ensure embedded and malformed help-like text does not activate it.
- [ ] Do not synthesize `--version`.
- [ ] Keep help parsing side-effect free and independent from option binding.
- [ ] Represent stable diagnostic kinds internally without displaying public codes in ordinary rich or plain output.
- [ ] Collect ordinary syntax errors in token order rather than stopping after the first malformed input, followed by
      conversion and validation failures in option declaration order with at most one such failure per option.
- [ ] Render all ordinary input failures under one heading and render safe help once afterward; use bullets in rich
      mode and one physical `error: ...` line per diagnostic in plain mode.
- [ ] Include argument position and option name where available. Show only bounded, escaped non-sensitive invalid
      values, render sensitive values as `<redacted>`, and omit an unknown option's attached payload.
- [ ] Retain all collected diagnostics internally but present at most 20 followed by `... and N more errors.`.

### Conversion and precedence

- [ ] Pass `string` through unchanged, apply the dedicated Boolean and enum grammars, unwrap nullable value types,
      and convert every other supported scalar through `IParsable<T>` using invariant culture.
- [ ] Parse an enum as one declared member name with ordinal case-insensitive matching; reject numeric literals,
      undefined values, and implicit comma-separated `[Flags]` combinations while allowing an explicitly declared
      composite member by name.
- [ ] Rely on model freeze to reject enum member-name collisions under ordinal case-insensitive comparison; do not
      give an exact-case token precedence within an otherwise ambiguous enum type.
- [ ] Report the permitted declared enum names deterministically when conversion fails.
- [ ] Test enum names that differ only by case, aliases with distinct names but the same underlying value, and named
      `[Flags]` composites.
- [ ] Test representative framework and application-defined parsable types, including numeric values, `Guid`,
      `DateTime`, and `TimeSpan`.
- [ ] Rely on the model-freeze type check for unsupported types; do not discover an unusable converter during an
      invocation.
- [ ] Implement enum conversion and diagnostics for permitted values.
- [ ] Apply precedence in one documented order: command line, declared environment fallback, authored default, absent.
- [ ] Treat an unset environment variable as absent and a set-but-empty variable as present; apply the same present
      empty-string rule to `--name=`.
- [ ] Make requiredness test source presence rather than string content: an explicit empty value satisfies
      `RequiredOption<string>`, while non-empty or non-whitespace requirements are expressed with validators.
- [ ] Read each declared environment fallback no more than once.
- [ ] Resolve a valid fallback name with the host operating system's ordinary case semantics rather than applying
      Rafter-owned case normalization.
- [ ] Run conversion and validation no more than once for the selected raw value.
- [ ] Apply every `.Validate(predicate, message)` to the resolved command-line, environment, or default value and
      report its authored message on failure.
- [ ] Keep validation synchronous and local to the resolved value; do not run processes, remote checks, or other
      asynchronous work during binding.
- [ ] Run validators once each in authored order and stop evaluating that option at its first failed predicate.
- [ ] Skip validators for complete optional absence; run them for explicit empty values and every required or
      defaulted value.
- [ ] Pass the complete immutable list to repeated-option validators and run them for absence as an empty list.
- [ ] Continue binding independent options after an ordinary conversion or validation rejection and order those
      diagnostics by option declaration after token-level syntax errors.
- [ ] Treat a thrown validator as an authoring failure, preserve its original exception, identify the option safely,
      and do not substitute the validator's ordinary invalid-value message.
- [ ] Abort binding on a thrown validator, retain safe diagnostics already collected, and do not invoke later
      converters or validators.
- [ ] Distinguish a missing optional value from a present value equal to `default(T)`.
- [ ] Resolve optional references as nullable and optional value types through nullable type arguments; never map
      absence to a non-nullable value type's `default(T)`.

### Snapshot and sensitivity

- [ ] Store values by option identity in a read-only invocation snapshot.
- [ ] Resolve absent `RepeatedOption<T>` as an empty immutable `IReadOnlyList<T>` and preserve occurrence order;
      expose no mutable array.
- [ ] Register selected non-empty raw command-line, environment, or default text for a sensitive scalar option before
      conversion, then register any distinct converted representation before validation. For `RepeatedOption<T>`,
      perform both steps independently for every command-line occurrence.
- [ ] Never register an empty string as a redaction pattern.
- [ ] Test sensitive scalar and repeated options across non-string framework and application-defined parsable types.
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
- [ ] Supply multiple independent malformed and invalid options and assert the complete deterministic diagnostic set.
- [ ] Test command-line/environment/default/absence precedence for every scalar option category and command-line
      occurrence/absence behavior for repeated options.
- [ ] Count calls to fallback readers, converters, and validators.
- [ ] Read one option from multiple targets and through multiple context APIs; every counter remains one.
- [ ] Prove repeated results expose no mutable collection and cannot alter the bound snapshot.
- [ ] Verify sensitive invalid input is absent from all diagnostics and exception text.
- [ ] Snapshot zero, one, twenty, and more-than-twenty input errors in rich and plain modes, including safe escaping,
      sensitive replacement, omitted counts, and exactly one following help block.
- [ ] Execute both paths in `validation-failures.cs` and prove neither crosses the invocation barrier.
- [ ] Snapshot help and error output in interactive-neutral form.
- [ ] Snapshot help for executable and file-based names, every option metadata shape, branching dependency graphs,
      entry/conditional/aggregate/no-work targets, and both direct help and parse-error paths.
- [ ] Add fuzz/property tests for parser termination, token preservation, and absence of unhandled exceptions.

## Completion gates

- [ ] **B1 — Grammar locked:** the accepted and rejected grammar table is documented and fully tested.
- [ ] **B2 — Exactly once:** instrumentation proves every binding stage occurs at most once.
- [ ] **B3 — Atomic barrier:** binding-failure tests prove no condition, target, process, or cleanup side effect occurs.
- [ ] **B4 — Immutable values:** repeated-value snapshot tests pass.
- [ ] **B5 — Secret safety:** raw sensitive values are absent from all captured failure channels.
- [ ] **B6 — Portfolio behavior:** all option, condition-input, and user-secret examples bind as designed.
- [ ] **B7 — Evidence recorded:** grammar table, precedence table, and exactly-once counter report are committed.

## Non-goals

- Fuzzy spelling suggestions and automatic correction; reconsider suggestions after v1 usage evidence.

No positional arguments, subcommands, response files, shell expansion, configuration file, completion protocol, or
general replacement for System.CommandLine is added without a syntax example and a new decision.
