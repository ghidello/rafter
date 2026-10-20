# SDK, target framework, and verification baseline

Reviewed 2026-10-20. Recommended baseline for the first working examples: **.NET 10, SDK 10.0.402, C# 14,
and a single net10.0 runtime-library target**. Start verification on Windows x64, then validate Linux and macOS
before making corresponding support claims. This is a planning recommendation, not a completed compatibility check.
No SDK was installed, no global.json or project was created, and no Rafter code was compiled during the initial review.
Subsequent issue #1 work now implements this baseline for the [evaluation harness](evaluation-harness.md), which records
the current configuration and observed results. Issue #9 extends it to the [minimal runtime](minimal-runtime.md).
Inventory observations below describe the pre-scaffolding state.

## Why .NET 10

.NET 10 is the current stable LTS release, supported through 2028-11-14. .NET 8 and 9 both reach end of support on
2026-11-10; .NET 11 is currently a release candidate. Starting this project on .NET 10 fits its file-based application
focus and avoids introducing older targets without a concrete consumer requirement.
[Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)

Microsoft's download page lists SDK **10.0.402**, runtime **10.0.13**, and C# **14.0**. These are different version
identities: the SDK version selects build tools, while net10.0 selects the library's API/runtime baseline.
[.NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

Use the target's normal stable C# language version; avoid LangVersion=latest or preview. Keep the runtime library
on net10.0 rather than net10.0-windows so platform-specific process implementations do not unnecessarily constrain
portable command, input, and output code. A portable target does not prove process containment works on every OS.

Keep analyzer hosting separate from the runtime target. Choose its target and Roslyn package baseline against the
compiler/editor hosts we actually support; do not automatically target the analyzer assembly at net10.0. Test projects
can use net10.0, with the agreed xUnit v3, Microsoft.Testing.Platform, and AwesomeAssertions stack verified separately.

## Local observations

Read-only commands run from the new repository: dotnet --list-sdks and dotnet --info.

- Installed .NET 10 SDKs: 10.0.400 and **10.0.402**. No SDK installation is needed for the proposed baseline here.
- The current resolver selects **11.0.100-rc.2.26504.105**; dotnet --info reports no global.json.
- The current host reports Windows build 10.0.26220 and RID win-x64.
- This inventory establishes availability only. It does not verify file-app execution, package compatibility,
  terminal behavior, or native process ownership, and does not establish a supported Windows release matrix.

## SDK selection when scaffolding

Create the repository's global.json with the following SDK policy when scaffolding the scoped work:

```json
{
  "sdk": {
    "version": "10.0.402",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

This allows installed servicing updates within the 10.0.4xx feature band, starting at 10.0.402; it does not select
a new feature band, .NET 11, or prereleases. It is a minimum baseline within a feature band, not an exact-version lock.
CI should install the named baseline deliberately and log the resolved SDK. Review updates and advance the recorded
baseline regularly. If an exact reproduction is needed, install only the specified SDK or use a deliberately stricter
evaluation environment. [SDK selection rules](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json)

Add the Microsoft.Testing.Platform runner setting when its compatible package/runner configuration is verified.
The JSON above specifies SDK selection only. Keep direct NuGet versions in Directory.Packages.props as already agreed.

## Verification scope

| Stage | Proposed environment | Evidence needed |
| --- | --- | --- |
| First local increment | Windows x64, using this workspace | Parser/renderer checks, real file-based app and regular-project consumer, compile-time API checks, terminal and redirected streams. |
| Repeatable Windows checks | A supported stable Windows x64 CI image, selected when configuring CI | Reproduce the local checks independently; record the image, OS, SDK, and test results. Local machine success alone is insufficient. |
| Portable command/input/output checks | Linux x64, then macOS Arm64 on available supported runners | Real consumers, paths, shell argument boundaries, help, and stream behavior. Runner versions must be recorded when selected. |
| Process support on each OS | Each OS/architecture being claimed | Launch-time ownership, graceful/forced shutdown, surviving descendants, Ctrl+C, and output settlement through real integration checks. |

Windows-first is the order of work, not a permanent Windows-only product decision. Linux/macOS implementation and
verification remain pending. Do not infer native ownership guarantees from portable unit tests, a successful build,
or Windows results. Additional architectures and a full OS-version matrix are not included automatically.

## Consumer checks and publishing boundary

Verify both a real file-based app and an ordinary net10.0 console project against a locally packed Rafter package.
Keep consumer fixtures separate from library project source. File-based apps inherit parent build configuration,
including Directory.Build.props, Directory.Packages.props, and SDK selection; use an isolated consumer to catch
accidental reliance on repository imports. Package-supplied namespace imports and analyzers need their own checks.
[File-based app configuration](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps)

Start with managed execution. Native AOT publishing is a separate compatibility gate: file-based apps enable
PublishAot by default, so a successful dotnet run is not proof that the app can be published natively. Explicitly use
PublishAot=false when testing managed publishing, and do not advertise AOT/trimming support until a dedicated publish
and execution check passes. This proposed initial scope can be revisited if native distribution becomes a first-slice
requirement. [Native AOT publishing for file-based apps](https://learn.microsoft.com/en-us/dotnet/core/sdk/file-based-apps#native-aot-publishing)

The [GitHub workflow](github-workflow.md) and evaluation issues are established.
[#1](https://github.com/sotsera/rafter/issues/1) now scopes the SDK/test configuration and harness, followed by parser
and renderer compatibility issues #2 and #3. Neither dependency is adopted by this SDK review.
