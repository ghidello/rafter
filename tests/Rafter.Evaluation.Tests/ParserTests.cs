using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using Rafter.Evaluation;
using Xunit;

namespace Rafter.Evaluation.Tests;

public sealed class ParserTests
{
    [Theory]
    [InlineData("--name")]
    [InlineData("-n")]
    [InlineData("--label")]
    [InlineData("-name")]
    [InlineData("--n")]
    public void Primary_and_explicit_alias_spellings_resolve(string spelling)
    {
        var result = new CandidateAdapter().Evaluate(spelling, "Ada");

        result.ExitCode.Should().Be(0);
        result.Name.Should().Be("Ada");
        result.NamePresent.Should().BeTrue();
    }

    [Theory]
    [InlineData("--Name")]
    [InlineData("-N")]
    [InlineData("--Label")]
    [InlineData("-fc")]
    [InlineData("--version")]
    [InlineData("--")]
    public void Matching_is_ordinal_and_does_not_add_bundles_or_builtin_options(string spelling)
    {
        var result = new CandidateAdapter().Evaluate(spelling);

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(new CandidateDiagnostic("UnexpectedInput", null));
    }

    [Theory]
    [InlineData("--flag", true)]
    [InlineData("--flag=true", true)]
    [InlineData("--flag=False", false)]
    [InlineData("-f=TRUE", true)]
    [InlineData("-f=false", false)]
    public void Bare_and_attached_Boolean_values_resolve(string argument, bool expected)
    {
        var result = new CandidateAdapter().Evaluate(argument);

        result.ExitCode.Should().Be(0);
        result.Flag.Should().Be(expected);
        result.FlagPresent.Should().BeTrue();
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("FALSE", false)]
    [InlineData("false", false)]
    public void Spaced_Boolean_literals_are_case_insensitive(string literal, bool expected)
    {
        var result = new CandidateAdapter().Evaluate("--flag", literal);

        result.ExitCode.Should().Be(0);
        result.Flag.Should().Be(expected);
    }

    [Theory]
    [InlineData("--flag=", null)]
    [InlineData("--flag", "")]
    [InlineData("--flag=yes", null)]
    [InlineData("--flag", "1")]
    [InlineData("--flag", "private-rejected-value-9a28")]
    [InlineData("--count=", null)]
    [InlineData("--count", "")]
    [InlineData("--count", "2147483648")]
    public void Invalid_values_have_safe_option_diagnostics(string option, string? value)
    {
        string[] arguments = value is null ? [option] : [option, value];
        var result = new CandidateAdapter().Evaluate(arguments);

        result.ExitCode.Should().Be(2);
        var expectedOption = option.StartsWith("--flag", StringComparison.Ordinal) ? "--flag" : "--count";
        result.Diagnostics.Should().Contain(new CandidateDiagnostic("InvalidValue", expectedOption));
        JsonSerializer.Serialize(result).Should().NotContain("private-rejected-value-9a28");
    }

    [Fact]
    public void Absence_is_distinct_from_empty_false_and_zero()
    {
        var absent = new CandidateAdapter().Evaluate();
        absent.ExitCode.Should().Be(0);
        absent.Name.Should().BeNull();
        absent.Count.Should().BeNull();
        absent.Flag.Should().BeNull();
        absent.NamePresent.Should().BeFalse();
        absent.CountPresent.Should().BeFalse();
        absent.FlagPresent.Should().BeFalse();

        var supplied = new CandidateAdapter { RequireName = true, RequireCount = true, RequireFlag = true }
            .Evaluate("--name=", "--count=0", "--flag=false");
        supplied.ExitCode.Should().Be(0);
        supplied.Name.Should().BeEmpty();
        supplied.Count.Should().Be(0);
        supplied.Flag.Should().BeFalse();
        supplied.NamePresent.Should().BeTrue();
        supplied.CountPresent.Should().BeTrue();
        supplied.FlagPresent.Should().BeTrue();
    }

    [Fact]
    public void Defaults_are_resolved_outside_the_parser_and_do_not_override_explicit_values()
    {
        var adapter = new CandidateAdapter { CountDefault = 7, FlagDefault = true };
        var absent = adapter.Evaluate();
        absent.ExitCode.Should().Be(0);
        absent.Count.Should().Be(7);
        absent.Flag.Should().BeTrue();
        absent.CountPresent.Should().BeFalse();
        absent.FlagPresent.Should().BeFalse();

        var supplied = adapter.Evaluate("--count=0", "--flag=false");
        supplied.ExitCode.Should().Be(0);
        supplied.Count.Should().Be(0);
        supplied.Flag.Should().BeFalse();
    }

