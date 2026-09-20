using System.Diagnostics;

namespace Sotsera.Rafter.IntegrationTests;

public sealed class FoundationIntegrationTests
{
    [Fact]
    public void TestProcessHasAStableWorkingDirectory()
    {
        Path.IsPathFullyQualified(Environment.CurrentDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task FileFixtureRendersHelpThroughTheRealConsoleBoundary()
    {
        ProcessResult result = await RunFixture("--help", "--plain");

        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.Should().ContainAll(
            "Rafter integration fixture.",
            "Usage",
            "Sotsera.Rafter.RunFixture [options]",
            "Command options",
            "Targets");
    }

    [Fact]
    public async Task FileFixtureRoutesInputFailureAndHelpToStandardError()
    {
        ProcessResult result = await RunFixture("--count=bad", "--plain");

        result.ExitCode.Should().Be(2);
        result.StandardOutput.Should().BeEmpty();
        result.StandardError.Should().ContainAll(
            "Input errors",
            "Invalid value \"bad\" for '--count'.",
            "Usage");
    }

    [Fact]
    public async Task FileFixtureHandlesARealProcessIsolatedConsoleSignal()
    {
        ProcessResult result = await RunFixture("--count=1", "--self-cancel");

        result.ExitCode.Should().Be(130);
    }

    [Fact]
    public async Task PropertyOutputWorksWhenJsonReflectionIsDisabled()
    {
        ProcessResult result = await RunFixture("--count=1", "--plain");

        result.ExitCode.Should().Be(0);
        result.StandardError.Should().BeEmpty();
        result.StandardOutput.ReplaceLineEndings("\n").Should().Contain("[entry] message=\"first\\nsecond\"\n")
            .And.Contain("[entry] jsonReflection=false\n");
    }

    private static async Task<ProcessResult> RunFixture(params string[] arguments)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        string assembly = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "Sotsera.Rafter.RunFixture",
            configuration,
            "Sotsera.Rafter.RunFixture.dll"));
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(assembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync()
                .WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await output.ConfigureAwait(false),
            await error.ConfigureAwait(false));
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
