# Phase 3: bounded parsing and exactly-once binding

## Objective

Parse the intentionally small Rafter command grammar and bind every authored option exactly once into an immutable
invocation snapshot before any graph behavior can run.

Implementation and verification are recorded in
[`phase-03-parsing-and-binding-evidence.md`](phase-03-parsing-and-binding-evidence.md).

## Grammar boundary

The accepted grammar is derived from the option examples. Unsupported command-system features remain errors rather
than accidental compatibility promises.

`.Default(...)` stores an already-computed authored value. Binding does not invoke a default factory.
Phase 2 permits that value only when its type can be copied safely into the frozen model: `string` or a value type
containing no managed references.

## Questions to resolve before implementation

- [x] **What is the complete Phase 3 `RunAsync` lifecycle?** Define the public exit or exception behavior for model
      diagnostics, help, input diagnostics, validator or converter exceptions, successful binding while graph
      execution is still unavailable, and sequential invocation reuse. Preserve the Phase 2 freeze and overlap
      contracts, and state exactly which later-phase behavior remains stubbed.
- [x] **Which presentation responsibilities belong to Phase 3?** Decide whether Phase 3 renders rich and plain help
      and input diagnostics directly or produces only a semantic report for Phase 6. Define the reusable seam,
      stdout/stderr routing, capability inputs, and injection points now so Phase 6 extends rather than replaces it.
- [x] **What is the exact public `context.Value(...)` contract?** Record the overload and return type for optional,
      required, defaulted, and repeated handles; define nullable-reference behavior, immutable repeated results,
      cross-command failure behavior, and the shared lookup primitive used by later context-owned APIs.
- [x] **How does the parser recover after every malformed token shape?** Approve a table covering Boolean values,
      hyphen-leading values, missing values, unknown options with attached or separate following tokens, unexpected
      positionals, duplicate scalar occurrences, and selection of any value that remains eligible for conversion.
      Ensure recovery is deterministic, terminating, and does not echo text that could be a mistyped secret.
- [x] **How do common options interact with malformed input and each other?** Define exact-token help precedence,
      duplicate `--plain` behavior, malformed common-option forms, and which exact `--plain` token controls help and
      error presentation when help otherwise short-circuits parsing.
- [x] **What is the precise `IParsable<T>` conversion contract?** Choose `TryParse` or `Parse`, specify invariant
      provider use and per-value call counts, classify converter exceptions, handle a successful null result for a
      reference type, and define whether conversion or validation continues for an option that already has a syntax
      diagnostic.
- [x] **How are authored defaults formatted?** Define a bounded invariant help and redaction representation for
      framework, enum, and application-defined parsable value types even though `IParsable<T>` does not imply an
      invariant formatting interface. Decide when a non-sensitive default must be omitted rather than formatted
      through an unstable or culture-sensitive fallback.
- [x] **Where is the sensitive-data trust boundary during binding failures?** Reconcile preservation of original
      application exceptions with redacted Rafter-managed diagnostics and presentation. Define what may remain raw
      in internal values or original exceptions and which captured channels the secret-safety gate actually scans.
- [x] **How is the invocation name discovered?** Define executable and .NET file-based application name discovery
      without adding a command-name API, including deterministic injected test inputs and fallback behavior when
      reliable source or host metadata is unavailable.
- [x] **Which invocation services must be injectable?** Define internal seams for environment lookup,
      invocation-name discovery, console capabilities and streams, conversion, and help/diagnostic sinks so
      exactly-once counters and platform-independent snapshots do not mutate process-global test state.
- [x] **Which portfolio claims can Phase 3 prove without graph execution or the Phase 6 output pipeline?** Separate
      compile fixtures and internal snapshot tests completed here from real multi-target lookup, callback-barrier,
      console-routing, and end-to-end example tests explicitly completed in later phases.
- [x] Reorder the implementation checklist into executable work packages after these answers are recorded: invocation
      services and outcomes; tokenization and grammar; conversion and precedence; immutable snapshot and lookup;
      sensitivity; help and input presentation; `RunAsync` integration; and verification evidence.

**Gate B0 — Initial binding contracts locked:** every question above is checked, its answer is recorded under fixed
decisions, and the implementation work packages and completion gates agree with those answers.

