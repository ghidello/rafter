[CmdletBinding()]
param([string] $Configuration = "Release")

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$examplesRoot = Join-Path $repositoryRoot "examples"
$reportRoot = Join-Path $repositoryRoot "artifacts/example-verification"
[System.IO.Directory]::CreateDirectory($reportRoot) | Out-Null
$fixtureName = if ($IsWindows) { "Sotsera.Rafter.ProcessFixture.exe" } else { "Sotsera.Rafter.ProcessFixture" }
$fixture = Join-Path $repositoryRoot "artifacts/bin/Sotsera.Rafter.ProcessFixture/$($Configuration.ToLowerInvariant())/$fixtureName"
if (-not (Test-Path -LiteralPath $fixture)) {
    throw "Build the solution in $Configuration before verifying examples."
}

# Keep the owning phase explicit. Phase 8/9 will extend this list and add package-mode portfolio coverage.
$implemented = @(
    "callback-cancellation", "cleanup", "concurrent-failures", "conditions", "console", "dependencies",
    "diagnostics", "environment", "failures", "filesystem", "minimal", "no-op", "option-types", "options",
    "presentation", "process-cancellation", "processes", "redaction", "root-explicit", "root-invocation",
    "root-source", "user-secrets", "validation-failures", "working-directory"
)
$deferred = @("dotnet", "extensibility", "git", "node", "repository")
$actual = @(Get-ChildItem -LiteralPath $examplesRoot -Filter "*.cs" | ForEach-Object BaseName | Sort-Object)
$classified = @(($implemented + $deferred) | Sort-Object)
if (@(Compare-Object $actual $classified).Count -ne 0) {
    throw "Every canonical example must have an explicit implemented or deferred classification."
}

function Invoke-CheckedDotNet([string] $Name, [string[]] $Arguments, [int] $ExpectedExit = 0) {
    $start = [System.Diagnostics.ProcessStartInfo]::new("dotnet")
    $start.WorkingDirectory = $repositoryRoot
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment["RAFTER_EXAMPLE_ROOT"] = $reportRoot
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill($true)
            $process.WaitForExit()
            throw "$Name exceeded the two-minute verification deadline."
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        [System.IO.File]::WriteAllText((Join-Path $reportRoot "$Name.stdout.txt"), $output)
        [System.IO.File]::WriteAllText((Join-Path $reportRoot "$Name.stderr.txt"), $errorOutput)
        if ($process.ExitCode -ne $ExpectedExit) {
            throw "$Name exited $($process.ExitCode), expected $ExpectedExit.`n$output`n$errorOutput"
        }
        return [pscustomobject]@{ Output = $output; Error = $errorOutput }
    }
    finally { $process.Dispose() }
}

function Invoke-Example([string] $Name, [string[]] $ExampleArguments = @(), [int] $ExpectedExit = 0) {
    $arguments = @("run", (Join-Path $examplesRoot "$Name.cs"), "--configuration", $Configuration,
        "--no-build", "--", "--plain") + $ExampleArguments
    $caseName = if ($ExampleArguments -contains "--help") { "$Name-help" } elseif ($ExpectedExit -ne 0) { "$Name-failure" } else { $Name }
    return Invoke-CheckedDotNet $caseName $arguments $ExpectedExit
}

foreach ($name in $implemented) {
    $source = Join-Path $examplesRoot "$name.cs"
    $null = Invoke-CheckedDotNet "$name-build" @("build", $source, "--configuration", $Configuration, "--nologo")
    $help = Invoke-Example $name @("--help")
    if (-not $help.Output.Contains("Usage") -or $help.Error.Length -ne 0) {
        throw "$name did not produce successful plain help."
    }
    Write-Host "Compiled $name.cs and verified help."
}

