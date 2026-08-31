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
dotnet test Rafter.slnx --configuration Release --no-build --results-directory artifacts/test-results/local --report-xunit-trx
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build
pwsh ./eng/verify-package.ps1 -Configuration Release
pwsh ./eng/validate-examples.ps1
git diff --exit-code -- examples
```

Generated build, test, package, and validation output belongs under `artifacts/` and must not be committed.

## Project ownership

- `src/Sotsera.Rafter` owns the runtime library and its NuGet package. It intentionally exposes no placeholder public
  API in Phase 1.
- `src/Sotsera.Rafter.Analyzers` is an isolated Roslyn packaging seam. Analyzer behavior and package integration are
  deferred to phase 9.
- `tests/Sotsera.Rafter.Tests` owns fast runtime unit tests.
- `tests/Sotsera.Rafter.IntegrationTests` owns multi-component and host-level tests.
- `tests/Sotsera.Rafter.Analyzers.Tests` owns analyzer tests.
- `tests/Sotsera.Rafter.RunFixture` is the deterministic child application used to exercise Rafter execution.
- `tests/Sotsera.Rafter.ProcessFixture` is the deterministic child process used for process and stream tests. Add new
  process scenarios here rather than relying on shell commands or machine-installed tools.
- `tests/Sotsera.Rafter.PackageConsumer` is copied outside the repository by `eng/verify-package.ps1` and proves that
  clean conventional and file-based consumers can restore and load the locally produced package without a project
  reference.

The package consumer is deliberately excluded from `Rafter.slnx`: it can only restore after packing succeeds. The
solution therefore remains independently restorable while the package check preserves the correct dependency order.

## Example validation

`eng/validate-examples.ps1` copies every canonical example to `artifacts/example-validation/sources`, changes only its
`#:package Sotsera.Rafter@...` directive, and records the original SHA-256 hash in a manifest. Phase 1 does not compile
examples whose APIs belong to later phases. Each owning phase promotes its implemented examples into required compile
and runtime checks. The final Git command above independently proves that the canonical sources were not edited.
