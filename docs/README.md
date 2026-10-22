# Rafter documentation

Start with the [repository README](../README.md) and [runnable examples](../examples/README.md).
The [first milestone summary](history/milestone-review.md) records the completed Windows x64 baseline and its limits.

| Folder | Contents |
| --- | --- |
| [guides](guides/) | Current script-authoring and local-package behavior. |
| [design](design/) | Accepted decisions, open topics, and the illustrative output preview. |
| [development](development/) | SDK, workflow, skills setup, repository organization, and generated-file policy. |
| [history](history/) | Completed evaluations, captured samples, investigations, and milestone records. |

The two short pages at the old diagnostic/package paths preserve links embedded in existing packages.

## Using and authoring Rafter scripts

| Topic | Guide |
| --- | --- |
| Consume the local package, automatic imports, and opt-out | [Local package](guides/local-package.md) |
| Commands, bodies, invocation outcomes, and help | [Minimal runtime](guides/minimal-runtime.md) |
| Options, aliases, value lookup, and validation | [Typed inputs](guides/typed-inputs.md) |
| Invocation roots and child paths | [Roots and paths](guides/roots-and-paths.md) |
| Sequential targets and dependencies | [Targets](guides/targets.md) |
| Windows processes, failures, cancellation, and ownership | [Process runtime](guides/process-runtime.md) |
| Compiler/editor authoring diagnostics | [Authoring diagnostics](guides/authoring-diagnostics.md) |
| Human/plain output and help contracts | [Output design](design/output-design.md) |

The [output preview](design/output-preview.html) illustrates presentation choices; the
[runtime review samples](history/runtime-review-samples.md) and linked verification records capture actual executions.

## Developing this repository

Open the Git root containing `README.md`, `global.json`, and [Rafter.slnx](../Rafter.slnx). The enclosing workspace
directory is not itself the repository. The solution contains the two production projects, the existing xUnit/MTP
suite, and its ProcessProbe executable. The other consumer and workflow projects remain isolated test/tool inputs;
their sample tests are exercised by integration scenarios rather than discovered as additional solution test suites.

Run from the repository root with the [pinned SDK and Windows baseline](development/sdk-and-platform-baseline.md):

```powershell
dotnet build Rafter.slnx --configuration Release
dotnet test --solution Rafter.slnx --configuration Release --report-trx --results-directory artifacts/test-results
```

The second command uses the repository's .NET 10 native Microsoft.Testing.Platform mode. To focus an individual
suite, consult the installed [run-tests skill](../.agents/skills/run-tests/SKILL.md). CI restores, builds, and tests
this same solution; its exact commands and evidence retention are in the
[Windows evaluation workflow](../.github/workflows/evaluation.yml). Package creation and manual/editor checks have
their own instructions in [local packaging](guides/local-package.md) and the [tool inventory](../tools/README.md).

| Development reference | Purpose |
| --- | --- |
| [GitHub workflow](development/github-workflow.md) | Upstream/fork identity, scoped issues, PRs, and merge conventions. |
| [Skills setup](development/dotnet-skills-setup.md) | Installed guidance, provenance, workspace bridge, and updates. |
| [Repository organization review](development/repository-review.md) | Folder/project roles and the agreed next organization steps. |
| [Development tools](../tools/README.md) | Current verification versus optional dependency evaluations, with reproduction commands. |
| [Generated files and evidence](development/generated-files.md) | Per-run evidence directories, CI retention, and bounded cleanup policy. |

## Product direction and open work

The [example design plan](design/example-design-plan.md) owns accepted API decisions and the broader topic inventory.
The [first working examples record](history/first-working-examples.md) preserves the completed milestone's selected scope.
Later capabilities remain separate work; a completed example group does not establish every reference feature or
platform. Maintained test/tool inputs now live under [tests/testassets](../tests/testassets/README.md); its inventory
identifies automated and optional-tool callers. The main suite is `tests/Rafter.Tests/`, grouped by responsibility.

## Evaluation and investigation history

These reports preserve evidence for their stated revisions. Their original commands, project names, and test counts
are historical snapshots; use the development entry points above for the current checkout.

| Record | Evidence and context |
| --- | --- |
| [Dependency decisions](history/dependency-decisions.md) | Initial shortlist and adoption gates. |
| [Evaluation harness](history/evaluation-harness.md) | Original SDK, test runner, and consumer scaffolding. |
| [Parser review](history/parser-review.md), [evaluation](history/parser-evaluation.md) | System.CommandLine source review and executable contract checks. |
| [Renderer review](history/renderer-review.md), [evaluation](history/renderer-evaluation.md) | Spectre.Console compatibility and comparison probes. |
| [Renderer measurements](history/renderer-measurements.md), [samples](history/renderer-samples.md) | Recorded startup/allocation observations and captures. |
| [Skills review](history/dotnet-skills-review.md) | Selection rationale for project-local skills. |
| [Aspire shutdown review](history/aspire-process-shutdown-review.md) | Upstream inspiration and process-lifetime considerations. |
| [Milestone review](history/milestone-review.md) | Completed baseline, terminal/editor observations, and verification limits. |
| [Process inheritance investigation](history/process-test-isolation.md) | Failed attempts, deterministic reproduction, and mixed-launcher fix. |

The previous implementation is preserved separately under [reference examples](../reference/examples/README.md)
and [reference output](../reference/output/README.md). Those snapshots describe the old project; some links in its
output notes intentionally depend on old files outside the preserved subset.
