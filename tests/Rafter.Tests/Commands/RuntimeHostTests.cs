using AwesomeAssertions;
using Sotsera.Rafter;
using Xunit;
using Toolkit = Sotsera.Rafter.Rafter;

namespace Rafter.Tests.Commands;

[CollectionDefinition("Process working directory", DisableParallelization = true)]
public sealed class WorkingDirectoryCollection;

[Collection("Process working directory")]
public sealed class RuntimeHostTests
{
    [Fact]
    public async Task Actual_host_root_is_captured_at_entry_and_cwd_is_never_changed_by_Rafter()
    {
        var original = Directory.GetCurrentDirectory();
        var temporary = Directory.CreateTempSubdirectory("rafter-root-");
        var before = temporary.CreateSubdirectory("before").FullName;
        var during = temporary.CreateSubdirectory("during").FullName;
        var command = Toolkit.Command("Root capture check.");
        RafterContext? captured = null;
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int>? run = null;
        try
        {
            Directory.SetCurrentDirectory(before);
            run = command.RunAsync(["--quiet"], async context =>
            {
                captured = context;
                await release.Task;
            });
            captured!.RootDirectory.Should().Be(before);
            Directory.GetCurrentDirectory().Should().Be(before);
            Directory.SetCurrentDirectory(during);
            captured.RootDirectory.Should().Be(before);
            release.SetResult();
            (await run).Should().Be(0);
            Directory.GetCurrentDirectory().Should().Be(during);
            (await command.RunAsync(["--quiet"], context =>
            {
                context.RootDirectory.Should().Be(during);
            })).Should().Be(0);
        }
        finally
        {
            release.TrySetResult();
            if (run is not null)
            {
                await run;
            }
            Directory.SetCurrentDirectory(original);
            temporary.Delete(recursive: true);
        }
    }
}
