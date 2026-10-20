# Rafter by Sotsera

Rafter is being designed afresh through small C# automation examples.

The [.NET 10 evaluation harness](docs/evaluation-harness.md) runs real file-based and project-consumer checks
through xUnit v3 and Microsoft.Testing.Platform. It is evaluation tooling; the Rafter runtime is not implemented yet.
The [parser evaluation](docs/parser-evaluation.md) records executable System.CommandLine compatibility checks and
the recommended internal adapter.

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
