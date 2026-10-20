param([int]$Samples = 9)

$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $repository
$evidenceDirectory = Join-Path $repository 'artifacts/renderer'
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

function Invoke-Probe([string[]]$ProbeArguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.WorkingDirectory = $repository
    foreach ($argument in $ProbeArguments) { $start.ArgumentList.Add($argument) }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $process = [Diagnostics.Process]::Start($start)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(120000)) {
            $process.Kill($true)
            $process.WaitForExit()
            [Threading.Tasks.Task]::WaitAll(@($stdout, $stderr))
            throw 'Renderer measurement process timed out.'
        }
        $output = $stdout.GetAwaiter().GetResult()
        $errorText = $stderr.GetAwaiter().GetResult()
        $timer.Stop()
        if ($process.ExitCode -ne 0) { throw "dotnet failed: $output $errorText" }
        return [pscustomobject]@{ Milliseconds = $timer.Elapsed.TotalMilliseconds; Output = $output; Error = $errorText }
    }
    finally { $process.Dispose() }
}

$consumers = @()
$builds = @()
foreach ($variant in @('framework', 'ansi', 'full')) {
    foreach ($hostKind in @('file-app', 'project')) {
        $target = if ($hostKind -eq 'file-app') { "fixtures/renderer/$variant/probe.cs" }
                  else { "fixtures/renderer/$variant/project/Consumer.csproj" }
        $restore = Invoke-Probe @('restore', $target)
        $build = Invoke-Probe @('build', $target, '--configuration', 'Release', '--no-restore', '--disable-build-servers')
        $builds += [pscustomobject]@{
            Variant = $variant; Host = $hostKind; RestoreMilliseconds = $restore.Milliseconds
            BuildMilliseconds = $build.Milliseconds; BuildOutput = $build.Output
        }
        [string[]]$command = if ($hostKind -eq 'file-app') {
            @('run', '--file', $target, '--configuration', 'Release', '--no-build', '--')
        } else { @("fixtures/renderer/$variant/project/bin/Release/net10.0/Consumer.dll") }
        $consumers += [pscustomobject]@{ Variant = $variant; Host = $hostKind; Command = $command }
    }
}

# Round-robin launches reduce temporal bias. Every sample is a fresh process; only OS/SDK caches warm across launches.
$runs = @()
for ($sample = 0; $sample -le $Samples; $sample++) {
    foreach ($consumer in $consumers) {
        $run = Invoke-Probe ($consumer.Command + @('--measure'))
        $runs += [pscustomobject]@{
            Variant = $consumer.Variant; Host = $consumer.Host; Sample = $sample
            ProcessMilliseconds = $run.Milliseconds; Metrics = ($run.Output | ConvertFrom-Json)
        }
    }
}
$steady = @()
foreach ($consumer in $consumers) {
    $run = Invoke-Probe ($consumer.Command + @('--measure', '--steady'))
    $steady += [pscustomobject]@{
        Variant = $consumer.Variant; Host = $consumer.Host; Metrics = ($run.Output | ConvertFrom-Json)
    }
}

$evidence = [pscustomobject]@{
    RecordedAtUtc = [DateTime]::UtcNow.ToString('O'); OS = [Environment]::OSVersion.VersionString
    SDK = (Invoke-Probe @('--version')).Output.Trim(); SamplesAfterFirst = $Samples
    Method = 'First launch after build, then fresh processes with warmed caches; not OS-cold-cache measurement. File-app timing includes dotnet run --no-build; project timing uses dotnet DLL. Allocations cover factory plus first render, or reused-writer steady state; output goes to TextWriter.Null.'
    Builds = $builds; Runs = $runs; Steady = $steady
}
$evidence | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath "$evidenceDirectory/measurements.json" -Encoding utf8NoBOM
foreach ($consumer in $consumers) {
    $group = @($runs | Where-Object { $_.Variant -eq $consumer.Variant -and $_.Host -eq $consumer.Host })
    $warm = @($group | Where-Object Sample -gt 0 | Sort-Object ProcessMilliseconds)
    [pscustomobject]@{
        Variant = $consumer.Variant; Host = $consumer.Host
        FirstMs = [math]::Round($group[0].ProcessMilliseconds, 2)
        WarmMedianMs = [math]::Round($warm[[int][math]::Floor($warm.Count / 2)].ProcessMilliseconds, 2)
        FirstRenderMs = [math]::Round($group[0].Metrics.FirstMilliseconds, 3)
        FirstBytes = $group[0].Metrics.FirstBytes
    }
}
