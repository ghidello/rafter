# Rafter example design plan

Draft for ongoing discussion.

We will shape the new Rafter through small, realistic example scripts, discuss their syntax and behavior, and then
implement connected groups of examples. The aim is to make scripts pleasant to write while checking design assumptions
against working code early.

The original scripts live in [reference/examples](../reference/examples/). They provide scenarios to consider; their
APIs and feature set are not requirements for the new implementation. Keep those copies unchanged so comparisons remain
possible. Current syntax drafts live in [examples](../examples/).

See [First working examples](first-working-examples.md) for the consistency review, proposed implementation scope,
remaining decision groups, and issue outlines. Its recommendations remain proposals until accepted.
See [Output and help](output-design.md) for the current agreed visual direction and remaining presentation decisions.
Use the [dependency review](dependency-decisions.md) when choosing the SDK, runtime packages, analyzer tooling, and
test dependencies before implementation. Its shortlist remains proposed until selection and compatibility checks.

## Working approach

1. Choose a topic and a concrete script scenario.
2. Draft the intended consumer code before implementing the supporting library. Placeholder work is acceptable when
   the example still makes its purpose and intended behavior clear.
3. Discuss syntax together with observable behavior: what executes, which values are available, how failures stop work,
   and what the user sees.
4. Record agreed decisions and open questions here. Consider alternatives when they resolve a concrete difficulty.
5. Implement a small group of connected examples, then use the working scripts to review the design.
6. Verify the agreed behavior and update the topic's progress with links to the relevant examples and evidence.

We do not need to design every topic before implementing the first group, or finish every edge case before exploring
the next topic. Agreements can be revised when implementation or actual use reveals a better approach.

## GitHub issue workflow

