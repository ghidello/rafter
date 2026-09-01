# Phase 1 completion evidence

## Status

The Phase 1 implementation and all locally executable gates pass. F1 was verified from the committed sources in a
fresh temporary clone. F2 awaits the first successful Windows, Linux, and macOS CI matrix and remains unchecked until
those environments have actually executed; configuring a job is not treated as proof that it ran.

## Toolchain and platform matrix

- SDK selected by `global.json`: `10.0.400` with `latestPatch` roll-forward.
- Runtime observed during formatting: `10.0.11`.
- Local verification host: Windows x64.
- Required CI hosts: `windows-latest`, `ubuntu-latest`, and `macos-latest`, all x64 GitHub-hosted runners.
- PowerShell 7 runs the package and example validation scripts on every supported host where they are used.

No ARM architecture is claimed in Phase 1 because the repository does not yet execute an ARM CI job.

## Commands executed locally

```powershell
dotnet restore Rafter.slnx --configfile nuget.config
dotnet build Rafter.slnx --configuration Release --no-restore
dotnet test Rafter.slnx --configuration Release --no-build --results-directory artifacts/test-results/local
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --no-build
dotnet format Rafter.slnx --verify-no-changes --no-restore --verbosity minimal
dotnet run --project test-assets/Sotsera.Rafter.RunFixture/Sotsera.Rafter.RunFixture.csproj --configuration Release --no-build
dotnet run --project test-assets/Sotsera.Rafter.ProcessFixture/Sotsera.Rafter.ProcessFixture.csproj --configuration Release --no-build
pwsh -NoProfile -File ./eng/verify-package.ps1 -Configuration Release
pwsh -NoProfile -File ./eng/validate-examples.ps1
dotnet restore examples/minimal.cs --configfile nuget.config -p:Configuration=Release
dotnet restore examples/minimal.cs --configfile nuget.config -p:Configuration=Release -p:RafterDependency=Package
git diff --exit-code -- examples
git diff --check
```

Observed results:

- A fresh local clone of commit `04310ce` restored and built in Release with 0 warnings and 0 errors.
- Release build: 0 warnings, 0 errors.
- Tests: 3 passed, 0 failed, 0 skipped across unit, integration, and analyzer assemblies.
- Fixtures printed `rafter-run-fixture` and `rafter-process-fixture` respectively.
- Package verification restored and ran both an external conventional project and an external file-based app.
- Example validation verified project-mode references for all 29 canonical sources without generating copies.
- The unchanged minimal example restored through the runtime project by default and through
  `Sotsera.Rafter.0.1.0-dev.1.nupkg` in package mode.
- Formatting and whitespace verification passed.

## Package determinism and contents

Two consecutive no-build package operations from identical inputs produced identical archives:

- `Sotsera.Rafter.0.1.0-dev.1.nupkg` SHA-256:
  `977005C57CE1006745769C1E6BA167C1EB153860C6390AD2A575240F257D4C93`
- `Sotsera.Rafter.0.1.0-dev.1.snupkg` SHA-256:
  `AA52C80000FB71BEF655A77CE8A22ACC1B657A6878122EA137C553B04A5B27E9`

`eng/verify-package.ps1` recorded and asserted this runtime package layout:

```text
_rels/.rels
[Content_Types].xml
lib/net10.0/Sotsera.Rafter.dll
lib/net10.0/Sotsera.Rafter.xml
package/services/metadata/core-properties/nuget.psmdcp
README.md
Sotsera.Rafter.nuspec
```

The analyzer project is deliberately not packed in Phase 1, so no analyzer or Roslyn assets appear in the runtime
package. Analyzer packaging is owned by phase 9.

## Gate follow-up

- F2: push the branch and require the complete three-platform CI matrix to pass before merging.
