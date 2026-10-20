#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Source)
    .Description("Build and test Rafter, optionally creating and verifying a local package.");

var configuration = command.Option<BuildConfiguration>("configuration")
    .Description("The build configuration used by every step.")
    .Alias('c')
    .Default(BuildConfiguration.Release);

var executable = command.Option<string>("dotnet")
    .Description("The dotnet executable name or absolute path.")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value))
    .Default("dotnet");

var timeout = command.Option<TimeSpan>("timeout")
    .Description("The execution deadline for each external tool, including owned descendants and output.")
    .Validate("Must be positive and no greater than 49d17h2m47s294ms.",
        value => value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1)
    .Default(TimeSpan.FromMinutes(10));

var packageRequested = command.Option<bool>("package")
    .Description("Create and verify a local NuGet package after a successful build and test run.")
    .Default(false);

var packages = command.Option<string>("packages")
    .Description("Local package output directory; relative paths use the repository directory.")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value))
    .Default(".artifacts/packages");

var version = command.Option<string>("version")
    .Description("The local package version; dotnet pack validates NuGet version syntax.")
    .Validate("Must not be blank.", value => !string.IsNullOrWhiteSpace(value))
    .Default("0.1.0-local");

var dotnetDefaults = new ProcessConfiguration()
    .WorkingDirectory("..")
    .Environment("DOTNET_NOLOGO", "1")
    .Environment("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    .Environment("MSBUILDDISABLENODEREUSE", "1")
    .Environment("UseSharedCompilation", "false")
    .Timeout(timeout);

var build = command.Target("build")
    .Description("Restore and build the solution, including analyzers and linked examples.")
    .Run(async context =>
    {
        await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Build the solution.")
            .Arguments("build", "Rafter.slnx")
            .Argument("--configuration", context.Value(configuration).ToString())
            .Argument("--verbosity", "minimal")
            .Argument("--disable-build-servers")
            .Argument($"-p:Version={context.Value(version)}")
            .RunAsync();
    });

var test = command.Target("test")
    .Description("Run the complete test suite using the freshly built binaries.")
    .DependsOn(build)
    .Run(async context =>
    {
        await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Run the test suite.")
            .Argument("test")
            .Argument("--solution", "Rafter.slnx")
            .Argument("--configuration", context.Value(configuration).ToString())
            .Argument("--no-build")
            .Argument("--no-restore")
            .RunAsync();
    });

var package = command.Target("package")
    .Description("Create a private-use package from the verified build without uploading it.")
    .DependsOn(test)
    .When("Run when --package is requested.", context => context.Value(packageRequested))
    .Run(async context =>
    {
        var result = await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Create the local package.")
            .Arguments("pack", "src/Sotsera.Rafter/Sotsera.Rafter.csproj")
            .Argument("--configuration", context.Value(configuration).ToString())
            .Argument("--no-build")
            .Argument("--no-restore")
            .Argument("--output", context.Value(packages))
            .Argument($"-p:Version={context.Value(version)}")
            .Argument("--disable-build-servers")
            .RunAsync();

        if (!result.Succeeded)
        {
            return;
        }

        context.Output.WriteLine(
            $"Local packages: {context.Value(packages)} (relative to the repository if not absolute).");
    });

var verifyPackage = command.Target("verify-package")
    .Description("Verify isolated package consumers, analyzers, symbols, and reproducible package contents.")
    .DependsOn(package)
    .When("Run when --package is requested.", context => context.Value(packageRequested))
    .Run(async context =>
    {
        await context.Process(context.Value(executable))
            .Configure(dotnetDefaults)
            .Description("Verify the local package.")
            .Argument("run")
            .Argument("--project", "tools/PackageVerification/PackageVerification.csproj")
            .Argument("--configuration", context.Value(configuration).ToString())
            .Argument("--no-build")
            .Argument("--no-restore")
            .Argument("--")
            .Argument("--plain")
            .Argument("--packages", context.Value(packages))
            .Argument("--version", context.Value(version))
            .Argument("--configuration", context.Value(configuration).ToString())
            .Argument("--dotnet", context.Value(executable))
            .Argument("--timeout", context.Value(timeout).ToString("c", System.Globalization.CultureInfo.InvariantCulture))
            .RunAsync();
    });

var verify = command.Target("verify")
    .Description("Complete the build and tests, plus local packaging and consumer verification when requested.")
    .DependsOn(verifyPackage);

return await command.RunAsync(verify, args);

enum BuildConfiguration
{
    Debug,
    Release
}
