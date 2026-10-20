# Evaluation harness

Issue [#1](https://github.com/sotsera/rafter/issues/1) establishes the environment for the parser and renderer evaluations.
It does not implement the Rafter runtime. The seven files in examples remain syntax drafts; reference contains historical
snapshots, and the HTML previews show illustrative output.

## Review using the installed skills

- **scaffold-dotnet-test-project:** there was no existing project or solution. Use one project-oriented test entry point,
  with no placeholder runtime, production reference, solution, or xUnit v2 template. Its generic unit-only smoke advice is
  superseded here by issue #1's explicit requirement for real consumer execution checks.
- **run-tests:** global.json selects native MTP. Use --project and direct MTP report arguments; no VSTest bridge separator
  or Microsoft.NET.Test.Sdk package is needed. The TRX extension is explicitly referenced.
- **csharp-scripts:** run an actual file-based app with the selected SDK, explicit stable language settings, and a narrowly
  scoped include. Keep it separate from the ordinary project and use managed execution. Do not remove the checked-in fixture.

No repository-specific skill overlays exist yet. The project settings and commands below provide the initial contract.

## Toolchain and dependencies

global.json selects SDK 10.0.402 with latestPatch and allowPrerelease=false. This permits servicing updates in the
10.0.4xx band while excluding .NET 11 previews. Both the test project and consumers target net10.0 with C# 14.

| Direct test dependency | Version | Purpose | License |
| --- | --- | --- | --- |
| xunit.v3.mtp-v2 | 4.0.2 | xUnit v3 tests and native MTP v2 integration | Apache-2.0 |
| AwesomeAssertions | 9.6.0 | Behavioral assertions | Apache-2.0 |
| Microsoft.Testing.Extensions.TrxReport | 2.5.1 | Persist test results and per-test output | MIT |

Versions live in Directory.Packages.props. The xUnit v3 product uses a 4.x package version; this does not select xUnit v2
or a different testing framework. MTP and MTP.MSBuild resolve transitively to 2.5.1. Other transitive packages include
xUnit's runner/core/assertion packages, xunit.analyzers 2.2.0, and the MTP telemetry dependencies.
These three test-tool references have PrivateAssets=all and the test project is not packable. There is no shipping project.
Issue #2 adds System.CommandLine 2.0.13 as a fourth private evaluation reference; its net8.0 asset adds no transitive
packages. See the [parser evaluation](parser-evaluation.md) for the candidate-specific graph and checks.

Versions and dependency ranges were inspected in official NuGet metadata before restore. The full resolved graph is
reported by the command below. These explicit versions are a baseline, not a committed transitive lock file.

Sources: [xUnit MTP integration](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform),
[native dotnet test](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test),
[xUnit package](https://www.nuget.org/packages/xunit.v3.mtp-v2/4.0.2),
[assertion package](https://www.nuget.org/packages/AwesomeAssertions/9.6.0),
[TRX extension](https://www.nuget.org/packages/Microsoft.Testing.Extensions.TrxReport/2.5.1).

## Run locally

From the repository root, in PowerShell:

```powershell
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet --info
dotnet restore tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj
dotnet build tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --no-restore -bl:artifacts/build.binlog
dotnet test --project tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj --configuration Release --no-build --report-trx --results-directory artifacts/test-results
dotnet list tests/Rafter.Evaluation.Tests/Rafter.Evaluation.Tests.csproj package --include-transitive
```

Run the commands in order and stop on a nonzero exit. The no-build test command intentionally consumes the preceding
Release build; after changing source, build again or omit no-build. The direct project command is also the CI entry point.

## What the checks establish

Four baseline test cases cover the file-based and project consumers:

- Arguments preserve empty strings, spaces, quotes, trailing backslashes, Unicode, and option-like tokens.
  The consumer observes the selected working directory and executes on the .NET 10 runtime.
- An intentional failure returns exit 23 while preserving separate, exact stdout and stderr text.

Each test copies only the consumer fixtures and explicit SDK/NuGet configuration to a new temporary directory outside
the repository, including a working-directory name with a space. Local Directory.Build.props, Directory.Build.targets,
and Directory.Packages.props stop ancestor build/package imports. Both consumers explicitly select their own target and
language settings; the file app explicitly disables Native AOT. The project links only the shared probe source.
A temporary directory is deleted only after its test's launched processes and output reads have settled.

The harness now also copies the parser fixture, with the root central package file placed beside that fixture to
override the package-free baseline consumer settings. Its regression cases are described in the parser evaluation.

The harness records SDK selection, build/run commands, working directory, exit code, stdout, and stderr in test output.
Both fixtures are built through dotnet, then executed through dotnet run --file or --project with --no-build.
The SDK selected inside each isolated directory must remain in the permitted 10.0.4xx band.

The process launcher is test infrastructure for controlled fixtures. Its timeout kills the child tree and waits for exit
and output; it is not Rafter's future launch-time containment implementation or evidence of descendant ownership.
The CI job has an independent 15-minute limit. Shared SDK caches remain outside the per-test temporary directories.

## Windows CI

[Evaluation workflow](../.github/workflows/evaluation.yml) runs on windows-2022 x64 for pull requests and pushes to main
or build/** branches. Build-branch pushes also let the development fork validate this first workflow before it merges.
Its stable check name is **Windows evaluation**. Actions are pinned to inspected commit SHAs. The job records the runner
image version, Windows identity, SDK information, build binlog, and TRX results. The windows-evaluation artifact is retained
for 14 days even when a later step fails. Workflow permissions are contents: read.

CI uses the same restore, Release build, and native-MTP test commands shown above. The named baseline SDK is installed
from global.json. Runner images can contain additional SDK servicing patches; the resolved version is recorded.

## Observed results

Local verification on 2026-10-20:

- Windows 10.0.26220, win-x64; SDK 10.0.402; MSBuild 18.9.15+a8dd0577c.
- Consumer runtime .NET 10.0.13. The installed dotnet multiplexer reports a .NET 11 RC host, but SDK resolution and the
  actual consumer runtime were verified independently as .NET 10.
- Restore succeeded; Release build succeeded with 0 warnings and 0 errors.
- Native MTP reported **Passed: 4, Failed: 0, Skipped: 0** (35 seconds for the initial run).
- TRX written to artifacts/test-results/Rafter.Evaluation.Tests_net10.0_x64.trx.

Windows CI on 2026-10-20: [run 39485041548](https://github.com/ghidello/rafter/actions/runs/39485041548) passed for
implementation commit 47299dbb223a8419c28204fb236c6d4817e757a6. The downloaded TRX reports **Passed: 4, Failed: 0,
Skipped: 0**. The retained windows-evaluation artifact includes SDK/OS/image details, the build binlog, and test output.
Runner: Windows Server 2022 Datacenter build 20348, x64; win22 image 20261012.333.1; SDK 10.0.402;
MSBuild 18.9.15+a8dd0577c. This run used the development fork's build-branch push event. Upstream Actions was enabled
but did not create a run for the first workflow PR. After PR #6 merged, the upstream main push passed in
[run 39486320504](https://github.com/sotsera/rafter/actions/runs/39486320504) at commit
74dac6418a6da2a687b72f876bdeb3989783630c, confirming the main trigger. Subsequent PR checks are recorded on their PRs.
The named Windows evaluation job is verified, but it has not been configured as a required merge check.

Linux, macOS, Native AOT, shipping-package consumption, runtime/analyzer behavior, and renderer compatibility remain
unexecuted gates. Issue #2 adds [parser compatibility evidence](parser-evaluation.md); renderer checks follow in #3.
