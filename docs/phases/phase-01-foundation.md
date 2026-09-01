# Phase 1: repository and package foundation

## Objective

Create a reproducible .NET 10 product repository that can build, test, pack, and validate file-based consumer
examples. This phase creates structure and seams, not feature behavior.

## Inputs and fixed decisions

- The [syntax portfolio](../../examples/README.md) is the public API target.
- The product is a library; there is no Rafter executable, CLI, MCP server, or configuration file.
- Root configuration serves the Rafter implementation and its tests.
- The runtime package uses Spectre.Console; analyzer packaging remains isolated from runtime dependencies.

## Implementation checklist

### Repository configuration

- [x] Add a `global.json` pinned to an available stable .NET 10 SDK with an intentional roll-forward policy.
- [x] Add root build properties for `net10.0`, nullable annotations, implicit usings, deterministic builds, symbols,
      language version, and warnings policy.
- [x] Centralize package versions without leaking test-only dependencies into product projects.
- [x] Define repository-wide output and artifact locations that keep source directories clean.
- [x] Add or extend `.editorconfig`, `.gitignore`, and package/source-link metadata.
- [x] Record the supported operating systems and architectures; do not claim an untested platform.

### Solution and projects

- [x] Create the root solution.
- [x] Create `src/Sotsera.Rafter` as the runtime package.
- [x] Create `src/Sotsera.Rafter.Analyzers` only as a packaging/build seam; analyzer behavior belongs to phase 9.
- [x] Create unit, integration, analyzer, and package-consumer test projects with clear responsibilities.
- [x] Create deterministic run and process fixture projects; fixtures must not depend on shell syntax or external tools.
- [x] Ensure the product solution contains product and test projects only.
- [x] Add public API baseline infrastructure without freezing placeholder APIs as permanent contracts.

### Package and consumer validation

- [x] Produce `Sotsera.Rafter` with correct ID, version placeholder, license, repository, symbols, and Source Link.
- [x] Ensure analyzer assets, if packaged, land under analyzer assets and are not runtime dependencies.
- [x] Create a local-package consumer path that restores the produced package rather than a project reference.
- [x] Make canonical examples reference the runtime project by default and provide an opt-in package mode that changes
      the evaluated reference without rewriting source; individual examples become required when their owning phase
      implements the referenced API.
- [x] Centralize the package-mode example version in `examples/Directory.Packages.props`.

### Automation and documentation

- [x] Provide one documented root sequence for restore, build, test, pack, and example validation.
- [x] Add CI jobs for the platforms needed by later filesystem and process contracts.
- [x] Upload test logs and packages on failure without committing generated output.
- [x] Explain project ownership and where deterministic fixtures belong.

## Required verification

- [x] A clean clone can restore without machine-specific paths or private feeds.
- [x] Release build emits no warnings under the agreed warnings policy.
- [ ] Unit and placeholder integration suites execute on every declared platform.
- [x] Repeated package builds from identical inputs are deterministic where the SDK permits comparison.
- [x] The packed archive contains only intended runtime, analyzer, documentation, symbols, and metadata assets.
- [x] A minimal file-based smoke app restores the local package and runs without requiring speculative public APIs.

## Completion gates

- [x] **F1 — Reproducible root:** clean restore and build succeed using only documented prerequisites.
- [ ] **F2 — Test topology:** every test and fixture project runs through the root solution on declared CI platforms.
- [x] **F3 — Package integrity:** package-content assertions and a package-consumer restore pass.
- [x] **F4 — Example preservation:** validation checks canonical project references without generated copies, and
      `git diff --exit-code -- examples` remains clean after validation.
- [x] **F5 — Configuration ownership:** root SDK/build/package files contain no experiment-specific settings.
- [x] **F6 — Evidence recorded:** exact SDK, commands, platform matrix, and package-content report are captured in the
      phase completion record.

## Non-goals

No parser, scheduler, filesystem mutation, console interception, process runtime, typed tool, or substantive analyzer
is implemented in this phase.
