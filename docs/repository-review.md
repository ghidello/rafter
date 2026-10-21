# Repository organization review

Reviewed on 2026-10-21 for [issue #25](https://github.com/sotsera/rafter/issues/25), after the
[first milestone](milestone-review.md). This is a structural review of files, project references, package contents,
fixture consumers, and CI entry points. It is not a new correctness audit of every method. Recommendations below
were initially proposed. The approved sequence and navigation implementation are recorded below; test/asset moves
and the later organization work remain separate changes.

## What is actually in the checkout

The merged baseline `eda3065` contains **195 tracked files** and **11 C# projects**. The local inspection also found
**2,241 ignored files**: 1,690 under `artifacts/`, 256 under `tests/`, 240 under `fixtures/`, and 55 under `src/`.
Those local counts change after builds and reviews. The maintained repository is much smaller than a view including
all generated files suggests.

| Area | Tracked files | Purpose | Assessment |
| --- | ---: | --- | --- |
| `src/` | 38 | Runtime and analyzer projects, package assets. | Keep this boundary. Only the runtime and analyzer DLLs enter the Rafter package. |
| `tests/` | 21 | One xUnit/MTP project containing evaluation, runtime, analyzer, package, and real-process tests. | Useful coverage; organization and the `Evaluation` name have fallen behind its broader role. |
| `fixtures/` | 43 | Source programs and projects built/launched by tests and review tools. | Active infrastructure, including the original parser/renderer probes; not disposable build output. |
| `examples/` | 10 | Eight consumer scripts, their README, and development authoring targets. | Keep small, runnable, and focused on user-facing syntax. |
| `docs/` | 27 | Product decisions, runtime guides, setup, evaluation reports, and previews. | Current guidance and historical evidence need a clearer reading order. |
| `reference/` | 34 | Preserved old examples and output previews. | Keep unchanged as reference material; no build or runtime dependency on this folder was found. |
| `tools/` | 6 | Renderer measurements, terminal/editor verification, and the editor probe. | Keep outside shipping code; distinguish current runtime checks from dependency comparison tools. |
| `.agents/` | 8 | Three installed skills, a filter catalog, source manifest, license, and supporting files. | Deliberate maintained guidance; retain provenance and avoid editing vendored instructions incidentally. |
| `.github/` | 1 | Windows evaluation workflow. | Clear single CI entry point. |
| Root files | 7 | README, agent guidance, package versions, SDK/NuGet configuration, license, and ignore rules. | Appropriate root-level configuration. |

The table describes the baseline before this review document was added. `artifacts/`, `bin/`, `obj/`, and
`TestResults/` are already ignored. `artifacts/` holds packages, test reports, measurements, and editor/terminal records;
some evidence is referenced by the documents. Removing it would reduce local clutter but would also remove those
local records. A retention/cleanup policy should distinguish disposable rebuild output from evidence worth keeping.

The enclosing `rafter.v2/` workspace is not the Git root: the repository is its `rafter/` child. In that parent,
`.agents/skills` is a junction to the repository's installed skills, documented in [skills setup](dotnet-skills-setup.md).
It supports this chat's current working directory and is not a duplicate installation. The inspected
`.skill-install-temp/codex/` directory was empty. That leftover is a separate cleanup candidate; neither parent entry
was changed by this review.

## Projects and build boundaries

| Projects | Count | Role |
| --- | ---: | --- |
| `src/Sotsera.Rafter`, `src/Sotsera.Rafter.Analyzers` | 2 | Production runtime and packaged authoring analyzer. |
| `tests/Rafter.Evaluation.Tests` | 1 | Main automated suite and current CI build/test entry point. |
| `fixtures/processes/ProcessProbe` | 1 | Real child-process program, referenced by the test project. |
| `fixtures/consumers/project` | 1 | Package-free host/CLI compatibility consumer. |
| `fixtures/runtime/project` | 1 | Current runtime consumer and terminal review host. |
| `fixtures/renderer/{full,ansi,framework}/project` | 3 | Dependency comparison and measurement consumers. |
| `fixtures/workflow/{src,sample-tests}` | 2 | Isolated library/test workflow exercised by the build/test examples. |

At the reviewed baseline there was **no `.sln` or `.slnx`**. CI built the main test project, which references both
production projects and ProcessProbe. Other consumers are built in isolated workspaces by integration checks or
explicitly by the review tools. The fixture test project is executed as an external-tool scenario; it is not missing
from an existing solution or CI solution filter.

Runtime package dependencies are System.CommandLine and Spectre.Console. Roslyn, the test framework, assertions, and
the comparison probes remain development/test concerns. The package-only tests check that the package contains just
the two intended DLLs, namespace-import targets, and README; they also check its dependency manifest.

Small or empty `Directory.Build.props`, `Directory.Build.targets`, and fixture package files are deliberate import
boundaries. They stop unrelated ancestor configuration from changing isolated consumers. Repeated `Program.cs`,
`probe.cs`, and `Consumer.csproj` names represent different hosting or dependency scenarios, often with shared bodies.
Their names alone are not evidence of redundant files.

## Findings and proposed order

The follow-up discussion approved three delivery groups: navigation, tests/test assets
([#28](https://github.com/sotsera/rafter/issues/28)), then documentation/tools and an evidence-retention policy
([#29](https://github.com/sotsera/rafter/issues/29)). Navigation is implemented under
[issue #27](https://github.com/sotsera/rafter/issues/27): root [Rafter.slnx](../Rafter.slnx) includes both production
projects, the existing main test suite, and ProcessProbe; CI uses it for restore/build/test. The
[documentation index](README.md) separates current guidance from evaluation history. The approved asset folder name
is **`tests/testassets/`**, replacing the initial `tests/Fixtures/` suggestion in this review. No test/asset files
have moved in the navigation step.

1. **Make navigation explicit first.** Open the actual repository root, add a short documentation index, and introduce
   a root `Rafter.slnx` for the maintained development projects. Keep isolated consumers and comparison probes outside
   its normal build/test membership unless their role warrants inclusion. Any solution/test wiring change needs its
   own discovery and consumer verification.
2. **Organize the existing test suite before splitting projects.** Rename its broader role to `Rafter.Tests` and group
   files by Commands, Inputs, Targets, Processes, Authoring, Packaging/Consumers, and dependency evaluations. Retain one
   test project initially. Its name also appears in friend-assembly configuration, probe runtimeconfig paths, CI,
   and documentation, so a rename requires a coordinated change rather than a directory move alone. Fixture moves
   also affect the review tools.
3. **Put fixture ownership next to the tests.** Use the approved `tests/testassets/` tree grouped into consumers,
   processes, workflows, and evaluations. The current source-copy rules link C#
   directly from fixtures and copy trees into `Fixtures/` beside the test executable. `IsolatedConsumer` then copies
   the full fixture tree for most consumers. Centralize that mapping and consider selecting only each consumer's
   required inputs. Preserve real file-app/project coverage and every comparison still exercised by tests or tools.
4. **Separate current documentation from the historical trail.** Keep a small current index for getting started,
   authoring/API contracts, development, and platform limits. Give completed parser/renderer evaluations and milestone
   reports an explicit history section. Preserve source/license references, failed attempts, and links when moving
   reports. The closeout has updated the main status summaries; it has not rewritten every historical report.
   A full local-file link check found nine unresolved occurrences inside the preserved old output notes. Their
   `reference/output/README.md` already explains that these are partial snapshots of the old documentation tree.
   Keep that boundary clear instead of treating those old targets as missing current-project documentation.
5. **Review local evidence retention and optional tools.** Group renderer comparison scripts separately from current
   runtime/editor verification. Decide which captured artifacts to retain before adding a bounded cleanup command.
   Avoid tracking generated evidence or deleting preserved reference material as a way to simplify navigation.

These proposals can be delivered in small PRs: navigation/documentation first, coordinated test/fixture organization
second, and optional evidence cleanup last. They do not require new product APIs or dependency upgrades.

## Source organization assessment

The production split is appropriate for the current scope: a small public authoring surface delegates to internal
input preparation, target planning, output, cancellation, and process ownership. The analyzer is a separate project
with no runtime Roslyn dependency. There is no demonstrated need for additional production projects or a new layering
framework.

The broad `Internal/` folder will become harder to scan as features grow. Natural groups are Inputs, Invocation,
Targets, Output, and Processes. Grouping those files is worth discussing after the test/fixture map is clearer; it
does not itself justify new abstractions or namespace changes. The largest production file is `WindowsProcess.cs`
(519 lines at this baseline), containing handle ownership and interop declarations. Any later decomposition must keep
creation-time job containment and explicit disposal readable. File length alone is not a defect.

The immediate maintenance need is clearer ownership and navigation, particularly around fixtures, current docs, and
generated evidence. No tracked file was demonstrated to be safely removable solely by this structural review.

## Verification of this review

Counts came from Git's tracked/ignored inventories, not from treating every disk file as maintained source. The project
references, linked/copied fixture items, package checks, CI definition, tools, and example directives were inspected.
The original documentation closeout changed no runtime, project, workflow, skill source, or fixture source.
The subsequent navigation step adds the solution and changes CI's entry point; it preserves project/test/fixture
sources. The [milestone summary](milestone-review.md) identifies the original implementation baseline and CI runs.
The original closeout's inline-Markdown file-target inventory checked 342 links across 45 tracked Markdown files:
the only nine unresolved occurrences were those documented archival links under `reference/output/`. All 177 local
file targets in its seven changed documents resolved. These checks cover neither external URLs nor fragment anchors.

Navigation verification on 2026-10-21 listed the exact `Rafter.slnx` and confirmed its four intended projects.
`dotnet test --solution Rafter.slnx --configuration Release --report-trx --results-directory artifacts/test-results/navigation`
then passed **413 tests, zero failed/skipped**. This verifies the new solution's test discovery, not just its build.
The report is under `artifacts/test-results/navigation/`. All 79 local file targets in the four navigation documents
resolved, and the solution project paths exist. Subsequent CI evidence is recorded in the delivery PR.
