# Environment-backed inputs

The selected behavior, syntax, and help/verbose layouts are agreed on 2026-10-22. Environment-backed option resolution
is not implemented or verified. The [example draft](../../examples/environment.cs) adapts the scenario from the
unchanged [reference example](../../reference/examples/environment.cs) to the current command and lookup APIs.

## Agreed resolution behavior

Options can obtain a value from an explicitly mapped environment variable. Resolve sources in this order:

1. A supplied command-line option, including its aliases.
2. The mapped environment variable, when present.
3. The declared default, when available.
4. The existing required/optional behavior when no source supplies a value.

An explicitly supplied empty command-line string wins over the environment. Invalid supplied values are input
errors, not permission to fall back to a lower-priority source. Only the selected source supplies the option's value;
an overridden environment variable must not be reported as the source.

Resolve and validate values during the initial phases of each `RunAsync`, before the body or selected targets begin.
Store the selected source alongside the invocation's prepared value. `GetValue` remains synchronous and reads that
stored value; changing the environment during execution does not change subsequent lookups in that invocation.

## Agreed variable-name validation and matching

Validate mapped names during initial `RunAsync` declaration validation. Reject null, empty, or whitespace-only names;
names containing `=`, NUL, or any other control character; and names with leading/trailing whitespace. Report invalid
names as declaration errors with exit code `2`, including when help is requested. Collect the declaration without
throwing solely for these validation failures, as with other option metadata.

Preserve the name exactly: do not trim it, change its case, impose uppercase-only spelling, or reuse the CLI option-name
grammar. Uppercase names such as `RAFTER_NAME` are a recommended convention for portable scripts, not a requirement.

Follow the platform's process-environment lookup semantics:

| Platform | Case matching |
| --- | --- |
| Windows | Case-insensitive. |
| Linux | Case-sensitive. |