The [repository workflow](github-workflow.md) now records verified identities, PR/merge rules, current enforcement,
and the [First working examples milestone](https://github.com/sotsera/rafter/milestone/1). Evaluation issues #1–#3 are
published upstream; the later runtime issues remain to be scoped as the evaluations conclude.

Keep the initial syntax discussions, decisions, and open questions in Markdown. Introduce GitHub issues after agreeing
on the first group of examples and before implementation begins. Do not create one issue per topic in advance;
topics may merge, split, or be deferred during design.

- Use this plan for design direction and topic progress.
- Use issues for scoped implementation work, bugs, and investigations, with links to the relevant design notes.
- Use focused pull requests to deliver changes and link them to their issues.
- Each issue should describe the intended behavior or problem, scope, relevant examples, acceptance criteria,
  and appropriate verification. Record exclusions where they clarify the scope.
- Close an issue when its scoped work is complete. A broader topic may remain partially complete in this plan.
  Avoid duplicating detailed acceptance checklists between issues and this document.

Once the first group is agreed, organize its implementation under a **First working examples** milestone with a few
concrete issues. Verify the target repository and establish the contribution workflow before creating GitHub resources.

## Progress tracking

Track these milestones independently:

- **Drafted:** a proposed example shows the intended authoring experience.
- **Agreed:** the syntax and observable behavior for the selected scope have been discussed and accepted.
- **Implemented:** the example runs against the new implementation.
- **Verified:** appropriate checks demonstrate the agreed behavior, with evidence linked from the topic notes.

In the table, `—` means pending and `✓` means complete. A topic can instead be marked **Deferred** or **Excluded**, with
the reason recorded in its notes. The initial inventory does not commit us to implementing every topic. Split a topic
into smaller scopes when needed; agreement on basic inputs, for example, does not complete all input types or policies.

| Topic | Drafted | Agreed | Implemented | Verified |
| --- | --- | --- | --- | --- |
| Minimal script | ✓ | ✓ | ✓ | ✓ |
| Roots and paths | — | — | — | — |
| Typed inputs | ✓ | Partial | Partial | Partial |
| Input validation | ✓ | ✓ | ✓ | ✓ |
| Environment inputs | — | — | — | — |
| Targets and dependencies | ✓ | ✓ | Partial | Partial |
| Conditional execution | — | — | — | — |
| Output and diagnostics | Partial | Partial | — | — |
| Failures | — | — | — | — |
| External processes | ✓ | — | — | — |
| Reusable process settings | — | — | — | — |
| Captured output | — | — | — | — |
| Cancellation and timeouts | — | — | — | — |
| Cleanup and shared resources | — | — | — | — |
| Sensitive values | — | — | — | — |
| Complete workflow | — | — | — | — |

## Topic inventory

Several examples cover more than one topic. The links below account for all 23 reference scripts.

| Topic | Questions to discuss | Reference examples |
| --- | --- | --- |
| Minimal script | How much must a greeting declare? Command creation, descriptions, callbacks, invocation, and exit results. | [minimal.cs](../reference/examples/minimal.cs) |
| Roots and paths | What is the default root? How should invocation, source, and explicit directories affect paths? | [root-invocation.cs](../reference/examples/root-invocation.cs), [root-source.cs](../reference/examples/root-source.cs), [root-explicit.cs](../reference/examples/root-explicit.cs) |
| Typed inputs | Required, optional, and defaulted values; aliases; flags; strings, integers, enums, and durations; value access and nullability. | [options.cs](../reference/examples/options.cs), [option-types.cs](../reference/examples/option-types.cs), [durations.cs](../reference/examples/durations.cs) |
| Input validation | Individual checks, relationships between inputs, validation timing, and how multiple rejections are reported. | [validators.cs](../reference/examples/validators.cs), [input-rules.cs](../reference/examples/input-rules.cs) |
| Environment inputs | Explicit mappings, precedence, empty values, conversion, and help behavior. | [environment.cs](../reference/examples/environment.cs) |
| Targets and dependencies | Entry selection, execution order, shared dependencies, groups, and invalid graphs. | [dependencies.cs](../reference/examples/dependencies.cs) |
| Conditional execution | When is a condition evaluated? Does skipping affect a body, its dependencies, or an entire branch? | [conditions.cs](../reference/examples/conditions.cs), [rafter.cs](../reference/examples/rafter.cs) |
| Output and diagnostics | Results, information, warnings, help, stream routing, and plain, quiet, or verbose output. | [diagnostics.cs](../reference/examples/diagnostics.cs), [minimal.cs](../reference/examples/minimal.cs) |
| Failures | Expected failures, unexpected exceptions, process failures, exit codes, and stopping subsequent callback statements. | [diagnostics.cs](../reference/examples/diagnostics.cs), [processes.cs](../reference/examples/processes.cs) |
| External processes | Executables, argument tokens, working directories, child environments, streaming, and process results. | [processes.cs](../reference/examples/processes.cs) |
| Reusable process settings | Sharing defaults, applying overrides, and resolving input-dependent settings. | [process-configuration.cs](../reference/examples/process-configuration.cs) |
| Captured output | Reading structured results, optional live echo, capture limits, overflow, and availability of captured data. | [process-capture.cs](../reference/examples/process-capture.cs) |
| Cancellation and timeouts | Cooperative managed work, child shutdown, grace periods, independent deadlines, and actual completion. | [process-cancellation.cs](../reference/examples/process-cancellation.cs), [process-timeout.cs](../reference/examples/process-timeout.cs), [managed-timeout.cs](../reference/examples/managed-timeout.cs), [cleanup-timeout.cs](../reference/examples/cleanup-timeout.cs) |
| Cleanup and shared resources | Target and command lifetimes, state shared between steps, cleanup eligibility, and combined failures. | [cleanup.cs](../reference/examples/cleanup.cs), [cleanup-timeout.cs](../reference/examples/cleanup-timeout.cs) |
| Sensitive values | Declaring sensitive inputs, redacting presentation, raw value access, and the boundaries of protection. | [redaction.cs](../reference/examples/redaction.cs) |
| Complete workflow | Does the combined API remain readable in a build, test, and optional packaging script? | [rafter.cs](../reference/examples/rafter.cs) |

## First group of examples

Proposed order:

1. **Minimal script:** perform one small action and return an outcome.
2. **Basic inputs:** add a required value, a defaulted value, and a Boolean flag.
3. **One external process:** invoke a tool with explicit arguments and handle its outcome.
4. **Two dependent targets:** connect real steps and establish what happens when the first step fails.

After discussing these examples, implement the smallest working foundation that supports them. Include the root,
output, and failure decisions necessary for that foundation, leaving broader policies open. Then review the scripts
as a group before expanding the API.

## Questions raised by the reference examples

These observations are starting points for discussion, not decisions about the new API.

- **Initial ceremony:** the greeting requires a root policy, command description, target name, target description,
  callback, and explicit entry selection. Which should be required, optional, or supplied by a default?
- **Input access:** declarations express useful guarantees, but larger scripts repeat `context.Value(...)` frequently.
  Can reading values be simpler while preserving validation timing and type guarantees?
- **Stopping after failure:** process consumers repeat `if (!result.Succeeded) { return; }`, and `context.Fail(...)`
  records a failure without stopping subsequent statements. What should the normal failure path look like?
- **Conditional branches:** dependencies run before a condition, and dependents can continue after a skip. The packaging
  workflow repeats one condition on two targets. What should a condition govern?
- **Shared state and cleanup:** the cleanup example captures nullable variables and uses `sharedFile!`. How should
  resources and values pass between steps, and when should their lifetime end?
- **Implementation details in scripts:** examples repeat a timeout bound based on `uint.MaxValue` and use
  `result.Capture!` after requesting capture. Which constraints and guarantees should the library express directly?

## Decisions and discussion notes

Established foundations:

- Keep Rafter, Sotsera branding, and `Sotsera.Rafter` naming.
- Make script output visually clear and pleasant for humans while keeping descriptions, help, and diagnostics useful
  to agents. The initial human/plain presentation is recorded in [Output and help](output-design.md); broader output
  details and any machine-readable interface remain open design topics.
- Start with a fresh implementation and retain the old examples as reference material.
- Use Markdown for project documents.
- Explore examples and behavior before implementing small connected groups, with separate design and delivery progress.

As topics are discussed, add a subsection for each with its scenario, proposed example link, agreed syntax and behavior,
open questions, and implementation or verification evidence. Record deferrals and exclusions there too. Keep the
progress table aligned with those notes, and revise superseded decisions so the current direction remains clear.

### Output and diagnostics

The [output design](output-design.md) records the direction accepted for now: compact target sections, consistent
indentation, visible descriptions, own-work target timings, one final invocation result, terminal-only result/child
prefixes, clean redirected results, grouped input errors, and separate command/execution/Rafter help sections.
The selected flatter layout puts target descriptions at column zero and uses one two-space content/diagnostic gutter,
preserving original content indentation. Cancellation-request messages are yellow in styled output and hidden in quiet mode.
Results and help go to stdout; lifecycle and diagnostics go to stderr; child streams retain their destinations.
Help lists primary option names first, then aliases in declaration order, separated by commas. Capable stdout terminals
style help headings and names, with equal emphasis for primary names and aliases, muted separators, and distinct,
readable type hints. The selected preview palette uses lavender type hints alongside blue option names.
Plain or redirected help preserves the text without Rafter styling.
Normal target headings include direct dependencies in declaration order: `[test] <-- (build)`, with the suffix
muted in styled output and retained in plain text. Entry groups use the same notation, such as
`> verify <-- (build, test)`. Omit the suffix when there are no dependencies; quiet hides these routine headings.
Body-only scripts do not acquire an implicit target. Normal, `--quiet`, `--verbose`, and independent `--plain` controls
are agreed as described in the output design. Quiet and verbose are mutually exclusive; redirected streams automatically
use plain presentation. Help organization for options, individual/combined validation rules, and the selected dependency
workflow is accepted through the three preview help examples. Multiple-entry headers and final summaries preserve
the supplied entry order; target sections follow dependency execution order, counting each executed body once and
excluding dependency-only groups from that count. Do not introduce a synthetic group name or imply that one entry
owns all selected work. In the first version, process output identifies the executable; verbose output and relevant
failure diagnostics include the child working directory. Rafter does not automatically print arguments or environment
values, even in verbose mode. Explicit selected-argument disclosure with redaction is deferred; script/child output
and exception details retain their existing policies. Further verbose content and remaining failure transcripts are still open. The old
[output previews](../reference/output/README.md) are preserved unchanged for comparison.

Failure detail is agreed: invalid declarations/inputs use grouped diagnostics without stacks; rejected process exits
identify the applicable target, executable, actual code, and accepted codes without framework stacks. Unexpected callback
exceptions retain scope, type, message, inner exceptions, and full stack traces in every verbosity mode, including quiet
and plain/redirected output. Final execution summaries identify the outcome and stopping point without repeating details.
Process diagnostics use scenario 18's compact status/executable heading and aligned details. Handled `Continue`
failures use yellow; unhandled or recorded invocation failures use red. Compact callback-failure headings retain
full exception details, and plain output retains all wording without color. This presentation does not change failure actions.

The topic is partially drafted and agreed. The new [HTML comparison](output-preview.html) contains 23 illustrative
scenarios with terminal/plain, stream, verbosity, and width controls. Remaining diagnostic wording
remains proposed for review. The [minimal runtime](minimal-runtime.md) now verifies body-only output and help;
[target output](targets.md) is now implemented too; processes remain pending. Historical previews and synthetic
fixtures are not runtime evidence.

### Minimal script

Scenario: print a greeting using a single command. [examples/minimal.cs](../examples/minimal.cs) is now runnable
against the new runtime through a project reference. See [implementation and verification](minimal-runtime.md).

Agreed:

- Commands declare metadata and inputs. Select execution at invocation time: `RunAsync(args, body)` runs a supplied
  synchronous or asynchronous callback; `RunAsync(args, target, additionalTargets)` runs selected targets and dependencies,
  with one required target and a `params` array of additional targets.
- Commands have no `.Run(...)` method or stored body. Target declarations retain `.Run(...)` to register their bodies.
- The command description is mandatory and must contain non-whitespace text. Non-null alone is insufficient.
  Descriptions should explain the script's purpose to both humans and agents.
- Supply the required description in `Rafter.Command(description)` rather than a separate fluent `.Description(...)`
  call. Omitting the argument is a compiler error in the intended API; long descriptions can wrap across lines.
- `RunAsync` validates the final description during initial validation, before executing the command body, even when
  analyzers are available. Null, empty, and whitespace-only descriptions produce a concise invalid-command diagnostic
  and exit code `2`. Command declaration records the description without throwing a validation exception.
- Use `context.WriteLine(...)` for script results, without an intermediate `Output` member. The broader rendering
  and diagnostic APIs remain open.
- Successful execution and requested help return `0`; invalid declarations or arguments return `2`; execution
  failures return `1`. Help does not execute the command body. See the invocation behavior below.
- `RunAsync` contains recoverable exceptions from invocation setup, validation, execution, and reporting, including
  awaited user callbacks, and returns an exit code rather than propagating those exceptions to the caller.
- `Rafter.Command(description)` requires no explicit root. It captures the process working directory when `RunAsync` starts,
  uses that root for Rafter-resolved relative paths, and does not change the process working directory.
  See [roots and paths](roots-and-paths.md) for the agreed behavior and examples.
- Body execution and target execution are separate invocation forms. Target execution does not append an additional
  command body. Both forms share validation, help, root capture, exception containment, and exit-code rules.

Open questions:

- What analyzer scope, severity, and packaging should enforce nonblank descriptions during authoring?
- Timeout policies and broader process cancellation remain in their dedicated topic. Cooperative managed cancellation
  is implemented for the body-only invocation.

The body-only scope and [target invocation form](targets.md) are implemented and verified. Analyzer authoring feedback
remains separate work.

#### Description validation

Initial runtime validation in `RunAsync` is agreed. It checks the final description before executing the body and
reports invalid descriptions through a concise diagnostic and exit code `2`, rather than an unhandled exception.
This remains necessary for dynamic values, disabled analyzers, and consumers that bypass static checks.

Proposed authoring diagnostics:

Add an analyzer to give authoring-time feedback for description arguments that are provably null, empty, or whitespace.
For those definite violations, the proposed severity is an error. Include constant expressions rather than checking
only string literals. Do not reject a dynamic expression merely because its value cannot be determined statically.

Requiring the description argument at command creation handles omission through the API itself, without analyzing
fluent call sequences. The analyzer proposal concerns invalid argument values. Analyzer scope, severity, packaging,
and exact diagnostic wording remain pending agreement. Command description validation is implemented; analyzers are not.

#### Invocation behavior

The agreed API has two execution forms, with separate delegate overloads for synchronous and asynchronous bodies:

```csharp
Task<int> RunAsync(string[] args, Action<RafterContext> body);
Task<int> RunAsync(string[] args, Func<RafterContext, Task> body);
Task<int> RunAsync(string[] args, Target target, params Target[] additionalTargets);
```

These are the agreed public signature shapes. The two body overloads and the required-entry target overload are implemented.
For a body invocation, validate and then execute the supplied callback. For a target invocation, validate and then
execute the selected dependency graph. Validation and help execute neither form's work callbacks.

| Outcome | Exit code | Behavior |
| --- | --- | --- |
| Successful execution | `0` | The supplied body or selected target workflow completes successfully. |
| Requested help | `0` | Display help without executing the supplied body or target callbacks. |
| Invalid declaration or arguments | `2` | Report validation diagnostics without executing work callbacks. |
| Execution failure | `1` | Report the failure; unexpected callback exceptions include their details. |
| Invocation cancelled | `130` | Report cancellation after running callbacks and owned process/output work settle, provided no unhandled or recorded execution failure remains. |

`RunAsync` is the exception boundary for the invocation. Callers should not need a surrounding `try/catch` to handle
ordinary command failures. Recoverable exceptions from Rafter operations and synchronous or awaited asynchronous
callbacks are caught and translated into failure outcomes. Exceptions during setup, validation, or reporting are
also contained; a thrown validator or framework exception is an execution failure, distinct from an ordinary
validation rejection. Failures must remain visible in diagnostics rather than being silently swallowed.

If reporting itself fails, return a failure result and attempt reporting only where possible; do not allow a second
reporting exception to escape. Output cannot be guaranteed when the destination is unavailable.

The boundary covers work executed or awaited by the invocation. It cannot contain exceptions raised while the caller
evaluates arguments before entering `RunAsync`, detached background work, or process-ending events such as a fatal
runtime failure or forced termination. No exit result can be promised when the process cannot continue.

Cancellation uses exit `130` when cleanup succeeds and no unhandled or recorded execution failure remains. Execution
failures and unconfirmed process/output settlement take precedence with exit `1`, as described below. Timeout policy
remains open. Managed body cancellation is implemented; process cancellation remains pending.

#### Help and validation

Agreed behavior:

1. Validate command declarations first, including the required nonblank description. Invalid declarations return `2`
   even when `--help` is supplied.
2. For valid declarations, parse the supplied arguments. Explicitly supplied invalid arguments still return `2` when
   combined with `--help`; help does not silently ignore malformed or unrecognized arguments.
3. If help is requested and those checks pass, display help and return `0` without executing the supplied body or targets.
   Omitted required input values do not prevent help, and help does not evaluate user validation callbacks.

For example, assuming a declared required integer option named `count`, `--help` succeeds without a value, while
`--help --count nonsense` returns `2` because the supplied value cannot be parsed as an integer. This example describes
help behavior independently of how the body accesses input values.

### Roots and paths

The default root behavior is agreed and documented in [roots and paths](roots-and-paths.md): capture the invocation's
working directory at `RunAsync`, retain it for that run, and resolve Rafter-managed relative paths against it without
changing the process working directory. Ordinary .NET file operations are unaffected by a Rafter root selection.

Child processes use the command root by default. `.WorkingDirectory(path)` overrides the child's working directory,
resolving relative paths against the command root and using absolute paths directly, without changing the command
root or the script process's working directory. See the external-process notes and root guide for examples.

Explicit root syntax, source-directory discovery, path validation, and executable-path resolution remain open.
Per-invocation root capture is implemented and verified. The broader path/process topic remains pending.

### Invalid values and repeated callback configuration

Agreed declaration rules:

- `.Default(null)` is invalid: a defaulted input promises a non-null value. An optional input without a default
  represents absence. Non-nullable annotations do not replace runtime validation of supplied defaults.
- Null callbacks are invalid, including command/target bodies, validation predicates, and process failure handlers.
- Calling `.Required()` more than once or `.Default(...)` more than once on an option is invalid, even when the
  repeated default is identical. Mixing those policies is also invalid. Initial validation catches separate calls
  through a retained original handle; a future analyzer should report repetitions it can prove statically.
- Calling `.Run(...)` twice on the same target is invalid; do not silently replace or combine target bodies.
- Calling `.OnFailure(...)` twice on the same process configuration is invalid; do not silently replace handlers.
- Collection-style methods such as `.Aliases(...)` and `.Validate(...)` continue to append according to their own
  rules. Repeated configuration is not universally invalid.

For command, input, and target declarations, report these errors during initial command `RunAsync` validation with
exit `2`, before executing user work. Fluent declarations collect configuration without throwing validation errors.
This includes supplied null command bodies even though the body is passed directly to `RunAsync`.

Process configuration created inside an executing body cannot be checked by the command's initial validation. Validate
it when that process's `RunAsync` begins, before attempting to start the child. Invalid process configuration throws
inside the body; if unhandled, the outer command reports execution failure with exit `1`, as specified below. These
errors are not initial command declaration failures with exit `2`.

### Typed inputs

Scenario: print a personalized greeting with a required name, defaulted repeat count, and defaulted uppercase flag.
The runnable example is [examples/options.cs](../examples/options.cs), including declarations and the greeting body.
The [typed-input runtime record](typed-inputs.md) describes the string/int/bool implementation and verification.
Additional types and analyzer feedback remain pending.

Agreed declaration syntax and behavior:

- Declare an input with `command.Option<T>(name, description)`. Its description is required, must contain non-whitespace
  text, and is checked during initial validation, like the command description.
- `.Required()` requires a supplied value; `.Default(value)` provides a fallback when the input is omitted.
- Required means present, not nonempty, positive, or true. An explicitly supplied empty string, zero, or false
  satisfies the presence requirement. Parsing still checks the declared type, and explicit validators decide whether
  a supplied value is acceptable. For example, rejecting a blank name or a negative repeat count requires a validator.
- With neither, the input is optional and omission is distinct from a supplied value such as zero or false.
- Use the non-nullable supplied type as `T`. The required, defaulted, or optional declaration determines the
  nullability of the value exposed to the script body.
- Read an option's validated value for the current invocation with `context.GetValue(option)`. The option handle
  represents the declaration; value lookup is explicit through the invocation context.
- `GetValue` reads the value resolved and validated during the initial phases of that `RunAsync` invocation, before
  the supplied body or selected target work begins. It does not reparse arguments, reevaluate defaults or validators,
  perform I/O, or recompute the value on each call. Stored values belong to the invocation, not to the option declaration.
- Required/defaulted handles expose non-nullable values; optional handles expose nullable values. For example,
  required/defaulted string and integer inputs return `string` and `int`, while optional inputs return `string?`
  and `int?`. `Option<T>` is the optional declaration handle; `ValueOption<T>` is the finalized required/defaulted
  handle. See the [lookup signatures](typed-inputs.md#typed-handles-and-lookups) for the implemented initial types.

#### Names and aliases

Agreed naming rules, with the requested extension for option aliases and explicit prefixes:

- Names use lowercase letters and digits, start with a letter, and allow single hyphens between nonempty segments
  (`output-directory`, `run-tests`). Match names case-sensitively; `--Name` does not match `--name`.
- Require unique target names and unique option names. Targets and options have separate namespaces and may share a name.
- Reserve `help`, `quiet`, `verbose`, and `plain` for Rafter options, including aliases. Those may still be target names.
- For option names and aliases without an explicit prefix, add `-` for one character and `--` for longer names.
  If a prefix is already supplied, preserve it rather than adding another prefix. Apply the name grammar to the part
  after the option prefix. Target names are not option tokens and do not receive these prefixes.
- Check invalid declarations during initial `RunAsync` validation, not while collecting fluent configuration.

Agreed API shape: `.Aliases(params string[] aliases)`. The current syntax draft uses:

```csharp
var name = command.Option<string>("name", "The person to greet.")
    .Aliases("n", "recipient")
    .Required();
```

| Declaration spelling | CLI spelling |
| --- | --- |
| `name` | `--name` |
| `n` | `-n` |
| `--name` | `--name` |
| `-n` | `-n` |
| `-name` | `-name` (explicit prefix preserved) |
| `--n` | `--n` (explicit prefix preserved) |

Aliases identify the same input and resolved value as its primary name. Supplying both `--name Ada` and `-n Ada`
counts as repeating the scalar option and is invalid, even though the values match. Help should show the declared
spellings together. This does not introduce target aliases or short-option bundling.

Agreed alias edge policies:

- Check collisions across primary names and aliases after prefix expansion; reject a spelling owned by two options.
- Append aliases on repeated `.Aliases(...)` calls and ignore duplicate spellings on the same option.
- Reject null/blank aliases, prefix-only tokens, and more than two leading dashes during initial validation.

Alias declaration errors produce exit `2` before execution. For example, `"n"` and `"-n"` expand to the same spelling:
they are harmless duplicates on one option but a conflict across different options. Explicit `"--n"` remains a distinct
spelling. Duplicate aliases do not permit repeated CLI occurrences of the same scalar input.

Handle design requirement:

- Keep aliases available on optional, required, and defaulted handles without changing the handle's value guarantees
  or re-exposing the mutually exclusive Required/Default methods.

#### Boolean syntax and repeated scalar options

Agreed:

| Supplied input | Resolved Boolean value |
| --- | --- |
| `--uppercase` | `true` |
| `--uppercase true` or `--uppercase=true` | `true` |
| `--uppercase false` or `--uppercase=false` | `false` |
| Option omitted | Use the declared default; otherwise apply its required/optional policy. |

Explicit `false` counts as supplied for a required Boolean option. An omitted optional Boolean without a default
remains absent rather than implicitly becoming false. The options example declares `.Default(false)` explicitly.

Reject repeated occurrences of a scalar option as invalid input (exit `2`), even when their values match. For example,
`--repeat 2 --repeat 3` and `--uppercase --uppercase=false` do not silently select the first or last value. This rule
applies before body or target execution. Collection-valued inputs are a separate future design topic.

Boolean literals are case-insensitive: `true`, `True`, and `TRUE` resolve to true, and corresponding false spellings
resolve to false. This is independent of case-sensitive option-name matching.

Additional syntax agreed during the parser evaluation:

- `--name=` supplies an explicit empty string, just like a separate empty argument. Empty Boolean and numeric values
  are invalid; `--flag=` must not become a bare true flag.
- A declared option starts a new input: `--name --help` reports a missing name value. Use `--name=--help` to pass that
  literal text as the name. The same boundary applies to declared aliases and attached option spellings.
- Colon option separators, response-file expansion, and directives are unsupported initially. Colons and equals
  signs inside string values remain literal. An `@file` supplied as a string value does not read that file.

The [parser evaluation](parser-evaluation.md) exercises these rules through direct arrays and a real file app.
That evaluation verifies a fixed adapter; the [runtime tests](typed-inputs.md#verification) now verify dynamic
declarations too. Positional arguments and a standalone `--` remain unsupported in the initial runtime grammar.

#### Input diagnostics and API constraints

Agreed:

- An analyzer warns for nullable option type arguments, including nullable reference types such as `string?` and
  nullable value types such as `int?` or `Nullable<int>`. Explain that authors should supply `string` or `int` and let
  the option declaration determine omission behavior.
- `.Required()` and `.Default(...)` return a handle that exposes neither method. This prevents contradictory
  fluent chains in either order through the API, without requiring analyzer-based chain tracking.
- Conflicts introduced through separate statements or helper methods are rejected during initial `RunAsync`
  validation with exit code `2` before the body executes. Fluent constraints do not replace runtime validation.
- Repeating either policy through a retained optional handle is also invalid: reject both repeated `.Required()`
  and repeated `.Default(...)`, including identical defaults. Do not silently accept or replace either policy.
- Reject null defaults during initial validation to preserve the promised non-null value of defaulted inputs.

Possible additions and open details:

- A code fix could remove nullable syntax where safe. A future analyzer should flag provable repeated or conflicting
  policies through separate statements; detection across arbitrary helpers is not a static guarantee.
- Further input types and analyzer implementation remain open. The initial typed handles and runtime policy checks
  are implemented; nullable reference type annotations cannot be detected at runtime.

#### Future asynchronous value resolution

Deferred idea: an option might asynchronously resolve or transform a user-supplied value at runtime. For example,
an environment name could be resolved to configuration from a service, or an identifier to a domain object.
This is a future design topic, not part of the initial implementation or a committed API.

Preferred direction to explore:

- Keep `context.GetValue(option)` synchronous and free of hidden work. Await resolution during invocation preparation,
  validate the resulting value, and store it before executing the body or targets. Repeated reads use that stored value.
- Do not run asynchronous resolvers for help or during fluent declaration. Define how required/defaulted and optional
  inputs participate, and preserve the distinction between the supplied value and a resolved value if useful.
- Decide input/output types, validation ordering, relationships between resolvers, cancellation, deadlines, and error
  reporting before choosing syntax. Avoid making invocation preparation an implicit workflow engine.
- If a value depends on work performed by a target, explore explicit target results or shared workflow state instead
  of silently changing an option value during execution.

No asynchronous option syntax or implementation is selected. Revisit this note when a concrete script needs it.

### Input validation

Scenario: validate two defaulted range bounds individually and together, with multiple rules per bound and an optional
label whose validation is skipped when omitted. The runnable example is [examples/validation.cs](../examples/validation.cs).
The individual and cross-input rule design below is implemented for string/int/bool;
see the [runtime record](typed-inputs.md) for verification and remaining scope.

Agreed individual value validation:

- Register a rule with `.Validate(description, predicate)`. Its description must contain non-whitespace text and
  explain the requirement; initial declaration validation checks it before callbacks execute.
- A predicate returns `true` to accept a value and `false` to reject it. Declaration registers the predicate without
  executing it.
- Validators run during `RunAsync` before the command body and also check default values used for omitted inputs.
- Omitted optional values skip value validation. Explicitly supplied values, including empty strings, zero, and false,
  are checked. The predicate receives the supplied value rather than an optional omission marker.
- Repeated `.Validate(...)` calls append rules. Ordinary rejections are collected and reported together, with exit
  code `2` and no command body execution.
- An exception thrown by a validator stops further validation, is contained, and is reported as an execution failure
  with exit code `1`. Preserve diagnostics already collected before the exception; do not execute the command body.
- Help displays rule descriptions without executing the predicates.

Agreed cross-input validation:

- Register a rule with `command.Validate(description, predicate)`, using a mandatory nonblank description that explains
  the requirement. Initial validation checks the declaration without executing the predicate.
- The predicate receives an input-validation context with `inputs.GetValue(option)` for reading validated input values.
  This context does not expose execution facilities such as process launching.
- Cross-input rules run only after argument parsing and individual value validation succeed. They run before the
  command body and can express relationships, mutually exclusive flags, and inputs that require other inputs.
- Collect ordinary rejections and return `2` without executing the body. If a predicate throws, stop further validation
  and return `1`, preserving earlier diagnostics and reporting the exception.
- Include rule descriptions in help without evaluating predicates.

### External processes

Scenarios: display the installed .NET SDK version, inspect a failed build while retaining command failure, and build
from a child-specific working directory with an environment override.
The syntax drafts are [examples/processes.cs](../examples/processes.cs) and
[examples/process-failure.cs](../examples/process-failure.cs), plus
[examples/process-working-directory.cs](../examples/process-working-directory.cs). Implementation and verification are pending.

Agreed basic syntax:

- `context.Process(executable)` selects the executable.
- `.Arguments(...)` supplies separate argument tokens.
- The process's `RunAsync()` starts and awaits the process, returning a process result whenever control returns normally
  to the body. The author can use or ignore that result. There is no separate result-returning execution method.
- The outer `command.RunAsync(args, body)` or `command.RunAsync(args, targets)` remains the invocation boundary and
  returns the command's exit result.
- The child's working directory defaults to the command root. `.WorkingDirectory(path)` selects a directory for that
  child only: relative paths resolve against the command root; absolute paths are used directly. It does not change
  the command root, script process working directory, or parent shell directory.
- Children inherit the script process's environment by default. `.Environment(name, value)` explicitly sets or
  overrides a variable for that child only, without changing the script process's or parent shell's environment.
- Command options are not automatically exported as environment variables. Authors can explicitly pass an option's
  value to a child environment setting when needed.

Agreed success and result behavior:

- Success means the process starts, finishes without cancellation or timeout, exits with an accepted code, and Rafter
  completes its owned output handling successfully. Process completion alone does not establish success.
- Only exit code `0` is accepted by default. `.AcceptExitCodes(0, 1)` illustrates explicitly choosing accepted codes
  for a tool whose additional codes represent valid outcomes.
- The result exposes at least `Succeeded` and `ExitCode`. A handled rejected exit remains unsuccessful in the result;
  handling determines control flow and command outcome, not whether the process succeeded.

Agreed failure handling:

- `.OnFailure(result => ...)` handles completed processes with rejected exit codes and returns a `ProcessFailureAction`.
  The actions are `Continue`, `Fail`, and `Throw`.
- With no handler, the default action is `Throw`. Ordinary process calls therefore do not need repetitive result checks.

| Outcome or action | Returns a result | Current body | Command outcome |
| --- | --- | --- | --- |
| Accepted exit code | Yes | Continues | Can succeed. |
| `Continue` | Yes | Continues | This process failure is handled and does not by itself fail the command. |
| `Fail` | Yes | Continues until the author returns or the body finishes | Failure is recorded; later targets do not run and the command returns `1`. |
| `Throw` or no handler | No | Unwinds unless script code catches the exception | The outer command catches an unhandled process failure and returns `1`. |

`Fail` does not skip subsequent statements in the current body. The author can inspect the returned result, perform
additional work, and explicitly `return`. `Continue` does not clear failures already recorded elsewhere.

Accepted reporting: once the handler selects `Continue`, emit one yellow handled-failure warning with the executable,
actual and accepted exit codes, and action. Keep that warning in quiet mode; the invocation can still succeed.
Once the handler selects `Fail`, record invocation failure and emit the red process diagnostic before returning the
unsuccessful result to the body. The body may inspect that result and continue with cleanup or other work; later
targets do not start. Keep the diagnostic in quiet mode. The final invocation summary reports failure without
repeating the process diagnostic, and subsequent successful work does not clear the recorded failure.
For `Throw` or its default, defer the framework failure diagnostic until the exception escapes the body. If script code
catches and handles it, preserve original child output and let the script explain its recovery without an automatic
framework error or stack trace. Independent recorded failures and cleanup uncertainty remain reportable.

`Throw` uses a specific process-failure exception carrying the process result. Ordinary C# `finally` blocks still run
as the body unwinds. The outer command reports a concise process failure without propagating it to its caller;
recognized process failures do not need an unexpected-error stack trace as their normal presentation.

If the failure handler throws, treat that as a callback failure and preserve the original process failure too.
Launch errors, cancellation, timeouts, and output failures are outside this exit-code handler's scope; the callback
must not silently turn them into handled unsuccessful exits.

Agreed process exception boundary:

- Invalid process configuration, launch failures, and owned output-handling failures cause process `RunAsync` to throw
  a descriptive exception inside the body. If unhandled, the outer command contains it, reports failure, and returns `1`.
- These failures do not invoke `OnFailure` or fabricate a normally returned completed-process result. Check process
  configuration before attempting to start the child.
- Deliberately catching a process exception handles that failure. The script can recover and ultimately succeed if
  no other failure was recorded. This includes a caught rejected-exit `Throw`; throwing alone does not mark an
  irreversible command failure.
- An already recorded `ProcessFailureAction.Fail` remains a command failure despite subsequent successful work.
  Catching another exception or handling another process failure does not clear that state.
- Recovery does not remove Rafter's resource-ownership obligations. Process/output settlement still applies before
  reporting completion. Invocation cancellation follows the separate policy below; timeout behavior remains unresolved.
  Catching process exceptions does not clear invocation cancellation.

Open questions:

- What detailed diagnostic presentation applies to launch/configuration/output exceptions, and when are handled
  process failures reported? What are the detailed child shutdown and timeout policies?
- How should accepted-code configuration be validated, and what additional result information is useful?
- What path validation, executable resolution, stream handling, and other process settings should be exposed?
- Should environment removal or starting with an empty environment be supported, and with what syntax?

The command-level exception containment contract applies to the outer invocation. Process `RunAsync()` intentionally
throws within the callback for the `Throw` action; these are separate API boundaries. Implementation is pending.

### Completion of owned process scopes

Agreed direction: Rafter is for fast, finite scripts that finish their work and terminate. Processes started through
Rafter must not leave owned helpers running after their scope completes. Persistent helpers require a different
lifetime model outside this scope; a background-service API is not a requirement for the initial project.

- When the main process exits, preserve its observed exit code and shut down any remaining owned descendants.
  Use the same graceful-shutdown, five-second grace, and forceful-termination sequence described below, while
  continuing to consume output.
- Confirm the owned scope has stopped and owned output work has completed before returning a process result or
  applying the rejected-exit policy. Forceful cleanup alone does not invalidate an otherwise accepted root exit code
  when cleanup and output handling succeed.
- Report **Termination unknown** when Rafter cannot confirm that the owned scope has stopped. This describes missing
  confirmation, not proof that processes are still running. Retain any observed main-process exit code as diagnostic
  information; it does not establish successful completion of the whole scope.
- If process termination is confirmed but output completion is not, report **Output incomplete** separately. Do not
  describe confirmed process termination as unknown merely because output delivery could not be completed or verified.
- Neither diagnostic represents successful completion or a clean cancellation. They do not fabricate a completed
  process result, invoke the rejected-exit handler, or release Rafter from its ownership obligations. The verification
  budget and invocation outcome follow the policy below; detailed cleanup-error propagation remains open.

### Awaiting process operations

Agreed policy: combine a focused analyzer with runtime tracking of process operations in their owning command body
or target callback. Ignoring a completed process result is valid; discarding the task that represents the operation is not.

The initial analyzer reports an **error** for a clearly discarded Rafter process task, including an explicit discard:

```csharp
process.RunAsync();     // Error: task ignored.
_ = process.RunAsync(); // Error: task explicitly discarded.

await process.RunAsync();              // Valid: await completion, ignore the result.
var result = await process.RunAsync(); // Valid: await completion and use the result.
```

- Returning the task to an awaiting caller is valid, including returning it as the asynchronous body task.
- Storing a task and awaiting it later, including through an awaited `Task.WhenAll`, is valid. Starting multiple
  processes this way does not introduce parallel target scheduling; the target graph remains sequential.
- Begin with cases that analysis can prove. Do not report speculative errors for tasks passed through helpers or
  complex control flow. This is not a guarantee of detecting every missing await.
- C# already provides [CS4014](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-messages/async-await-errors#async-method-warnings)
  for some unawaited calls. The Rafter diagnostic expresses its specific ownership requirement and covers explicit
  discards; exact diagnostic identifiers and code-fix coverage remain implementation details to design.

At runtime, track every started process operation in its owning body or target. If the callback finishes while any
of its process operations remain outstanding:

1. Report a clear execution error identifying the owning body or target and the unfinished process work.
2. Prevent later targets from starting.
3. Shut down outstanding owned processes and settle output/cleanup using the agreed shutdown policy.
4. Report invocation exit `1`, preserving **Termination unknown** or **Output incomplete** diagnostics when applicable.

Track the complete operation, including descendant cleanup and output handling, not just the main process's running
state. The runtime check detects unfinished operations; it cannot prove that an already-completed task was awaited.
It therefore complements the analyzer rather than replacing it. Implementation and verification remain pending.

### Cancellation and owned child processes

Agreed cancellation baseline:

- Ctrl+C requests invocation cancellation and prevents later targets from starting.
- Expose `context.CancellationToken` for cooperative script work, such as
  `await Task.Delay(delay, context.CancellationToken)`.
- Processes started through that context automatically participate in invocation cancellation.
- Wait for running callbacks and owned process/output work to settle before reporting completion. A callback that
  ignores cancellation may delay this; cancellation does not forcibly stop arbitrary managed callback code.
- Report **Cancelled** with exit `130` when cleanup succeeds and no unhandled or recorded execution failure remains,
  rather than classifying expected invocation cancellation as an unexpected exception.
- Once invocation cancellation is requested, catching a cancellation exception cannot turn the invocation into success.
  Catching remains useful for cleanup. `OnFailure` does not handle cancellation.

Agreed outcome precedence when cancellation overlaps with failure:

- Cancellation with successful cleanup and no unhandled or recorded execution failure produces **Cancelled**, exit `130`.
- An unhandled execution failure or an already recorded `ProcessFailureAction.Fail` produces **Failed**, exit `1`,
  even if cancellation is also requested. Preserve the original failure diagnostic; cancellation must not hide it.
- A failure already handled through `Continue` or a deliberately caught process exception remains handled. It does not
  become an invocation failure merely because cancellation follows. Resource-ownership obligations still apply.
- **Termination unknown** or **Output incomplete** produces exit `1`, with the specific status clearly visible,
  including when cancellation was requested. Preserve any original execution failure alongside the cleanup diagnostic.
- Errors caused by expected invocation cancellation, such as a cancelled output read, do not automatically count as
  independent execution failures. Cancellation still requires shutdown verification and completed output handling;
  if output remains incomplete, report that condition with exit `1`.

These rules select the reported outcome; they do not permit abandoning Rafter's own readers or cleanup operations.

Child shutdown sequence accepted for now:

1. Establish ownership when each process is launched, covering the child and descendants in its OS-managed scope.
   Do not wait until cancellation to discover a tree of process IDs. Unrelated processes, pre-existing services, and
   work delegated through external service brokers are outside that ownership; do not kill by executable name.
2. On cancellation, stop admitting new process launches and request graceful shutdown where a supported mechanism
   can target only the owned scope. Continue consuming output during shutdown.
3. Allow a shared five-second grace period for the affected scopes, not five seconds per descendant. If scoped graceful
   signalling is unavailable, proceed directly to termination rather than waiting without having sent a request.
4. Force termination of remaining owned processes after the grace period. Allow a separate shared five-second budget
   for termination verification and output draining across all affected scopes, not five seconds per process or stream.
   Confirm that the scopes have no running processes and finish owned output delivery. Grace expiry starts escalation;
   it does not prove shutdown has completed.
5. A second Ctrl+C skips the remaining grace period and requests forceful child termination. It does not declare
   completion or abort managed callbacks. Preserve the verification/drain budget; repeated interrupts do not restart it.
6. If the verification/drain budget expires without confirmation, report **Termination unknown**; if only output
   completion is unconfirmed, report **Output incomplete**. Both use exit `1` under the agreed precedence.
   Detailed escalation-error handling remains to be designed.

Both five-second budgets are initial defaults and maximum waits, not fixed delays. Proceed as soon as each phase
finishes. They also apply when cleaning up surviving descendants after a normal main-process exit. These budgets
bound the graceful-shutdown and verification/drain phases, not total invocation duration: arbitrary script callbacks
that ignore cancellation may still delay `RunAsync`. Rafter must settle its own readers and cleanup operations before
returning; deadline expiry alone does not make those operations safe to abandon. How to interrupt pending I/O and
settle those operations safely is an implementation requirement to verify, not a claim of a ten-second return guarantee.

Implementation direction to investigate, not a platform-support commitment. See the pinned
[Aspire shutdown review](aspire-process-shutdown-review.md) for its current implementation and differences from Rafter's
contract; that research does not automatically adopt Aspire's policies or dependencies.

- On Windows, use a dedicated Job Object per process execution with breakaway disabled and kill-on-close protection.
  Establish membership at creation, before child code runs; retain the controlling handle until shutdown verification
  and owned output settlement finish. If ownership setup fails, fail the launch rather than silently weakening the contract.
- A Windows console group can support a targeted Ctrl+Break request when console requirements are satisfied. Do not
  broadcast Ctrl+C to unrelated processes sharing the terminal. Some tools provide no useful graceful shutdown protocol;
  the fallback remains scoped forceful termination.
- Validate other operating systems separately before claiming equivalent containment. Process groups alone should not
  be described as inescapable ownership of every possible descendant. Public APIs need not expose Windows Job Objects.

Research basis, checked 2026-10-19:

- [.NET Process.Kill](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0):
  `Kill(entireProcessTree: true)` followed by `WaitForExit` does not verify that all descendants have exited.
- [Windows Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects): process grouping,
  inherited membership with documented exceptions, termination, and kill-on-close behavior. Job notifications alone
  are not universally guaranteed; verification must establish actual settlement.
- [Creating a process in a job](https://devblogs.microsoft.com/oldnewthing/20230209-00/?p=107812): creation-time job
  assignment avoids the gap between launching a process and placing it under ownership.
- [Console control events](https://learn.microsoft.com/en-us/windows/console/generateconsolectrlevent): Ctrl+C cannot
  target a specific process group; Ctrl+Break targeting has shared-console restrictions.

Still discuss cleanup-error propagation and external
cancellation-token entry points. These policies are not inferred from the old implementation.
Implementation and real-process verification remain pending.

### Targets and dependencies

Scenario: build a project and then run its tests through a dependency-only grouping target. The process-based draft is
[examples/dependencies.cs](../examples/dependencies.cs). The [callback-based target slice](targets.md) implements
sequential graph execution now, demonstrated by [examples/targets.cs](../examples/targets.cs). External-process
integration and analyzer coverage remain pending.

Agreed execution shape:

- Declare targets on the command with `command.Target(name, description)` and register their bodies with `.Run(...)`.
- `.DependsOn(build)` expresses a prerequisite; in the example, build completes successfully before test runs.
- Select entry targets with `command.RunAsync(args, test)` or multiple targets through the overload requiring one
  target followed by a `params` array of additional targets.
- There is no stored command body to run after the selected targets. A callback passed to the body overload is an
  alternative execution form, not an additional workflow step.
- Failures prevent further target scheduling, with the current body's continuation governed by the agreed failure policy.

Agreed ordering and deduplication:

- Execute targets sequentially. Visit selected targets in the order supplied to `RunAsync`; for each target, visit
  its dependencies in their supplied order before executing that target.
- Dependency prerequisites take precedence over the selection order. Execute each target at most once per invocation,
  including dependencies shared by several targets and targets also listed explicitly as entries.
- For `test.DependsOn(build)`, all three selections below produce `build`, then `test`, assuming success:

```csharp
return await command.RunAsync(args, test);
return await command.RunAsync(args, build, test);
return await command.RunAsync(args, test, build);
```

These are alternative invocations. In the last one, visiting `test` first visits and executes `build`; after `test`
finishes, the explicit `build` entry is already complete and is not executed again. A recorded failure prevents
scheduling the remaining work.

Agreed grouping and empty-target rules:

- A target can group dependencies without declaring a body. For example,
  `command.Target("verify", "Build and test the project.").DependsOn(build, test)` is valid.
- A target with neither a body nor dependencies is invalid. Initial `RunAsync` validation reports the declaration
  error and returns `2` before executing work callbacks. Calling `.DependsOn()` with no targets does not supply a
  dependency and does not make an otherwise empty target valid.
- Add analyzer feedback for empty targets where static analysis can reliably establish the missing body and
  dependencies at invocation. Do not diagnose the initial `Target(...)` call just because configuration is supplied
  later through `.Run(...)` or `.DependsOn(...)`.
- Handle straightforward local configuration where feasible. If aliases, helper methods, or control flow make the
  final declaration uncertain, avoid speculative diagnostics and rely on initial runtime validation. Analyzer feedback
  complements runtime validation; it does not replace it. Exact coverage and severity remain to be settled during
  analyzer design and verified with valid configuration patterns as well as invalid ones.

Agreed graph validation:

- Validate all declared targets during initial `RunAsync` validation, including targets not reachable from the selected
  entries. This declaration validation also applies to the body overload and to help; unused declarations do not bypass it.
- Reject dependency cycles, including self-dependencies, and show the cycle path in the diagnostic, such as
  `build -> test -> build`.
- Reject selected targets or dependencies belonging to another command, null target/dependency entries, and duplicate
  target names within the command. Graph declaration errors return `2` before work callbacks execute.
- Repeated references to the same target are harmless in selections and dependency lists. Retain the first occurrence's
  traversal position and execute each target only once; this differs from distinct targets sharing a duplicate name.
- Only the selected targets and their reachable dependencies execute. Validating an unused target does not schedule it.

Agreed nonempty invocation contract:

- The target overload is `RunAsync(string[] args, Target target, params Target[] additionalTargets)`. Requiring the
  first target makes `RunAsync(args)` a compile-time error without needing an analyzer for that omission.
- Every invocation supplies either a synchronous/asynchronous body or at least one target. A target-array-only
  overload that could accept an empty selection is not part of this design.
- Help uses the same invocation shapes: the script supplies its body or targets, and `--help` describes the invocation
  without executing its work. There is no separate no-work invocation form for help.
- Compiler enforcement of argument presence does not replace runtime validation of nulls, ownership, and graph validity.

The target and dependency design above is agreed. Callback-based graph execution is implemented and verified in the
[target runtime record](targets.md); analyzer coverage and the build/test process workflow remain pending.

### Proposed automatic namespace import

The package can supply an MSBuild `Using` item for `Sotsera.Rafter`, generating a global namespace import so consumer
scripts do not need an explicit `using Sotsera.Rafter;`. The SDK documents [Using items](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#using),
and NuGet supports [package-supplied props and targets](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets).

Proposed packaging behavior:

- Enable the namespace import for direct C# consumers of the Rafter package, with an explicit opt-out property.
- Use package build assets rather than a source generator. Consider a `.targets` file for adding the conditional
  `Using` item after consumer properties are evaluated; a `.props` file can supply default properties if needed.
- Keep the import scoped to direct package consumers rather than automatically propagating it through transitive
  dependencies. Local project references need their own development setup because they do not consume the packaged assets.
- Verify actual file-based apps, regular C# project consumers, editor behavior, and the opt-out using a built package.
  File-based apps use a generated project, as described in the [SDK guide](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps),
  so the package mechanism is applicable, but Rafter's package integration is not yet implemented or verified.

This is a packaging proposal. Current drafts retain their explicit using directives until the approach is adopted.
