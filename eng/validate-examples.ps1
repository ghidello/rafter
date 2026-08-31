[CmdletBinding()]
param(
    [string] $PackageVersion = "0.1.0-dev.1",
    [string] $OutputDirectory = (Join-Path $PSScriptRoot "../artifacts/example-validation")
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$examplesRoot = Join-Path $repositoryRoot "examples"
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$sourcesRoot = Join-Path $outputRoot "sources"

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

[System.IO.Directory]::CreateDirectory($sourcesRoot) | Out-Null
$manifest = [System.Collections.Generic.List[object]]::new()

foreach ($source in Get-ChildItem -LiteralPath $examplesRoot -Filter "*.cs" | Sort-Object Name) {
    $originalHash = (Get-FileHash -LiteralPath $source.FullName -Algorithm SHA256).Hash
    $content = [System.IO.File]::ReadAllText($source.FullName)
    $updated = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '(?m)^#:package\s+Sotsera\.Rafter@[^\r\n]+$',
        "#:package Sotsera.Rafter@$PackageVersion")

    $destination = Join-Path $sourcesRoot $source.Name
    [System.IO.File]::WriteAllText($destination, $updated, [System.Text.UTF8Encoding]::new($false))
    $manifest.Add([pscustomobject]@{
        source = "examples/$($source.Name)"
        generated = "sources/$($source.Name)"
        originalSha256 = $originalHash
        packageVersion = $PackageVersion
    })
}

[System.IO.File]::WriteAllText(
    (Join-Path $outputRoot "manifest.json"),
    ($manifest | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))

foreach ($entry in $manifest) {
    $sourcePath = Join-Path $repositoryRoot $entry.source
    $currentHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
    if ($currentHash -ne $entry.originalSha256) {
        throw "Example validation modified $($entry.source)."
    }
}

Write-Host "Generated $($manifest.Count) example validation copies in $sourcesRoot."