Do not retry with alternative spellings or introduce Rafter-specific case folding. Authors should use the same spelling
when declaring and supplying variables across platforms. See
[.NET environment lookup documentation](https://learn.microsoft.com/en-us/dotnet/api/system.environment.getenvironmentvariable).
These are agreed rules for the planned feature, not a claim that Rafter's environment resolution is implemented.

## Empty is different from missing

| Environment state | Agreed behavior |
| --- | --- |
| Variable absent | Use the default, or apply the option's existing required/optional behavior. |
| Variable present with an empty string, for a string option | Supply `""`; individual validators may reject it. It satisfies `.Required()` because presence does not imply nonempty text. |
| Variable present with an empty string, for an integer or Boolean option | Report a conversion error. Do not silently use the default or treat it as omission. |

An empty string is not itself a warning. A Boolean environment value must supply a value; an empty variable does
not act like a bare command-line Boolean flag.

Windows permits empty process environment variables. Starting with .NET 9, the process-scoped
`Environment.SetEnvironmentVariable(name, "")` preserves an empty value, while passing `null` removes the variable.
Rafter's .NET 10 baseline supports this distinction. See Microsoft's
[empty-environment-variable compatibility note](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/9.0/empty-env-variable).
Shell behavior can differ: PowerShell's environment provider supports assigning an empty string starting in 7.5;
older versions remove the variable instead. See
[PowerShell environment variables](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_environment_variables?view=powershell-7.5).
These platform facts are not evidence that Rafter implements environment inputs yet.

## Agreed whitespace and Boolean conversion

Preserve string values exactly, including leading/trailing whitespace and whitespace-only strings. Do not trim strings
or treat whitespace-only text as missing. It satisfies `.Required()`; an explicit validator can reject blank text.
The `(empty)` source marker describes a zero-length value, not whitespace-only text.

For integer and Boolean options, ignore surrounding whitespace before conversion. Whitespace-only values are invalid
input (exit `2`), not omission; do not fall back to a default or another source. Validators receive the converted value.

Boolean values accept only `true` and `false`, case-insensitively, after surrounding whitespace is ignored:

| Environment text | Boolean result |
| --- | --- |
| `true`, `True`, `TRUE` | `true` |
| `false`, `False`, `FALSE` | `false` |
| Either accepted word with surrounding whitespace | The corresponding Boolean value. |
| `1`, `0`, `yes`, `no`, `on`, `off` | Input error. |
| Empty or whitespace-only text | Input error. |
| Literal quote characters around the word, such as `"true"` | Input error. |

An environment mapping has no bare-flag form. The command-line Boolean flag syntax remains unchanged.
Do not interpret quotes or escape sequences in environment text. A string containing literal quotes or backslashes
retains them; Boolean text containing surrounding quote characters fails conversion. Shell quoting used to assign a
variable is separate from quote characters actually stored in its value.

These conversion rules are agreed for the planned environment feature; this document does not change the existing
command-line parser or claim executable verification of environment resolution.

## Agreed help and output behavior

| Surface | Behavior |
| --- | --- |
| Help | Identify the mapped environment-variable name in the option's metadata. |
| Verbose stderr | Identify the source actually selected for an option. For an environment source, show the variable name and explicitly mark an empty value. If the command line wins, identify that source instead. |
| Normal stderr | Include environment-source information when explaining an input error; omit routine source announcements. |
| Quiet stderr | Retain input errors, including their source context; hide routine source announcements. |

Help is based on declarations only: show the mapped variable name, declared default, and required/optional policy.
Do not read or convert environment values, run input validators, or reveal current values when displaying help.
An invalid or empty environment value cannot prevent help from displaying. Existing checks for invalid declarations
and malformed supplied command-line arguments still apply before help; help is not a bypass for declaration errors.

### Help layout

Keep the existing option blocks: names and type at two spaces, description/policy and metadata at four spaces.
For a mapped non-Boolean option, add `Environment: NAME.` after the description/policy and before validation rules.
Retain declaration order and the existing primary-name/alias ordering. Explain precedence once in help:
`Source order: command line, environment, default.`

Agreed illustrative layout, not captured runtime output:

```text
Command options
  --name <string>
    The person to greet; an empty name is allowed. Required.
    Environment: RAFTER_NAME.

  --repeat <int>
    Number of greetings. Default: 1.
    Environment: RAFTER_REPEAT.
    Rules:
      - Must be zero or greater.
```

For a mapped Boolean, distinguish the command-line flag syntax from the environment conversion. After the
description/policy, show the command-line explanation followed by the environment line, then any validation rules:

```text
    Command line: bare flag means true; explicit true/false allowed.
    Environment: RAFTER_UPPERCASE; requires true or false.
```

Retain existing help colors and wrapping behavior. Environment-variable names use the same blue emphasis as option
names; type hints keep their existing color. Plain/redirected help retains all wording without styling.

Source diagnostics do not automatically print resolved value contents. This extends the existing
[output policy](output-design.md) without adding an environment dump. Script-authored output, declared defaults in
help, and unexpected exception details retain their existing policies; this is not a general redaction feature.
Plain/redirected presentation preserves the selected information without Rafter styling. Script results stay on stdout.

### Verbose source layout

After all input validation succeeds, print one `Input sources` block on stderr before the body or target execution.
List every declared option in declaration order using its primary name, including when an alias was supplied or no
environment mapping exists. Report the selected source only: `command line`, `environment NAME`, `default`, or
`not supplied` for an omitted optional option. Append ` (empty)` only to zero-length environment values.

Agreed illustrative layout, not captured runtime output:

```text
Input sources
  --name: environment RAFTER_NAME (empty)
  --repeat: default
  --uppercase: command line
```

An omitted optional option instead has a row such as:

```text
  --uppercase: not supplied
```

Use a column-zero emphasized heading and a two-space content gutter. Option/environment names use the existing
blue color; source labels and `(empty)` use ordinary text, without warning coloring. Do not append resolved values.
Normal/quiet modes hide this routine block. Plain/redirected verbose output preserves its text without styling.
If input validation fails, retain grouped input errors with source context and do not also print the routine source block.

Illustrative input error detail:

```text
  --repeat: environment variable RAFTER_REPEAT is empty; expected an integer.
```

The source lines must describe the selected source, even when a lower-priority environment value was also present.
Keep input errors grouped under the existing heading; source context must not expose rejected value contents.

## Agreed declaration syntax

Use `.FromEnvironment("NAME")`. Environment mapping is independent of the presence/default policy, so it is available
both before and after `.Required()` or `.Default(...)`. Fluent call order does not change source precedence:

```csharp
var name = command.Option<string>("name", "The person to greet; an empty name is allowed.")
    .Required()
    .FromEnvironment("RAFTER_NAME");

var repeat = command.Option<int>("repeat", "Number of greetings.")
    .Default(1)
    .FromEnvironment("RAFTER_REPEAT");
```

`FromEnvironment` preserves the handle kind: an optional `Option<T>` remains optional, and a finalized
`ValueOption<T>` retains its non-null lookup guarantee without regaining `.Required()` or `.Default(...)`.

Allow one environment mapping per option. Reject multiple `FromEnvironment` calls, whether they repeat the same
variable or specify different variables. Calls through retained optional and finalized handles still configure the
same option and count together; do not silently replace, combine, or deduplicate mappings.

As with other option declaration conflicts, reject repeated mappings during initial `RunAsync` declaration validation
with exit code `2`, before input resolution, help, or body execution. Fluent declaration should not throw solely
because a mapping was repeated. A future analyzer may diagnose repetitions it can prove; runtime validation remains required.

The complete draft contains a required string, a validated/defaulted integer, and an optional Boolean. It uses the
current `RunAsync(args, body)`, `GetValue`, and `WriteLine` shape, with no manual environment reads or source
announcements in the body. The new method is agreed syntax, not an implemented API yet.

## Implementation boundary

The selected design is agreed and ready for a scoped implementation issue. Runtime implementation and executable
acceptance checks remain pending. Analyzer extensions remain a later topic. Async value resolution, secret redaction,
automatic name inference, `.env` loading, and child-process environment configuration are separate topics.
