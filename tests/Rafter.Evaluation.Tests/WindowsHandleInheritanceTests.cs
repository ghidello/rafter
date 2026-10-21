using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using AwesomeAssertions;
using Sotsera.Rafter.Internal;
using Xunit;

namespace Rafter.Evaluation.Tests;

// System.Diagnostics.Process uses broad handle inheritance on Windows. Do not let unrelated test launchers
// overlap the native launcher's temporary inheritable handles. Concurrency inside each test remains enabled.
[CollectionDefinition("Native process handles", DisableParallelization = true)]
public sealed class NativeProcessHandleCollection;

[SupportedOSPlatform("windows")]
[Collection("Native process handles")]
public sealed class WindowsHandleInheritanceTests
{
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
