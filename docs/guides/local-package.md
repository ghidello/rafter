# Local package

The runtime and first C# authoring diagnostics are packed together as `Sotsera.Rafter` for
[issue #17](https://github.com/sotsera/rafter/issues/17). The initial version is `0.1.0-local`; this is a local development
artifact, not a public release or a commitment to release-version numbering. Nothing is published to NuGet.org.

## Build and consume

From the repository root:

```powershell
dotnet pack src/Sotsera.Rafter/Sotsera.Rafter.csproj --configuration Release --output artifacts/packages
```

Create an otherwise independent consumer folder with the repository's SDK baseline (10.0.402) and a `NuGet.Config`.
Replace the local path below with the absolute path to the generated feed. Keep the source mapping so a similarly
named public package cannot substitute for this development artifact.

```xml
<configuration>
  <packageSources>
    <clear />
    <add key="rafter-local" value="E:\path\to\rafter\artifacts\packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="rafter-local"><package pattern="Sotsera.Rafter" /></packageSource>
    <packageSource key="nuget.org"><package pattern="*" /></packageSource>
  </packageSourceMapping>
  <config>
    <add key="globalPackagesFolder" value=".packages" />
  </config>
</configuration>
```

Create `hello.cs` outside a project directory:

```csharp
#:package Sotsera.Rafter@0.1.0-local
#:property PublishAot=false

var command = Rafter.Command("Say hello.");
return await command.RunAsync(args, context =>
{
    context.WriteLine("Hello from Rafter.");
});
```

Run `dotnet run --file hello.cs -- --quiet`. A regular net10.0 C# project instead adds
`<PackageReference Include="Sotsera.Rafter" Version="0.1.0-local" />` and uses the same code without directives.
Fresh package-only project/file-app consumers are also verified by the full portable suite on Ubuntu 24.04 x64;
see the [testing guide](../development/testing.md#linux-ci-scope). Owned processes require Windows, and AOT remains unverified.

Use a fresh consumer/package cache when rebuilding the same local version, or pass a new local `-p:Version=...` to
pack and update the consumer version. NuGet caches packages by version; it does not replace an already-restored package
just because the feed now contains different bytes. The integration checks use a fresh cache for each consumer.

## Namespace import and local development

Direct C# package consumers receive a `Sotsera.Rafter` global namespace import. It is provided by
`build/net10.0/Sotsera.Rafter.targets`, after project properties are evaluated. It does not use `buildTransitive`, so an
application that only obtains Rafter through another project/dependency does not inherit this import. This setting is
independent of the SDK's general `ImplicitUsings` setting.

Set `<RafterImplicitUsings>false</RafterImplicitUsings>` in a project, or add
`#:property RafterImplicitUsings=false` to a file app, to opt out. Then write `using Sotsera.Rafter;` explicitly.
Explicit imports remain valid when automatic imports are enabled too.

Project references do not carry NuGet build/analyzer assets. The repository's
[`examples/Directory.Build.targets`](../../examples/Directory.Build.targets) imports the same namespace target and loads
the analyzer built by the runtime project reference, giving the eight development examples the same authoring behavior.
This setup uses the repository's standard `bin/$(Configuration)/netstandard2.0` layout. Custom output layouts and unrelated
project-reference consumers need their own explicit analyzer reference/import configuration.

## Package contents and verification

The package contains the net10.0 runtime, the separate netstandard2.0 C# analyzer, the namespace target, and a package
README. Its only direct runtime package dependencies remain Spectre.Console 0.57.2 and System.CommandLine 2.0.13;
compatible higher versions can resolve normally. Roslyn, xUnit, MTP, and AwesomeAssertions are not consumer dependencies.

The automated package test builds the actual nupkg and verifies its manifest/files, then restores and executes both a
fresh file app and a regular project with no Rafter source/project references. It checks diagnostics, import opt-out,
runtime checks with diagnostics disabled, design-time asset resolution, and the import boundary through a project reference.
CI retains a separately packed artifact under `packages/` alongside test reports. The diagnostic reference records
the limits of static coverage; the [milestone review](../history/milestone-review.md) records live VS Code diagnostics and completion.

References: [SDK Using items](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#using),
[NuGet build asset scopes](https://learn.microsoft.com/en-us/nuget/concepts/msbuild-props-and-targets),
[diagnostic reference](authoring-diagnostics.md).
