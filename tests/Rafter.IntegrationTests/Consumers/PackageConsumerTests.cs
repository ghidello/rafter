using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using Xunit;

namespace Rafter.IntegrationTests.Consumers;

[Trait("LongRunning", "true")]
public sealed class PackageConsumerTests
{
    [Fact]
    public async Task Local_package_delivers_runtime_and_analyzers_to_fresh_project_and_file_app_consumers()
    {
        var output = TestContext.Current.TestOutputHelper!;
        using var builder = new IsolatedConsumer("project", output, "src/Sotsera.Rafter/Sotsera.Rafter.csproj");
        var feed = Path.Combine(builder.WorkingDirectory, "feed");
        var pack = await builder.ExecuteAsync("pack", "src/Sotsera.Rafter/Sotsera.Rafter.csproj",
            "--configuration", "Release", "--output", feed, "--disable-build-servers");
        pack.ExitCode.Should().Be(0, "{0}\n{1}", pack.StandardOutput, pack.StandardError);
        var package = Path.Combine(feed, "Sotsera.Rafter.0.1.0-local.nupkg");
        VerifyPackage(package);

        await VerifyConsumerAsync("project", feed);
        await VerifyConsumerAsync("file-app", feed);
    }

    private static void VerifyPackage(string package)
    {
        using var zip = ZipFile.OpenRead(package);
        var files = zip.Entries.Select(entry => entry.FullName).ToArray();
        files.Should().Contain("lib/net10.0/Sotsera.Rafter.dll")
            .And.Contain("analyzers/dotnet/cs/Sotsera.Rafter.Analyzers.dll").And.Contain("README.md")
            .And.Contain("build/net10.0/Sotsera.Rafter.targets");
        files.Should().NotContain(file => file.StartsWith("buildTransitive/", StringComparison.Ordinal));
        files.Where(file => file.EndsWith(".dll", StringComparison.Ordinal)).Should().HaveCount(2);
        using var manifestStream = zip.GetEntry("Sotsera.Rafter.nuspec")!.Open();
        var manifest = XDocument.Load(manifestStream);
        var dependencies = manifest.Descendants().Where(element => element.Name.LocalName == "dependency").ToArray();
        dependencies.Select(element => element.Attribute("id")!.Value)
            .Should().BeEquivalentTo("Spectre.Console", "System.CommandLine");
        dependencies.Should().OnlyContain(element => !element.Attribute("version")!.Value.StartsWith('['),
            "runtime dependency minimums must allow compatible consumer resolution");
    }

