# Development guide

## Prerequisites and support matrix

- .NET SDK `10.0.400`, selected by `global.json`; the latest patch in that feature band may roll forward.
- PowerShell 7 for repository validation scripts.
- Git for Source Link metadata and the example-preservation check.

Phase 1 CI verifies Windows x64, Linux x64, and macOS x64. Other architectures may work, but are not yet claimed as
supported because they are not exercised by the repository CI matrix.

## Complete verification sequence

Run these commands from the repository root:

```powershell
dotnet restore Rafter.slnx --configfile nuget.config
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet test Rafter.slnx --configuration Release --no-build --report-xunit-trx
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build
pwsh ./eng/verify-package.ps1 -Configuration Release
pwsh ./eng/validate-examples.ps1
pwsh ./eng/verify-implemented-examples.ps1 -Configuration Release
git diff --exit-code -- examples
```

Generated build, test, and package output belongs under `artifacts/` and must not be committed.
`tests/Directory.Build.props` supplies the shared Microsoft.Testing.Platform results directory, so individual test
commands do not need to repeat `--results-directory` and must not create a root `TestResults/` directory.

## Project ownership

`tests/` contains only projects executed by Microsoft.Testing.Platform. `test-assets/` contains executable fixtures
and consumer templates used by those tests or repository validation.

- `src/Sotsera.Rafter` owns the runtime library and its NuGet package, including command, execution, output, and
  generic process APIs through Phase 7.
- `src/Sotsera.Rafter.Analyzers` is an isolated Roslyn packaging seam. Analyzer behavior and package integration are
  deferred to phase 9.
- `tests/Sotsera.Rafter.Tests` owns fast runtime unit tests.
- `tests/Sotsera.Rafter.IntegrationTests` owns multi-component and host-level tests.
- `tests/Sotsera.Rafter.Analyzers.Tests` owns analyzer tests.
- `test-assets/Sotsera.Rafter.RunFixture` is the deterministic child application used to exercise Rafter execution.
- `test-assets/Sotsera.Rafter.ProcessFixture` is the deterministic child process used for process and stream tests. Add new
  process scenarios here rather than relying on shell commands or machine-installed tools.
- `test-assets/Sotsera.Rafter.PackageConsumer` is copied outside the repository by `eng/verify-package.ps1` and proves that
  clean conventional and file-based consumers can restore and load the locally produced package without a project
  reference. Each check uses an isolated NuGet cache and a temporary path containing spaces, then binds and executes
  a command through the public API. The package verifier also checks that packaged symbols match the assembly and
  contain the canonical Source Link map for the current Git revision.

The package consumer is deliberately excluded from `Rafter.slnx`: it can only restore after packing succeeds. The
solution therefore remains independently restorable while the package check preserves the correct dependency order.

## Example validation

Every canonical example references the runtime project directly. `eng/validate-examples.ps1` verifies that invariant
without copying or compiling the portfolio. The example-scoped MSBuild files default to project mode and can switch an
unchanged example to the centrally versioned package by passing `-p:RafterDependency=Package`.

`eng/verify-implemented-examples.ps1` classifies all 29 canonical examples: 24 are compiled unchanged and checked for
plain help; `dotnet`, `git`, `node`, `repository`, and `extensibility` remain Phase 8/9 portfolio work. The script also
runs deterministic graph, cleanup, output, and generic-process scenarios and saves separate stdout/stderr logs under
`artifacts/example-verification`. Cancellation examples compile and render help; process-isolated tests exercise
cancellation without sending a signal to the developer's shell. Phase 9 adds the full portfolio in package mode.
`examples/Directory.Packages.props` holds the one development package version used by package mode.

## Source Link and dependency auditing

The pinned .NET SDK provides Source Link. Do not add an older `Microsoft.SourceLink.GitHub` package override: it can
replace the serviced SDK implementation with vulnerable transitive build tooling. NuGet auditing and
warnings-as-errors remain enabled.

Builds use `GitRepositoryConfigurationScope=local` so machine-specific global Git URL rewrites do not turn the
repository URL into an SSH alias in package metadata. Build from a Git checkout with readable metadata. A package
without a usable Source Link map fails `eng/verify-package.ps1`; a clean compiler output alone does not prove symbol
integrity. This check validates mapping and assembly/PDB identity, not remote source availability or checksum
equivalence for uncommitted source edits.
