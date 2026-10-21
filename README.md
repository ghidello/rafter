# Rafter by Sotsera

Rafter is being built afresh through small C# automation examples.

The first [runtime slice](docs/minimal-runtime.md) runs body-only commands with validation, help, direct output,
exception containment, and cooperative managed cancellation. Try the real [minimal example](examples/minimal.cs):

```powershell
dotnet run --file examples/minimal.cs -- --help
dotnet run --file examples/minimal.cs -- --quiet
```

The [typed-input slice](docs/typed-inputs.md) adds string, integer, and Boolean options, aliases, required/default
policies, typed value lookup, individual and combined validation, and generated help. Try the runnable examples:

```powershell
dotnet run --file examples/options.cs -- --name Ada --repeat 2
dotnet run --file examples/validation.cs -- --minimum 3 --maximum 5
```

The [target slice](docs/targets.md) adds sequential targets, dependency groups, graph validation, and target-aware
help/output. Try its callback-based [example](examples/targets.cs):

```powershell
dotnet run --file examples/targets.cs -- --name Ada
dotnet run --file examples/targets.cs -- --help
```

The [process runtime](docs/process-runtime.md) adds owned Windows child processes, separate output streams,
child directory/environment settings, failure actions, and cancellation with descendant cleanup:

```powershell
dotnet run --file examples/processes.cs -- --quiet
dotnet run --file examples/dependencies.cs -- --project fixtures/workflow/sample-tests/BuildSample.Tests.csproj
```

All eight examples now run through project references with [authoring diagnostics](docs/authoring-diagnostics.md)
and an automatic namespace import. The [local package](docs/local-package.md) provides the same authoring support to
fresh file apps and regular projects; public publication and process ownership on other platforms remain separate work.
Authoring diagnostics also catch repeated or conflicting option presence policies in supported straight-line code.
See [example prerequisites](examples/README.md) before running build-oriented scripts.

The [milestone review](docs/milestone-review.md) records actual Windows terminal output and live VS Code diagnostics
and completion. Writes to a shared terminal are coordinated so stdout/stderr cannot split a Rafter prefix or style sequence.

The [.NET 10 evaluation harness](docs/evaluation-harness.md) runs real file-based and project-consumer checks
through xUnit v3 and Microsoft.Testing.Platform. It also verifies the runtime through a production project reference.
The [parser evaluation](docs/parser-evaluation.md) records executable System.CommandLine compatibility checks and
the recommended internal adapter.
The [renderer evaluation](docs/renderer-evaluation.md) records real terminal checks, captured help, and the comparison
between Spectre.Console, its Ansi-only package, and framework output.

See the [example design plan](docs/example-design-plan.md) for the working approach, topic checklist, and open questions.
The [first working examples milestone](docs/first-working-examples.md) proposes the initial implementation scope and remaining decisions.
The [dependency review](docs/dependency-decisions.md) lists candidate packages and the checks needed before adoption.
The [SDK and platform review](docs/sdk-and-platform-baseline.md) recommends the build baseline and verification order.
The [GitHub workflow](docs/github-workflow.md) identifies upstream, the development fork, and the published evaluation issues.
The [dotnet/skills review](docs/dotnet-skills-review.md) recommends task-specific guidance and records adoption caveats.
The [skills setup](docs/dotnet-skills-setup.md) records the installed project-local skills, verification, and updates.
The [output and help design](docs/output-design.md) records the current agreed presentation and remaining questions.
Explore the [current output preview](docs/output-preview.html) for illustrative success, failure, and cancellation transcripts.
The agreed [root and path behavior](docs/roots-and-paths.md) explains the default invocation directory and explicit roots.
The previous implementation's scripts are preserved in [reference/examples](reference/examples/).
Its output previews and accompanying notes are preserved in [reference/output](reference/output/README.md).
