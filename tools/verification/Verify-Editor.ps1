param([Parameter(Mandatory)][string]$CodeExecutable)

$ErrorActionPreference = 'Stop'
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$root = Join-Path $repository ('artifacts/evidence/verification/editor/' + $runId)
$workspace = Join-Path $root 'workspace'
$profile = Join-Path $root 'profile'
New-Item -ItemType Directory -Path "$workspace/.vscode", $profile -Force | Out-Null
Write-Host "Evidence: $root"
Copy-Item -LiteralPath (Join-Path $repository 'global.json') -Destination "$workspace/global.json"
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Sotsera.Rafter" Version="0.1.0-local" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath "$workspace/EditorProbe.csproj" -Encoding utf8
@'
var command = Rafter.Command("");
var name = command.Option<string?>("name", "Name.");
name.Required();
name.Default("Ada");
return await command.RunAsync(args, context =>
{
    _ = context.Process("must-never-launch").RunAsync();
});
'@ | Set-Content -LiteralPath "$workspace/Program.cs" -Encoding utf8
@'
<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>
'@ | Set-Content -LiteralPath "$workspace/Directory.Packages.props" -Encoding utf8
# Prevent future repository build imports from changing this package-only workspace.
'<Project />' | Set-Content -LiteralPath "$workspace/Directory.Build.props" -Encoding utf8
'<Project />' | Set-Content -LiteralPath "$workspace/Directory.Build.targets" -Encoding utf8
@{ 'dotnet.backgroundAnalysis.analyzerDiagnosticsScope' = 'openFiles' } |
    ConvertTo-Json | Set-Content -LiteralPath "$workspace/.vscode/settings.json" -Encoding utf8
$config = [xml]'<configuration><packageSources><clear/><add key="local" value=""/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><config><add key="globalPackagesFolder" value=".packages"/></config><packageSourceMapping><clear/><packageSource key="local"><package pattern="Sotsera.Rafter"/></packageSource><packageSource key="nuget.org"><package pattern="*"/></packageSource></packageSourceMapping></configuration>'
$config.configuration.packageSources.add[0].value = Join-Path $repository 'artifacts/packages'
$config.Save("$workspace/NuGet.Config")
dotnet restore "$workspace/EditorProbe.csproj"
if ($LASTEXITCODE -ne 0) { throw 'Editor consumer restore failed. Pack the local package first.' }

$fileApp = Join-Path $root 'file-app'
New-Item -ItemType Directory -Path $fileApp -Force | Out-Null
foreach ($name in @('global.json', 'NuGet.Config', 'Directory.Build.props', 'Directory.Build.targets', 'Directory.Packages.props')) {
    Copy-Item -LiteralPath "$workspace/$name" -Destination "$fileApp/$name"
}
$fileSource = "#:package Sotsera.Rafter@0.1.0-local`n#:property PublishAot=false`n#:property Nullable=enable`n" +
    (Get-Content -LiteralPath "$workspace/Program.cs" -Raw)
$fileSource | Set-Content -LiteralPath "$fileApp/probe.cs" -Encoding utf8
dotnet restore "$fileApp/probe.cs"
if ($LASTEXITCODE -ne 0) { throw 'Editor file-app restore failed.' }

$previousWorkspace = $env:RAFTER_EDITOR_WORKSPACE
$previousResult = $env:RAFTER_EDITOR_RESULT
$previousFileApp = $env:RAFTER_EDITOR_FILEAPP
$env:RAFTER_EDITOR_WORKSPACE = $workspace
$env:RAFTER_EDITOR_RESULT = Join-Path $root 'result.json'
$env:RAFTER_EDITOR_FILEAPP = Join-Path $fileApp 'probe.cs'
$probe = Join-Path $PSScriptRoot 'editor-probe'
$launchArgs = @('--new-window', '--skip-welcome', '--skip-release-notes', '--disable-workspace-trust',
    '--disable-telemetry', "--user-data-dir=`"$profile`"", "--extensionDevelopmentPath=`"$probe`"",
    "--extensionTestsPath=`"$probe/test.js`"", "`"$workspace`"")
try {
    $process = Start-Process -FilePath $CodeExecutable -ArgumentList $launchArgs -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput "$root/stdout.log" -RedirectStandardError "$root/stderr.log"
    try {
        if (-not $process.WaitForExit(300000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "Isolated editor timed out. Evidence: $root"
        }
    }
    finally { $process.Dispose() }
    if (-not (Test-Path -LiteralPath $env:RAFTER_EDITOR_RESULT)) { throw "Editor did not write evidence: $root" }
    $result = Get-Content -LiteralPath $env:RAFTER_EDITOR_RESULT -Raw | ConvertFrom-Json
    if (-not $result.passed) { throw "Editor checks failed. See $env:RAFTER_EDITOR_RESULT" }
    Write-Output "Editor checks passed: $env:RAFTER_EDITOR_RESULT"
}
finally {
    $env:RAFTER_EDITOR_WORKSPACE = $previousWorkspace
    $env:RAFTER_EDITOR_RESULT = $previousResult
    $env:RAFTER_EDITOR_FILEAPP = $previousFileApp
}
