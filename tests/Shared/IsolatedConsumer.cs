using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Rafter.Testing;

// Test-only process launcher. This does not implement Rafter process containment.
internal sealed class IsolatedConsumer : IDisposable
{
    private readonly DirectoryInfo _temporaryRoot;
    private readonly string _kind;
    private readonly ITestOutputHelper _output;
    private readonly string _entryPoint;

    public string WorkingDirectory { get; }

    public IsolatedConsumer(string kind, ITestOutputHelper output, string? entryPoint = null, bool copyWorkspace = true)
    {
        _kind = kind;
        _output = output;
        _temporaryRoot = Directory.CreateTempSubdirectory("rafter-tests-");
        WorkingDirectory = Path.Combine(_temporaryRoot.FullName, "consumer workspace");

        var templateRoot = Path.Combine(AppContext.BaseDirectory, "Workspace");
        var sources = copyWorkspace
            ? Directory.EnumerateFiles(templateRoot, "*", SearchOption.AllDirectories)
            : [Path.Combine(templateRoot, "global.json")];
        foreach (var source in sources)
        {
            var destination = Path.Combine(WorkingDirectory, Path.GetRelativePath(templateRoot, source));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }

        _entryPoint = entryPoint ?? (kind == "file-app"
            ? Path.Combine("tests", "testassets", "consumers", "host", "file-app", "probe.cs")
            : Path.Combine("tests", "testassets", "consumers", "host", "project", "ConsumerProbe.csproj"));
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
        => RunFromAsync(WorkingDirectory, arguments);

    public Task<ProcessResult> RunFromAsync(string directory, params string[] arguments)
    {
        var selector = _kind == "file-app" ? "--file" : "--project";
        var entryPoint = Path.GetFullPath(_entryPoint, WorkingDirectory);
        string[] command = ["run", selector, entryPoint, "--configuration", "Release", "--no-build", "--", .. arguments];
        return ExecuteInAsync(Path.GetFullPath(directory, WorkingDirectory), command);
    }

    public void Dispose()
    {
        // Only this test's newly allocated directory is removed, after all launches have settled.
        _temporaryRoot.Delete(recursive: true);
    }

    internal Task<ProcessResult> ExecuteAsync(params string[] arguments) => ExecuteInAsync(WorkingDirectory, arguments);

    private async Task<ProcessResult> ExecuteInAsync(string directory, string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = directory,
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
            string.Join(" ", arguments), directory, result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }
}

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
