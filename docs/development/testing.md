# Testing Rafter

Implemented for [issue #32](https://github.com/sotsera/rafter/issues/32) on 2026-10-22, following the
[agreed plan](test-suite-plan.md). Windows x64 covers the full suite. The separate
[issue #33](https://github.com/sotsera/rafter/issues/33) adds verified portable-suite coverage on Ubuntu 24.04 x64;
Linux-native process ownership and macOS remain unverified.

## Suite boundaries

| Project | Responsibility | Ordinary cases | Long-running cases |
| --- | --- | ---: | ---: |
| [Rafter.UnitTests](../../tests/Rafter.UnitTests/Rafter.UnitTests.csproj) | In-process command/input/target contracts, compiler analyzer checks, dependency contracts, rendering coordination, and process configuration without child launch. | 342 | 0 |
| [Rafter.IntegrationTests](../../tests/Rafter.IntegrationTests/Rafter.IntegrationTests.csproj) | Real host working-directory behavior plus file-app/project/compiler/package consumers. | 1 | 21 |
| [Rafter.WindowsTests](../../tests/Rafter.WindowsTests/Rafter.WindowsTests.csproj) | Windows process ownership, handles, pipes, cancellation, and process consumers. | 41 | 11 |
| Total | Counts include theory rows; they describe this revision rather than a permanent test-count contract. | 384 | 32 |

All three suites remain in [Rafter.slnx](../../Rafter.slnx). [Rafter.Portable.slnf](../../Rafter.Portable.slnf) selects the
two portable projects and their production dependencies, excluding WindowsTests and ProcessProbe before their test
application starts. Its membership and execution are verified on Windows and Ubuntu. The filter uses backslash
project identifiers and a root-relative solution filename; the same file works with the pinned SDK on both systems.

Each project explicitly imports [shared settings](../../tests/build/Rafter.Test.props) and
[execution policy](../../tests/build/Rafter.Test.targets). Main-suite settings do not flow through an automatic
`tests/Directory.Build.*` import to standalone assets. The integration/Windows suites share the small
[consumer launcher](../../tests/Shared/IsolatedConsumer.cs) and
[workspace-copy mapping](../../tests/build/ConsumerWorkspace.targets). UnitTests has neither a copied consumer
workspace nor a ProcessProbe reference. Other asset projects remain outside normal solution discovery; the sample
workflow test is deliberately executed by consumer scenarios.

## Run profiles

Run from the repository root using the pinned SDK and native Microsoft.Testing.Platform mode. Ordinary runs exclude
`LongRunning=true`. Platform and execution cost are independent: short native Windows checks remain ordinary.

```powershell
# Ordinary Windows tests; plain dotnet test at the root uses the same selection
dotnet test --solution Rafter.slnx -c Release --minimum-expected-tests 1

# Smallest in-process development loop
dotnet test --project tests/Rafter.UnitTests/Rafter.UnitTests.csproj -c Release --minimum-expected-tests 1

# Full Windows regression, including SDK/compiler/package and deliberate deadline tests
dotnet test --solution Rafter.slnx -c Release -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1

# Portable view: ordinary, or full with the same opt-in
dotnet test --solution Rafter.Portable.slnf -c Release --minimum-expected-tests 1
dotnet test --solution Rafter.Portable.slnf -c Release -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1

# Only long-running tests on Windows; intentionally empty ordinary modules are tolerated
dotnet test --solution Rafter.slnx -c Release -p:IncludeLongRunningTests=true --filter-trait LongRunning=true --ignore-exit-code 8 --minimum-expected-tests 1 --max-parallel-test-modules 1
```

For long-only portable execution, select `Rafter.Portable.slnf` instead. After building, `--no-build` reuses the test
assemblies; switching `IncludeLongRunningTests` still changes selection because its arguments are evaluated at
invocation time. Changing a compiled trait requires rebuilding. The inclusion property includes ordinary tests too;
it does not itself mean long-only. Enabling the trait filter without the inclusion property leaves the exclusion active.

Filtering does not remove projects from a solution build. Use the UnitTests project directly to avoid building and
copying the consumer/Windows graphs. Full and long-only profiles start with one concurrent test module; individual
tests still exercise their intended concurrency. xUnit collections do not synchronize separate runner processes.

## Selection policy and environments

Apply `[Trait("LongRunning", "true")]` to an expensive class or method. Use `LongRunningTestProject=true` only when
every test in a project qualifies; none of these three mixed-purpose suites requires that switch today. It generates
the assembly trait and tolerates exit 8 only while that entirely long-running project is intentionally excluded.
Do not add `Explicit=true` as a second opt-in mechanism.

Ordinary projects and enabled all-long-running projects retain normal zero-selection failures. The long-only solution
profile explicitly tolerates empty modules and requires a nonempty global selection. The global minimum counts skips;
inspect module results and capability skips rather than treating one discovered/skipped test as coverage.

WindowsTests has the assembly trait `OS=Windows` and centralized
[WindowsFact/WindowsTheory guards](../../tests/Rafter.WindowsTests/Infrastructure/WindowsTestAttributes.cs).
Wrong-OS direct/IDE runs should report explicit skips. A project name, target-framework suffix, or
`SupportedOSPlatform` annotation alone does not prevent execution. Keep native work out of constructors/discovery/
fixtures, because conditional skips can occur after setup. The portable view is the primary project exclusion.

Real host working-directory changes retain their nonparallel collection in IntegrationTests. The deliberate native
handle-inheritance reproduction retains its isolated collection in WindowsTests. All SDK children use isolated
workspaces, and process/stream settlement still precedes cleanup.

Graceful console signalling and forceful termination now have separate cases. The graceful case declares
`Requires=Console` for discovery. A headless host explicitly skips the
graceful-only case after cleaning up its probe; it does not silently substitute forceful shutdown as proof of a
graceful signal. Forceful and headless ownership cases still execute. To verify the graceful path with a console:

```powershell
dotnet test --project tests/Rafter.WindowsTests/Rafter.WindowsTests.csproj -c Release --filter-method '*Scoped_graceful_signal_settles_output' --minimum-expected-tests 1
```

Run that command with both terminal streams attached. Missing required SDK/package access on a selected consumer
run is a failure, not a reason to skip it. The [terminal/editor tools](../../tools/README.md) retain their own actual
terminal, VS Code, extension, and package prerequisites. They are not included in the xUnit counts above.

MSBuild's default filters apply to project/solution CLI invocations, including no-build runs. Direct executables,
`dotnet run`, `--test-modules`, and IDE Run All do not automatically inherit that policy. Apply equivalent filters
explicitly in those runners; do not advertise IDE Run All as the CLI ordinary profile.

## Verification and coverage mapping

The former 413-case suite maps to 416 cases. Its 171 original test methods map to 173 methods: 169 method bodies are
unchanged. Only two composite scenarios were split:

- The two file-app/project runtime-API cases retain their portable assertions and move their process assertions into
  two Windows consumer cases.
- The graceful-or-force case becomes separate graceful and forceful cases, preserving both sets of assertions and
  making console availability visible.

Moving the parser/renderer consumers and thirteen process-configuration rows changes their project/class ownership,
not their assertions. The migration inventory accounts for every original case, and the ordinary and long-only case
sets are disjoint and together equal the full set. Raw source/method/case maps and reports are retained locally under
`artifacts/test-suite-reorganization/`.

Observed locally on Windows x64 / SDK 10.0.402:

- Unit project: **342 passed, 0 failed/skipped**. Ordinary root: **383 passed, 0 failed, 1 console-capability skip**
  from 384 cases, about ten seconds with prebuilt assemblies on this run.
- Full root: **415 passed, 0 failed, 1 console-capability skip** from 416 cases. Long-only: **32 passed, 0 failed/skipped**.
- Portable ordinary: **343 passed, 0 failed/skipped**; portable full: **364 passed, 0 failed/skipped**. These are
  Windows observations, not Linux execution evidence.
- The separate pseudoterminal graceful-only invocation: **1 passed, 0 failed/skipped**. A headless skip is not counted
  as that verification.
- Direct IntegrationTests and WindowsTests invocations discover their ordinary cases. Bare root `dotnet test`
  discovers the same 384-case ordinary selection with the portable filter file present.
- A bogus trait on UnitTests returns **8**; ignoring 8 while requiring a nonempty global selection returns **9**.
  An owned two-test trial verifies no-build switching, class-fixture/body exclusion, project-wide classification,
  scoped empty handling, and preservation of a real assertion failure (**exit 2**). No deliberate failure remains
  in maintained test sources.

## Linux CI scope

The separate `Linux portable evaluation` job targets Ubuntu 24.04 x64. It uses the same SDK selection and native
MTP configuration as Windows, with `Rafter.Portable.slnf` as the entry point for restore, build, and both test profiles:

```bash
dotnet restore Rafter.Portable.slnf
dotnet build Rafter.Portable.slnf --configuration Release --no-restore
dotnet test --solution Rafter.Portable.slnf --configuration Release --no-build --minimum-expected-tests 1 --max-parallel-test-modules 1
dotnet test --solution Rafter.Portable.slnf --configuration Release --no-build -p:IncludeLongRunningTests=true --minimum-expected-tests 1 --max-parallel-test-modules 1
```

CI adds TRX reporting and separate ordinary/full result directories. It records the actual OS, runner image, SDK,
and filter membership, then checks that WindowsTests and ProcessProbe have no restore/build output in the clean
checkout. Required consumer restores and package builds remain failures when unavailable; they are not skipped.
The full integration profile creates and consumes its own local package in real project/file-app hosts.

Observed on 2026-10-22 for commit `5db9f74840d3a539b9c773a2f828a4a7335feae4` in the
[first Linux run](https://github.com/ghidello/rafter/actions/runs/39771953044/job/119801407841): Ubuntu 24.04.5 LTS x64,
SDK 10.0.402, runtime 10.0.13, runner image `20261012.334.1`. The build reports **0 warnings/errors**. Ordinary
selection reports **343 passed, 0 failed, 0 skipped**; full selection reports **364 passed, 0 failed, 0 skipped**
(342 unit and 22 integration cases). The existing filter, consumers, package layout, and test assertions required no
portability changes. Exact-commit results for the final delivery belong to the PR.

This job exercises portable command/input/target/output behavior, in-process analyzer checks, and managed
SDK/compiler/package consumers. It does not establish Linux-native child-process ownership, process-tree cleanup,
interactive terminal behavior, VS Code integration, AOT, macOS, or a wider Linux distribution/architecture matrix.
Use the portable filter on Linux; a whole-solution invocation still builds the Windows project graph before skips.

PR CI also retains the ordinary and explicitly enabled full Windows profiles, packaging, and redirected runtime checks.
Exact-commit CI results belong to the delivery PR. Existing evidence and older build output are retained according
to the [generated-files policy](generated-files.md); no automatic cleanup or dependency upgrade accompanies this split.