## Fixed decisions

- `RunAsync` retains the Phase 2 null checks, overlap guard, argument snapshot, one-time atomic freeze, permanent
  builder closure, and sequential reuse contract. A failed frozen model is reported safely and returns `2`; help
  cannot bypass an invalid authored model.
- After a successful freeze, an exact help request returns `0` without parsing authored options, reading environment
  fallbacks, converting, validating, resolving roots, or running graph behavior. Ordinary syntax, conversion,
  missing-required, and validator-rejection diagnostics return `2`.
- A converter or validator exception is an application or author failure rather than invalid user input. Rafter
  retains the original exception internally, presents it through the safe failure boundary, and returns `1`.
- Successful Phase 3 binding creates the immutable invocation snapshot and then throws the explicit temporary
  `NotSupportedException` because graph execution is not implemented. Conditions, targets, process factories, and
  target or command cleanup never run in Phase 3. Every terminal path releases the overlap guard.
- Phase 3 owns immutable semantic models for help and command/input diagnostic reports plus rich and deterministic
  plain renderers. The semantic models remain independent from Spectre.Console, and rendering receives injected
  stdout, stderr, and per-stream capability information for deterministic tests.
- Successful help is written to stdout. Invalid frozen models are written to stderr without help because no valid
  model exists. Parsing and binding diagnostics plus their one following safe help block are written to stderr.
- Phase 3 implements the binding-sensitive redaction required by its reports. Phase 6 reuses these report models and
  renderers while adding general semantic output, target attribution, console interception, concurrent coordination,
  stream routing, and the complete cross-channel redaction boundary; it does not replace the Phase 3 presentation
  work.
- `RafterContext` exposes `T? Value<T>(Option<T>)`, `T Value<T>(RequiredOption<T>)`,
  `T Value<T>(DefaultedOption<T>)`, and `IReadOnlyList<T> Value<T>(RepeatedOption<T>)`. Optional references may resolve
  to `null`; optional value types use nullable type arguments such as `Option<int?>`. Required and defaulted handles
  return their declared `T`, and repeated values retain occurrence order in an immutable collection.
- Every `Value(...)` overload reads the same invocation snapshot without repeating source selection, conversion,
  validation, or environment access. It checks command ownership before lookup. A foreign handle throws a safe
  `InvalidOperationException` and becomes a target failure when called from a callback; a missing owned identity is
  an internal invariant failure rather than optional absence. Later context-owned convenience APIs use this same
  identity-checked lookup primitive.
- Long options accept `--name=value`, split only at the first equals sign, or `--name value`. An empty attached value
  is present. A whitespace-separated token is consumed as a value only when it is not option-shaped; hyphen-leading
  values therefore require the long attached form. A missing non-Boolean value is diagnosed without consuming the
  following option-shaped token, while a bare Boolean before an option or the end resolves to `true`. A non-option
  token after a Boolean is consumed and later subjected to Boolean conversion.
- Short aliases accept only `-c value` or the bare Boolean `-c`. Attached, equals, and bundled forms such as
  `-cvalue`, `-c=value`, or `-abc` produce one unsupported-short-syntax diagnostic and are never decomposed.
- An unknown attached long option reports only its name and never retains or displays its payload in diagnostics.
  A separate following token is parsed independently; if positional, its position is reported without its text.
  Unexpected positional text is never displayed because it could be a mistyped sensitive value.
- Every scalar occurrence after the first is a duplicate diagnostic. The first syntactically value-bearing
  occurrence remains eligible for conversion even when an earlier occurrence was missing, so independent conversion
  feedback is still available on an invocation that will fail. Repeated options retain every syntactically
  value-bearing occurrence in order.
- The unsupported `--` token produces a diagnostic but does not change parser mode; later tokens continue through
  the ordinary grammar. "Normalized token" means only structurally decomposed name and value fields: lookup and
  retained values use the exact authored text without trimming, case folding, or Unicode normalization.
- After a successful model freeze, Rafter scans argument elements for exact standalone `--help`, `-h`, and
  `--plain` tokens. Any exact help token short-circuits every other input error; repeated help tokens are idempotent.
  If any exact `--plain` is also present, help uses plain rendering regardless of token order. Attached authored
  values such as `--name=--help` never activate common options.