    [Fact]
    public void Required_inputs_aggregate_but_do_not_prevent_help()
    {
        var adapter = new CandidateAdapter { RequireName = true, RequireCount = true, RequireFlag = true };
        var missing = adapter.Evaluate();
        missing.ExitCode.Should().Be(2);
        missing.Diagnostics.Should().BeEquivalentTo(new[]
        {
            new CandidateDiagnostic("MissingRequired", "--name"),
            new CandidateDiagnostic("MissingRequired", "--count"),
            new CandidateDiagnostic("MissingRequired", "--flag")
        });

        var help = adapter.Evaluate("--help");
        help.ExitCode.Should().Be(0);
        help.Help.Should().BeTrue();
    }

    [Fact]
    public void Invalid_declarations_fail_before_help()
    {
        var result = new CandidateAdapter { RequireCount = true, CountDefault = 3 }.Evaluate("--help");

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().ContainSingle().Which.Category.Should().Be("InvalidDeclaration");
    }

    [Theory]
    [InlineData("--name", "Ada", "--label", "Ada", "--name")]
    [InlineData("-name", "Ada", "--n", "Bob", "--name")]
    [InlineData("--count", "1", "-c", "2", "--count")]
    [InlineData("--flag", "true", "-f", "false", "--flag")]
    public void Repeated_scalar_occurrences_across_aliases_fail(
        string first, string firstValue, string second, string secondValue, string canonical)
    {
        var result = new CandidateAdapter().Evaluate(first, firstValue, second, secondValue);

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(new CandidateDiagnostic("RepeatedOption", canonical));
    }

