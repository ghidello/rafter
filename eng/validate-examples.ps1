[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$examplesRoot = Join-Path $repositoryRoot "examples"
$expectedRafterReference = "#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj"
$sources = @(Get-ChildItem -LiteralPath $examplesRoot -Filter "*.cs" | Sort-Object Name)

if ($sources.Count -eq 0) {
    throw "No canonical examples were found in $examplesRoot."
}

foreach ($source in $sources) {
    $firstLine = Get-Content -LiteralPath $source.FullName -TotalCount 1
    if ($firstLine -ne $expectedRafterReference) {
        throw "$($source.Name) must begin with '$expectedRafterReference'."
    }
}

Write-Host "Validated project-mode references for $($sources.Count) canonical examples."
