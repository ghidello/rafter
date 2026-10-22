# Dependency decisions and implementation readiness

Initial review: 2026-10-20. Dependency selection is an explicit part of the readiness review before scaffolding.
The parser and renderer evaluations now recommend their candidates for the initial named-input and output slices.
Issue #9 adopts both internally in the [minimal runtime](../guides/minimal-runtime.md); no package has been published.
Issue #1 now establishes the SDK and test-tool baseline; see the [evaluation harness](evaluation-harness.md)
for exact versions, resolved dependencies, and observed results. Existing test-tool preferences remain in force.

## Proposed dependency boundaries

| Area | Initial direction | Status |
| --- | --- | --- |
| SDK and target framework | .NET 10 SDK 10.0.402, C# 14, and net10.0, with Windows x64 verification first. | Configured for the harness and minimal runtime; see their recorded results. |
| CLI parsing | Use `System.CommandLine` internally, with Rafter owning invocation, validation ordering, help, and diagnostics. | 2.0.13 adopted for runtime controls and dynamic string/int/bool inputs; see the typed-input record. |
| Terminal rendering | Use `Spectre.Console` for help layout and framework messages, with explicit literal/plain output paths. | 0.57.2 adopted for runtime output and dynamic help, with Ansi 0.57.2 as its only transitive package. |
| Process execution | BCL streams/tasks plus Windows Job Object and process-creation interop. | [Implemented and checked on Windows](../guides/process-runtime.md); no extra runtime package. Other platforms remain unverified. |
| Analyzer development | Separate netstandard2.0 assembly using Microsoft.CodeAnalysis.CSharp 4.14.0, private development/test dependency. | [Initial diagnostics](../guides/authoring-diagnostics.md) and [local package](../guides/local-package.md); package consumers receive no Roslyn runtime dependency. |
| Tests | xUnit v3, Microsoft.Testing.Platform, and AwesomeAssertions, as specified in `AGENTS.md`. | xunit.v3.mtp-v2 4.0.2, MTP 2.5.1, AwesomeAssertions 9.6.0; local native-MTP run passed. |
| Dependency injection, hosting, logging, filesystem helpers | Do not introduce a framework package without a concrete need in the first examples. Start with direct composition and framework APIs where sufficient. | Proposed initial restraint, not a permanent exclusion. |

Keep parser and rendering dependencies behind Rafter's public API where practical. Their defaults must not silently
become Rafter behavior. Analyzer tooling and tests must not become shipping runtime dependencies; verify the resulting
NuGet dependency graph rather than relying only on project references looking correct.

The analyzer uses stable [Microsoft.CodeAnalysis.CSharp 4.14.0](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/4.14.0)
(MIT), deliberately below the current compiler API rather than requiring the latest host. Local SDK 10.0.402 reports
compiler 5.9.0-1.26505.111; actual package loading is verified against that host. Other compiler/editor versions remain
unverified. Its netstandard2.0 restore resolves CodeAnalysis.Common 4.14.0, CodeAnalysis.Analyzers 3.11.0,
Collections.Immutable and Reflection.Metadata 9.0.0, Text.Encoding.CodePages 7.0.0, Memory 4.5.5, Buffers 4.5.1,
Numerics.Vectors 4.5.0, Runtime.CompilerServices.Unsafe 6.0.0, Threading.Tasks.Extensions 4.5.4, and the framework
support packages NETStandard.Library 2.0.3 / Microsoft.NETCore.Platforms 1.1.0. These are tooling dependencies;
only the authored analyzer DLL is included in the runtime package. Unit checks use the same Roslyn API package,
while real package-consumer builds verify loading under the installed compiler.

## SDK and verification baseline

The [SDK and platform review](../development/sdk-and-platform-baseline.md) recommends .NET 10 SDK 10.0.402, C# 14, and a single
net10.0 runtime target. The evaluation harness now pins the documented SDK policy and locally resolves 10.0.402.
Begin verification on Windows x64, then establish Linux/macOS
evidence before claiming support there; process ownership needs platform-specific checks. Managed execution is the
initial scope, with native AOT publishing a separate compatibility gate. The minimal runtime now uses this baseline too.

## Parser: first evaluation

The [detailed parser review](parser-review.md) and [executable evaluation](parser-evaluation.md) record findings from
System.CommandLine 2.0.13. The checks support a parsing-only integration with bounded adaptation for empty values,
option boundaries, Boolean conversion, and safe diagnostics. Built-in help, bundling, response files, colon separators,
and directives are explicitly controlled. The fixed evaluation adapter is not the Rafter runtime.

Microsoft's documentation describes typed options, aliases, configurable arity, and parser defaults. It also documents
root-command defaults such as help/version options. This makes `System.CommandLine` a reasonable candidate, but does
not establish compatibility with Rafter's contract. [Official syntax documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/syntax)

The evaluation covers these agreed behaviors; preserve them when implementing the runtime:

