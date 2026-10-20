# .NET skills setup

Implemented locally on 2026-10-20 for [issue #4](https://github.com/sotsera/rafter/issues/4).

## Scope and contents

Use repository-local skills in `.agents/skills/`. They travel with this checkout and do not install personal
skills or entire plugins. The three initial skills are:

| Skill | Purpose |
| --- | --- |
| csharp-scripts | Real C# file-based consumer apps. |
| scaffold-dotnet-test-project | Create or repair test-project wiring and verify discovery. |
| run-tests | Select and execute the test command appropriate to the configured runner. |

The source is [dotnet/skills at cbb2bb74cb70e0c5ee49cca10470a92da03d4b25](https://github.com/dotnet/skills/tree/cbb2bb74cb70e0c5ee49cca10470a92da03d4b25).
Each selected upstream skill directory contains only its SKILL.md at this revision. All three files are retained
unchanged. The separate reference-only filter-syntax catalog is retained unchanged as
[run-tests/references/filter-syntax.md](../.agents/skills/run-tests/references/filter-syntax.md).
Read that catalog when run-tests calls for filter details; it is not a fourth invocable skill.

[Source manifest](../.agents/skills/dotnet-skills-source.json) maps every vendored file to its upstream path and SHA-256.
[MIT notice](../.agents/skills/LICENSE.dotnet-skills) covers the imported instructions and reference.
The skill directory enforces LF line endings so Git checkouts preserve the recorded file hashes on Windows too.
No upstream scripts, plugin manifests, MCP servers, or custom agents are included.

## Project constraints

Skills provide guidance within the task scope. Rafter retains .NET 10 / C# 14, the documented stable SDK policy,
explicit stable direct dependency versions, and xUnit v3 / Microsoft.Testing.Platform / AwesomeAssertions.
Generic examples permitting floating versions, previews, other test frameworks, or cleanup of temporary apps do not
change these decisions or authorize deleting the project's consumer examples.

The filter catalog abbreviates some examples by SDK generation. Resolve native MTP versus bridge command mode using
run-tests and the actual repository configuration before using those examples; SDK 10 alone does not select native MTP.
No SDK, NuGet dependency, build configuration, or runtime code was changed by installation.

Optional references to other specialist skills do not imply they are installed. Broader MSBuild, platform-detection,
native interop, and benchmarking skills remain deferred. Test overlays can be added after issue #1 establishes the
actual project layout and runner commands.

## Installation and discovery

The bundled skill-installer helper installed the three complete directories using its `--repo dotnet/skills`,
`--ref cbb2bb74cb70e0c5ee49cca10470a92da03d4b25`, explicit `--path` arguments, and
`--dest <repository>/.agents/skills` options. The catalog and license were then copied from the same revision.
Existing destinations are not overwritten by that installer.

On this Windows host, the archive initially exceeded the extraction path limit. Installation succeeded with a
process-local extended-length temporary path and backslash-separated source paths. No permanent environment-variable
changes were needed. Future clones already contain the installed files and need no installer.

Codex [discovers local skills](https://learn.chatgpt.com/docs/build-skills#where-codex-loads-local-skills) from
`.agents/skills` at its working directory and applicable ancestors. Start Codex in the repository root or a subfolder.

This existing chat starts at `E:\Ghidello\Git\rafter.v2`, one directory above the repository.
A local Windows junction at `E:\Ghidello\Git\rafter.v2\.agents\skills` points to
`E:\Ghidello\Git\rafter.v2\rafter\.agents\skills`, so the same files are discoverable here.
The junction is outside Git and is unnecessary when opening the actual repository directly.
It is not a personal installation and does not modify the old Rafter checkout.

Verification used Codex CLI `0.163.0-alpha.10.2` and the documented app-server
[`skills/list` operation](https://learn.chatgpt.com/docs/app-server#skills), with `forceReload: true` and both
working directories. Each returned all three selected skills with `scope: repo`, `enabled: true`, and no loading errors.
The temporary server only handled discovery requests; no agent turn was started.
All five upstream files matched their source bytes. This verifies installation and discovery, not .NET build/test behavior.
The installed skills will be available on the next turn; if the app does not refresh its list, restart Codex.

## Updating

1. Choose an explicit upstream revision and review changes to the three skills, the filter catalog, license,
   supporting resources, and any newly introduced tool requirements.
2. Use the skill-installer helper to install that revision into a separate temporary destination. Preserve the
   existing installation until the replacement has been reviewed; do not install whole plugins as an update shortcut.
3. Copy the reviewed skill directories and required references into the repository, retaining upstream formatting.
   Keep the filter catalog under run-tests/references and update the source manifest and MIT notice as needed.
4. Verify each manifest SHA-256 against both the new upstream bytes and the installed bytes, check local references,
   and repeat skills/list from the repository and any workspace bridge in use.
5. Review the diff and record the new revision and observed checks in the update PR. Run affected real file-app or
   test scenarios when the changed guidance affects their commands. Never count unexecuted scenarios as passing.

Skills setup is a soft prerequisite for issue #1. If installation or discovery fails on another machine, record the
limitation and continue with ordinary tooling and the accepted project decisions.
