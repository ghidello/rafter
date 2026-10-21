# Rafter documentation

Start with the [repository README](../README.md) and [runnable examples](../examples/README.md).
The [first milestone summary](milestone-review.md) records the completed Windows x64 baseline and its limits.

## Using and authoring Rafter scripts

| Topic | Guide |
| --- | --- |
| Consume the local package, automatic imports, and opt-out | [Local package](local-package.md) |
| Commands, bodies, invocation outcomes, and help | [Minimal runtime](minimal-runtime.md) |
| Options, aliases, value lookup, and validation | [Typed inputs](typed-inputs.md) |
| Invocation roots and child paths | [Roots and paths](roots-and-paths.md) |
| Sequential targets and dependencies | [Targets](targets.md) |
| Windows processes, failures, cancellation, and ownership | [Process runtime](process-runtime.md) |
| Compiler/editor authoring diagnostics | [Authoring diagnostics](authoring-diagnostics.md) |
| Human/plain output and help contracts | [Output design](output-design.md) |

The [output preview](output-preview.html) illustrates presentation choices; the
[runtime review samples](runtime-review-samples.md) and linked verification records capture actual executions.

## Developing this repository

Open the Git root containing `README.md`, `global.json`, and [Rafter.slnx](../Rafter.slnx). The enclosing workspace
directory is not itself the repository. The solution contains the two production projects, the existing xUnit/MTP
suite, and its ProcessProbe executable. The other consumer and workflow projects remain isolated test/tool inputs;
their sample tests are exercised by integration scenarios rather than discovered as additional solution test suites.

Run from the repository root with the [pinned SDK and Windows baseline](sdk-and-platform-baseline.md):

```powershell
dotnet build Rafter.slnx --configuration Release
dotnet test --solution Rafter.slnx --configuration Release --report-trx --results-directory artifacts/test-results
```

The second command uses the repository's .NET 10 native Microsoft.Testing.Platform mode. To focus an individual
suite, consult the installed [run-tests skill](../.agents/skills/run-tests/SKILL.md). CI restores, builds, and tests
this same solution; its exact commands and evidence retention are in the
[Windows evaluation workflow](../.github/workflows/evaluation.yml). Package creation and manual/editor checks have
their own instructions in [local packaging](local-package.md) and the [milestone review](milestone-review.md).

| Development reference | Purpose |
| --- | --- |
| [GitHub workflow](github-workflow.md) | Upstream/fork identity, scoped issues, PRs, and merge conventions. |
| [Skills setup](dotnet-skills-setup.md) | Installed guidance, provenance, workspace bridge, and updates. |
| [Repository organization review](repository-review.md) | Folder/project roles and the agreed next organization steps. |

## Product direction and open work

The [example design plan](example-design-plan.md) owns accepted API decisions and the broader topic inventory.
The [first working examples record](first-working-examples.md) preserves the completed milestone's selected scope.
Later capabilities remain separate work; a completed example group does not establish every reference feature or
platform. Maintained test/tool inputs now live under [tests/testassets](../tests/testassets/README.md); its inventory
identifies automated and optional-tool callers. The main suite is `tests/Rafter.Tests/`, grouped by responsibility.

## Evaluation and investigation history

These reports preserve evidence for their stated revisions. Their original commands, project names, and test counts
are historical snapshots; use the development entry points above for the current checkout.

| Record | Evidence and context |
| --- | --- |
| [Dependency decisions](dependency-decisions.md) | Initial shortlist and adoption gates. |
| [Evaluation harness](evaluation-harness.md) | Original SDK, test runner, and consumer scaffolding. |
| [Parser review](parser-review.md), [evaluation](parser-evaluation.md) | System.CommandLine source review and executable contract checks. |
| [Renderer review](renderer-review.md), [evaluation](renderer-evaluation.md) | Spectre.Console compatibility and comparison probes. |
| [Renderer measurements](renderer-measurements.md), [samples](renderer-samples.md) | Recorded startup/allocation observations and captures. |
| [Skills review](dotnet-skills-review.md) | Selection rationale for project-local skills. |
| [Aspire shutdown review](aspire-process-shutdown-review.md) | Upstream inspiration and process-lifetime considerations. |
| [Milestone review](milestone-review.md) | Completed baseline, terminal/editor observations, and verification limits. |
| [Process inheritance investigation](process-test-isolation.md) | Failed attempts, deterministic reproduction, and mixed-launcher fix. |

The previous implementation is preserved separately under [reference examples](../reference/examples/README.md)
and [reference output](../reference/output/README.md). Those snapshots describe the old project; some links in its
output notes intentionally depend on old files outside the preserved subset.