- Normalized primary names and aliases, including explicitly preserved `-name` and `--n` spellings, case-sensitive
  matching, and declaration-order display. Do not inherit extra built-in aliases or directives without a decision.
- Bare Boolean flags and explicit true/false values with space or equals syntax; optional values must remain distinct
  from absent defaults. Reject repeated scalar options, including occurrences through different aliases.
- Keep Required/Default exclusivity and typed `GetValue` handles in Rafter's API, regardless of parser capabilities.
- Validate declarations before help; explicit invalid arguments still fail with help, while missing required inputs
  alone do not prevent it. Help must not execute user validation predicates or work.
- Preserve Rafter's grouped diagnostics, exit codes, and privacy policy. Do not automatically print raw parser errors
  containing input values or invoke a parser-owned help/exception/shutdown pipeline.
- Establish what malformed-input recovery can reliably aggregate; do not promise more than the parser can support.

Keep the executable checks as regression evidence for upgrades and runtime integration. Reconsider the parser if new
input types or grammar require extensive duplicate tokenization; a custom parser remains a fallback to assess.

## Rendering: second evaluation

The [detailed renderer review](renderer-review.md) and [executable evaluation](renderer-evaluation.md) recommend
Spectre.Console 0.57.2 with separate destination policies and coordinated literal/plain output. Tests cover the CI
override and non-ANSI WriteAnsi no-op risks. Real terminal checks, captured help, and cost comparisons with Ansi-only
and framework consumers support adoption. The minimal runtime now implements the body-only portion of this integration;
the evaluation fixtures remain independent evidence for the broader output design.

Spectre.Console documents styled rendering, console capability detection, and configuration overrides. These are useful
building blocks for the selected help and output design. [Official rendering model](https://spectreconsole.net/console/explanation/understanding-rendering-model/)

Preserve the evaluated independent stdout/stderr behavior, plain output, literal brackets, original child/script
indentation, narrow help, and full exceptions when implementing the runtime. Do not replace the explicit policy with
default capability detection or profile enrichment.

The initial milestone does not need live dashboards or animation. Compare startup cost, package/transitive footprint,
and implementation effort before selecting a general renderer or a small framework-based implementation. If using
Spectre.Console for rendering, that does not also select `Spectre.Console.Cli` as the parser or application model.

## Process execution: ownership comes first

`System.Diagnostics.Process` is a framework starting point, not the complete ownership solution. Microsoft explicitly
documents that waiting for the root process after tree termination does not verify descendant exit.
[Process.Kill documentation](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.process.kill?view=net-10.0)

CliWrap is an optional candidate: its documentation provides process piping and separate graceful/forceful cancellation
tokens. That alone does not demonstrate Rafter's launch-time containment, surviving-descendant cleanup, or output
settlement requirements. Do not adopt or reject it without checking those integration points.
[CliWrap documentation](https://github.com/Tyrrrz/CliWrap#cancellation)

Use the [Aspire review](aspire-process-shutdown-review.md) and agreed
[ownership policy](../design/example-design-plan.md#completion-of-owned-process-scopes) as evaluation criteria. Native interop or
a supervisor may still be needed. A wrapper must reduce work without weakening the contract or requiring a duplicate
process-launch layer. The [Windows implementation](../guides/process-runtime.md) uses direct native creation with a job attribute,
scoped Ctrl+Break where supported, asynchronous BCL named pipes, and independent ownership verification. No wrapper
package is needed for this implementation; this does not claim that CliWrap is incompatible in general.

## Selection and version gate

For each adopted package, record its purpose, exact stable version, supported target/host versions, license,
transitive dependencies, and the compatibility checks actually run. Separate source/documentation findings from
observed behavior. Check maintenance and release status at selection time; this review does not establish them.

Pin the SDK in `global.json` and direct package versions centrally in `Directory.Packages.props` when scaffolding.
Do not create placeholder project files or choose versions merely to fill this document. Analyzer host compatibility
and test-runner compatibility need their own checks; the newest compiler package need not match the oldest supported host.

The [GitHub workflow](../development/github-workflow.md) is established and evaluation issues #1–#3 are published.
[#1: the evaluation harness](https://github.com/sotsera/rafter/issues/1) is implemented in
[PR #6](https://github.com/sotsera/rafter/pull/6), with local and Windows CI checks passing. Issue #2 now provides
parser compatibility evidence and an adoption recommendation. Issue #3 adds renderer compatibility and cost evidence.

Readiness sequence:

1. Reconcile accepted decisions and deferred scope in the planning documents.
2. Agree the initial verification platform, SDK/target baseline, and parser/renderer evaluation scope.
3. GitHub identity/workflow and evaluation issues are established; create subsequent implementation issues as scoped.
4. Run the small compatibility checks, choose and pin the dependencies needed for the first slice, then implement
   the minimal example. Add later dependencies only when the corresponding slice needs them.

This order keeps dependency choice deliberate without requiring every future process, capture, or timeout feature to
be fully designed before the first script can run.
