# Repository guidance

## C# conventions

- Prefer supported modern C# when it improves clarity; avoid novelty that makes code harder to understand. Keep one
  statement and declaration per line and follow `.editorconfig` naming rules.
- Treat the configured 120-character C# line length as a soft ceiling, not a target. Keep readable calls and
  declarations on one line when comfortably within it; wrap only for actual length or complexity.
- When wrapping, use consistent indentation and visible separators without stranding a lone argument. Reserve one
  argument per line for genuinely complex calls.
- In file-app examples, separate application setup, application creation, command options, target definitions, and
  the final run with blank lines. Keep related declarations within each section together.
- Use an expression callback for one simple fluent or configuration action and a block callback for multiple actions
  or meaningful behavior. Do not force a multiline fluent chain into an expression merely to avoid a block.
- Use named arguments only when they clarify ambiguous values. Begin multiline ternary branches with `?` and `:`,
  and begin multiline Boolean continuation lines with their operators.
- Treat more than four method or constructor parameters as a design-review prompt. Split responsibilities or group
  cohesive values when justified; do not create an arbitrary parameter object merely to meet the threshold.
  Immutable data records may exceed the threshold when every parameter is a cohesive, clearly named model field.
- Order members as fields, constructors and factories, properties and events, public methods, protected or internal
  methods, private methods, and nested types. Every added or modified type must conform; do not reorder otherwise
  untouched committed code solely for style.
- Use block bodies for meaningful behavior when readable. Document public APIs; add implementation comments only for
  non-obvious intent, constraints, lifecycle, or ownership.
- Use asynchronous I/O, propagate `CancellationToken` where cancellation belongs, and never block with `.Result`,
  `.Wait()`, or `GetAwaiter().GetResult()`. Do not assume continuation thread identity or introduce thread-affine
  state without an explicit requirement.
- Use the `Async` suffix for conventional task-returning methods. Preserve intentionally fluent terminal names and
  already-approved public syntax, including process `.Run()` and command `RunAsync(...)`.
- Do not expose mutable collection types. Snapshot retained caller-owned arrays and sequences before later mutation
  could affect behavior. Do not promise arbitrary deep cloning; reject values that cannot satisfy a required
  snapshot boundary.

## Project configuration

- Prefer supported SDK-style project properties and items in `.csproj` or shared `.props` files over source-level
  assembly attributes. Use an assembly-info source file only when MSBuild cannot express the required metadata.
- Keep repository-specific `.editorconfig` changes in the final `Project customizations` section. Put analyzer policy
  changes there with the narrowest practical file scope and a short justification; do not scatter pragmas or broad
  suppressions through the source tree.
