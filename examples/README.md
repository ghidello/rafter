# Rafter examples

[minimal.cs](minimal.cs) runs against the new runtime through its project reference. The other six scripts remain
syntax drafts for typed inputs, validation, targets, and external processes. Their opening comments identify the
features still pending. See the [design plan](../docs/example-design-plan.md) for accepted decisions and open topics.

Use block lambdas for command and target bodies, even when the body contains one statement. Keep short predicates
and value-returning callbacks inline. Use `return;` only when a body needs to exit early, as in
[process-failure.cs](process-failure.cs).

Runnable [runtime consumers](../fixtures/runtime/) verify the implemented API in file apps and ordinary projects.
The separate [baseline consumers](../fixtures/consumers/) verify the SDK and consumer execution environment.