- Without help, `--plain` may occur once and an exact duplicate is an input diagnostic. Its presence still selects
  plain rendering for all other input diagnostics. Common options are consumed without entering the authored option
  snapshot.
- Forms such as `--help=value`, `--plain=value`, `-h=value`, and attached or bundled short spellings are malformed
  common options and do not activate their behavior; attached payloads are never displayed. `--plain false`
  activates plain mode and leaves `false` as an unexpected positional whose text is not displayed.
- Strings pass through unchanged. Boolean conversion accepts only exact untrimmed `true` or `false` text using
  ordinal case-insensitive comparison. Enums use the declared-name grammar, and nullable value types convert through
  their underlying type; a present empty value is never converted to `null`.
- Every other supported scalar uses a typed, cached `IParsable<T>.TryParse(raw, CultureInfo.InvariantCulture, out
  value)` delegate exactly once per attempted value; Rafter never calls `Parse`. A `false` result is an ordinary
  conversion diagnostic, skips that option's validators, and does not stop independent options. A thrown converter
  preserves its original exception as an author failure and aborts every later converter and validator.
- A reference converter that reports success with a `null` result violates the converter contract and is an author
  failure. Runtime binding cannot reliably recover the caller's nullable-reference annotation, and present input
  never silently becomes absence.
- Syntax diagnostics do not prevent conversion of the first value-bearing candidate or, when no command-line value
  exists, the selected environment/default source. Repeated values convert in occurrence order and stop that option
  at its first conversion failure; an incomplete list is not validated. Independent later options still bind after
  ordinary conversion or validation rejection.
- Validators run only after successful source selection and conversion, once each in authored order, and stop at
  that option's first rejection. "Exactly once" means each source read, conversion attempt, and validator actually
  reached by this deterministic flow runs at most once; later work is not forced after an earlier abort or rejection.
- Rafter uses the exact bounded formatter policy below for model normalization, help, diagnostics, and later semantic
  output. It never invokes arbitrary application `ToString()` or application `IFormattable` implementations merely
  to render help.
- An enum authored default must correspond to a declared member; an unnamed value is a model diagnostic. When enum
  aliases share an underlying value, the first declared member name is the canonical representation. Approved
  default representations are computed and retained once during model normalization rather than reformatted by
  each help invocation.
- A non-sensitive application-defined default without an approved invariant formatter remains valid, but help shows
  only `default: configured`. A sensitive default without an approved invariant formatter is rejected at model
  freeze with guidance to use a string or an approved invariantly formatted value, because Rafter cannot derive a
  stable redaction pattern. Sensitive default text is never displayed even when its representation is retained for
  redaction.
- Resolved raw and typed values, the redaction-pattern registry, and preserved original converter or validator
  exceptions are internal data and may contain secrets. Rafter never mutates an original exception to create a
  misleading sanitized object. These internal objects are outside the no-secret presentation gate.
- Binding parses the complete command line and immediately registers every non-empty occurrence associated with a
  known sensitive option, including duplicate or rejected occurrences, before the first environment lookup. It then
  selects every option source before invoking any converter, reads each required environment fallback once, and
  immediately registers each selected non-empty sensitive environment value or approved authored default before the
  next fallible operation. An environment lookup exception is an infrastructure failure: Rafter retains it
  internally, renders no arbitrary exception details, and reports only through the registry accumulated so far. The
  complete raw registry exists before the first converter can throw. Binding adds each distinct stable converted
  representation before validating that value and freezes the registry before any execution context exists. Empty
  text is never a redaction pattern.
- Rafter ordinary output never renders `Exception.ToString()`, stack traces, `Data`, or arbitrary exception
  properties. A sensitive converter or validator failure shows only the safe option name, lifecycle stage, and
  exception type. A non-sensitive failure may show a bounded escaped message after redaction. Unexpected positional
  text and unknown attached payloads remain hidden because Rafter cannot classify them as safe.
- The secret-safety gate scans every Rafter-produced semantic report and captured stdout/stderr byte. It explicitly
  excludes the internal binding snapshot, redaction registry, and preserved original exception. Application-created
  transformations of a secret remain caller-owned, and v1 has no manual registration API.
