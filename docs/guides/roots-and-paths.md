# Rafter roots and paths

Agreed design for the new Rafter. The [minimal runtime](minimal-runtime.md) implements per-invocation root capture;
the [process runtime](process-runtime.md) implements child working directories and environment overrides on Windows.
Explicit roots remain pending. The captured root is currently internal to the invocation.

## Default root

`Rafter.Command(description)` uses the process working directory at the start of `RunAsync` as the command root.
For an ordinary shell invocation, this is the directory from which the user launches the script.
The script's location does not determine the default root.

```csharp
var command = Rafter.Command("Print a greeting.");

return await command.RunAsync(args, context =>
{
    context.WriteLine("Hello from Rafter.");
});
```

The root is captured when `RunAsync` starts, rather than when `Rafter.Command(description)` is declared. Each invocation captures
its own root and retains that value for the run. A later working-directory change does not change the captured root.
If the script calls `Directory.SetCurrentDirectory(...)` before `RunAsync`, the default root is that new working
directory. This changes the script process's working directory, not the parent shell's directory.

For example, given a script at `C:\work\sample\scripts\hello.cs`:

| Working directory when launched | Default command root |
| --- | --- |
| `C:\work\sample` | `C:\work\sample` |
| `C:\work\sample\scripts` | `C:\work\sample\scripts` |
| `C:\scratch` | `C:\scratch` |

Moving the script alone does not change this rule. Launching it from another directory changes the default root.

## Path resolution and working directory

The command root is the base for relative paths resolved by Rafter. Rafter does not change the process working
directory to implement this behavior.

Ordinary .NET file operations and other libraries retain their own path-resolution behavior. For example, a direct
`File.ReadAllText("input.txt")` uses the process working directory; selecting a different Rafter root would not redirect
that call. Script code must explicitly resolve paths against the command root when that is its intent.

## Child process working directory

Child processes use the command root as their default working directory. `.WorkingDirectory(path)` overrides it for
that child: relative paths resolve against the command root, and absolute paths are used directly.

```csharp
await context.Process("dotnet")
    .WorkingDirectory("src")
    .Arguments("build")
    .RunAsync();
```

With a command root of `C:\work\sample`, this starts the child in `C:\work\sample\src`. The setting changes neither
the script process's working directory nor the parent shell's directory. It also leaves the command root unchanged.
See the [working-directory example](../../examples/process-working-directory.cs).

The process builder rejects a second `WorkingDirectory` call, even with the same path. An executable path containing
a separator or drive prefix also resolves against the captured root. Bare executable names use Windows direct-launch
lookup, including the script process's PATH; a child PATH override does not change executable lookup. Argument tokens
are passed unchanged, so relative paths inside arguments are interpreted by the child from its own working directory.

## Explicit roots

Scripts that need paths anchored to their source directory or another chosen directory can select an explicit root.
The exact root configuration syntax, source-directory discovery, path validation rules, and executable-path resolution
remain topics for discussion. The agreed defaults and child working-directory override do not depend on those choices.
