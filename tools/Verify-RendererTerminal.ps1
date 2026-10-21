$ErrorActionPreference = 'Stop'
if ([Console]::IsOutputRedirected -or [Console]::IsErrorRedirected) {
    throw 'Run this evidence script in a real terminal or pseudoterminal with both parent streams attached.'
}

$repository = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repository
$evidenceDirectory = Join-Path $repository 'artifacts/renderer/terminal'
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$assembly = Join-Path $repository 'tests/testassets/evaluations/renderer/full/project/bin/Release/net10.0/Consumer.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build the full renderer project consumer first.' }
$cases = @()

function Invoke-TerminalCase([bool]$RedirectOutput, [bool]$RedirectError, [string]$Mode,
    [string]$Scenario = '', [bool]$Narrow = $false) {
    $id = "$RedirectOutput-$RedirectError-$Mode-$Scenario-$Narrow"
    $recordPath = Join-Path $evidenceDirectory "$id.json"
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $RedirectOutput
    $start.RedirectStandardError = $RedirectError
    $start.Environment['TERM'] = 'xterm-256color'
    $start.Environment['CI'] = 'true'
    $start.Environment['GITHUB_ACTIONS'] = 'true'
    $start.Environment.Remove('NO_COLOR') | Out-Null
    if ($Mode -eq 'no-color') { $start.Environment['NO_COLOR'] = '1' }
    foreach ($argument in @($assembly, '--record', $recordPath)) { $start.ArgumentList.Add($argument) }
    if ($Mode -eq 'plain') { $start.ArgumentList.Add('--plain') }
    if ($Scenario) {
        $start.ArgumentList.Add('--help-sample')
        $start.ArgumentList.Add($Scenario)
        $start.ArgumentList.Add('--adversarial')
    }
    if ($Narrow) { $start.ArgumentList.Add('--narrow') }
    $process = [Diagnostics.Process]::Start($start)
    try {
        $reads = @()
        if ($RedirectOutput) { $reads += $process.StandardOutput.ReadToEndAsync() }
        if ($RedirectError) { $reads += $process.StandardError.ReadToEndAsync() }
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            $process.WaitForExit()
            foreach ($read in $reads) { $read.GetAwaiter().GetResult() | Out-Null }
            throw "Terminal case timed out: $id"
        }
        foreach ($read in $reads) { $read.GetAwaiter().GetResult() | Out-Null }
        if ($process.ExitCode -ne 0) { throw "Terminal consumer failed: $id" }
    }
    finally { $process.Dispose() }

    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    if ($record.OutputRedirected -ne $RedirectOutput -or $record.ErrorRedirected -ne $RedirectError) {
        throw "Actual handle redirection did not match the case: $id"
    }
    if (-not $record.WritersRetained) { throw "Host writer ownership changed: $id" }
    foreach ($stream in @('Output', 'Error')) {
        $redirected = if ($stream -eq 'Output') { $RedirectOutput } else { $RedirectError }
        $expectedRich = -not $redirected -and $Mode -ne 'plain'
        if ($record."${stream}Rich" -ne $expectedRich) { throw "Wrong presentation policy: $id $stream" }
        if (-not $expectedRich -and $record.$stream.Contains([char]27)) { throw "ANSI leaked: $id $stream" }
        if ($expectedRich -and $Mode -eq 'normal' -and $record.$stream.Length -gt 0 -and
            -not $record.$stream.Contains([char]27)) { throw "Expected styling absent: $id $stream" }
        if (-not $Scenario) {
            $prefix = if ($stream -eq 'Output') { '  | ' } else { '  > ' }
            if ($record.$stream.Contains($prefix) -ne $expectedRich) { throw "Wrong prefix policy: $id $stream" }
            if ($Mode -eq 'no-color' -and $record.$stream.Contains([char]27)) {
                throw "Colorless fixture emitted unexpected ANSI: $id $stream"
            }
        }
    }
    if ($Scenario) {
        if ($record.Error.Length -ne 0) { throw "Help wrote stderr: $id" }
        if (-not $record.Output.Contains('[red]brackets[/]')) { throw "Help interpreted markup: $id" }
    }
    else {
        $outputText = $record.Output -replace '\x1b\[[0-9;]*m', '' -replace '(?m)^  [|>] ', ''
        $errorText = $record.Error -replace '\x1b\[[0-9;]*m', '' -replace '(?m)^  > ', ''
        if ($outputText -cne "  script [red]literal[/]`n`nchild stdout`rprogress`npartial") {
            throw "Literal stdout changed: $id"
        }
        if ($errorText -cne "Cancellation requested.$([Environment]::NewLine)  child stderr`n") {
            throw "Literal stderr changed: $id"
        }
    }
    return [pscustomobject]@{ Case = $id; Passed = $true; Record = $recordPath }
}

foreach ($mode in @('normal', 'plain', 'no-color')) {
    foreach ($redirectOutput in @($false, $true)) {
        foreach ($redirectError in @($false, $true)) {
            $cases += Invoke-TerminalCase $redirectOutput $redirectError $mode
        }
    }
}
foreach ($scenario in @('options', 'validation', 'dependencies')) {
    foreach ($narrow in @($false, $true)) {
        $cases += Invoke-TerminalCase $false $false 'normal' $scenario $narrow
    }
}
$cases | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$evidenceDirectory/results.json" -Encoding utf8NoBOM
Write-Output "Terminal cases passed: $($cases.Count). Raw pre-terminal text and detected handle states: $evidenceDirectory"
