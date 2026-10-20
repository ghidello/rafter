# Captured renderer help

Captured 2026-10-20 from the real managed project consumer in a Windows pseudoterminal. These are executed output, not the illustrative HTML preview. SGR styling and trailing spaces are removed only for Markdown display; wording, wrapping, indentation, and blank lines are retained. The adversarial alias and Unicode/bracket line are evaluation additions.

All 18 terminal cases passed. The four redirection combinations were checked in normal, plain, and NO_COLOR modes with CI/GITHUB_ACTIONS set; six further cases captured these help examples. Both streams used UTF-8, .NET 10.0.13 on Windows 10.0.26220. Each help case emitted no stderr. Width is the configured content width, independent of the enclosing terminal width.

## options — 100 columns

```text
Print a personalized greeting.

Command options
  --name, -n, --recipient <string>
    The person to greet. Required.
  --repeat, -r <int>
    Number of greetings. Default: 1.
  --uppercase, -u [bool]
    Use uppercase text. Default: false.
    Bare flag means true; explicit true/false allowed.

Execution
  Runs the supplied command body after input validation.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results and errors.
  --verbose
    Add execution context. Cannot combine with --quiet.
  --plain
    Disable Rafter styling and presentation prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻 remain visible.
```

## options — 48 columns

```text
Print a personalized greeting.

Command options
  --name, -n, --recipient <string>
    The person to greet. Required.
  --repeat, -r <int>
    Number of greetings. Default: 1.
  --uppercase, -u [bool]
    Use uppercase text. Default: false.
    Bare flag means true; explicit true/false
    allowed.

Execution
  Runs the supplied command body after input
  validation.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results
    and errors.
  --verbose
    Add execution context. Cannot combine with
    --quiet.
  --plain
    Disable Rafter styling and presentation
    prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxx
  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻
    remain visible.
```

## validation — 100 columns

```text
Describe a range after validating its bounds and optional label.

Command options
  --minimum <int>
    The inclusive lower bound. Default: 1.
    Rules:
      - Must be zero or greater.
      - Must not exceed one hundred.

  --maximum <int>
    The inclusive upper bound. Default: 10.
    Rules:
      - Must be zero or greater.
      - Must not exceed one hundred.

  --label <string>
    An optional name for the range. Optional; no default.
    Rules when supplied:
      - Must not be blank when supplied.

Execution
  Runs the supplied command body after input validation.
  Combined input rules:
    - Minimum must not exceed maximum.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results and errors.
  --verbose
    Add execution context. Cannot combine with --quiet.
  --plain
    Disable Rafter styling and presentation prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻 remain visible.
```

## validation — 48 columns

```text
Describe a range after validating its bounds and
optional label.

Command options
  --minimum <int>
    The inclusive lower bound. Default: 1.
    Rules:
      - Must be zero or greater.
      - Must not exceed one hundred.

  --maximum <int>
    The inclusive upper bound. Default: 10.
    Rules:
      - Must be zero or greater.
      - Must not exceed one hundred.

  --label <string>
    An optional name for the range. Optional; no
    default.
    Rules when supplied:
      - Must not be blank when supplied.

Execution
  Runs the supplied command body after input
  validation.
  Combined input rules:
    - Minimum must not exceed maximum.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results
    and errors.
  --verbose
    Add execution context. Cannot combine with
    --quiet.
  --plain
    Disable Rafter styling and presentation
    prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxx
  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻
    remain visible.
```

## dependencies — 100 columns

```text
Build the project and run its tests.

Command options
  None declared.

Execution
  The script selects verify. Each target body runs once.

  > verify <-- (build, test)
    Build and test the project. (Group; no body.)

  [build]
    Build the project.

  [test] <-- (build)
    Run the tests after a successful build.

  Body order: build, test.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results and errors.
  --verbose
    Add execution context. Cannot combine with --quiet.
  --plain
    Disable Rafter styling and presentation prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻 remain visible.
```

## dependencies — 48 columns

```text
Build the project and run its tests.

Command options
  None declared.

Execution
  The script selects verify. Each target body
  runs once.

  > verify <-- (build, test)
    Build and test the project. (Group; no
    body.)

  [build]
    Build the project.

  [test] <-- (build)
    Run the tests after a successful build.

  Body order: build, test.

Rafter options
  --help
    Show help without executing work.
  --quiet
    Hide routine lifecycle output; keep results
    and errors.
  --verbose
    Add execution context. Cannot combine with
    --quiet.
  --plain
    Disable Rafter styling and presentation
    prefixes.

  --very-long-alias-xxxxxxxxxxxxxxxxxxxxxxxxxxxx
  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx, -z
  <string>
    Literal [red]brackets[/], café, 界面, and 👩‍💻
    remain visible.
```
