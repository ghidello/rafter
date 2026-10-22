# Review of dotnet/skills for Rafter

Reviewed 2026-10-20 at upstream revision [cbb2bb74cb70](https://github.com/dotnet/skills/commit/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25).
This is a relevance and integration review of the catalog, selected skill instructions, supporting guidance, and plugin
manifests. It is not an exhaustive correctness audit of every skill/reference/script. No skills, plugins, MCP servers,
or SDKs were installed or activated, and no external skill instructions were executed.

Recommendation: use a small set of task-specific skills as implementation guidance, keeping Rafter's accepted behavior
and repository policies authoritative. Preserve bundled references if skills are installed later; copying only a
SKILL.md can omit required guidance. Installation scope and mechanism remain a separate step.

## Most useful for the initial evaluations

| Skill / plugin | Rafter application | Boundary |
| --- | --- | --- |
| [csharp-scripts](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-advanced/skills/csharp-scripts/SKILL.md) | Real file-based consumers for issues #1–#3; directives, argument passing, SDK feature bands, and inherited build configuration. | Apply to consumer scripts, not as a replacement for the runtime/test projects. Keep stable explicit package versions or intentional CPM. |
| [scaffold-dotnet-test-project](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/scaffold-dotnet-test-project/SKILL.md) | Issue #1: create and register the smallest useful xUnit v3 test project and prove discovery through the real entry point. | Explicitly honors xUnit v3; warns against generating xUnit v2 scaffolding and rewriting it afterward. Keep AwesomeAssertions and the chosen MTP mode. |
| [run-tests](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/run-tests/SKILL.md) | Issues #1–#3: correct native MTP command shape, xUnit v3 filtering, and actual test-result reporting. | Determine configuration before choosing flags; do not confuse VSTest bridge syntax with native MTP. |
| [platform-detection](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/platform-detection/SKILL.md) | Check that SDK, runner properties, imports, and packages establish the intended test platform. | A diagnostic helper when configuration is unclear, not a compulsory extra stage before every test run. |
| [msbuild](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet/skills/msbuild/SKILL.md) | Investigate restore/build/pack failures and review imported build contracts as scaffolding takes shape. | Use when there is a concrete build question; references overlap the larger MSBuild plugin. No reason to install both just for ordinary troubleshooting. |

These are recommended capabilities, not proof of installation. Issue #1 remains the place to establish and verify the
actual test/package configuration.

## Useful as the corresponding code appears

| Skill / plugin | When it earns its place |
| --- | --- |
| [directory-build-organization](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-msbuild/skills/directory-build-organization/SKILL.md) | Shared runtime/test/analyzer policy once several projects or an existing import hierarchy make it relevant. Its trigger explicitly excludes a lone project without existing Directory.Build files; do not invent a hierarchy to satisfy it. |
| [msbuild-antipatterns](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-msbuild/skills/msbuild-antipatterns/SKILL.md) | Review authored props/targets for leaking tooling dependencies, hardcoded paths, overrides, and incorrect incremental behavior. |
| [extension-points](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-msbuild/skills/extension-points/SKILL.md) | Packaging discussion: verify build versus buildTransitive imports, package/file naming, and the proposed automatic namespace import with an opt-out. It does not itself adopt that proposal. |
| [microbenchmarking](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-diag/skills/microbenchmarking/SKILL.md) | Issue #3 if isolated parser/rendering/allocation comparisons warrant BenchmarkDotNet. Keep benchmark tooling out of the runtime. Measure whole-process startup separately; a warm microbenchmark does not answer cold script-launch cost. |
| [dotnet-pinvoke](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-advanced/skills/dotnet-pinvoke/SKILL.md) | Process slice: native signatures, platform type sizes, string marshalling, error capture, and owned-handle disposal. It helps implement interop; it does not establish launch-time containment or prove descendant termination/output settlement. |
| [test-anti-patterns](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/test-anti-patterns/SKILL.md), [assertion-quality](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/assertion-quality/SKILL.md), [test-gap-analysis](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/test-gap-analysis/SKILL.md) | Review real tests for weak assertions, flaky sequencing, and missing caller-visible behavior. Favor actual process fixtures and observable outcomes; do not turn assertion diversity or raw test counts into goals. |

The P/Invoke skill contains categorical recommendations that require API-specific checking, including its claim that
LibraryImport is the only Native AOT option and its blanket SafeHandle guidance. Prefer LibraryImport for suitable new
interop, but establish actual marshalling support, ownership, and the correct release operation from platform/runtime
documentation. Microsoft's [Native AOT interop documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/interop)
describes native P/Invoke behavior; the skill is guidance, not a substitute for verifying the concrete signature.

## Defer or omit

- [convert-to-cpm](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-nuget/skills/convert-to-cpm/SKILL.md): useful for later conversion or package alignment. We are starting with
  central package management, so its before/after migration workflow is not needed for greenfield scaffolding.
- [setup-local-sdk](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet/skills/setup-local-sdk/SKILL.md): useful if we later need an isolated SDK install. SDK 10.0.402 already exists
  here. Its defaults/examples include .NET 11 previews; those must not replace our stable baseline or roll-forward policy.
- [csharp-expert](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet/skills/csharp-expert/SKILL.md): primarily a specialist router with C# fallback guidance. Optional; direct,
  narrow skills add more immediate value to the already-scoped work.
- [code-testing](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/skills/code-testing/SKILL.md): focused test-writing guidance can help, but its broad workflow expects
  a named test-engineer agent and intermediate state artifacts. Evaluate that workflow deliberately before adoption;
  do not introduce it automatically for our small integrated increments.
- [dotnet-aot-compat](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-upgrade/skills/dotnet-aot-compat/SKILL.md): later, when Native AOT compatibility becomes an explicit task.
  Resolving analyzer warnings still does not replace a real publish/run check.
- [nuget-trusted-publishing](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-advanced/skills/nuget-trusted-publishing/SKILL.md): later, when public NuGet publishing is in scope.
  Local package consumer testing does not need a publishing identity or OIDC workflow.
- [process-api-net11](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet11/skills/process-api-net11/SKILL.md): future reference only. It explicitly excludes .NET 10 and earlier.
  Do not move Rafter to .NET 11 or adopt its process APIs as part of this review.
- ASP.NET Core, Blazor, MAUI, WinForms, EF/data, and AI/ML plugins do not match Rafter's current library/script scope.
  Framework/test migration skills are unnecessary for a fresh xUnit v3/MTP project. Template-authoring skills can wait
  until distributing dotnet templates is an actual feature. SIMD and specialized mobile diagnostics have no current use case.
- Repository-maintenance skills under .agents/ and .github/, and test fixtures under eng/, are not the shipping
  skill shortlist for Rafter.

## Guidance to adapt rather than copy

1. **Versions and language:** keep .NET 10/C# 14, explicit stable direct dependency versions, and the documented SDK
   policy. The file-app skill permits floating versions in some scenarios; Rafter's repository policy does not.
   Build-organization samples contain different test packages and older/example versions; they are not selections.
2. **Tests:** retain xUnit v3, MTP, and AwesomeAssertions. MSTest examples or migration offerings do not change those
   decisions. Do not add mocking libraries solely because a generic skill shows them.
3. **Resource lifetime:** real process/stream settlement remains mandatory. Generic cancellation, timeout, or
   interop advice cannot weaken Rafter's ownership contract.
4. **Build diagnostics:** [binlog-generation](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-msbuild/skills/binlog-generation/SKILL.md) mandates a binlog on every MSBuild command.
   Recommend targeted diagnostic/evaluation capture rather than adopting that blanket rule into AGENTS.md.
   If that skill is later invoked unchanged, its broader requirement must be resolved explicitly.
5. **Installation contents:** the [MSBuild plugin manifest](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-msbuild/.codex-plugin/plugin.json)
   launches Microsoft.AITools.BinlogMcp through dnx with --yes and --prerelease. Installing that plugin includes a tool
   configuration, not just Markdown guidance. This build-tool dependency needs a deliberate decision; do not inherit it
   from the useful build skills.
6. **Agent portability:** the [testing plugin manifest](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/plugin.json) lists named custom
   agents, while its [Codex-specific manifest](https://github.com/dotnet/skills/blob/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25/plugins/dotnet-test/.codex-plugin/plugin.json) includes skills only.
   Do not assume the broad test-engineer workflow becomes available merely by installing the skill set.
7. **Repository bindings:** scaffold-dotnet-test-project and run-tests support optional, validated overlays. These may
   help later record Rafter's exact commands and conventions without forking upstream instructions. Add them only after
   the actual layout/runner is established, and keep durable policy in AGENTS.md.

## Agreed next step

Use csharp-scripts, scaffold-dotnet-test-project, and run-tests as the initial shortlist, retaining their needed
references/helpers. Keep platform-detection and the core msbuild guidance available for specific configuration or
build questions. Bring in native interop, packaging, and performance skills when those tasks arrive.

Setup is tracked in [issue #4](https://github.com/sotsera/rafter/issues/4), in the First working examples milestone.
Complete it before #1 when practical, so the guidance is available during scaffolding. This is a soft prerequisite:
installation problems must not block the harness or implementation with ordinary tooling.

The subsequent [skills setup](../development/dotnet-skills-setup.md) installs these three skills at the reviewed revision in
the repository, retains the filter catalog and license, and records discovery verification and updates.
The review above describes the earlier assessment; installation evidence is in the setup document.
