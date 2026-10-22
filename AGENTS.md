# Working on Rafter

Start with the [example design plan](docs/design/example-design-plan.md) for the current discussion topics and progress.
Use the [first milestone record](docs/history/first-working-examples.md) and [completion summary](docs/history/milestone-review.md)
for the delivered Windows baseline; do not treat their future recommendations as accepted requirements.
Use Markdown for project documents. Keep proposed syntax distinct from agreed behavior and implemented capabilities.
Use the [documentation index](docs/README.md) for current development entry points and historical evaluation records.

## Project direction and references

- Keep the Rafter name, Sotsera branding, and `Sotsera.Rafter` identity.
- Build Rafter afresh as a C# automation toolkit centered on small file-based applications. Use concrete scripts to
  establish the command, input, target, and external-process experience.
- The previous Rafter checkout is a reference for use cases and design lessons, not a source or build dependency.
  Do not modify it as part of work on this repository unless requested.
- Do not automatically inherit the reference project's APIs, architecture, dependencies, toolchain pins, platform
  scope, or completion claims. Distinguish confirmed requirements from proposals and unresolved decisions.
- Keep this file focused on durable guidance. Put product decisions, milestone plans, build commands, and detailed
  verification procedures in separate documents and link them here when they exist.

## Implementation and resource lifetimes

- Build the smallest useful integrated increment and demonstrate it through a consumer script. Add abstractions,
  dependencies, and capabilities when a concrete use case needs them.
- Keep authoring straightforward and execution behavior explicit. Evaluate public APIs through realistic examples.
- Keep build, inspection, and test tooling outside the shipping runtime unless it is required by the product.
- Make ownership and disposal explicit for child processes, streams, subscriptions, native handles, and asynchronous
  operations. Keep resources alive until their actual use ends.
- Cancellation or timeout does not prove that execution, process shutdown, output delivery, or cleanup has ended.
  Report completion only when the work owned by that scope has actually settled.
- For upstream code adaptations, record source revision, paths, changes, and applicable license notices.
  Design inspiration alone does not require importing upstream code or dependencies.

## C# readability

- Prefer clear statements, named intermediate values, and small methods over compressed expressions. Use LINQ when
  it describes one coherent transformation clearly.
- Use fluent chains for one coherent configuration, with one call per line when wrapping. Separate unrelated steps.
- Use `var` for local variables wherever C# can infer the intended type. Use explicit types only when inference is
  unavailable or would change the intended semantics.
- Use braces, one statement/declaration per line, and blank lines between logical steps. Aim around 120 characters
  without mechanically worsening readability.
- Use expression bodies only for trivial accessors or forwarding members. Keep simple expression callbacks inline
  and multi-step callbacks as blocks or named methods.
- Use `_camelCase` for private instance and static fields, including readonly fields; constants use `PascalCase`.
  Types/members use normal C# PascalCase and parameters/locals camelCase.
- Group members consistently: constants, static fields, instance fields, properties/indexers/events, constructors,
  public/protected/internal/private methods, nested types. Never reorder fields for style when declaration order
  affects initialization, memory layout, or an interop/ABI contract.
- Apply style rules to authored code; change generators when generated output needs improvement, and preserve
  upstream formatting in vendored code.

## MSBuild XML readability

- In `.props`, `.targets`, and `.csproj` files, separate main sections such as property groups, item groups, imports,
  and targets with blank lines.
- Within each parent element, group children by purpose or semantic meaning. Keep framework and compiler settings
  together, analysis settings together, and package metadata together.
- Separate distinct groups within a parent with blank lines, while keeping closely related elements adjacent.
- Preserve evaluation order and conditions when organizing elements; formatting must not change build behavior.

## Testing and verification

- The initial .NET skills are installed locally; see [setup and updates](docs/development/dotnet-skills-setup.md).
  When run-tests needs its filter-syntax catalog, read
  [.agents/skills/run-tests/references/filter-syntax.md](.agents/skills/run-tests/references/filter-syntax.md).
  Keep the project constraints below authoritative over generic skill examples.
- Use xUnit v3, Microsoft.Testing.Platform, and AwesomeAssertions for .NET tests. Verify compatible stable versions
  and the required runner configuration when scaffolding or upgrading the toolchain.
- Use the [testing guide](docs/development/testing.md) for project boundaries and execution profiles. Ordinary runs
  exclude `LongRunning=true`; preserve explicit full Windows and portable Linux coverage in PR CI. Use the portable
  solution filter on Linux; portable test results do not establish Linux-native process ownership.
- Run checks appropriate to the change. Test observable behavior and meaningful failure cases rather than mirroring
  implementation details.
- Verify file-based application behavior with actual file-based applications. Verify child-process behavior with
  real process integration checks where applicable; mocks alone do not establish host or operating-system behavior.
- Keep test dependencies outside shipping runtime packages.
- Record observed outcomes and unavailable checks. Historical results do not verify the current implementation;
  never mark skipped or unexecuted acceptance scenarios complete.
- Follow the [generated-file and evidence policy](docs/development/generated-files.md); ignored files can contain
  retained investigation evidence. Use the [tool inventory](tools/README.md) for current verification entry points.
- Keep documentation and milestone status aligned with the behavior and platforms actually verified.

## Dependency versions

- Select and verify the SDK and tooling when scaffolding the solution; do not copy pins from the reference project
  without checking their suitability.
- Declare explicit stable versions for direct NuGet dependencies centrally in `Directory.Packages.props`.
  Avoid floating versions such as `4.*`.
- Upgrade dependencies deliberately and run checks relevant to the change. Explicit versions establish a tested
  baseline; they do not prohibit upgrades.
- Distinguish declared versions from a locked dependency graph. If committed lock files are adopted, use locked
  restore in CI and update the lock files as part of dependency changes.
- For published libraries, allow compatible dependency resolution by consuming applications. Use exact-version
  constraints or upper bounds only for a demonstrated compatibility requirement.

## Git and collaboration

- Confirm the working-tree state before making changes and preserve unrelated, uncommitted, and staged work.
  Keep changes within the requested scope; do not alter sibling checkouts or repository remotes incidentally.
- Verify the target repository and acting account before GitHub writes. Do not infer upstream/fork identity from
  the name `origin` or assume the active account is the intended one.
- Respect existing user authorization. Ask only when an action requires a decision or permission that has not
  already been provided; do not introduce blanket approval requirements for routine authorized work.
- Keep branches and pull requests focused. Use separate worktrees when concurrent tasks need isolated files;
  one checkout is sufficient for sequential work. Do not discard changes or delete unfinished branches as cleanup.
- Describe the problem, resulting behavior, relevant decisions, validation, and limitations in pull requests.
  Keep final titles and descriptions aligned with the delivered change rather than intermediate work history.
- Establish repository ownership, integration branch, merge strategy, issue workflow, and required CI checks
  explicitly before relying on them. Do not inherit another project's GitHub configuration or protection claims.
- Follow the [GitHub issue workflow](docs/design/example-design-plan.md#github-issue-workflow): discuss initial syntax in
  Markdown, then create scoped issues before implementation. Link decisions and acceptance criteria without duplicating
  detailed checklists across documents and issues.
