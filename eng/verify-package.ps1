[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $PackageVersion = "0.1.0-dev.1"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$packageRoot = Join-Path $repositoryRoot "artifacts/package/$Configuration"
$packagePath = Join-Path $packageRoot "Sotsera.Rafter.$PackageVersion.nupkg"

if (-not (Test-Path -LiteralPath $packagePath)) {
    throw "Package not found: $packagePath. Run dotnet pack first."
}

$archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
try {
    $entries = @($archive.Entries | ForEach-Object FullName | Sort-Object)
}
finally {
    $archive.Dispose()
}

$requiredEntries = @(
    "README.md",
    "Sotsera.Rafter.nuspec",
    "lib/net10.0/Sotsera.Rafter.dll",
    "lib/net10.0/Sotsera.Rafter.xml"
)

foreach ($requiredEntry in $requiredEntries) {
    if ($requiredEntry -notin $entries) {
        throw "Package is missing required entry: $requiredEntry"
    }
}

$forbiddenEntries = @($entries | Where-Object {
    $_ -like "analyzers/*" -or $_ -like "lib/*/xunit*" -or $_ -like "lib/*/Microsoft.CodeAnalysis*"
})
if ($forbiddenEntries.Count -gt 0) {
    throw "Package contains unintended entries: $($forbiddenEntries -join ', ')"
}

$reportPath = Join-Path $packageRoot "Sotsera.Rafter.$PackageVersion.contents.txt"
[System.IO.File]::WriteAllLines($reportPath, $entries)

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("rafter-package-consumer-" + [Guid]::NewGuid().ToString("N"))
try {
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $consumerProjectSource = Join-Path $repositoryRoot "tests/Sotsera.Rafter.PackageConsumer/Sotsera.Rafter.PackageConsumer.csproj"
    $consumerProject = [System.IO.File]::ReadAllText($consumerProjectSource).Replace("0.1.0-dev.1", $PackageVersion)
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot "Sotsera.Rafter.PackageConsumer.csproj"), $consumerProject)
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "tests/Sotsera.Rafter.PackageConsumer/Program.cs") -Destination $temporaryRoot

    $nugetConfigContent = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="rafter-local" value="$($packageRoot.Replace('\', '/'))" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot "NuGet.Config"), $nugetConfigContent)

    $consumerProjectPath = Join-Path $temporaryRoot "Sotsera.Rafter.PackageConsumer.csproj"
    $consumerConfigPath = Join-Path $temporaryRoot "NuGet.Config"
    & dotnet restore $consumerProjectPath --configfile $consumerConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "Package consumer restore failed with exit code $LASTEXITCODE."
    }

    & dotnet run --project $consumerProjectPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Package consumer failed with exit code $LASTEXITCODE."
    }

    $consumerScriptSource = Join-Path $repositoryRoot "tests/Sotsera.Rafter.PackageConsumer/consumer.cs"
    $consumerScript = [System.IO.File]::ReadAllText($consumerScriptSource).Replace("0.1.0-dev.1", $PackageVersion)
    $consumerScriptPath = Join-Path $temporaryRoot "consumer.cs"
    [System.IO.File]::WriteAllText($consumerScriptPath, $consumerScript)

    & dotnet restore $consumerScriptPath --configfile $consumerConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "File-based package consumer restore failed with exit code $LASTEXITCODE."
    }

    & dotnet run $consumerScriptPath --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "File-based package consumer failed with exit code $LASTEXITCODE."
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Host "Verified package contents, external project restore, and file-based app restore."
