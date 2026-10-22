# Repository organization review

Reviewed on 2026-10-21 for [issue #25](https://github.com/sotsera/rafter/issues/25), after the
[first milestone](../history/milestone-review.md). This is a structural review of files, project references, package contents,
fixture consumers, and CI entry points. It is not a new correctness audit of every method. Recommendations below
were initially proposed. The approved sequence and navigation implementation are recorded below; test/asset moves
and later organization work are delivered separately.

The current layout now has one `tests/Rafter.Tests/` project grouped by responsibility, with maintained executable
inputs under [tests/testassets](../../tests/testassets/README.md). [Issue #28](https://github.com/sotsera/rafter/issues/28)
records the coordinated rename/move; the tables below retain the original review baseline rather than rewriting its
historical counts. Documentation/tool grouping and generated-evidence policy are delivered under
[issue #29](https://github.com/sotsera/rafter/issues/29), as recorded below.

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
[issue #27](https://github.com/sotsera/rafter/issues/27): root [Rafter.slnx](../../Rafter.slnx) includes both production
projects, the existing main test suite, and ProcessProbe; CI uses it for restore/build/test. The
[documentation index](../README.md) separates current guidance from evaluation history. The approved asset folder name
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
sources. The [milestone summary](../history/milestone-review.md) identifies the original implementation baseline and CI runs.
The original closeout's inline-Markdown file-target inventory checked 342 links across 45 tracked Markdown files:
the only nine unresolved occurrences were those documented archival links under `reference/output/`. All 177 local
file targets in its seven changed documents resolved. These checks cover neither external URLs nor fragment anchors.

Navigation verification on 2026-10-21 listed the exact `Rafter.slnx` and confirmed its four intended projects.
`dotnet test --solution Rafter.slnx --configuration Release --report-trx --results-directory artifacts/test-results/navigation`
then passed **413 tests, zero failed/skipped**. This verifies the new solution's test discovery, not just its build.
The report is under `artifacts/test-results/navigation/`. All 79 local file targets in the four navigation documents
resolved, and the solution project paths exist. Subsequent CI evidence is recorded in the delivery PR.

## Test and asset organization delivered

[Issue #28](https://github.com/sotsera/rafter/issues/28) keeps one
[Rafter.Tests project](../../tests/Rafter.Tests/Rafter.Tests.csproj), grouped into Commands, Inputs, Targets, Processes,
Authoring, Output, Consumers, Evaluation, and Infrastructure. The solution still contains the same four maintained
projects. Friend-assembly configuration and the process probe's test runtimeconfig path use the renamed assembly.
The [test-asset inventory](../../tests/testassets/README.md) identifies the automated and optional-tool callers for all
43 retained assets. Only three asset files changed content, to adjust their relative production-project references;
the other 40 are byte-identical to the pre-move files.

The copied consumer workspace now mirrors repository paths instead of flattening several asset trees into
`Fixtures/`. Root SDK, NuGet, and central package configuration is copied once. The working-directory example is
launched from the copied workflow directory, preserving the behavior exercised by its integration test. No runtime
policy, dependency version, test assertion, or old reference material changed.

Local Windows x64 verification on 2026-10-21 with SDK 10.0.402:

- `dotnet test --solution Rafter.slnx --configuration Release --report-trx --results-directory artifacts/test-results/testassets-first`
  passed **413 tests, zero failed/skipped**. Comparing the previous navigation TRX with this report found the same
  413 case identities after accounting for renamed namespaces and moved entrypoint arguments. This includes real
  file-app/project/package consumers and process ownership/cancellation cases.
- Both moved review consumers built in Release with **zero warnings/errors**. `tools/Verify-RuntimeTerminal.ps1`
  passed **38 cases**, and `tools/Verify-RendererTerminal.ps1` passed **18 cases**, in an actual pseudoterminal.
- `tools/Measure-Renderer.ps1 -Samples 1` built and ran all six renderer/host combinations. This was a path and
  execution smoke check, not a replacement performance conclusion. Prior runtime/renderer evidence was copied to
  `artifacts/testassets-organization/prior-evidence/` before these tools wrote their usual output locations.
- The runtime/analyzer NuGet package packed successfully with the existing package configuration. The editor tool
  and its generated package-only probe have no moved-asset references and were unchanged; VS Code was not rerun.
- The Markdown file-target inventory checked **409 targets**. Only the same nine documented archival targets under
  `reference/output/` were unresolved; all current targets resolved. This does not verify external URLs or anchors.

Raw reports remain ignored under `artifacts/`; the delivery PR records CI against its exact commit. Pre-existing
generated output at the former fixture/test paths is preserved. Its cleanup and evidence-retention policy remain
part of [issue #29](https://github.com/sotsera/rafter/issues/29).

## Documentation, tools, and evidence organization delivered

[Issue #29](https://github.com/sotsera/rafter/issues/29) groups the 28 documents formerly beside the index into
seven current guides, three design files, four development records, and fourteen historical records. The new
[generated-files policy](generated-files.md) joins the development group. The [documentation index](../README.md)
remains the entry point. Short pages at the former diagnostic/package URLs preserve the six distinct links embedded
in the existing analyzer and package README, including all four diagnostic anchors.

The [tool inventory](../../tools/README.md) separates current runtime/editor verification under `tools/verification/`
from retained renderer comparisons under `tools/evaluations/`. Each script resolves the repository from its new
location. The editor probe moves with its harness, unchanged. CI calls the moved redirected-runtime script.
The tools allocate unique evidence directories, print their paths at startup, and retain any partial results after
failure. Two successive runtime matrices produced separate manifests without replacing the earlier result.

The retention policy distinguishes rebuildable output from evidence. Local evidence has no automatic expiry; CI's
existing artifact retention remains fourteen days. No pre-existing generated output or evidence was deleted or moved.
Historical fenced commands/transcripts are unchanged, including failed attempts and original artifact paths; current
reproduction instructions live in the tool inventory. The only changes inside `.agents/` and `reference/` are the
project-authored navigation READMEs' links to moved documents. All five vendored file hashes still match the unchanged
source manifest, the old examples/output source bytes are unchanged, and the parent discovery junction is unchanged.

Local Windows x64 verification on 2026-10-21 with SDK 10.0.402:

- The two review consumers built in Release with **zero warnings/errors**. The moved runtime script passed
  **38 terminal cases** and **23 redirected cases**; the latter was also launched from the enclosing workspace to
  check repository discovery. The moved renderer-terminal script passed **18 cases**.
- The moved measurement tool built all six renderer/host combinations and recorded twelve fresh-process samples
  plus six steady-state observations with `-Samples 1`. This is execution verification, not a new performance finding.
- The moved editor harness passed **12 checks across two consumers**, using VS Code **1.143.0**, C# **2.160.4**, and
  C# Dev Kit **3.40.215**. Its separate editor exited after reporting the live diagnostics/import/completion results.
- Local file targets and active Markdown/HTML anchors resolved, including the six embedded package/analyzer URLs.
  The same nine unresolved archival paths remain under `reference/output/`; external sites and archival anchors were
  not revalidated. The raw inventory is `artifacts/docs-tools-organization/link-audit.json`.
- Legacy `artifacts/runtime-review/` and `artifacts/renderer/` files were not modified by these new tool invocations.
  New run directories are under `artifacts/evidence/`; the editor result, both runtime manifests, terminal renderer
  manifest, and measurement JSON remain available there.

Production sources, test behavior, SDK/package versions, and solution membership are unchanged. The local full .NET
suite was not repeated for these path/documentation changes; the delivery PR records the exact-commit Windows CI
regression result separately. This completes the three agreed organization groups without expanding product scope.
