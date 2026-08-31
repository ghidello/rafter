# Rafter syntax portfolio

These file-based applications define the proposed public experience for the library-only Rafter rebuild. They are design artifacts until the
corresponding package surface is implemented.

The portfolio deliberately assumes:

- one file-based application defines one command;
- `Rafter.Command(...)` receives only the root policy;
- descriptions are authored fluently;
- exactly one target is passed to `RunAsync` as the command entry point;
- targets are internal graph nodes rather than command-line subcommands;
- Rafter parses the bounded command grammar and uses Spectre.Console internally;
- no external Rafter CLI, MCP server, `rafter.json`, recipe system, or command discovery is required;
- source execution is the initial experience, while published and Native AOT execution remain subject to later spikes.

## Portfolio

- `minimal.cs`: the smallest useful command.
- `options.cs`, `option-types.cs`, and `user-secrets.cs`: authored inputs and application-owned configuration.
- `root-invocation.cs`, `root-source.cs`, `root-explicit.cs`, and `working-directory.cs`: roots and scoped working directories.
- `dependencies.cs` and `no-op.cs`: executable, aggregate, and no-op targets.
- `conditions.cs`, `cleanup.cs`, `failures.cs`, `expected-failure.cs`, and `concurrent-failures.cs`: lifecycle behavior.
- `long-running.cs` and `long-running-processes.cs`: cancellation and cleanup.
- `diagnostics.cs`, `console.cs`, `redaction.cs`, and `presentation.cs`: semantic, managed-console, redacted, human, and machine output.
- `processes.cs` and `environment.cs`: generic argument-safe child processes.
- `filesystem.cs`: common filesystem preparation operations.
- `dotnet.cs`, `git.cs`, and `node.cs`: typed tool integrations.
- `repository.cs`: a realistic repository verification graph.

Every example intentionally includes the proposed stable package directive. During development, validation may generate temporary copies that
replace only that directive with a project reference or an exact development-package version.

## Resolving authored values

Authored code resolves an option explicitly when it needs the value for conditions, calculations, branching, or output:

```csharp
if (context.Value(publish))
{
    // Application-owned behavior.
}
```

Context-owned APIs accept required and defaulted option handles directly when the resolved type satisfies the operation:

```csharp
await context.Process(tool).Argument("verify").Option("--output", output).Run();
await context.DotNet.Build(solution).Configuration(configuration).Run();
context.FileSystem.EnsureEmptyDirectory(output);
```

Optional options do not participate in this convenience because their value may be absent. Authors resolve and handle them explicitly.

Rafter binds every authored option exactly once per invocation, before graph execution begins. Binding parses command-line input, reads declared
environment fallbacks, applies defaults, converts values, validates constraints, registers sensitive values for redaction, and snapshots repeated
or mutable values. A binding failure prevents every condition, target, cleanup callback, and child process from running.

`context.Value(option)` and context-owned APIs both read the same immutable bound snapshot. Reading an option from multiple targets never repeats
conversion, validation, environment access, default evaluation, or caller-owned collection access.

`redaction.cs` intentionally emits a synthetic sensitive value through every supported output path. It is a contract fixture, not a pattern for
writing real credentials to output. Its value must always be a disposable test value.

## Process working directories

Targets expose `.WorkingDirectory(...)` as the default logical directory for their callbacks. Relative target directories resolve against the
command root. Every generic process, typed process, and filesystem operation created from that target's context uses the target directory by
default. Target cleanup uses its owner's directory; command cleanup uses the command root.

Generic processes and every typed process integration also expose `.WorkingDirectory(...)` as a per-process override. A relative process override
resolves against the target working directory and affects only that child-process specification. Both target and process modifiers accept a path
string or a required or defaulted string option handle.

Rafter never changes the process-wide current directory for an individual target because targets may execute concurrently. Direct `System.IO`
calls therefore do not inherit a target directory automatically; authors use `context.WorkingDirectory` or the context-owned filesystem facade
when they need that behavior.

## Filesystem safety

The initial context-owned filesystem facade mutates only paths contained beneath the command root. It normalizes each path before mutation and
rejects absolute or relative input that resolves outside that boundary. Authors that intentionally need another location select an appropriate
command root rather than bypassing the boundary.

`EnsureEmptyDirectory` additionally rejects a filesystem root, the command root, and the target working directory itself. It never follows a
symbolic link, junction, or other reparse point while cleaning, and rejects the target when an existing path component needed to reach it crosses
one. A link encountered inside the directory is removed as an entry without traversing its destination. These checks happen before any contents
are removed so a rejected request leaves the filesystem unchanged.

An API for explicitly opting into mutations outside the command root is deferred until a concrete automation scenario justifies its syntax and
safety contract.
