using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Rafter.Evaluation.Tests;

// Test-only process launcher. This does not implement Rafter process containment.
internal sealed class IsolatedConsumer : IDisposable
{
    private readonly DirectoryInfo _temporaryRoot;
    private readonly string _kind;
    private readonly ITestOutputHelper _output;
    private readonly string _entryPoint;

    public string WorkingDirectory { get; }

    public IsolatedConsumer(string kind, ITestOutputHelper output, string? entryPoint = null, bool copyFixtures = true)
    {
        _kind = kind;
        _output = output;
        _temporaryRoot = Directory.CreateTempSubdirectory("rafter-evaluation-");
        WorkingDirectory = Path.Combine(_temporaryRoot.FullName, "consumer workspace");

        var fixtureRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var sources = copyFixtures
            ? Directory.EnumerateFiles(fixtureRoot, "*", SearchOption.AllDirectories)
            : [Path.Combine(fixtureRoot, "global.json")];
        foreach (var source in sources)
        {
            var destination = Path.Combine(WorkingDirectory, Path.GetRelativePath(fixtureRoot, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }

        _entryPoint = entryPoint ?? (kind == "file-app"
            ? Path.Combine("file-app", "probe.cs")
            : Path.Combine("project", "ConsumerProbe.csproj"));
    }

    public async Task BuildAsync()
    {
        var sdk = await ExecuteAsync("--version");
        sdk.ExitCode.Should().Be(0);
        var version = Version.Parse(sdk.StandardOutput.Trim());
        version.Major.Should().Be(10);
        version.Minor.Should().Be(0);
        version.Build.Should().BeInRange(402, 499);

        var build = await CompileAsync();
        build.ExitCode.Should().Be(0, "the isolated consumer must build: {0}", build.StandardError);
    }

    public Task<ProcessResult> CompileAsync() => ExecuteAsync("build", _entryPoint, "--configuration", "Release",
        "--verbosity", "minimal", "--disable-build-servers");

    public Task<ProcessResult> RunAsync(params string[] arguments)
    {
        var selector = _kind == "file-app" ? "--file" : "--project";
        string[] command = ["run", selector, _entryPoint, "--configuration", "Release", "--no-build", "--", .. arguments];
        return ExecuteAsync(command);
    }

    public void Dispose()
    {
        // Only this test's newly allocated directory is removed, after all launches have settled.
        _temporaryRoot.Delete(recursive: true);
    }

    internal async Task<ProcessResult> ExecuteAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the fixture's dotnet process.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
        }
        catch (TimeoutException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            await Task.WhenAll(stdout, stderr);
            throw;
        }

        var result = new ProcessResult(process.ExitCode, await stdout, await stderr);
        _output.WriteLine("dotnet {0}\nWorking directory: {1}\nExit: {2}\nstdout:\n{3}\nstderr:\n{4}",
            string.Join(" ", arguments), WorkingDirectory, result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }
}

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
