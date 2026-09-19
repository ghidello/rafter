# Rafter by Sotsera

Structural support for .NET automation scripts.

Rafter is a library for .NET 10 file-based automation applications. Commands combine typed options, dependency
graphs, concurrent targets, scoped filesystem operations, semantic output, and argument-safe child processes.

The implementation includes generic streaming `Run()` and bounded raw `Capture()`. Typed DotNet, Git, npm, and pnpm
builders remain Phase 8 work. The [example portfolio](examples/README.md) defines the public syntax; examples that
depend on future phases are identified by the verification harness.

The agreed implementation sequence and acceptance criteria are recorded in the
[implementation plan](docs/implementation-plan.md).

## Current status

Phases 1–4 have completed evidence. Phases 5–7 have implementation and passing cross-platform CI, but their remaining
completion gates are open. In particular, Phase 6 still needs its terminal capability profiles, lifecycle observer,
and complete state presentation; Phase 7 still needs the full synthetic race and measured-memory verification.
See the [closeout audit](docs/phases/phase-05-07-closeout.md) for verified behavior and outstanding work.

The package version is `0.1.0-dev.1`. The initial scope is source execution; published, self-contained, and Native AOT
applications are not yet claimed as supported.

## Building

Rafter requires the .NET 10 SDK selected by `global.json` and PowerShell 7. CI is configured for Windows, Ubuntu, and
macOS. See the [development guide](docs/development.md) for the complete local verification sequence and project
ownership map.
