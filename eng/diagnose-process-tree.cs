#:property IsPackable=false

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

if (args.Length != 1)
{
    throw new ArgumentException("Expected the process fixture executable path.", nameof(args));
}

string fixturePath = Path.GetFullPath(args[0]);
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}");
Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}; probe PID: {Environment.ProcessId}");

for (int iteration = 1; iteration <= 5; iteration++)
{
    string controlDirectory = Path.GetFullPath(Path.Combine("artifacts", "process-tree-probe", Guid.NewGuid().ToString("N")));
    Directory.CreateDirectory(controlDirectory);
    ProcessStartInfo startInfo = new(fixturePath)
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    startInfo.ArgumentList.Add("spawn-child");
    startInfo.ArgumentList.Add("--control-directory");
    startInfo.ArgumentList.Add(controlDirectory);
    startInfo.ArgumentList.Add("--parent-process-id");
    startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    startInfo.ArgumentList.Add("--depth");
    startInfo.ArgumentList.Add("2");

    Console.WriteLine($"Iteration {iteration}: starting fixture; metadata: {controlDirectory}");
    using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Fixture did not start.");
    Task output = process.StandardOutput.ReadToEndAsync();
    Task error = process.StandardError.ReadToEndAsync();
    Task exit = process.WaitForExitAsync();
    try
    {
        await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(false);
        Console.WriteLine($"Iteration {iteration}: calling Process.Kill(entireProcessTree: true), PID {process.Id}");
        Stopwatch duration = Stopwatch.StartNew();
        // Match Rafter's dedicated kill thread, without referencing any Rafter code or the test framework.
        await Task.Factory.StartNew(
            () => process.Kill(entireProcessTree: true),
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Console.WriteLine($"Iteration {iteration}: tree kill returned after {duration.Elapsed}");
        await Task.WhenAll(exit, output, error).WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        Console.WriteLine($"Iteration {iteration}: exit and both drains settled");
    }
    finally
    {
        // Keep cleanup independent of the API under investigation, using only fixture-reported PIDs.
        foreach (string metadataPath in Directory.EnumerateFiles(controlDirectory, "*.json"))
        {
            using JsonDocument metadata = JsonDocument.Parse(
                await File.ReadAllTextAsync(metadataPath).ConfigureAwait(false));
            int processId = metadata.RootElement.GetProperty("processId").GetInt32();
            Console.WriteLine($"Iteration {iteration}: checking fixture PID {processId}");
            try
            {
                using Process remaining = Process.GetProcessById(processId);
                Console.WriteLine($"Iteration {iteration}: PID {processId} still has a process-table entry; killing it");
                remaining.Kill();
                using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
                await remaining.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            }
            catch (ArgumentException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        Console.WriteLine($"Iteration {iteration}: cleanup settled");
    }
}
