# Phase 9: analyzers and portfolio conformance

## Objective

Lock the initial product experience through high-signal analyzers, public API compatibility checks, package-consumer
tests, and executable validation of the complete syntax portfolio.

## Questions to resolve before implementation

- [ ] Is `RAFTER005` an accepted diagnostic contract because `no-op.cs` suppresses it explicitly, or should that
      suppression be removed until analyzer admission is decided?
- [ ] If retained, what precise syntax and semantic state triggers RAFTER005, and how does an author intentionally
      declare a no-op without a pragma where appropriate?
- [ ] What severity, message, help, false-positive boundary, and code-fix behavior belong to the rule?
- [ ] Record whether RAFTER005 is required or removed from the portfolio before evaluating optional analyzer rules.

## Analyzer admission rule

An analyzer is added only when it can identify a misuse statically with a clear location and remedy, and when runtime
validation would be materially later or less actionable. A possible rule is not a requirement until its diagnostic
contract is approved in this phase.

## Implementation checklist

### Analyzer architecture

- [ ] Keep analyzer assemblies independent from the Rafter runtime and Spectre.Console.
- [ ] Target the analyzer framework needed by supported hosts without forcing consumers onto .NET 10 for analysis.
- [ ] Use stable diagnostic IDs, categories, severities, messages, help links, and release tracking.
- [ ] Enable concurrent execution and avoid generated code unless a rule explicitly needs it.
- [ ] Make analyzers deterministic, cancellation-aware, and free of filesystem/network access.

### Candidate-rule evaluation

- [ ] Resolve RAFTER005 first as a portfolio contract rather than treating it as an optional candidate.
- [ ] Inventory misuse cases discovered in phases 2–8 and compare analyzer versus runtime diagnostics.
- [ ] Approve only rules with low false-positive risk and a concrete portfolio-aligned correction.
- [ ] Consider, without presuming adoption, cross-command handles, invalid literal names, ignored returned builders,
      unsupported optional handles in context-owned APIs, and unsafe API combinations.
- [ ] Reject style-only rules and rules that duplicate clear compiler errors.
- [ ] Provide a code fix only when it preserves behavior and has one unambiguous transformation.
- [ ] Document why each candidate was adopted, deferred, or rejected.

### Analyzer verification

- [ ] Test no-diagnostic, diagnostic, malformed-code, and fix cases using source spans and stable messages.
- [ ] Test top-level statements and file-based-app syntax used by the portfolio.
- [ ] Test aliases, fully qualified names, generics, nullable annotations, and incomplete editor code.
- [ ] Test analyzer packaging by consuming the produced NuGet package.
- [ ] Measure analyzer performance on the portfolio and representative generated stress sources.

### Portfolio harness

- [ ] Treat every checked-in example as immutable source input.
- [ ] Generate validation copies in artifacts, changing only the package directive/reference mechanism.
- [ ] Compile every example against the packed product, not merely project outputs.
- [ ] Assign each example its required execution fixtures, arguments, environment, and expected result.
- [ ] Execute deterministic examples on supported platforms; explicitly classify documentation-only or
      external-tool-dependent examples.
- [ ] Assert help, diagnostics, output, filesystem effects, process cleanup, and exit code as applicable.
- [ ] Prove the harness leaves the checked-in portfolio and repository working tree unchanged.

### Public API and package compatibility

- [ ] Approve the runtime and analyzer public API baselines.
- [ ] Fail CI on unreviewed public additions, removals, signature changes, or nullable-contract changes.
- [ ] Inspect package contents and dependency groups.
- [ ] Restore and run a clean file-based consumer using only the produced package and public feeds/local artifact
      source.
- [ ] Test the package from a path containing spaces and outside the repository tree.
- [ ] Verify symbols and Source Link metadata.
- [ ] Run trimming analysis and record warnings without claiming deferred publish/AOT scenarios as supported.

### Release readiness

- [ ] Consolidate behavioral tables from all earlier phase evidence.
- [ ] Document supported syntax, platforms, limitations, exit behavior, filesystem boundary, redaction limits, and
      process cancellation policy.
- [ ] Ensure README examples are selected from the validated portfolio rather than handwritten variants.
- [ ] Produce release notes that clearly label the initial source-execution scope and deferred areas.
- [ ] Run the full clean restore/build/test/pack/consumer sequence twice from empty artifact directories.

## Required verification

- [ ] Analyzer tests pass under supported compiler hosts with no unexpected diagnostics on the portfolio.
- [ ] Every adopted diagnostic has at least one negative test preventing its main false-positive risk.
- [ ] Every example compiles from its checked-in text through the generated-copy mechanism.
- [ ] Deterministic examples execute with approved outputs and side effects on the platform matrix.
- [ ] Package-consumer tests prove no accidental source-project or repository dependency.
- [ ] Public API and package-content comparisons are clean.
- [ ] Full-suite repetition produces the same normalized results and no surviving fixture processes.

## Completion gates

- [ ] **C1 — Analyzer quality:** every shipped diagnostic has approved rationale, negative coverage, documentation,
      and acceptable performance.
- [ ] **C2 — Portfolio compilation:** every example compiles unchanged except for generated dependency substitution.
- [ ] **C3 — Portfolio behavior:** every deterministic example passes its declared execution contract.
- [ ] **C4 — Public API lock:** runtime/analyzer baselines contain no unreviewed or speculative surface.
- [ ] **C5 — Real package use:** an external temporary consumer restores and runs only the packed artifacts.
- [ ] **C6 — Clean repository:** validation produces no source-tree mutation or untracked build output.
- [ ] **C7 — Repeatable release candidate:** two clean end-to-end runs yield equivalent normalized results.
- [ ] **C8 — Documentation truth:** support and limitation statements match tested evidence from all phases.
- [ ] **C9 — Evidence recorded:** analyzer decisions, portfolio manifest, API diff, package report, platform results, and
      release checklist are committed.
- [ ] **C10 — RAFTER005 resolved:** the diagnostic and its tests ship as approved, or the portfolio suppression is
      removed through an explicit syntax decision.

## Non-goals

This phase does not add new runtime features to make examples pass. Syntax gaps return to the owning phase for a
design decision. Published-script performance and Native AOT support remain deferred evaluations.
