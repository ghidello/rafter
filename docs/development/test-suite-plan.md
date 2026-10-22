# Test suite reorganization plan

Agreed on 2026-10-22. Test reorganization is delivered under [issue #32](https://github.com/sotsera/rafter/issues/32);
Linux CI follows in a separate PR under [issue #33](https://github.com/sotsera/rafter/issues/33).
See the [testing guide](testing.md) for current commands and observed results. This plan adapts the user-provided
`xunit-long-running-test-strategy.md` to Rafter's existing tests, assets, and toolchain. The attachment is a design
reference, not a requirement to copy its build files or change the repository's SDK/package policy.

## Recommendation

Use three test projects, one main solution, and one portable solution filter. Use project boundaries for dependencies
and operating-system ownership, and the single `LongRunning=true` trait for opt-in execution cost. Keep fast,
self-contained integration tests in ordinary runs. A Windows-only test need not be long-running.

| Agreed project | Responsibility | Ordinary versus long-running |
| --- | --- | --- |
| `tests/Rafter.UnitTests/` | In-process command/input/target contracts, analyzer diagnostics, rendering/coordination, parser/renderer dependency contracts, and process configuration that never launches a child. | Ordinary by default. No consumer build workspace or ProcessProbe reference. |
| `tests/Rafter.IntegrationTests/` | Portable host/filesystem behavior and SDK/compiler/file-app/project/package consumer verification. | Host/filesystem checks remain ordinary; build/restore/package consumer checks carry `LongRunning=true` on their classes or methods. |
| `tests/Rafter.WindowsTests/` | Windows process ownership, jobs, pipes, handle inheritance, Windows process examples, and Windows-only portions of consumer scenarios. | Short native checks remain ordinary; deliberate grace/drain waits, expensive fault scenarios, and consumer builds are individually long-running. |

The portable boundary is a target to verify, not an existing Linux/macOS support claim. Moving a test or adding a
target framework suffix does not establish its platform behavior.

```text
Rafter.slnx                         # All maintained production and test projects
Rafter.Portable.slnf                # View of that solution excluding WindowsTests and ProcessProbe
tests/
  build/
    Rafter.Test.props              # Explicit imports in the three real test projects
    Rafter.Test.targets
    testconfig.json
  Shared/                         # Small shared harness sources; no extra test project initially
  Rafter.UnitTests/
  Rafter.IntegrationTests/
  Rafter.WindowsTests/
  testassets/                     # Retain the existing source assets and inventory
```

Continue grouping files by feature inside each project. Shared harness files can be linked into the integration
and Windows projects, with their workspace-copy mapping in a shared import. Do not create a support assembly merely
to move one small launcher. Keep the ProcessProbe reference and its runtimeconfig selection with WindowsTests;
update friend-assembly declarations for actual internal-API consumers.

## Why a Windows project instead of one mixed platform project

We currently have one concrete native implementation, with distinct Windows APIs and lifecycle tests. A dedicated
Windows project makes dependencies and CI selection visible and allows a portable build/test view to exclude it
before its test application or fixtures start. A project name alone does not make a solution run skip that project.
[Solution filters](https://learn.microsoft.com/en-us/visualstudio/msbuild/solution-filters?view=visualstudio)
select a subset from the same underlying solution; they do not replace its project inventory.

Use SDK-compatible project identifiers and a root-relative solution filename in the portable filter. The pinned
Windows SDK requires backslash project identifiers; Linux discovery/build/execution is verified separately in #33.
Do not assume current MSBuild documentation establishes this exact repository/SDK combination without that check.

Also attach an assembly-level `OS=Windows` trait for discovery and use centralized Windows-only Fact/Theory
attributes for an explicit skipped result when a developer directly runs that project on another OS. The trait and
`SupportedOSPlatform` annotations alone are not execution guards. xUnit supports
[conditional skipping](https://xunit.net/docs/getting-started/v3/whats-new#dynamically-skippable-tests), but its runtime
skip pipeline can still construct test classes and run setup. Keep unsupported native work out of constructors,
discovery, and fixture setup. The portable filter is the primary exclusion mechanism; conditional skips are a backstop.

Do not introduce a general `PlatformTests` project and Linux/macOS traits before there are concrete native tests
for those systems. Revisit the boundary when that implementation exists. Runtime platform support remains a
separate product decision.

## Move tests by behavior, not just by their current folder

| Current tests | Proposed destination or split |
| --- | --- |
| `InvocationTests`, `TypedInputTests`, `TargetTests`, `AuthoringAnalyzerTests`, `TerminalCoordinationTests` | UnitTests; retain controlled writers, fake signal subscriptions, and real in-memory compiler analysis. |
| In-process cases in `ParserTests`, `ParserBaselineTests`, and `RendererTests` | UnitTests under dependency contracts. Keep them in regression coverage rather than treating them as disposable historical probes. |
| Parser file-app case and two renderer consumer cases | IntegrationTests, long-running. These currently hide build/launch work inside otherwise in-process classes. |
| `RuntimeHostTests` | IntegrationTests, ordinary; retain its nonparallel process-working-directory collection. It mutates real host state despite being very fast. |
| `ConsumerTests`, `TypedInputConsumerTests`, `TargetConsumerTests`, and portable `RuntimeConsumerTests` cases | IntegrationTests, long-running. Audit each scenario for OS assumptions before declaring it portable. |
| `PackageConsumerTests` | IntegrationTests, long-running. Preserve fresh package caches and package-only consumers. Improve phase/host failure reporting; split the large fact only when its package-build lifetime and consumer isolation stay explicit. |
| Configuration-only cases in `ProcessTests` | UnitTests in a process-configuration class; remove their unnecessary native-process collection membership. |
| Real-process `ProcessTests`, `WindowsProcessTests`, `WindowsHandleInheritanceTests`, `ProcessObservationTests` | WindowsTests. Keep the deliberate handle-inheritance collection isolation and real concurrent-launch scenarios. |
| `ProcessConsumerTests` | WindowsTests, long-running, because these scripts execute Rafter-owned Windows processes. |
| Windows process portion of `RuntimeConsumerTests.Public_api_works_in_real_consumers` | Extract into WindowsTests, long-running; retain its portable API checks in IntegrationTests. |

The present baseline has 413 discovered cases, including theory rows. Inventory each original case and its assertions
before moving it. Some composite cases may become multiple focused cases: document that mapping and justified count
changes instead of preserving 413 artificially or using a total alone as proof that nothing was lost.

Existing test assets remain under [testassets](../../tests/testassets/README.md). SDK subprocesses must continue to
use real file apps/projects and settle before temporary workspace disposal. Do not turn lifecycle tests into mocks
as a way to make the quick run faster.

## Adapt the attached execution policy

Adopt `LongRunning=true`, `IncludeLongRunningTests=true`, and support `LongRunningTestProject=true` only when an entire
project qualifies. The three proposed projects need not all use the project-wide switch: mixed integration/Windows
projects should mark expensive classes or methods. Do not add `Explicit=true` as a second opt-in mechanism.

Use explicit imports from `tests/build/` instead of copying the attachment's unguarded `tests/Directory.Build.*` files.
Our ProcessProbe, standalone consumers, and sample workflow test project also live beneath `tests/`. They must not
inherit main-suite test-project properties, LongRunning filters, runner configuration, or generated xUnit attributes.
The root `Directory.Build.props` assumed by the attachment does not currently exist; do not import a nonexistent file
or create one solely to match the template.

The shared imports should centralize real-test-project runner settings, result-directory configuration, and appended
filter arguments. Preserve existing arguments. Read project-wide classification after project properties have been
set; keep the inclusion opt-in an invocation property so changing mode works without rebuilding. Changing actual
compiled traits still requires rebuilding. [xUnit's MTP options](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform)
provide the trait filters used by the attachment.

Default runs exclude LongRunning bodies; enabled full runs include ordinary and long-running tests. Do not hide
arbitrary zero-test failures: tolerate exit 8 only for intentionally excluded all-long-running modules, or explicitly
in the guarded long-running-only profile. Require a nonempty whole-run selection and inspect per-project results.
The [SDK whole-run minimum](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp#whole-run-and-per-module-minimums)
counts skipped tests too, so a minimum of one does not prove that required Windows or console behavior executed.

Our SDK 10.0.402, xUnit v3 4.0.2, and MTP 2.5.1 match the attachment's stated baseline. Retain `xunit.v3.mtp-v2`,
the existing package versions, and `global.json`'s `latestPatch` policy. Current online docs also describe newer
SDK/MTP zero-test behavior; verify the attachment's exit-8/exit-9 expectations on our pinned toolchain rather than
adopting those newer semantics or upgrading dependencies as part of this move.

## Run profiles

These profiles are implemented for the reorganized layout; the testing guide records their observed selections.
They use native MTP syntax. All test projects remain in `Rafter.slnx`; the portable filter selects only the two
portable test projects and required production dependencies.

```powershell
# Ordinary Windows run: unit, ordinary portable integration, and ordinary Windows integration
dotnet test --solution Rafter.slnx -c Release --minimum-expected-tests 1

# Full Windows regression: the PR acceptance baseline, including consumer/package checks
dotnet test --solution Rafter.slnx -c Release -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1

# Ordinary portable run: suitable for Linux/macOS after that platform is verified
dotnet test --solution Rafter.Portable.slnf -c Release --minimum-expected-tests 1

# Full portable regression, including SDK/compiler/package consumers
dotnet test --solution Rafter.Portable.slnf -c Release -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1

# Long-running tests only, on Windows; intentionally empty ordinary modules are allowed
dotnet test --solution Rafter.slnx -c Release -p:IncludeLongRunningTests=true --filter-trait LongRunning=true --ignore-exit-code 8 --minimum-expected-tests 1 --max-parallel-test-modules 1
```

On Linux/macOS the long-running-only variant must select the portable filter. A unit-only developer loop can target
the UnitTests project directly, avoiding builds and source copying for consumer/Windows projects. Merely filtering
test cases during a whole-solution invocation still builds the included project graph.

Start full consumer/native validation with one concurrent test module, retaining useful within-test concurrency.
Project splitting changes runner-level parallelism; xUnit collection isolation does not serialize different test
executables. Measure before relaxing that limit, given the process-output deadlines and earlier load-sensitive
failures. This is an initial reproducibility choice, not a permanent performance requirement.

CLI MSBuild filtering does not automatically configure IDE test execution or direct executable runs. Document and
verify equivalent explicit selection there; an IDE's generic Run All must not be advertised as the CLI quick profile.

## CI and environment checks

Keep full Windows regression on each PR when the default exclusion is introduced: set `IncludeLongRunningTests=true`
in the same change. Do not make packaging and process cleanup opt-in in CI accidentally. Retain the separate current
redirected-output check and package creation.

After the reorganization merges, add Linux ordinary and full portable verification in the separate #33 PR, initially
as a candidate boundary that must be proven. Use the portable filter for restore/build/test, not just an OS trait on
an already-launched Windows module.
Add macOS coverage only with an explicit runner/budget decision and observed results. Portable tests passing there
would verify those APIs and consumers, not Windows process ownership on another OS.

The [terminal/editor tools](../../tools/README.md) remain separate, explicitly selected environment checks. Do not add
an empty fourth desktop test project. If these checks later become xUnit tests, give them LongRunning classification
and their actual terminal/editor prerequisites. Missing required SDK/tools on a selected CI job should fail setup;
OS/capability skips must be explicit and cannot count as coverage. Split graceful-console behavior from headless
forceful fallback so one passing branch does not imply the other executed.

Retain ordinary runner pass/fail output; request TRX/diagnostics when useful for CI, failures, and migration inventories.
Do not generate permanent success bundles for every unit test. Preserve the existing
[evidence policy](generated-files.md), acceptance baselines, and investigation records rather than silently applying
the attachment's final retention advice as authorization to delete them.

## Delivery and acceptance sequence

1. These project boundaries, portable selection, LongRunning defaults, and full-PR CI policy are agreed. Create scoped
   implementation work under #32; keep Linux CI in the separate #33 PR.
2. Record the current discovered cases and dependency/side-effect map. Split mixed classes/scenarios and move them
   while preserving assertions. Centralize only the shared harness/configuration that the new projects actually need.
3. Update friend assemblies, namespace references, copied workspace mappings, ProcessProbe runtimeconfig paths,
   exact solution membership, portable filter, CI, and current docs together. List both solution views immediately
   after wiring them and verify their intended project references.
4. Verify direct-project and exact solution/filter discovery, ordinary/full/long-only selection, no-build mode
   switching, and that excluded bodies/fixtures do not execute. Check a bogus trait and an entirely empty guarded
   selection, plus a deliberate assertion failure in a disposable trial. None may become a false green.
5. Run full Windows verification now and Linux portable verification in #33; account for every original assertion
   and any new split cases.
   Check cleanup and unrelated process survival. Repeat affected terminal/editor checks when their caller, package,
   or workspace paths change. Record unavailable capability paths honestly.
6. Publish the verified run profiles in the development guide. Keep this proposal's accepted decisions distinct from
   observed platform support and update the source-of-truth inventory as future tests are added.

The project split and selection policy are implemented; the testing guide records the adoption checks. Linux and
macOS execution remain unverified by this Windows-only reorganization.
