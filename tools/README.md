# Development tools

Run these PowerShell tools from the repository root with the pinned SDK. They are optional development checks;
the [Windows workflow](../.github/workflows/evaluation.yml) also runs the redirected runtime matrix.
Executable inputs live under [tests/testassets](../tests/testassets/README.md), outside the shipping package.

| Tool | Purpose and prerequisites |
| --- | --- |
| [verification/Verify-RuntimeTerminal.ps1](verification/Verify-RuntimeTerminal.ps1) | Current Rafter output, help, failure, and stream policies. Build the runtime consumer in Release first. Both parent streams must be attached for the 38-case terminal matrix; `-RedirectedOnly` runs 23 headless cases. |
| [verification/Verify-Editor.ps1](verification/Verify-Editor.ps1) | Live diagnostics and completion in package-only file-app/project consumers. Pack the local package first and supply the installed VS Code executable; C# extensions must already be installed. Uses a separate workspace/profile and closes its own test editor. |
| [evaluations/Verify-RendererTerminal.ps1](evaluations/Verify-RendererTerminal.ps1) | The retained dependency-comparison renderer's 18-case terminal matrix. Build its full project consumer in Release first; both parent streams must be attached. This does not substitute for current runtime checks. |
| [evaluations/Measure-Renderer.ps1](evaluations/Measure-Renderer.ps1) | Builds framework/Ansi/full renderer consumers in file-app/project hosts and records startup/allocation observations. `-Samples 1` is a path/execution smoke check; the default is nine samples after the first launch. |

Current verification:

```powershell
dotnet build tests/testassets/consumers/runtime/project/Consumer.csproj --configuration Release
./tools/verification/Verify-RuntimeTerminal.ps1
# For headless execution:
./tools/verification/Verify-RuntimeTerminal.ps1 -RedirectedOnly

dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --output artifacts/packages
./tools/verification/Verify-Editor.ps1 -CodeExecutable 'C:\path\to\Microsoft VS Code\Code.exe'
```

Optional dependency evaluations:

```powershell
dotnet build tests/testassets/evaluations/renderer/full/project/Consumer.csproj --configuration Release
./tools/evaluations/Verify-RendererTerminal.ps1
./tools/evaluations/Measure-Renderer.ps1 -Samples 1
```

Every tool invocation writes a unique run directory and prints its evidence path. See the
[generated-files policy](../docs/development/generated-files.md) for layouts, retention, and cleanup boundaries.
The editor's [probe extension](verification/editor-probe/) belongs to its verification harness and is not a user extension.