$minimal = Invoke-Example "minimal"
if (-not $minimal.Output.Contains("Hello from Rafter.")) { throw "Missing minimal greeting." }
$dependencies = Invoke-Example "dependencies"
if ([regex]::Matches($dependencies.Output, [regex]::Escape("Restored.")).Count -ne 1) {
    throw "The shared dependency must execute exactly once."
}
$null = Invoke-Example "conditions" @("--enabled")
$null = Invoke-Example "no-op"
$cleanup = Invoke-Example "cleanup"
if (-not $cleanup.Output.Contains("[command] Command cleanup.")) { throw "Missing command cleanup attribution." }
$console = Invoke-Example "console"
foreach ($line in @("[immediate] immediate: stdout", "[first] first: stdout", "[second] second: task-run")) {
    if (-not $console.Output.Contains($line)) { throw "Missing console attribution: $line" }
}
foreach ($line in @("[first] first: stderr", "[second] second: configure-await-false")) {
    if (-not $console.Error.Contains($line)) { throw "Missing console attribution: $line" }
}
foreach ($failed in @($false, $true)) {
    $presentationScenario = if ($failed) { "presentation-failure" } else { "presentation-success" }
    $presentationResult = if ($failed) { Invoke-Example "presentation" @("--fail") 1 } else { Invoke-Example "presentation" }
    $presentationFixturePath = Join-Path $repositoryRoot "docs/phases/phase-06-presentation-fixtures/$presentationScenario.json"
    $presentationFixture = Get-Content -LiteralPath $presentationFixturePath -Raw | ConvertFrom-Json
    $presentationExpected = $presentationFixture.documents | Where-Object { $_.profile.name -eq "plain" }
    if ($presentationResult.Output -cne $presentationExpected.stdout -or $presentationResult.Error -cne $presentationExpected.stderr) {
        throw "$presentationScenario does not match its accepted plain stdout/stderr documents."
    }
}
$failures = Invoke-Example "failures" @() 1
if ($failures.Output.Contains("This target should be blocked.")) { throw "A blocked callback ran." }
if (-not $failures.Output.Contains("Independent work settled.")) { throw "Independent work did not settle." }
$concurrent = Invoke-Example "concurrent-failures" @() 1
$firstFailure = $concurrent.Error.IndexOf("Target 'first'", [StringComparison]::Ordinal)
$secondFailure = $concurrent.Error.IndexOf("Target 'second'", [StringComparison]::Ordinal)
if ($firstFailure -lt 0 -or $secondFailure -lt 0 -or $firstFailure -ge $secondFailure) {
    throw "Concurrent failures were not reported in plan order."
}
$processes = Invoke-Example "processes" @("--fixture", $fixture)
if (-not $processes.Output.Contains('captured="fixture output"')) { throw "Missing exact captured data." }
$environmentFixture = $fixture
if (-not $IsWindows) {
    # The example clears DOTNET_ROOT. Use an explicit host for SDKs installed outside the global search path.
    $environmentFixture = Join-Path $reportRoot "environment-fixture.sh"
    $dotnetToken = "'" + (Get-Command dotnet).Source.Replace("'", "'\''") + "'"
    $assemblyToken = "'" + (Join-Path (Split-Path $fixture) "Sotsera.Rafter.ProcessFixture.dll").Replace("'", "'\''") + "'"
    [System.IO.File]::WriteAllText($environmentFixture, "#!/bin/sh`nexec $dotnetToken $assemblyToken" + ' "$@"' + "`n")
    [System.IO.File]::SetUnixFileMode($environmentFixture,
        [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite -bor [System.IO.UnixFileMode]::UserExecute)
}
$environment = Invoke-Example "environment" @("--fixture", $environmentFixture)
if (-not $environment.Output.Contains('"CI":"true"')) { throw "Child environment was not applied." }
$redaction = Invoke-Example "redaction" @("--fixture", $fixture, "--synthetic-secret", "example-disposable-secret")
if (($redaction.Output + $redaction.Error).Contains("example-disposable-secret")) {
    throw "The synthetic secret escaped managed output."
}
$working = Invoke-Example "working-directory" @("--fixture", $fixture, "--target-directory", "artifacts", "--process-directory", ".")
if (-not $working.Output.Contains("targetCleanupWorkingDirectory")) { throw "Missing target cleanup directory." }

Write-Host "Verified 24 unchanged examples in project mode, including 14 deterministic execution cases."
Write-Host "Cancellation examples compile and render help; bounded cancellation execution remains covered by process fixtures."
Write-Host "Phase 8/9 portfolio deferrals: $($deferred -join ', '). Reports: $reportRoot"