    private static async Task VerifyConsumerAsync(string kind, string feed)
    {
        var entry = kind == "project" ? "Consumer.csproj" : "consumer.cs";
        using var consumer = new IsolatedConsumer(kind, TestContext.Current.TestOutputHelper!, entry, copyWorkspace: false);
        var configuration = new XDocument(new XElement("configuration",
            new XElement("packageSources", new XElement("clear"),
                new XElement("add", new XAttribute("key", "local"), new XAttribute("value", feed)),
                new XElement("add", new XAttribute("key", "nuget.org"), new XAttribute("value", "https://api.nuget.org/v3/index.json"))),
            new XElement("packageSourceMapping", new XElement("clear"),
                new XElement("packageSource", new XAttribute("key", "local"),
                    new XElement("package", new XAttribute("pattern", "Sotsera.Rafter"))),
                new XElement("packageSource", new XAttribute("key", "nuget.org"),
                    new XElement("package", new XAttribute("pattern", "*")))),
            new XElement("config", new XElement("add", new XAttribute("key", "globalPackagesFolder"),
                new XAttribute("value", Path.Combine(consumer.WorkingDirectory, ".packages"))))));
        configuration.Save(Path.Combine(consumer.WorkingDirectory, "NuGet.Config"));
        if (kind == "project")
        {
            await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, entry), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Sotsera.Rafter" Version="0.1.0-local" />
                  </ItemGroup>
                </Project>
                """, TestContext.Current.CancellationToken);
        }

        var sourcePath = Path.Combine(consumer.WorkingDirectory, "consumer.cs");
        var directives = kind == "file-app"
            ? "#:package Sotsera.Rafter@0.1.0-local\n#:property PublishAot=false\n#:property Nullable=enable\n"
            : "";
        const string valid = """
            var command = Rafter.Command("Package consumer.");
            var name = command.Option<string>("name", "Name.").Default("Ada");
            return await command.RunAsync(args, context => context.WriteLine($"Hello, {context.GetValue(name)}!"));
            """;
        await File.WriteAllTextAsync(sourcePath, directives + valid, TestContext.Current.CancellationToken);
        await consumer.BuildAsync();
        var run = await consumer.RunAsync("--quiet");
        run.ExitCode.Should().Be(0);
        run.StandardOutput.Should().Be("Hello, Ada!" + Environment.NewLine);
        run.StandardError.Should().BeEmpty();

        // An opt-out must take effect even when it is set in the consumer body, after package .props imports.
        var projectPath = Path.Combine(consumer.WorkingDirectory, entry);
        var originalProject = kind == "project" ? await File.ReadAllTextAsync(projectPath) : "";
        if (kind == "project")
        {
            await File.WriteAllTextAsync(projectPath, originalProject.Replace("<Nullable>enable</Nullable>",
                "<Nullable>enable</Nullable><RafterImplicitUsings>false</RafterImplicitUsings>"));
        }
        var optOutDirectives = directives + (kind == "file-app" ? "#:property RafterImplicitUsings=false\n" : "");
        await File.WriteAllTextAsync(sourcePath, optOutDirectives + valid, TestContext.Current.CancellationToken);
        var withoutImport = await consumer.CompileAsync();
        withoutImport.ExitCode.Should().NotBe(0);
        (withoutImport.StandardOutput + withoutImport.StandardError).Should().Contain("CS0103");
        await File.WriteAllTextAsync(sourcePath, optOutDirectives + "using Sotsera.Rafter;\n" + valid,
            TestContext.Current.CancellationToken);
        (await consumer.CompileAsync()).ExitCode.Should().Be(0);
        if (kind == "project")
        {
            await File.WriteAllTextAsync(projectPath, originalProject);
            var designTime = await consumer.ExecuteAsync("msbuild", entry, "-p:DesignTimeBuild=true",
                "-target:ResolveReferences", "-getItem:Analyzer,Using");
            designTime.ExitCode.Should().Be(0);
            designTime.StandardOutput.Should().Contain("Sotsera.Rafter.Analyzers.dll").And.Contain("Sotsera.Rafter");
        }

        const string invalid = """
            using Sotsera.Rafter;
            var command = Rafter.Command("");
            var name = command.Option<string?>("name", "Name.");
            name.Required();
            name.Default("Ada");
            return await command.RunAsync(args, context =>
            {
                _ = context.Process("must-never-launch").RunAsync();
            });
            """;
        await File.WriteAllTextAsync(sourcePath, directives + invalid, TestContext.Current.CancellationToken);
        var rejected = await consumer.CompileAsync();
        rejected.ExitCode.Should().NotBe(0);
        var diagnostics = rejected.StandardOutput + rejected.StandardError;
        diagnostics.Should().Contain("error RAF001").And.Contain("warning RAF002").And.Contain("error RAF003")
            .And.Contain("error RAF004");
        diagnostics.Should().NotContain("AD0001").And.NotContain("CS8032").And.NotContain("CS9057");

        const string suppression = "#pragma warning disable RAF001, RAF002, RAF003, RAF004\n";
        await File.WriteAllTextAsync(sourcePath, directives + suppression + invalid,
            TestContext.Current.CancellationToken);
        (await consumer.CompileAsync()).ExitCode.Should().Be(0);
        var runtimeValidation = await consumer.RunAsync("--quiet");
        runtimeValidation.ExitCode.Should().Be(2);
        runtimeValidation.StandardError.Should().Contain("Command description must contain non-whitespace text.");

        // A valid command description leaves the policy conflict as the only invalid declaration.
        var invalidPolicy = invalid.Replace("Rafter.Command(\"\")", "Rafter.Command(\"Package consumer.\")");
        await File.WriteAllTextAsync(sourcePath, directives + suppression + invalidPolicy,
            TestContext.Current.CancellationToken);
        (await consumer.CompileAsync()).ExitCode.Should().Be(0);
        var policyValidation = await consumer.RunAsync("--quiet");
        policyValidation.ExitCode.Should().Be(2);
        policyValidation.StandardError.Should().Contain("Required or Default may be configured only once.")
            .And.NotContain("Process launch failed");
        policyValidation.StandardOutput.Should().BeEmpty();

        // The consumer has no project/source references to Rafter and restores its own fresh package cache.
        var cache = Path.Combine(consumer.WorkingDirectory, ".packages");
        Directory.Exists(Path.Combine(cache, "sotsera.rafter", "0.1.0-local")).Should().BeTrue();
        Directory.EnumerateDirectories(cache).Select(Path.GetFileName)
            .Should().NotContain(name => name!.StartsWith("microsoft.codeanalysis", StringComparison.OrdinalIgnoreCase));
        if (kind == "project")
        {
            using var assets = JsonDocument.Parse(await File.ReadAllTextAsync(
                Path.Combine(consumer.WorkingDirectory, "obj", "project.assets.json"), TestContext.Current.CancellationToken));
            assets.RootElement.GetProperty("libraries").EnumerateObject()
                .Should().OnlyContain(library => library.Value.GetProperty("type").GetString() == "package");
            await VerifyTransitiveImportAsync(consumer, originalProject, valid);
        }
    }

    private static async Task VerifyTransitiveImportAsync(IsolatedConsumer consumer, string project, string source)
    {
        var bridge = Path.Combine(consumer.WorkingDirectory, "bridge");
        Directory.CreateDirectory(bridge);
        await File.WriteAllTextAsync(Path.Combine(bridge, "Bridge.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Sotsera.Rafter" Version="0.1.0-local" /></ItemGroup>
            </Project>
            """, TestContext.Current.CancellationToken);
        var transitiveProject = project.Replace("<PackageReference Include=\"Sotsera.Rafter\" Version=\"0.1.0-local\" />",
            "<ProjectReference Include=\"bridge/Bridge.csproj\" />")
            .Replace("<OutputType>Exe</OutputType>", "<OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems>")
            .Replace("<ItemGroup>", "<ItemGroup><Compile Include=\"consumer.cs\" />");
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "Consumer.csproj"), transitiveProject,
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "consumer.cs"), source,
            TestContext.Current.CancellationToken);
        var missingImport = await consumer.CompileAsync();
        missingImport.ExitCode.Should().NotBe(0);
        (missingImport.StandardOutput + missingImport.StandardError).Should().Contain("CS0103");
        await File.WriteAllTextAsync(Path.Combine(consumer.WorkingDirectory, "consumer.cs"), "using Sotsera.Rafter;\n" + source,
            TestContext.Current.CancellationToken);
        (await consumer.CompileAsync()).ExitCode.Should().Be(0);
    }
}