    [Theory]
    [InlineData("--flag", "-f")]
    [InlineData("--flag", "--flag=false")]
    [InlineData("--help", "--help")]
    public void Bare_and_attached_repeats_fail(string first, string second)
    {
        var result = new CandidateAdapter().Evaluate(first, second);

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Category == "RepeatedOption");
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("a=b:c")]
    [InlineData("https://example.invalid/?x=y")]
    [InlineData("--help")]
    [InlineData("--flag=")]
    [InlineData("--flag:false")]
    [InlineData("[directive:value]")]
    [InlineData("@private-file")]
    [InlineData("quote\"inside\\")]
    public void Attached_string_values_preserve_boundaries_and_punctuation(string value)
    {
        var result = new CandidateAdapter().Evaluate("--name=" + value);

        result.ExitCode.Should().Be(0);
        result.Name.Should().Be(value);
        result.Help.Should().BeFalse();
        result.FlagPresent.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("a=b:c")]
    [InlineData("C:\\some folder\\")]
    [InlineData("--undeclared")]
    [InlineData("@literal")]
    [InlineData("[literal]")]
    public void Separate_string_values_preserve_literal_content(string value)
    {
        var result = new CandidateAdapter().Evaluate("--name", value);

        result.ExitCode.Should().Be(0);
        result.Name.Should().Be(value);
    }

    [Theory]
    [InlineData("--name")]
    [InlineData("--count")]
    public void Declared_option_boundaries_do_not_swallow_help(string option)
    {
        var result = new CandidateAdapter().Evaluate(option, "--help");

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(new CandidateDiagnostic("MissingValue", option));
    }

    [Fact]
    public void Empty_attached_string_does_not_swallow_the_next_option()
    {
        var result = new CandidateAdapter().Evaluate("--name=", "--help");

        result.ExitCode.Should().Be(0);
        result.Name.Should().BeEmpty();
        result.Help.Should().BeTrue();
    }

    [Theory]
    [InlineData("--label=")]
    [InlineData("-name=")]
    [InlineData("--n=")]
    public void Empty_alias_values_remain_separate_occurrences(string alias)
    {
        var result = new CandidateAdapter().Evaluate("--name", alias, "--help");

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(new CandidateDiagnostic("RepeatedOption", "--name"));
    }

    [Fact]
    public void Valid_declared_option_values_require_equals_to_be_passed_as_strings()
    {
        var adapter = new CandidateAdapter();
        adapter.Evaluate("--name", "--flag=false").Diagnostics
            .Should().Contain(new CandidateDiagnostic("MissingValue", "--name"));
        adapter.Evaluate("--name=--flag=false").Name.Should().Be("--flag=false");
    }

    [Theory]
    [InlineData("--count", "-42")]
    [InlineData("--count=-42", null)]
    public void Negative_numbers_are_values(string option, string? value)
    {
        string[] arguments = value is null ? [option] : [option, value];
        var result = new CandidateAdapter().Evaluate(arguments);

        result.ExitCode.Should().Be(0);
        result.Count.Should().Be(-42);
    }

    [Theory]
    [InlineData("--name:private-rejected-value-9a28", "UnsupportedSeparator", "--name")]
    [InlineData("--flag:false", "UnsupportedSeparator", "--flag")]
    [InlineData("[private-rejected-value-9a28]", "UnsupportedDirective", null)]
    [InlineData("@private-rejected-value-9a28", "UnexpectedInput", null)]
    public void Unsupported_syntax_is_rejected_without_echoing_values(string argument, string category, string? option)
    {
        var result = new CandidateAdapter().Evaluate(argument);

        result.ExitCode.Should().Be(2);
        result.Diagnostics.Should().Contain(new CandidateDiagnostic(category, option));
        JsonSerializer.Serialize(result).Should().NotContain("private-rejected-value-9a28");
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("it-IT")]
    [InlineData("fr-FR")]
    public void Independent_malformed_inputs_aggregate_without_localized_message_matching(string culture)
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            var result = new CandidateAdapter().Evaluate("--help", "--count=private-rejected-value-9a28",
                "--flag=private-rejected-value-9a28", "--name", "--quiet", "--verbose");

            result.ExitCode.Should().Be(2);
            result.Diagnostics.Should().BeEquivalentTo(new[]
            {
                new CandidateDiagnostic("InvalidValue", "--count"),
                new CandidateDiagnostic("InvalidValue", "--flag"),
                new CandidateDiagnostic("MissingValue", "--name"),
                new CandidateDiagnostic("ConflictingControls", "--quiet, --verbose")
            });
            JsonSerializer.Serialize(result).Should().NotContain("private-rejected-value-9a28");
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Help_and_parse_errors_skip_user_predicates_but_valid_execution_calls_them()
    {
        var calls = 0;
        var adapter = new CandidateAdapter
        {
            NameValidator = _ =>
            {
                calls++;
                return false;
            }
        };

        adapter.Evaluate("--help", "--name=").ExitCode.Should().Be(0);
        adapter.Evaluate("--help", "--name=Ada", "--count=bad").ExitCode.Should().Be(2);
        adapter.Evaluate().ExitCode.Should().Be(0);
        calls.Should().Be(0);

        var validated = adapter.Evaluate("--name=");
        validated.ExitCode.Should().Be(2);
        validated.ValidatorCalls.Should().Be(1);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task File_app_executes_the_same_adapter_with_real_argument_boundaries_and_exit_codes()
    {
        using var consumer = new IsolatedConsumer("file-app", TestContext.Current.TestOutputHelper!, "parser/probe.cs");
        await consumer.BuildAsync();
        var responseFile = Path.Combine(consumer.WorkingDirectory, "arguments with spaces.rsp");
        await File.WriteAllTextAsync(responseFile, "--name=expanded-from-response-file",
            TestContext.Current.CancellationToken);
        var cases = new (string[] Arguments, int ExitCode, string? Name, bool Help)[]
        {
            (["--name=", "--help"], 0, "", true),
            (["--name", "", "--help"], 0, "", true),
            (["--name", "two words \"caffè\"\\", "--count=-42", "--flag=FALSE"], 0, "two words \"caffè\"\\", false),
            (["--name=--flag:false"], 0, "--flag:false", false),
            (["--help", "--flag=private-rejected-value-9a28"], 2, null, false),
            (["--name", "--help"], 2, null, false),
            (["@" + responseFile], 2, null, false),
            (["--name", "@" + responseFile], 0, "@" + responseFile, false)
        };

        foreach (var item in cases)
        {
            var actual = await consumer.RunAsync(item.Arguments);
            actual.ExitCode.Should().Be(item.ExitCode);
            var outcome = JsonSerializer.Deserialize<CandidateOutcome>(actual.StandardOutput)!;
            outcome.ExitCode.Should().Be(item.ExitCode);
            outcome.Name.Should().Be(item.Name);
            outcome.Help.Should().Be(item.Help);
            if (item.ExitCode == 0)
            {
                actual.StandardError.Should().BeEmpty();
            }
            else
            {
                actual.StandardError.Should().NotBeEmpty().And.NotContain("private-rejected-value-9a28");
                actual.StandardOutput.Should().NotContain("private-rejected-value-9a28");
            }
        }
    }
}
