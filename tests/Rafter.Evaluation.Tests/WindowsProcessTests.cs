using System.Diagnostics;
using System.Text.Json;
using System.Runtime.Versioning;
using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

[Collection("Native process handles")]
public sealed class WindowsProcessTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Direct_arguments_preserve_empty_quotes_backslashes_and_shell_characters()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("This integration suite verifies Windows ownership.");
        }
        string[] values = ["", "a b", "\"quoted\"", "c:\\path with spaces\\", "\\\\\"", "a&b|c", "héllo 世界"];
        using var child = WindowsProcess.Start("dotnet", ProbeArguments("echo", values),
            Directory.GetCurrentDirectory(), new Dictionary<string, string>());
        var output = child.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = child.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
        await UntilAsync(() => child.IsEmpty);
        child.ExitCode.Should().Be(0);
        JsonSerializer.Deserialize<string[]>(await output).Should().Equal(values);
        (await error).Should().BeEmpty();
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Job_contains_descendants_after_root_exit_and_force_closes_their_inherited_pipes()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }
        var ready = Path.Combine(Path.GetTempPath(), "rafter-child-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var child = WindowsProcess.Start("dotnet", ProbeArguments("tree", ready),
                Directory.GetCurrentDirectory(), new Dictionary<string, string>());
            var output = child.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = child.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
            await UntilAsync(() => child.ExitCode.HasValue);
            child.ExitCode.Should().Be(0);
            child.IsEmpty.Should().BeFalse();
            var descendant = Process.GetProcessById(int.Parse(File.ReadAllText(ready)));
            using (descendant)
            {
                child.ForceStop();
                await UntilAsync(() => child.IsEmpty);
                descendant.HasExited.Should().BeTrue();
                (await output).Should().Be("parent finished");
                (await error).Should().BeEmpty();
            }
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Scoped_graceful_signal_or_immediate_force_settles_output()
    {
        var ready = Path.Combine(Path.GetTempPath(), "rafter-graceful-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var child = WindowsProcess.Start("dotnet", ProbeArguments("graceful", ready),
                Directory.GetCurrentDirectory(), new Dictionary<string, string>());
            var output = child.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = child.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
            await UntilAsync(() => File.Exists(ready));
            var supported = child.RequestGracefulStop();
            TestContext.Current.TestOutputHelper!.WriteLine("Scoped console signal supported: " + supported);
            if (!supported)
            {
                child.ForceStop();
            }
            await UntilAsync(() => child.IsEmpty);
            (await output).Should().Be(supported ? "graceful stop" : "");
            (await error).Should().BeEmpty();
            if (supported)
            {
                child.ExitCode.Should().Be(0);
            }
        }
        finally
        {
            File.Delete(ready);
        }
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task Child_directory_and_environment_overrides_do_not_change_host_state()
    {
        var directory = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var originalDirectory = Directory.GetCurrentDirectory();
        var name = "RAFTER_PROBE_" + Guid.NewGuid().ToString("N");
        using var child = WindowsProcess.Start("dotnet", ProbeArguments("environment", name), directory,
            new Dictionary<string, string> { [name] = "a=b; Unicode 世界" });
        var output = child.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = child.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
        await UntilAsync(() => child.IsEmpty);
        child.ExitCode.Should().Be(0);
        JsonSerializer.Deserialize<string[]>(await output).Should().Equal(directory, "a=b; Unicode 世界");
        (await error).Should().BeEmpty();
        Environment.GetEnvironmentVariable(name).Should().BeNull();
        Directory.GetCurrentDirectory().Should().Be(originalDirectory);
    }

    internal static string[] ProbeArguments(string mode, params string[] arguments)
    {
        var runtime = Path.Combine(AppContext.BaseDirectory, "Rafter.Evaluation.Tests.runtimeconfig.json");
        return ["exec", "--runtimeconfig", runtime, typeof(ProcessProbe.Program).Assembly.Location, runtime, mode, .. arguments];
    }

    internal static async Task UntilAsync(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition())
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(15))
            {
                throw new TimeoutException("The process fixture did not reach the expected state.");
            }
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }
    }
}
