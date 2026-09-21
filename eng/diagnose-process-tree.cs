#:property IsPackable=false

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

try
{
    await RunAsync(args).ConfigureAwait(false);
    return 0;
}
catch (Exception exception)
{
    // A diagnostic failure should return promptly, without entering OS crash-report collection.
    await Console.Error.WriteLineAsync(exception.ToString()).ConfigureAwait(false);
    return 1;
}

static async Task RunAsync(string[] arguments)
{
    if (arguments.Length is < 1 or > 2
        || arguments.Length == 2 && !string.Equals(arguments[1], "--console-signals", StringComparison.Ordinal))
    {
        throw new ArgumentException(
            "Expected the fixture executable path and optional --console-signals.", nameof(arguments));
    }

    string fixturePath = Path.GetFullPath(arguments[0]);
    bool consoleSignals = arguments.Length == 2;
    Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; {RuntimeInformation.OSDescription}");
    Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}; probe PID: {Environment.ProcessId}");
    Console.WriteLine($"Console signal subscription: {consoleSignals}");

    for (int iteration = 1; iteration <= 5; iteration++)
    {
        // Exercise subscription lifetime without suppressing a real user/runner cancellation signal.
        ConsoleCancelEventHandler handler = static (_, _) => { };
        try
        {
            if (consoleSignals)
            {
                Console.WriteLine($"Iteration {iteration}: subscribing console signals");
                Console.CancelKeyPress += handler;
            }

            await RunIterationAsync(fixturePath, iteration).ConfigureAwait(false);
        }
        finally
        {
            if (consoleSignals)
            {
                Console.WriteLine($"Iteration {iteration}: unsubscribing console signals");
                Console.CancelKeyPress -= handler;
                Console.WriteLine($"Iteration {iteration}: console signal unsubscription settled");
            }
        }
    }
}

static async Task RunIterationAsync(string fixturePath, int iteration)
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
        await VerifyTreeAsync(controlDirectory, process.Id).ConfigureAwait(false);
    }
    finally
    {
        await CleanupAsync(controlDirectory, iteration).ConfigureAwait(false);
    }
}

static async Task VerifyTreeAsync(string controlDirectory, int rootProcessId)
{
    Dictionary<int, int> parents = [];
    foreach (string path in Directory.EnumerateFiles(controlDirectory, "*.json"))
    {
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(path).ConfigureAwait(false));
        JsonElement metadata = document.RootElement;
        parents.Add(metadata.GetProperty("processId").GetInt32(), metadata.GetProperty("parentProcessId").GetInt32());
    }

    if (parents.Count != 3
        || !parents.TryGetValue(rootProcessId, out int parent)
        || parent != Environment.ProcessId
        || parents.Values.Count(value => value == rootProcessId) != 1
        || parents.Values.Count(parents.ContainsKey) != 2)
    {
        throw new InvalidDataException("Expected three reported fixture processes in the requested tree.");
    }

    int childProcessId = parents.Single(pair => pair.Value == rootProcessId).Key;
    if (parents.Values.Count(value => value == childProcessId) != 1)
    {
        throw new InvalidDataException("Expected the reported child to own exactly one grandchild.");
    }

    Console.WriteLine("Verified all three fixture processes reported their parent relationships.");
}

static async Task CleanupAsync(string controlDirectory, int iteration)
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
