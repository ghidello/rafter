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
$symbolPackagePath = Join-Path $packageRoot "Sotsera.Rafter.$PackageVersion.snupkg"

function Get-ArchiveEntries([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Package not found: $Path. Run dotnet pack first."
    }

    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        return @($archive.Entries | ForEach-Object FullName | Sort-Object)
    }
    finally {
        $archive.Dispose()
    }
}

function Assert-ArchiveLayout([string] $Path, [string[]] $ExpectedEntries) {
    $actualEntries = @(Get-ArchiveEntries $Path)
    [string[]] $actualEntriesForComparison = @($actualEntries)
    [string[]] $expectedEntriesForComparison = @($ExpectedEntries)
    [Array]::Sort($actualEntriesForComparison, [StringComparer]::Ordinal)
    [Array]::Sort($expectedEntriesForComparison, [StringComparer]::Ordinal)

    $compareArguments = @{
        ReferenceObject = $expectedEntriesForComparison
        DifferenceObject = $actualEntriesForComparison
        CaseSensitive = $true
    }
    $differences = @(Compare-Object @compareArguments)
    $missingEntries = @($differences | Where-Object SideIndicator -EQ "<=" | ForEach-Object InputObject)
    $unexpectedEntries = @($differences | Where-Object SideIndicator -EQ "=>" | ForEach-Object InputObject)

    if ($missingEntries.Count -gt 0 -or $unexpectedEntries.Count -gt 0) {
        $details = @(
            if ($missingEntries.Count -gt 0) {
                "missing: $($missingEntries -join ', ')"
            }
            if ($unexpectedEntries.Count -gt 0) {
                "unexpected: $($unexpectedEntries -join ', ')"
            }
        )
        throw "Package layout mismatch for $Path ($($details -join '; '))."
    }

    return $actualEntries
}

function Write-NuGetConfig([string] $Path, [string] $LocalSource) {
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Indent = $true
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)

    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement("configuration")
        $writer.WriteStartElement("packageSources")
        $writer.WriteStartElement("clear")
        $writer.WriteEndElement()
        $writer.WriteStartElement("add")
        $writer.WriteAttributeString("key", "rafter-local")
        $writer.WriteAttributeString("value", $LocalSource.Replace('\', '/'))
        $writer.WriteEndElement()
        $writer.WriteStartElement("add")
        $writer.WriteAttributeString("key", "nuget.org")
        $writer.WriteAttributeString("value", "https://api.nuget.org/v3/index.json")
        $writer.WriteAttributeString("protocolVersion", "3")
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    }
    finally {
        $writer.Dispose()
    }
}

$expectedPackageEntries = @(
    "_rels/.rels",
    "[Content_Types].xml",
    "package/services/metadata/core-properties/nuget.psmdcp",
    "README.md",
    "Sotsera.Rafter.nuspec",
    "lib/net10.0/Sotsera.Rafter.dll",
    "lib/net10.0/Sotsera.Rafter.xml"
)

$expectedSymbolPackageEntries = @(
    "_rels/.rels",
    "[Content_Types].xml",
    "package/services/metadata/core-properties/nuget.psmdcp",
    "Sotsera.Rafter.nuspec",
    "lib/net10.0/Sotsera.Rafter.pdb"
)

$entries = @(Assert-ArchiveLayout $packagePath $expectedPackageEntries)
$symbolEntries = @(Assert-ArchiveLayout $symbolPackagePath $expectedSymbolPackageEntries)

$reportPath = Join-Path $packageRoot "Sotsera.Rafter.$PackageVersion.contents.txt"
[System.IO.File]::WriteAllLines($reportPath, $entries)
$symbolReportPath = Join-Path $packageRoot "Sotsera.Rafter.$PackageVersion.symbols.contents.txt"
[System.IO.File]::WriteAllLines($symbolReportPath, $symbolEntries)

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("rafter-package-consumer-" + [Guid]::NewGuid().ToString("N"))
try {
    [System.IO.Directory]::CreateDirectory($temporaryRoot) | Out-Null
    $consumerProjectSource = Join-Path $repositoryRoot "test-assets/Sotsera.Rafter.PackageConsumer/Sotsera.Rafter.PackageConsumer.csproj"
    $consumerProject = [System.IO.File]::ReadAllText($consumerProjectSource).Replace("0.1.0-dev.1", $PackageVersion)
    [System.IO.File]::WriteAllText((Join-Path $temporaryRoot "Sotsera.Rafter.PackageConsumer.csproj"), $consumerProject)
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "test-assets/Sotsera.Rafter.PackageConsumer/Program.cs") -Destination $temporaryRoot

    $consumerProjectPath = Join-Path $temporaryRoot "Sotsera.Rafter.PackageConsumer.csproj"
    $consumerConfigPath = Join-Path $temporaryRoot "NuGet.Config"
    Write-NuGetConfig $consumerConfigPath $packageRoot
    & dotnet restore $consumerProjectPath --configfile $consumerConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "Package consumer restore failed with exit code $LASTEXITCODE."
    }

    & dotnet run --project $consumerProjectPath --configuration $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "Package consumer failed with exit code $LASTEXITCODE."
    }

    $consumerScriptSource = Join-Path $repositoryRoot "test-assets/Sotsera.Rafter.PackageConsumer/consumer.cs"
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

Write-Host "Verified package and symbol-package contents, external project restore, and file-based app restore."
