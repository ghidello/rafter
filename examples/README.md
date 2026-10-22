# Rafter examples

All eight examples run against the new runtime through project references. The process examples currently require
Windows; the target-only example prepares and prints a greeting without child processes. See the
[design plan](../docs/design/example-design-plan.md) for accepted decisions and open topics.

`Directory.Build.targets` enables the same namespace import and analyzers as the
[local NuGet package](../docs/guides/local-package.md). The examples therefore omit `using Sotsera.Rafter;`. Set
`RafterImplicitUsings=false` to opt out and add the explicit import. When copying an example outside this checkout,
use a package reference with a configured local feed, or supply equivalent project-reference authoring assets.

| Example | Prerequisites |
| --- | --- |
| [minimal.cs](minimal.cs), [options.cs](options.cs), [validation.cs](validation.cs), [targets.cs](targets.cs) | .NET 10 SDK; use `--help` for declared inputs. |
| [processes.cs](processes.cs) | Windows and `dotnet` on the script process's executable search path. |
| [process-failure.cs](process-failure.cs) | `--project` identifies a buildable project; a failed build demonstrates `Fail` returning its result. |
| [process-working-directory.cs](process-working-directory.cs) | Launch from a directory containing `src` with a buildable project or solution. |
| [dependencies.cs](dependencies.cs) | `--project` identifies a test project; uses .NET 10 native Microsoft.Testing.Platform mode. |

The [workflow fixture](../tests/testassets/workflows/) supplies a small library and a one-test project. From the repository root:

```powershell
dotnet run --file examples/process-failure.cs -- --project tests/testassets/workflows/src/BuildSample.csproj
dotnet run --file examples/dependencies.cs -- --project tests/testassets/workflows/sample-tests/BuildSample.Tests.csproj
```

To exercise `process-working-directory.cs`, launch the file by its absolute path from `tests/testassets/workflows`.
The script location does not change the captured invocation root. The integration suite copies these fixtures into
temporary directories and also verifies intentionally broken builds and failing tests.

Use block lambdas for command and target bodies, even when the body contains one statement. Keep short predicates
and value-returning callbacks inline. Use `return;` only when a body needs to exit early, as in
[process-failure.cs](process-failure.cs).

Runnable [runtime consumers](../tests/testassets/consumers/runtime/) verify the implemented API in file apps and ordinary projects.
The separate [baseline consumers](../tests/testassets/consumers/host/) verify the SDK and consumer execution environment.
