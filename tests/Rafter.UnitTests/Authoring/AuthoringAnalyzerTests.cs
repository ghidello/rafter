using System.Collections.Immutable;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Sotsera.Rafter.Analyzers;
using Xunit;

namespace Rafter.UnitTests.Authoring;

public sealed class AuthoringAnalyzerTests
{
    private static readonly ImmutableArray<MetadataReference> _references =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
            .Append(typeof(Sotsera.Rafter.Command).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToImmutableArray<MetadataReference>();

    [Theory]
    [InlineData("string?", 1)]
    [InlineData("int?", 1)]
    [InlineData("bool?", 1)]
    [InlineData("System.Nullable<int>", 1)]
    [InlineData("string", 0)]
    [InlineData("int", 0)]
    [InlineData("bool", 0)]
    public async Task Nullable_option_types_warn_on_the_type_argument(string type, int expected)
    {
        var source = $$"""
            using Sotsera.Rafter;
            var command = Rafter.Command("Example.");
            var option = command.Option<{{type}}>("value", "Value.");
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().HaveCount(expected);
        if (expected != 0)
        {
            diagnostics[0].Id.Should().Be("RAF002");
            diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
            source.Substring(diagnostics[0].Location.SourceSpan.Start, diagnostics[0].Location.SourceSpan.Length)
                .Should().Be(type);
        }
    }

    [Theory]
    [InlineData("context.Process(\"tool\").RunAsync();")]
    [InlineData("_ = context.Process(\"tool\").RunAsync();")]
    [InlineData("_ = (context.Process(\"tool\").RunAsync());")]
    [InlineData("context.Process(\"tool\").RunAsync().ConfigureAwait(false);")]
    [InlineData("_ = context.Process(\"tool\").RunAsync().ConfigureAwait(false);")]
    [InlineData("context?.Process(\"tool\").RunAsync();")]
    [InlineData("System.Action action = () => context.Process(\"tool\").RunAsync();")]
    public async Task Clearly_discarded_process_tasks_are_errors(string statement)
    {
        var source = $$"""
            using Sotsera.Rafter;
            class Example
            {
                void Run(RafterContext context)
                {
                    {{statement}}
                }
            }
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("RAF003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData("await context.Process(\"tool\").RunAsync();")]
    [InlineData("_ = await context.Process(\"tool\").RunAsync();")]
    [InlineData("await context.Process(\"tool\").RunAsync().ConfigureAwait(false);")]
    [InlineData("var task = context.Process(\"tool\").RunAsync(); await task;")]
    [InlineData("await Task.WhenAll(context.Process(\"one\").RunAsync(), context.Process(\"two\").RunAsync());")]
    [InlineData("Observe(context.Process(\"tool\").RunAsync());")]
    [InlineData("Task<ProcessResult> _; _ = context.Process(\"tool\").RunAsync(); await _;")]
    [InlineData("System.Func<Task<ProcessResult>> run = () => context.Process(\"tool\").RunAsync(); await run();")]
    public async Task Valid_observation_and_helper_boundaries_are_not_diagnosed(string statement)
    {
        var source = $$"""
            using Sotsera.Rafter;
            using System.Threading.Tasks;
            class Example
            {
                async Task Run(RafterContext context)
                {
                    {{statement}}
                }
                void Observe(Task<ProcessResult> task) { }
            }
            """;
        (await AnalyzeAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task Returned_tasks_and_unrelated_RunAsync_methods_are_not_diagnosed()
    {
        const string source = """
            using Sotsera.Rafter;
            using System.Threading.Tasks;
            class Example
            {
                Task<ProcessResult> Forward(RafterContext context) => context.Process("tool").RunAsync();
                Task<ProcessResult> ForwardBlock(RafterContext context) { return context.Process("tool").RunAsync(); }
                void Other() { _ = Task.Delay(1); _ = RunAsync(); _ = Option<string?>(); }
                Task RunAsync() => Task.CompletedTask;
                T? Option<T>() => default;
            }
            """;
        (await AnalyzeAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task Compiler_configuration_can_disable_a_diagnostic()
    {
        const string source = """
            using Sotsera.Rafter;
            class Example
            {
                void Run(RafterContext context) { _ = context.Process("tool").RunAsync(); }
            }
            """;
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable,
            specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic> { ["RAF003"] = ReportDiagnostic.Suppress });
        (await AnalyzeAsync(source, options)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\" \\t\\r\\n\"")]
    [InlineData("null!")]
    [InlineData("default(string)!")]
    [InlineData("Blank + \" \"")]
    public async Task Constant_blank_descriptions_are_errors_for_every_description_site(string description)
    {
        var source = $$"""
            using Sotsera.Rafter;
            const string Blank = "";
            var command = Rafter.Command(description: {{description}});
            var option = command.Option<string>(description: {{description}}, name: "name");
            option.Validate({{description}}, _ => true);
            option.Required().Validate({{description}}, _ => true);
            command.Target("target", {{description}});
            command.Validate({{description}}, _ => true);
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().HaveCount(6).And.OnlyContain(diagnostic => diagnostic.Id == "RAF001" &&
            diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task Dynamic_descriptions_split_configuration_and_generated_code_are_not_diagnosed()
    {
        const string source = """
            using Sotsera.Rafter;
            var description = System.Environment.GetEnvironmentVariable("DESCRIPTION")!;
            var command = Rafter.Command(description);
            var option = command.Option<string>("name", description);
            Configure(option);
            static void Configure(Option<string> option) { option.Required(); }
            """;
        (await AnalyzeAsync(source)).Should().BeEmpty();
        const string generated = """
            // <auto-generated/>
            using Sotsera.Rafter;
            var command = Rafter.Command("");
            var name = command.Option<string?>("name", "Name.");
            """;
        (await AnalyzeAsync(generated)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Required()", "Required()")]
    [InlineData("Default(1)", "Default(1)")]
    [InlineData("Default(1)", "Default(2)")]
    [InlineData("Required()", "Default(0)")]
    [InlineData("Default(0)", "Required()")]
    public async Task Repeated_and_conflicting_policies_report_the_later_call_and_link_the_first(string first, string second)
    {
        var source = $$"""
            using Sotsera.Rafter;
            var command = Rafter.Command("Example.");
            var option = command.Option<int>("count", "Count.");
            option.{{first}};
            option.{{second}};
            """;
        var diagnostic = (await AnalyzeAsync(source)).Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("RAF004");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.Location.SourceSpan.Start.Should().Be(source.LastIndexOf("option.", StringComparison.Ordinal));
        diagnostic.AdditionalLocations.Should().ContainSingle().Which.SourceSpan.Start
            .Should().Be(source.IndexOf("option.", StringComparison.Ordinal));
        diagnostic.GetMessage().Should().Contain("'option'").And.Contain(first.Split('(')[0])
            .And.Contain(second.Split('(')[0]);
    }

    [Theory]
    [InlineData("var required = option.Required();", "var defaulted = option.Default(0);")]
    [InlineData("_ = option.Required();", "_ = option.Required();")]
    [InlineData("var required = (option.Required());", "((Option<int>)option).Required();")]
    [InlineData("option.Default(value: 0);", "option.Default(value: 0);")]
    [InlineData("option.Required(); var other = command.Option<int>(\"other\", \"Other.\");", "option.Required();")]
    public async Task Policy_tracking_accepts_stored_handles_discards_wrappers_and_independent_declarations(
        string first, string second)
    {
        var source = $$"""
            using Sotsera.Rafter;
            var command = Rafter.Command("Example.");
            var option = command.Option<int>("count", "Count.");
            {{first}}
            {{second}}
            """;
        (await AnalyzeAsync(source)).Should().ContainSingle().Which.Id.Should().Be("RAF004");
    }

    [Theory]
    [InlineData("option.Required();")]
    [InlineData("option.Default(0);")]
    [InlineData("option.Required(); var other = command.Option<int>(\"other\", \"Other.\"); other.Required();")]
    [InlineData("option.Required(); option = command.Option<int>(\"other\", \"Other.\"); option.Required();")]
    [InlineData("if (flag) { option.Required(); } else { option.Default(0); }")]
    [InlineData("option.Required(); if (flag) { option.Required(); }")]
    [InlineData("for (var i = 0; i < 2; i++) { option.Required(); }")]
    [InlineData("option.Required(); Helper(); option.Required();")]
    [InlineData("option.Required(); option.Default(GetDefault());")]
    [InlineData("var alias = option; option.Required(); alias.Required();")]
    [InlineData("ref var alias = ref option; option.Required(); alias = command.Option<int>(\"other\", \"Other.\"); option.Required();")]
    [InlineData("option.Required(); System.Action reset = () => option = command.Option<int>(\"other\", \"Other.\"); reset(); option.Required();")]
    [InlineData("option.Required(); option?.Required();")]
    [InlineData("option.Required(); { Helper(); } option.Required();")]
    [InlineData("option.Required(); return; option.Required();")]
    [InlineData("option.Required(); option.Aliases(\"c\"); option.Required();")]
    public async Task Option_policy_boundaries_do_not_guess_about_control_flow_mutation_or_aliases(string statements)
    {
        var source = $$"""
            using Sotsera.Rafter;
            class Example
            {
                void Configure(bool flag)
                {
                    var command = Rafter.Command("Example.");
                    var option = command.Option<int>("count", "Count.");
                    {{statements}}
                }
                void Helper() { }
                int GetDefault() => 0;
            }
            """;
        (await AnalyzeAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task Nested_bodies_are_analyzed_independently_without_combining_executions()
    {
        const string source = """
            using Sotsera.Rafter;
            var command = Rafter.Command("Example.");
            var option = command.Option<int>("count", "Count.");
            option.Required();
            System.Action configure = () => { option.Required(); option.Default(0); };
            void Configure() { option.Required(); option.Default(0); }
            """;
        var diagnostics = await AnalyzeAsync(source);
        diagnostics.Should().HaveCount(2).And.OnlyContain(diagnostic => diagnostic.Id == "RAF004");
    }

    [Fact]
    public async Task Option_policy_tracking_excludes_fields_parameters_and_unrelated_methods()
    {
        const string source = """
            using Sotsera.Rafter;
            class Example
            {
                Option<int> field = Rafter.Command("Example.").Option<int>("count", "Count.");
                void Configure(Option<int> option)
                {
                    field.Required(); field.Required();
                    option.Required(); option.Required();
                    var other = new Other(); other.Required(); other.Required();
                }
            }
            class Other { public void Required() { } }
            """;
        (await AnalyzeAsync(source)).Should().BeEmpty();
    }

    [Fact]
    public async Task Option_policy_errors_respect_suppression_and_generated_code()
    {
        const string source = """
            using Sotsera.Rafter;
            var command = Rafter.Command("Example.");
            var option = command.Option<int>("count", "Count.");
            option.Required(); option.Required();
            """;
        (await AnalyzeAsync("#pragma warning disable RAF004\n" + source)).Should().BeEmpty();
        (await AnalyzeAsync("// <auto-generated/>\n" + source)).Should().BeEmpty();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, CSharpCompilationOptions? options = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create("AnalyzerConsumer", [tree], _references,
            options ?? new CSharpCompilationOptions(OutputKind.ConsoleApplication,
                nullableContextOptions: NullableContextOptions.Enable));
        // Sources with only class declarations are libraries; others exercise actual top-level syntax.
        if (options is null && !tree.GetRoot().ChildNodes().Any(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.GlobalStatementSyntax))
        {
            compilation = compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));
        }
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("the source must compile before analyzer assertions");
        return await compilation.WithAnalyzers([new RafterAuthoringAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }
}
