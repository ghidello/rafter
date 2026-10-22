# Captured runtime review samples

Captured on 2026-10-21 from the current runtime fixture on Windows x64. These are actual executions. ANSI styling and trailing padding are removed in this Markdown transcription; stdout and stderr remain separate. Timings are observed samples, not performance targets. The child prints deterministic fixture text rather than running a real build.

## Successful dependency workflow

Exit code: 0.

Output:

```text
  >   Child [red]literal[/] output.
  | Result for Ada.
  | Next target ran.
```

Error:

```text
> verify <-- (build, test)
Review actual runtime output with dependencies and child processes.

[build]
Run a deterministic child fixture.

  Process | dotnet
  > Child diagnostic.
[OK] build | 95 ms

[test] <-- (build)
Run after build.

[OK] test | 0 ms

[OK] verify | 2 target bodies completed | 135 ms
```

## Handled failure that continues

Exit code: 0.

Output:

```text
  >   Child [red]literal[/] output.
  | Result for Ada.
  | Next target ran.
```

Error:

```text
> verify <-- (build, test)
Review actual runtime output with dependencies and child processes.

[build]
Run a deterministic child fixture.

  Process | dotnet
  > Child diagnostic.
  Process failure handled | dotnet
  Exit code: 1 | accepted: 0
  Action: Continue
[OK] build | 116 ms

[test] <-- (build)
Run after build.

[OK] test | 0 ms

[OK] verify | 2 target bodies completed | 159 ms
```

## Recorded failure

Exit code: 1.

Output:

```text
  >   Child [red]literal[/] output.
  | Result for Ada.
```

Error:

```text
> verify <-- (build, test)
Review actual runtime output with dependencies and child processes.

[build]
Run a deterministic child fixture.

  Process | dotnet
  > Child diagnostic.
  Process failed | dotnet
  Exit code: 1 | accepted: 0
  Action: Fail

[Failed] verify | stopped in build | 166 ms
```

## Narrow help (48 columns, NO_COLOR)

Exit code: 0.

Output:

```text
Review actual runtime output with dependencies
and child processes.

Command options
  --name, -n, --recipient <string>
    Name with literal [red]brackets[/], Unicode
    界面, and a long description that wraps.
    Default: "Ada".
    Rules:
      - Name must not be blank.

  --enabled [bool]
    Enable the sample behavior. Default: false.
    Bare flag means true; explicit true/false
    allowed.

Execution
  The script selects verify. Each target body
  runs once.

  > verify <-- (build, test)
    Review both target bodies. (Group; no body.)

  [build]
    Run a deterministic child fixture.

  [test] <-- (build)
    Run after build.

  Body order: build, test.
  Combined input rules:
    - Review combined input rules without
    executing them during help.

Rafter options
  --help
    Show help without executing targets.
  --quiet
    Hide routine lifecycle output; keep results
    and failures.
  --verbose
    Show additional execution context.
  --plain
    Disable styling and presentation prefixes.
```

Error: empty.

## Ctrl+C and managed cleanup

Exit code: 130.

Output:

```text
  | Ready for Ctrl+C.
  | Managed cleanup finished.
```

Error:

```text
Print a greeting.

Cancellation requested.

[Cancelled] Cancelled | 17939 ms
```