- Invocation-name discovery captures its inputs once. For a .NET file-based app, Rafter first uses the non-empty
  `AppContext` `EntryPointFilePath` host setting and takes its filename without extension. This setting is supplied
  by the .NET 10 file-app host as documented in the [.NET SDK file-app design](https://github.com/dotnet/sdk/blob/main/documentation/general/dotnet-run-file.md).
  Otherwise Rafter uses a non-`dotnet` process executable stem, then the entry assembly simple name, then a usable
  launch-token stem, and finally the stable fallback `command`. Only the resulting leaf name is rendered.
- Phase 3 introduces one immutable internal invocation-services boundary. Production services capture environment
  lookup, invocation identity inputs, exact stdout/stderr writers, and each stream's capabilities once per
  invocation. Tests inject deterministic equivalents without changing environment variables, console globals, host
  metadata, or terminal state. Conversion remains a pure typed strategy exercised with instrumented parsable test
  types rather than an interchangeable production service.
- Phase 3 proves grammar, binding, report rendering, repeated snapshot lookup, callback non-entry, and every public
  option/`Value(...)` shape through phase-safe fixtures. Tests may create multiple internal contexts over one snapshot
  but do not claim real multi-target execution. Phase 5 proves lookup through executing targets and conditions;
  Phase 6 proves final console routing and cross-channel presentation; phases 7 and 8 add process paths; Phase 9 runs
  the complete canonical portfolio.

### Approved default representations

| Type | Canonical retained representation |
| --- | --- |
| `string` | Exact value internally; quoted and escaped with the bounded presentation grammar below |
| `bool` | Lowercase `true` or `false` |
| `char` | Exact one-character string internally; quoted and escaped with the presentation grammar below |
| `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `Int128`, `UInt128`, `nint`, `nuint` | Invariant decimal |
| `Half`, `float`, `double` | Invariant round-trip `R` |
| `decimal` | Invariant `G29` |
| `Guid` | Lowercase `D` |
| `DateTime`, `DateTimeOffset` | Invariant round-trip `O` |
| `DateOnly`, `TimeOnly` | Invariant `O` |
| `TimeSpan` | Invariant constant `c` |
| enum | Canonical declared member name |

An authored default cannot be `null`: null represents optional absence rather than a resolved default value. An
undefined enum default is a model diagnostic. When several declared enum names share one underlying value, the first
declared member is canonical. Every other snapshot-safe application-defined default remains usable but is shown as
`default: configured`; if sensitive, it is rejected because it has no approved stable redaction representation. The
whitelist is exact rather than interface-based, so model normalization never executes application formatting code.

The bounded string and character presentation grammar is exact:

- Surround the result with ASCII double quotes.
- Enumerate Unicode scalar values, treating an isolated UTF-16 surrogate code unit as one invalid element. A string
  retains at most the first 64 elements; if more remain, append the ASCII truncation marker `...` before the closing
  quote. A character is never truncated.
- Escape `"` as `\"` and `\` as `\\`. Escape backspace, tab, line feed, form feed, and carriage return as `\b`,
  `\t`, `\n`, `\f`, and `\r`.
- Render every other Unicode control, format, line-separator, or paragraph-separator scalar, plus every isolated
  surrogate, as an uppercase hexadecimal escape: `\uXXXX` for a BMP value or UTF-16 code unit and `\UXXXXXXXX` for
  a supplementary scalar. Render every remaining scalar unchanged.

The 64-element limit is applied before escaping, so the maximum rendered length remains finite even when every
retained element expands to an escape. The delimiters and truncation marker do not belong to the exact internal value
used for binding or redaction.

### Phase 3 text-redaction contract

- Redaction uses exact ordinal UTF-16 matching with no trimming, casing change, or Unicode normalization. Callers
  receive protection for the exact selected representations; canonically equivalent or otherwise transformed
  application-created text remains caller-owned.
- Registry construction ignores empty patterns and deduplicates exact duplicates. For a complete text value, the
  redactor finds every occurrence of every pattern against the original text, merges overlapping match intervals,
  and replaces each merged interval so a shorter match cannot leave the remainder of a longer overlapping secret.
- Substring, Unicode, and multiline patterns use the same interval algorithm. Phase 3 always redacts a complete
  semantic text value before rendering it; Phase 6 reuses this algorithm and adds buffering across streaming chunk
  and partial-line boundaries.
- The replacement marker is selected so it contains no registered pattern. Every renderer buffers a complete report
  and verifies the final serialized text, including rich control sequences and renderer-owned labels, after semantic
  redaction. If it cannot select a safe marker or any registered pattern remains, it discards the entire buffer and
  returns an infrastructure failure without writing a prefix or other uncertain text.
- Replacement output is treated as trusted renderer data and is not recursively redacted. Rich renderers escape the
  marker as data rather than interpreting it as Spectre markup.

## Implementation checklist

### Work package 1: invocation services and outcomes

- [x] Define immutable internal invocation services for environment lookup, invocation identity, exact output
      writers, and independent stream capabilities; provide production capture and deterministic test factories.
- [x] Capture every process-global input once at invocation entry and never mutate environment variables, console
      writers, current culture, or host metadata during tests.
- [x] Implement the file-based, executable, entry-assembly, launch-token, and stable-fallback invocation-name policy;
      retain only the safe leaf name needed by help.
- [x] Define internal outcomes for invalid model, successful help, ordinary input rejection, environment-lookup,
      converter/validator, or presentation infrastructure failure, and successful binding so `RunAsync` maps each
      path exactly once.
- [x] Extend model normalization with canonical enum-default validation and the bounded default-format representation;
      implement every exact whitelisted type and format without invoking application formatting code.
- [x] Keep invocation services and structured outcomes internal; Phase 3 adds no hosting or structured-result public
      API.

### Work package 2: tokenization and grammar

- [x] Define option spelling, case sensitivity, long-option format, value separation, Boolean flag behavior, repeated
      occurrence behavior, `--` handling, and unknown-token behavior.
- [x] Parse a bare Boolean option as `true` and accept explicit `true` or `false` values; do not synthesize `--no-*`
      aliases.
- [x] Accept long-option values as `--name value` or `--name=value`, splitting only on the first equals sign.
- [x] Accept short aliases only as `-c value`, plus a bare Boolean alias for `true`; reject `-c=value`, attached
      values, and bundled flags with one clear diagnostic.
- [x] Preserve original tokens for diagnostics while using normalized tokens for lookup.
- [x] Match option names and aliases ordinally and case-sensitively on every platform.
- [x] Detect missing values, duplicate scalar occurrences, unknown options, and unexpected positionals.
- [x] Report an unknown option without fuzzy suggestions or autocorrection, omit any attached `=value` text, and
      report an unexpected positional by position without displaying its text.
- [x] Bind one unsplit value per `RepeatedOption<T>` occurrence, preserve occurrence order, and reject duplicate
      scalar occurrences; do not split comma- or semicolon-delimited text.
- [x] Reject `--` as unsupported in v1; document `--name=value` for values beginning with hyphens.
- [x] Let an exact standalone `--help` or `-h` token short-circuit successfully despite other malformed tokens;
      ensure embedded and malformed help-like text does not activate it, and make repeated exact help idempotent.
- [x] Consume one exact `--plain` as a common option, diagnose later occurrences when help is absent, and let any
      exact `--plain` select deterministic plain rendering for help or input diagnostics.
- [x] Diagnose malformed common-option forms without activating them or exposing attached payloads; never synthesize
      `--version`.
- [x] Keep help parsing side-effect free and independent from option binding.
- [x] Represent stable diagnostic kinds internally without displaying public codes in ordinary rich or plain output.
- [x] Collect ordinary syntax errors in token order rather than stopping after the first malformed input, followed by
      conversion and validation failures in option declaration order with at most one such failure per option.
- [x] Include argument position and option name where available. Show only bounded, escaped non-sensitive invalid
      values, render sensitive values as `<redacted>`, and omit an unknown option's attached payload.
- [x] Prove every recovery step consumes or advances past a token, preserves exact source text only where required,
      and terminates for arbitrary bounded argument arrays.

### Work package 3: conversion and precedence

- [x] Pass `string` through unchanged, apply the dedicated Boolean and enum grammars, unwrap nullable value types,
      and convert every other supported scalar through a cached typed `IParsable<T>.TryParse` delegate using
      invariant culture exactly once per attempted value; never call `Parse`.
- [x] Parse an enum as one declared member name with ordinal case-insensitive matching; reject numeric literals,
      undefined values, and implicit comma-separated `[Flags]` combinations while allowing an explicitly declared
      composite member by name.
- [x] Rely on model freeze to reject enum member-name collisions under ordinal case-insensitive comparison; do not
      give an exact-case token precedence within an otherwise ambiguous enum type.
- [x] Report the permitted declared enum names deterministically when conversion fails.
- [x] Test enum names that differ only by case, aliases with distinct names but the same underlying value, and named
      `[Flags]` composites.
- [x] Test representative framework and application-defined parsable types, including numeric values, `Guid`,
      `DateTime`, and `TimeSpan`.
- [x] Rely on the model-freeze type check for unsupported types; do not discover an unusable converter during an
      invocation.
- [x] Treat `TryParse == false` as ordinary invalid input and a thrown converter or successful null reference result
      as an author failure that preserves the original exception where one exists and aborts later binding work.
- [x] Apply precedence in one documented order: command line, declared environment fallback, authored default, absent.
- [x] Treat an unset environment variable as absent and a set-but-empty variable as present; apply the same present
      empty-string rule to `--name=`.
- [x] Make requiredness test source presence rather than string content: an explicit empty value satisfies
      `RequiredOption<string>`, while non-empty or non-whitespace requirements are expressed with validators.
- [x] Read each declared environment fallback no more than once.
- [x] Resolve a valid fallback name with the host operating system's ordinary case semantics rather than applying
      Rafter-owned case normalization.
- [x] Treat a thrown environment lookup as an infrastructure failure, retain the original exception internally, omit
      its arbitrary details from presentation, abort binding, and do not invoke converters or validators.
- [x] Complete source selection for every option and all once-only environment reads before invoking the first
      converter, so every selected raw sensitive representation is registered before application conversion code can
      throw.
- [x] Convert the first value-bearing scalar occurrence even when the option also has syntax diagnostics; when no
      command-line value exists, continue precedence through environment, default, and absence.
- [x] Apply every `.Validate(predicate, message)` to the resolved command-line, environment, or default value and
      report its authored message on failure.
- [x] Keep validation synchronous and local to the resolved value; do not run processes, remote checks, or other
      asynchronous work during binding.
- [x] Run validators once each in authored order and stop evaluating that option at its first failed predicate.
- [x] Skip validators for complete optional absence; run them for explicit empty values and every required or
      defaulted value.
- [x] Pass the complete immutable list to repeated-option validators and run them for absence as an empty list.
- [x] Continue binding independent options after an ordinary conversion or validation rejection and order those
      diagnostics by option declaration after token-level syntax errors.
- [x] Treat a thrown validator as an authoring failure, preserve its original exception, identify the option safely,
      and do not substitute the validator's ordinary invalid-value message.
- [x] Abort binding on a thrown validator, retain safe diagnostics already collected, and do not invoke later
      converters or validators.
- [x] Convert repeated occurrences in order, stop that option at its first conversion failure, never validate an
      incomplete repeated list, and continue independent options after ordinary rejection.
- [x] Distinguish a missing optional value from a present value equal to `default(T)`.
- [x] Resolve optional references as nullable and optional value types through nullable type arguments; never map
      absence to a non-nullable value type's `default(T)`.

### Work package 4: immutable snapshot and lookup

- [x] Store values by option identity in a read-only invocation snapshot.
- [x] Resolve absent `RepeatedOption<T>` as an empty immutable `IReadOnlyList<T>` and preserve occurrence order;
      expose no mutable array.
- [x] Implement the four typed `context.Value(...)` overloads with their approved nullable, required, defaulted, and
      immutable repeated return shapes.
- [x] Make every lookup enforce command ownership and distinguish a foreign-handle programming failure from a
      missing-owned-identity invariant failure.
- [x] Provide the same lookup primitive to context-owned APIs so convenience overloads do not rebind.
- [x] Prove repeated lookups through multiple contexts over one snapshot do not reread, reconvert, or revalidate.

### Work package 5: sensitivity registry

- [x] Build a mutable invocation-local registry during binding, then freeze it before exposing any execution context.
- [x] Immediately after parsing, register every non-empty raw occurrence associated with a known sensitive option,
      including duplicate or rejected scalar occurrences and each repeated occurrence, before any environment lookup.
- [x] Register a selected non-empty sensitive environment value immediately after its successful lookup and an
      approved authored-default representation immediately when that default becomes selected; do not defer either
      registration past another fallible operation.
- [x] Register each distinct stable converted representation before validating that value; never register an empty
      string.
- [x] Implement exact ordinal matching, duplicate removal, original-text match discovery, and merged overlapping
      intervals for complete substring, Unicode, and multiline semantic text.
- [x] Select a replacement marker containing no registered pattern, verify no pattern remains after redaction, and
      suppress the complete report as an infrastructure failure if marker selection or verification cannot be made
      safe.
- [x] Keep raw snapshots, patterns, and original application exceptions internal without claiming they are sanitized.
- [x] Ensure sensitive converter and validator failure reports omit application messages and arbitrary exception
      details; pass every other rendered message through the accumulated redactor.
- [x] Test sensitive scalar and repeated options across strings, framework scalars, and application-defined parsable
      types while respecting the approved stable-representation boundary.

### Work package 6: help and input presentation

- [x] Build immutable Spectre-independent semantic models for successful help, model failures, ordinary input
      failures, and author failures.
- [x] Render command description and `Usage` first with the captured invocation name and no command-name API.
- [x] Separate authored `Command options` from Rafter-owned `Common options`; list `--plain` and `-h, --help` as the
      common options in v1.
- [x] Render sensitive metadata and environment names but never sensitive defaults or values. Render approved
      non-sensitive defaults invariantly and use `default: configured` when no approved formatter exists.
- [x] Render a `Targets` execution-manifest section that explicitly says targets are not command-line selections;
      preserve target declaration order and include descriptions, authored-order dependencies, entry and conditional
      markers, and callback-free `Aggregate` or `No work` shapes.
- [x] Do not expose callback, cleanup, permit, or working-directory implementation details, and do not add JSON help
      or target-selection syntax in v1.
- [x] Render friendly scalar/enum metavariables, optional Boolean values, and `...` only for repeated options; do not
      automatically turn validator failure messages into help text.
- [x] Implement injected rich and width-independent plain renderers. Successful help goes to stdout; model failures
      go to stderr without help; ordinary input failures and one following safe help block go to stderr.
- [x] Buffer each complete rendered report, verify its final serialized text against the registry, and write it only
      after verification succeeds so renderer labels, escaping, markup, and control sequences cannot reintroduce a
      registered pattern or leak a partial report.
- [x] Render ordinary input failures under one heading, using rich bullets or one physical `error: ...` line each in
      plain mode. Retain every diagnostic internally but show at most 20 followed by `... and N more errors.`.
- [x] Never render raw exception objects. Show only the approved bounded safe exception information for author
      failures.

### Work package 7: `RunAsync` integration and barrier

- [x] Complete all parsing, fallback reads, defaults, conversion, validation, and sensitivity registration before
      evaluating any condition.
- [x] Map invalid model and ordinary input outcomes to `2`, help to `0`, and environment-lookup, converter, validator,
      redaction, or presentation failure to `1`, with the approved report and stream behavior.
- [x] On successful binding, create the immutable snapshot and then throw the temporary Phase 3
      `NotSupportedException` without evaluating conditions or callbacks.
- [x] Do not create execution contexts on binding failure. Prove conditions, target callbacks, and target or command
      cleanup cannot run on any rejected Phase 3 path.
- [x] Release the overlap guard on every terminal path and preserve sequential reuse of a successfully frozen command.

### Work package 8: verification evidence

- [x] Complete the required verification below after every preceding work package passes its focused tests.
- [x] Record the grammar table, precedence table, invocation outcome table, presentation routing table, formatter
      policy, sensitivity trust boundary, exactly-once counters, and deferred cross-phase coverage.

## Required verification

- [x] Table-test every grammar rule, including empty input and malformed final tokens.
- [x] Supply multiple independent malformed and invalid options and assert the complete deterministic diagnostic set.
- [x] Test invalid model, rich/plain help, ordinary input rejection, converter/validator author failure, successful
      binding stub, overlap rejection, and sequential reuse against the complete Phase 3 invocation outcome table.
- [x] Test command-line/environment/default/absence precedence for every scalar option category and command-line
      occurrence/absence behavior for repeated options.
- [x] Count calls to fallback readers, converters, and validators.
- [x] Make injected fallback readers throw before and after earlier sensitive values are registered; assert exit `1`,
      no later binding work, retained internal exception identity, safe presentation, and no registered pattern in
      captured output.
- [x] Read each handle repeatedly through multiple internal contexts over one bound snapshot; every binding counter
      remains one. Defer lookup from actually executing targets and conditions to Phase 5.
- [x] Prove repeated results expose no mutable collection and cannot alter the bound snapshot.
- [x] Verify sensitive invalid input is absent from every Rafter semantic report and captured stdout/stderr byte;
      separately prove preserved internal values and original exceptions retain their identity without treating them
      as sanitized presentation artifacts.
- [x] Snapshot zero, one, twenty, and more-than-twenty input errors in rich and plain modes, including safe escaping,
      sensitive replacement, omitted counts, and exactly one following help block.
- [x] Execute a Phase 3-safe fixture matching both input-rejection and thrown-validator paths from
      `validation-failures.cs`; prove neither crosses the invocation barrier. Run the canonical file end to end only
      after its output and execution APIs exist.
- [x] Snapshot help and error output in interactive-neutral form.
- [x] Snapshot help for executable and file-based names, every option metadata shape, branching dependency graphs,
      entry/conditional/aggregate/no-work targets, and both direct help and parse-error paths.
- [x] Table-test every approved default type and exact format under changed current cultures, plus null, undefined
      and aliased enum defaults, application-defined configured-only defaults, every escape class, the 64-element
      boundary, truncation, supplementary scalars, and isolated surrogates.
- [x] Test duplicate, overlapping, substring, one-character, Unicode, canonically distinct, multiline,
      marker-collision, and verification-failure redaction cases without exposing any registered pattern.
- [x] Test invocation-name discovery from injected file-app metadata, executable, `dotnet` host plus entry assembly,
      launch token, malformed inputs, and the stable fallback without reading real test-host identity.
- [x] Add deterministic, fixed-seed fuzz/property tests with bounded token count and length for parser termination,
      token preservation, recovery progress, secret-safe diagnostics, and absence of unhandled ordinary-input
      exceptions.
- [x] Compile every Phase 3 public option and `Value(...)` shape through output-free fixtures; record the canonical
      examples whose real callback, console, process, or filesystem paths remain assigned to later phases.

## Completion gates

- [x] **B0 — Initial contracts locked:** every blocking question is answered, recorded, and reflected in the ordered
      work packages and completion gates.
- [x] **B1 — Grammar locked:** the accepted and rejected grammar table is documented and fully tested.
- [x] **B2 — Exactly once:** instrumentation proves every binding stage occurs at most once.
- [x] **B3 — Atomic barrier:** rejected and successful-stub paths prove no condition, target, or cleanup callback runs;
      process construction and execution remain explicitly deferred to phases 7 and 8.
- [x] **B4 — Immutable values:** repeated-value snapshot tests pass.
- [x] **B5 — Secret presentation safety:** raw sensitive values are absent from all Rafter reports and captured output;
      overlap, marker-collision, and fail-closed tests pass, while the explicitly raw internal snapshot, registry,
      and preserved original exception remain outside this gate.
- [x] **B6 — Phase 3 portfolio contract:** every option and `Value(...)` syntax shape compiles, equivalent phase-safe
      fixtures bind as designed, and deferred end-to-end example coverage is assigned to named later phases.
- [x] **B7 — Presentation seam:** rich/plain help and diagnostic snapshots, stream routing, invocation naming, and the
      Phase 6 reuse boundary are approved and deterministic.
- [x] **B8 — Evidence recorded:** grammar, precedence, invocation outcome, routing, formatter, sensitivity, and
      exactly-once evidence is committed.

## Non-goals

- Fuzzy spelling suggestions and automatic correction; reconsider suggestions after v1 usage evidence.

No positional arguments, subcommands, response files, shell expansion, configuration file, completion protocol, or
general replacement for System.CommandLine is added without a syntax example and a new decision.
