using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Tests.Processes;

// The baseline reproduction deliberately creates an inheritable pipe in the test host. Keep unrelated SDK
// launchers out of that reproduction. Mixed-launcher concurrency is exercised explicitly below.
[CollectionDefinition("Native process handles", DisableParallelization = true)]
public sealed class NativeProcessHandleCollection;

[SupportedOSPlatform("windows")]
[Collection("Native process handles")]
public sealed class WindowsHandleInheritanceTests
{
    [Fact]
    public async Task Redirected_standard_input_reaches_the_child_through_the_donor()
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            StandardInputEncoding = new System.Text.UTF8Encoding(false),
            RedirectStandardOutput = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            RedirectStandardError = true,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        foreach (var argument in WindowsProcessTests.ProbeArguments("stdin-host"))
        {
            start.ArgumentList.Add(argument);
        }
        using var host = Process.Start(start)!;
        var output = host.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var error = host.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        try
        {
            await host.StandardInput.WriteLineAsync("héllo input".AsMemory(), TestContext.Current.CancellationToken);
            host.StandardInput.Close();
            await host.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(15),
                TestContext.Current.CancellationToken);
            host.ExitCode.Should().Be(0);
            (await output).Should().Be("redirected:héllo input");
            (await error).Should().BeEmpty();
        }
        finally
        {
            if (!host.HasExited)
            {
                host.Kill(entireProcessTree: true);
                await host.WaitForExitAsync(TestContext.Current.CancellationToken);
            }
            await Task.WhenAll(output, error);
        }
    }

    [Theory]
    [InlineData("output")]
    [InlineData("large-output")]
    [InlineData("graceful")]
    [InlineData("hold")]
    [InlineData("tree")]
    public async Task Foreign_launch_during_handle_preparation_cannot_retain_owned_output(string mode)
    {
        var ready = Path.Combine(Path.GetTempPath(), "rafter-mixed-" + Guid.NewGuid().ToString("N"));
        var foreignReady = ready + "-foreign";
        Process? foreign = null;
        Process? donor = null;
        WindowsProcess? owned = null;
        Task<string>? foreignOutput = null;
        Task<string>? foreignError = null;
        Task<string>? output = null;
        Task<string>? error = null;
        try
        {
            var arguments = mode is "output" or "large-output"
                ? WindowsProcessTests.ProbeArguments(mode)
                : WindowsProcessTests.ProbeArguments(mode, ready);
            owned = WindowsProcess.Start("dotnet", arguments, Directory.GetCurrentDirectory(),
                new Dictionary<string, string>(), donorId =>
                {
                    donor = Process.GetProcessById(donorId);
                    // Deterministically launch while every inheritable stdio copy is still open.
                    foreign = StartForeign(foreignReady);
                    foreignOutput = foreign.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                    foreignError = foreign.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
                });
            donor!.HasExited.Should().BeTrue("the helper must settle before Start returns");
            output = owned.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
            error = owned.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
            await WindowsProcessTests.UntilAsync(() => File.Exists(foreignReady));
            var graceful = false;
            if (mode is "graceful" or "hold" or "tree")
            {
                await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
                if (mode == "tree")
                {
                    await WindowsProcessTests.UntilAsync(() => owned.ExitCode.HasValue);
                    owned.IsEmpty.Should().BeFalse("the descendant remains owned after root exit");
                }
                graceful = mode == "graceful" && owned.RequestGracefulStop();
                if (!graceful)
                {
                    owned.ForceStop();
                }
            }
            await WindowsProcessTests.UntilAsync(() => owned.IsEmpty);
            var text = await output.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var errors = await error.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var expected = mode switch
            {
                "output" => "  first\n\nlast",
                "large-output" => string.Concat(Enumerable.Repeat("abcdefghij", 10000)),
                "graceful" when graceful => "graceful stop",
                "tree" => "parent finished",
                _ => ""
            };
            text.Should().Be(expected);
            errors.Should().Be(mode switch
            {
                "output" => "  error\npartial",
                "large-output" => string.Concat(Enumerable.Repeat("0123456789", 10000)),
                _ => ""
            });
            foreign!.HasExited.Should().BeFalse("unrelated process lifetime must not control Rafter output completion");
        }
        finally
        {
            try
            {
                owned?.ForceStop();
                if (owned is not null)
                {
                    await WindowsProcessTests.UntilAsync(() => owned.IsEmpty);
                }
                if (foreign is { HasExited: false })
                {
                    foreign.Kill(entireProcessTree: true);
                    await foreign.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
                await Task.WhenAll(output ?? Task.FromResult(""), error ?? Task.FromResult(""),
                    foreignOutput ?? Task.FromResult(""), foreignError ?? Task.FromResult(""))
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            finally
            {
                owned?.Dispose();
                foreign?.Dispose();
                donor?.Dispose();
                File.Delete(ready);
                File.Delete(foreignReady);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failed_launch_also_terminates_the_handle_donor(bool preparationFails)
    {
        Process? donor = null;
        try
        {
            Action launch = () =>
            {
                using var child = WindowsProcess.Start("rafter-missing-" + Guid.NewGuid().ToString("N"), [],
                    Directory.GetCurrentDirectory(), new Dictionary<string, string>(), donorId =>
                    {
                        donor = Process.GetProcessById(donorId);
                        if (preparationFails)
                        {
                            throw new IOException("Simulated preparation failure.");
                        }
                    });
            };
            launch.Should().Throw<Exception>();
            donor.Should().NotBeNull();
            donor!.HasExited.Should().BeTrue();
        }
        finally
        {
            donor?.Dispose();
        }
    }

    private static Process StartForeign(string ready)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in WindowsProcessTests.ProbeArguments("hold", ready))
        {
            start.ArgumentList.Add(argument);
        }
        return Process.Start(start)!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_the_explicit_handle_list_excludes_an_unrelated_inheritable_pipe(bool explicitHandleList)
    {
        var pipeName = "rafter-inheritance-" + Guid.NewGuid().ToString("N");
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out, PipeOptions.None,
            System.Security.Principal.TokenImpersonationLevel.Anonymous, HandleInheritability.Inheritable);
        var connected = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);
        await client.ConnectAsync(TestContext.Current.CancellationToken);
        await connected;

        var ready = Path.Combine(Path.GetTempPath(), "rafter-inheritance-" + Guid.NewGuid().ToString("N"));
        WindowsProcess? owned = null;
        Process? foreign = null;
        Task<string>? output = null;
        Task<string>? error = null;
        Task<int>? read = null;
        try
        {
            var arguments = WindowsProcessTests.ProbeArguments("hold", ready);
            if (explicitHandleList)
            {
                owned = WindowsProcess.Start("dotnet", arguments, Directory.GetCurrentDirectory(),
                    new Dictionary<string, string>());
                output = owned.Output.ReadToEndAsync(TestContext.Current.CancellationToken);
                error = owned.Error.ReadToEndAsync(TestContext.Current.CancellationToken);
            }
            else
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (var argument in arguments)
                {
                    start.ArgumentList.Add(argument);
                }
                foreign = Process.Start(start)!;
                output = foreign.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
                error = foreign.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            }
            await WindowsProcessTests.UntilAsync(() => File.Exists(ready));
            client.Dispose();
            read = server.ReadAsync(new byte[1], TestContext.Current.CancellationToken).AsTask();
            if (explicitHandleList)
            {
                (await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Be(0);
                owned!.IsEmpty.Should().BeFalse("the unrelated pipe closes while the owned child is still alive");
            }
            else
            {
                var delay = Task.Delay(200, TestContext.Current.CancellationToken);
                (await Task.WhenAny(read, delay)).Should().Be(delay,
                    "the foreign process inherited a writer and keeps the unrelated pipe open");
                foreign!.HasExited.Should().BeFalse();
            }
        }
        finally
        {
            client.Dispose();
            try
            {
                if (owned is not null)
                {
                    owned.ForceStop();
                    await WindowsProcessTests.UntilAsync(() => owned.IsEmpty);
                }
                if (foreign is { HasExited: false })
                {
                    foreign.Kill(entireProcessTree: true);
                    await foreign.WaitForExitAsync(TestContext.Current.CancellationToken);
                }
                await Task.WhenAll(output ?? Task.FromResult(""), error ?? Task.FromResult(""))
                    .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                if (read is not null)
                {
                    (await read.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Should().Be(0);
                }
            }
            finally
            {
                owned?.Dispose();
                foreign?.Dispose();
                File.Delete(ready);
            }
        }
    }
}
