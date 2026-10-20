# GitHub workflow

Established 2026-10-20 for the fresh Rafter implementation. This records repository facts verified through the GitHub
API and the working workflow selected for this project. It does not claim that GitHub enforces the working rules.

## Repositories and identity

| Role | Verified value |
| --- | --- |
| Canonical repository and issue tracker | [sotsera/rafter](https://github.com/sotsera/rafter) |
| Development fork | [ghidello/rafter](https://github.com/ghidello/rafter), whose parent is sotsera/rafter |
| Local origin | git@github-ghidello:ghidello/rafter.git |
| Acting GitHub account | ghidello; API reports admin/push permissions on both repositories |
| Integration branch | Upstream main; both repositories use main as their default |
| Current local base | main at d85ddb2f6e432d18a740eaa96c050f46fb03c551, fast-forwarded from upstream after PR #5 merged; fork main has not been synchronized by this task |

Upstream issues are enabled; fork issues are disabled. Create project issues and milestones upstream. Publish
development branches on the fork and open PRs into sotsera/rafter:main. Do not change remotes or treat origin as upstream.
The old checkout remains a reference and is not part of this workflow.

## Delivery rules and actual enforcement

- Use a focused branch and PR for each cohesive change; link its issue and include the relevant observed checks.
- Use squash merges into upstream main. Upstream permits squash only; merge commits and rebase merges are disabled.
- Merge after review and the checks applicable to that slice pass. Do not equate a skipped check with a successful one.
- Start with a Windows evaluation CI check under issue #1: restore/build/test plus actual file-app/project consumers.
  Add package/analyzer and process integration checks as those slices arrive. Record concrete check names once the
  workflow exists, before configuring any required-status-check enforcement.
- Keep Linux/macOS support claims tied to their own results. Local Windows execution is not cross-platform evidence.

At the initial workflow inspection, main was unprotected in both repositories and upstream's protect-main ruleset was disabled.
Upstream had no Actions workflows. The fork's Actions API listed an active historical CI record, but its
.github/workflows/ci.yml is absent from main, so that record is not a usable current CI definition.
No branch protection, ruleset, merge settings, or Actions configuration was changed during this setup.

Issue #1 adds the [Windows evaluation workflow](../.github/workflows/evaluation.yml). Its stable job name is
Windows evaluation; see the [harness record](evaluation-harness.md) for actual run results. Adding this workflow
does not enable branch protection or make the check required for merging.

## Published initial work

Milestone: [First working examples](https://github.com/sotsera/rafter/milestone/1).

| Issue | Purpose | Sequence |
| --- | --- | --- |
| [#4: Configure the initial .NET skills for Rafter](https://github.com/sotsera/rafter/issues/4) | Choose installation scope, install the three initial skills with their resources, and verify discovery. | Completed through PR #5. |
| [#1: Establish the .NET 10 evaluation harness and Windows CI](https://github.com/sotsera/rafter/issues/1) | Configure the stable SDK/test baseline, publish design references, and establish real consumers and CI. | After #4 when practical; skill installation problems must not block it. |
| [#2: Verify System.CommandLine compatibility before adoption](https://github.com/sotsera/rafter/issues/2) | Prove the parsing contract and decide adoption from executable evidence. | Depends on #1. |
| [#3: Verify Spectre.Console output compatibility and startup cost](https://github.com/sotsera/rafter/issues/3) | Prove stream/literal/plain behavior and assess renderer cost. | Depends on #1; independent of #2. |

The issues contain the detailed acceptance checklists. Keep design rationale and accepted behavior in Markdown;
record execution results and scoped completion in the issues/PRs without maintaining duplicate checklists.
Issue #4 is complete; see [skills setup](dotnet-skills-setup.md). [PR #5](https://github.com/sotsera/rafter/pull/5)
merged on 2026-10-20 as d85ddb2f6e432d18a740eaa96c050f46fb03c551 and closed the issue. Local main is updated to
that commit. The earlier design work is published with #1 in [PR #6](https://github.com/sotsera/rafter/pull/6).
Installation and discovery were verified locally;
no CI checks were configured for this PR.
Create the later runtime issues from the [milestone outline](first-working-examples.md#proposed-github-issue-breakdown)
as the evaluations establish the first implementation slice.

The planning documents, syntax drafts, and historical references accompany issue #1's evaluation harness PR.
Issue references must point to the published PR commit until it merges, then may use main. The harness verifies its
own SDK/test/consumer environment; runtime implementation and parser/renderer compatibility remain separate work.
