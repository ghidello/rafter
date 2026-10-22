# Generated files and verification evidence

Maintained source is tracked by Git. Build output and raw verification evidence are ignored; an ignored file is not
automatically disposable. Preserve the old [reference snapshots](../../reference/) and the vendored skills, source
manifest, licenses, and parent-workspace discovery bridge.

## Output ownership and retention

| Files | Purpose and policy |
| --- | --- |
| Project `bin/` and `obj/` trees | Disposable compiler/restore output. Rebuild when needed; do not treat these trees as the authoritative verification record. |
| `artifacts/packages/` | Rebuildable local NuGet feed. Reusing a version does not refresh restored consumer caches; follow the [package instructions](../guides/local-package.md). Preserve the exact package separately when a retained investigation requires its bytes. |
| `artifacts/test-results/` and other named test-result directories | TRX and associated execution records. Retain the relevant run when it supports acceptance or an unresolved failure. A green later run does not erase a failed attempt. |
| `artifacts/evidence/verification/runtime-terminal/<run>/` | Per-case raw stream/capability records and a terminal or redirected result manifest for the current runtime. |
| `artifacts/evidence/verification/editor/<run>/` | Editor result JSON, stdout/stderr logs, generated consumer sources, package cache, and isolated editor profile. Raw results/logs/sources support the finding; bulk caches/profile data can be considered separately during a reviewed cleanup. |
| `artifacts/evidence/evaluations/renderer-terminal/<run>/` | Dependency-comparison terminal records and result manifest. |
| `artifacts/evidence/evaluations/renderer-measurements/<run>/` | Measurement JSON with SDK/OS, method, builds, fresh-process samples, and steady-state observations. Preserve the method and sample count with any conclusion. |
| Other existing `artifacts/` directories | Earlier test runs, editor reviews, investigations, and CI downloads. Keep them in place until their purpose and retention have been reviewed individually. |

The four [development tools](../../tools/README.md) allocate a fresh UTC timestamp plus GUID for each invocation.
They print the resulting directory and do not overwrite previous runs. Failed runs keep any evidence written before
the failure. Existing `artifacts/runtime-review/`, `artifacts/renderer/`, and `artifacts/editor-review*` records remain
at their historical paths; documents describing them are not claims that a new invocation writes there.

Local evidence has **no automatic expiry**. Retain acceptance baselines and unresolved-failure evidence until there is
an explicit decision about the named run. For each reported result, record the source revision, command/options,
environment, outcome, and raw report location in the PR or investigation document. Keep local changes identifiable
when a run predates its final commit. Do not present a one-sample smoke run as a performance result.

CI uploads `artifacts/` even after failures and keeps its artifact for **14 days**, as configured in the
[workflow](../../.github/workflows/evaluation.yml). Source revisions and Markdown/PR summaries remain the durable
record; a CI URL alone does not promise permanent access to raw artifacts. Retain needed raw evidence separately
before expiry rather than committing whole generated trees into Git.

## Bounded cleanup

This organization change deletes no existing evidence or pre-existing generated output and introduces no automatic
cleanup command. In particular, former `fixtures/` and `tests/Rafter.Evaluation.Tests/` directories can remain locally
because they contain ignored build output. Maintained sources now live under `tests/testassets/` and the three
projects described in the [testing guide](testing.md); ignored output under the former `tests/Rafter.Tests/` path
can likewise remain until a bounded cleanup is requested.

When cleanup is requested, first identify the exact files and their owners. Stop the related build, test, child-process,
or editor session before removing its output. Resolve every target inside the intended checkout, reject junctions or
other paths that escape it, and check that no tracked source or retained evidence is included. Remove only the reviewed
build directories or named evidence runs. Do not use an unbounded clean of all ignored files to tidy the checkout.

For a retained run, keep its raw result, relevant logs and generated repro sources, method/environment, revision, and
outcome together. Deciding that a cache can be rebuilt does not by itself authorize discarding the investigation it
accompanies. Apply the same distinction to successful and failed runs.
